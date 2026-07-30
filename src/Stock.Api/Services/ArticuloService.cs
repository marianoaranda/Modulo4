using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;

namespace Stock.Api.Services;

/// <summary>Motivo por el que una operación sobre artículos no se aplicó.</summary>
public enum FalloDeArticulo
{
    /// <summary>400.</summary>
    Validacion,

    /// <summary>404.</summary>
    NoEncontrado,

    /// <summary>409 — código duplicado (RF-017) o baja restringida (RF-014a).</summary>
    Conflicto,
}

public sealed record OperacionArticulo(
    bool Exito,
    Articulo? Articulo = null,
    FalloDeArticulo? Fallo = null,
    string? Mensaje = null,
    IReadOnlyList<ErrorDeValidacion>? Errores = null)
{
    public static OperacionArticulo Correcta(Articulo? articulo = null) => new(true, articulo);

    public static OperacionArticulo Invalida(IReadOnlyList<ErrorDeValidacion> errores) =>
        new(false, Fallo: FalloDeArticulo.Validacion, Errores: errores);

    public static OperacionArticulo NoEncontrado() =>
        new(false, Fallo: FalloDeArticulo.NoEncontrado, Mensaje: "El artículo no existe.");

    public static OperacionArticulo Conflicto(string mensaje) =>
        new(false, Fallo: FalloDeArticulo.Conflicto, Mensaje: mensaje);
}

/// <summary>
/// T088 — Reglas de aplicación del ABM de artículos.
///
/// Verifica el duplicado y la baja restringida <b>antes</b> de tocar la base, no para reemplazar
/// al índice único y a la clave foránea —que siguen siendo la garantía última— sino para poder
/// devolver un 409 con un mensaje legible en lugar de una violación de restricción que llegaría al
/// usuario como un 500.
/// </summary>
public class ArticuloService
{
    private readonly StockDbContext _db;

    public ArticuloService(StockDbContext db) => _db = db;

    public async Task<OperacionArticulo> AltaAsync(ArticuloAValidar solicitud, CancellationToken ct)
    {
        var errores = ArticuloValidator.Validar(solicitud);

        if (errores.Count > 0)
        {
            return OperacionArticulo.Invalida(errores);
        }

        if (await CodigoYaUsadoAsync(solicitud.Codigo, exceptoArticuloId: null, ct))
        {
            return OperacionArticulo.Conflicto(
                $"Ya existe un artículo con el código {solicitud.Codigo}.");
        }

        var articulo = new Articulo();
        Copiar(solicitud, articulo);

        _db.Articulos.Add(articulo);
        await _db.SaveChangesAsync(ct);

        // Se relee para traer PrecioVenta, que lo calcula el motor (RF-016).
        await _db.Entry(articulo).ReloadAsync(ct);

        return OperacionArticulo.Correcta(articulo);
    }

    public async Task<OperacionArticulo> ModificarAsync(
        int articuloId, ArticuloAValidar solicitud, CancellationToken ct)
    {
        var errores = ArticuloValidator.Validar(solicitud);

        if (errores.Count > 0)
        {
            return OperacionArticulo.Invalida(errores);
        }

        var articulo = await _db.Articulos.FirstOrDefaultAsync(a => a.ArticuloId == articuloId, ct);

        if (articulo is null)
        {
            return OperacionArticulo.NoEncontrado();
        }

        // El propio artículo se excluye del chequeo: si no, ninguna modificación que conserve el
        // Código sería posible.
        if (await CodigoYaUsadoAsync(solicitud.Codigo, exceptoArticuloId: articuloId, ct))
        {
            return OperacionArticulo.Conflicto(
                $"Ya existe un artículo con el código {solicitud.Codigo}.");
        }

        Copiar(solicitud, articulo);
        await _db.SaveChangesAsync(ct);

        return OperacionArticulo.Correcta(articulo);
    }

    public async Task<OperacionArticulo> BajaAsync(int articuloId, CancellationToken ct)
    {
        var articulo = await _db.Articulos.FirstOrDefaultAsync(a => a.ArticuloId == articuloId, ct);

        if (articulo is null)
        {
            return OperacionArticulo.NoEncontrado();
        }

        // RF-014a: baja restringida. No hay baja lógica ni cascada; el histórico de movimientos y
        // el Stock Actual derivado se preservan íntegros.
        var tieneMovimientos = await _db.MovimientoDetalles.AnyAsync(d => d.ArticuloId == articuloId, ct);

        if (tieneMovimientos)
        {
            return OperacionArticulo.Conflicto(
                $"El artículo {articulo.Codigo} tiene movimientos asociados y no puede eliminarse.");
        }

        _db.Articulos.Remove(articulo);
        await _db.SaveChangesAsync(ct);

        return OperacionArticulo.Correcta();
    }

    /// <summary>
    /// La comparación la resuelve la collation <c>Modern_Spanish_CI_AS</c> de la columna: por eso
    /// "A-001" y "a-001" colisionan y "PANO-1" y "PAÑO-1" no (RF-017a). Normalizar la cadena en C#
    /// daría una regla distinta de la que aplica el índice único, y las dos discreparían en los
    /// acentos.
    /// </summary>
    private Task<bool> CodigoYaUsadoAsync(string codigo, int? exceptoArticuloId, CancellationToken ct) =>
        _db.Articulos.AnyAsync(
            a => a.Codigo == codigo && (exceptoArticuloId == null || a.ArticuloId != exceptoArticuloId),
            ct);

    private static void Copiar(ArticuloAValidar origen, Articulo destino)
    {
        destino.Codigo = origen.Codigo;
        destino.Descripcion = origen.Descripcion;
        destino.PrecioCosto = origen.PrecioCosto;
        destino.Margen = origen.Margen;
        destino.StockMinimo = origen.StockMinimo;
        destino.PuntoPedido = origen.PuntoPedido;
        destino.StockIdeal = origen.StockIdeal;

        // PrecioVenta no se copia: es columna calculada. Que no exista línea que la asigne es lo
        // que hace imposible que un cuerpo de solicitud la fije (RF-016).
    }
}
