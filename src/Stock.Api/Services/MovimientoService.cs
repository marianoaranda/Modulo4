using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;

namespace Stock.Api.Services;

/// <summary>Motivo por el que una operación sobre movimientos no se aplicó.</summary>
public enum FalloDeMovimiento
{
    /// <summary>400 — entrada que viola una regla de validación de campo.</summary>
    Validacion,

    /// <summary>404 — el movimiento no existe.</summary>
    NoEncontrado,

    /// <summary>422 — la operación dejaría el Stock Actual de algún artículo por debajo de 0.</summary>
    StockInsuficiente,
}

public sealed record OperacionMovimiento(
    bool Exito,
    int Numero = 0,
    FalloDeMovimiento? Fallo = null,
    string? Mensaje = null,
    IReadOnlyList<ErrorDeValidacion>? Errores = null)
{
    public static OperacionMovimiento Correcta(int numero) => new(true, numero);

    public static OperacionMovimiento Invalida(IReadOnlyList<ErrorDeValidacion> errores) =>
        new(false, Fallo: FalloDeMovimiento.Validacion, Errores: errores);

    public static OperacionMovimiento Invalida(string campo, string mensaje) =>
        Invalida([new ErrorDeValidacion(campo, mensaje)]);

    public static OperacionMovimiento NoEncontrado() =>
        new(false, Fallo: FalloDeMovimiento.NoEncontrado, Mensaje: "El Movimiento no existe.");

    public static OperacionMovimiento SinStock(string mensaje) =>
        new(false, Fallo: FalloDeMovimiento.StockInsuficiente, Mensaje: mensaje);
}

/// <summary>
/// T074 — Dueño del <b>protocolo de escritura de movimientos</b>, el invariante de arquitectura
/// que sostiene RF-024a, RF-024b y RF-024c.
///
/// Toda ruta que cree, modifique o elimine movimientos pasa por acá y sigue esta secuencia:
/// <list type="number">
///   <item>abrir transacción;</item>
///   <item>bloquear las filas de <c>Articulo</c> afectadas, en orden ascendente de Id;</item>
///   <item>leer el saldo desde <c>vw_StockActual</c>, ya dentro de la transacción;</item>
///   <item>validar que el saldo <b>resultante</b> sea ≥ 0 para TODOS los artículos afectados;</item>
///   <item>aplicar encabezado y detalle;</item>
///   <item>confirmar.</item>
/// </list>
///
/// El paso 4 se evalúa sobre el efecto <b>conjunto</b> de todas las líneas y antes de aplicar
/// ninguna: ahí está el todo-o-nada de RF-024c. Validar línea por línea contra el saldo inicial
/// dejaría pasar dos líneas del mismo artículo que individualmente entran y juntas no.
/// </summary>
public class MovimientoService
{
    private readonly StockDbContext _db;
    private readonly ArticuloLockRepository _bloqueo;

    public MovimientoService(StockDbContext db, ArticuloLockRepository bloqueo)
    {
        _db = db;
        _bloqueo = bloqueo;
    }

    /// <summary>Compra suma al Stock Actual, Venta resta (RF-020b).</summary>
    private static int Signo(TipoMovimiento tipo) => tipo == TipoMovimiento.Compra ? 1 : -1;

    public async Task<OperacionMovimiento> AltaAsync(MovimientoAValidar solicitud, CancellationToken ct)
    {
        var errores = MovimientoValidator.Validar(solicitud, DateOnly.FromDateTime(DateTime.Today));

        if (errores.Count > 0)
        {
            return OperacionMovimiento.Invalida(errores);
        }

        var faltante = await ArticuloInexistenteAsync(solicitud.Detalle, ct);

        if (faltante is not null)
        {
            return OperacionMovimiento.Invalida("detalle.articuloId", faltante);
        }

        var efecto = EfectoDe(solicitud.Tipo, solicitud.Detalle);

        return await EnTransaccionAsync(efecto, async () =>
        {
            var movimiento = new Movimiento { Tipo = solicitud.Tipo, Fecha = solicitud.Fecha };

            foreach (var linea in solicitud.Detalle)
            {
                movimiento.Detalle.Add(new MovimientoDetalle
                {
                    ArticuloId = linea.ArticuloId,
                    Cantidad = linea.Cantidad,
                    PrecioUnitario = linea.PrecioUnitario,
                });
            }

            _db.Movimientos.Add(movimiento);
            await _db.SaveChangesAsync(ct);

            return movimiento.Numero;
        }, ct);
    }

    public async Task<OperacionMovimiento> ModificarAsync(
        int numero, MovimientoAValidar solicitud, CancellationToken ct)
    {
        var errores = MovimientoValidator.Validar(solicitud, DateOnly.FromDateTime(DateTime.Today));

        if (errores.Count > 0)
        {
            return OperacionMovimiento.Invalida(errores);
        }

        var existente = await _db.Movimientos
            .Include(m => m.Detalle)
            .FirstOrDefaultAsync(m => m.Numero == numero, ct);

        if (existente is null)
        {
            return OperacionMovimiento.NoEncontrado();
        }

        var faltante = await ArticuloInexistenteAsync(solicitud.Detalle, ct);

        if (faltante is not null)
        {
            return OperacionMovimiento.Invalida("detalle.articuloId", faltante);
        }

        // El efecto de una modificación es la diferencia entre lo que quedará y lo que había.
        var efecto = EfectoDe(solicitud.Tipo, solicitud.Detalle);
        Restar(efecto, EfectoActualDe(existente));

        return await EnTransaccionAsync(efecto, async () =>
        {
            existente.Tipo = solicitud.Tipo;
            existente.Fecha = solicitud.Fecha;

            // Se reemplaza el detalle completo: distinguir altas, bajas y cambios de línea
            // agregaría complejidad sin cambiar el resultado, y el detalle no tiene identidad
            // propia de negocio.
            _db.MovimientoDetalles.RemoveRange(existente.Detalle);

            foreach (var linea in solicitud.Detalle)
            {
                _db.MovimientoDetalles.Add(new MovimientoDetalle
                {
                    MovimientoNumero = numero,
                    ArticuloId = linea.ArticuloId,
                    Cantidad = linea.Cantidad,
                    PrecioUnitario = linea.PrecioUnitario,
                });
            }

            await _db.SaveChangesAsync(ct);

            return numero;
        }, ct);
    }

    public async Task<OperacionMovimiento> BajaAsync(int numero, CancellationToken ct)
    {
        var existente = await _db.Movimientos
            .Include(m => m.Detalle)
            .FirstOrDefaultAsync(m => m.Numero == numero, ct);

        if (existente is null)
        {
            return OperacionMovimiento.NoEncontrado();
        }

        // RF-024a: la baja también mueve el saldo. Dar de baja una compra ya consumida por ventas
        // posteriores dejaría el stock en negativo, y se rechaza igual que una venta sin stock.
        var efecto = new Dictionary<int, int>();
        Restar(efecto, EfectoActualDe(existente));

        return await EnTransaccionAsync(efecto, async () =>
        {
            // El detalle se va en cascada por la FK (RF-021).
            _db.Movimientos.Remove(existente);
            await _db.SaveChangesAsync(ct);

            return numero;
        }, ct);
    }

    /// <summary>
    /// Los pasos 1, 2, 3, 4 y 6 del protocolo. El paso 5 —aplicar— lo aporta quien llama, de modo
    /// que las tres operaciones compartan una única implementación del invariante y ninguna pueda
    /// olvidarse de un paso.
    /// </summary>
    private async Task<OperacionMovimiento> EnTransaccionAsync(
        IReadOnlyDictionary<int, int> efecto, Func<Task<int>> aplicar, CancellationToken ct)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

        // 2. Bloqueo pesimista, en orden ascendente de ArticuloId.
        await _bloqueo.BloquearAsync(efecto.Keys, ct);

        // 3. Saldo actual, ya dentro de la transacción y después del bloqueo: si otra operación
        //    estaba a mitad de camino, acá se espera y se lee lo que efectivamente confirmó.
        var afectados = efecto.Keys.ToList();

        var saldos = await _db.StockActual
            .Where(s => afectados.Contains(s.ArticuloId))
            .ToDictionaryAsync(s => s.ArticuloId, s => new { s.Codigo, s.StockActual }, ct);

        // 4. Validar el resultado para TODOS los artículos antes de aplicar ninguna línea.
        foreach (var (articuloId, delta) in efecto)
        {
            if (!saldos.TryGetValue(articuloId, out var saldo))
            {
                continue;
            }

            if (saldo.StockActual + delta < 0)
            {
                await transaccion.RollbackAsync(ct);

                return OperacionMovimiento.SinStock(
                    $"Stock insuficiente para el artículo {saldo.Codigo}. " +
                    $"Stock Actual: {saldo.StockActual}; la operación lo dejaría en " +
                    $"{saldo.StockActual + delta}.");
            }
        }

        // 5. Aplicar.
        var numero = await aplicar();

        // 6. Confirmar.
        await transaccion.CommitAsync(ct);

        return OperacionMovimiento.Correcta(numero);
    }

    private static Dictionary<int, int> EfectoDe(
        TipoMovimiento tipo, IReadOnlyList<LineaAValidar> detalle)
    {
        var efecto = new Dictionary<int, int>();

        foreach (var linea in detalle)
        {
            // Se acumula por artículo: dos líneas del mismo artículo se evalúan por su efecto
            // conjunto, no por separado.
            efecto[linea.ArticuloId] =
                efecto.GetValueOrDefault(linea.ArticuloId) + (Signo(tipo) * linea.Cantidad);
        }

        return efecto;
    }

    private static Dictionary<int, int> EfectoActualDe(Movimiento movimiento) =>
        EfectoDe(
            movimiento.Tipo,
            movimiento.Detalle
                .Select(d => new LineaAValidar(d.ArticuloId, d.Cantidad, d.PrecioUnitario))
                .ToList());

    private static void Restar(Dictionary<int, int> efecto, Dictionary<int, int> aRestar)
    {
        foreach (var (articuloId, delta) in aRestar)
        {
            efecto[articuloId] = efecto.GetValueOrDefault(articuloId) - delta;
        }
    }

    private async Task<string?> ArticuloInexistenteAsync(
        IReadOnlyList<LineaAValidar> detalle, CancellationToken ct)
    {
        var pedidos = detalle.Select(l => l.ArticuloId).Distinct().ToList();

        var existentes = await _db.Articulos
            .Where(a => pedidos.Contains(a.ArticuloId))
            .Select(a => a.ArticuloId)
            .ToListAsync(ct);

        var faltantes = pedidos.Except(existentes).ToList();

        return faltantes.Count == 0
            ? null
            : $"No existe el artículo con identificador {string.Join(", ", faltantes)}.";
    }
}
