using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Validation;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

/// <summary>
/// Cuerpo de alta y modificación de artículo.
///
/// Los tres parámetros de reposición son <c>int</c>: ese tipado es lo que implementa RF-018a. Y
/// <b>no hay propiedad <c>PrecioVenta</c></b>: al no existir en el DTO, un cuerpo que la incluya
/// se ignora sin necesidad de código defensivo, y el precio no puede desalinearse de costo y
/// margen (RF-016).
/// </summary>
public sealed class ArticuloRequest
{
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public decimal PrecioCosto { get; set; }

    public decimal Margen { get; set; }

    public int StockMinimo { get; set; }

    public int PuntoPedido { get; set; }

    public int StockIdeal { get; set; }
}

/// <summary>Extremos del catálogo según el orden de RF-025a; ambos nulos si está vacío.</summary>
public sealed record ExtremosResponse(string? CodigoDesde, string? CodigoHasta);

public sealed record ArticuloResponse(
    int ArticuloId,
    string Codigo,
    string Descripcion,
    decimal PrecioCosto,
    decimal Margen,
    decimal PrecioVenta,
    int StockMinimo,
    int PuntoPedido,
    int StockIdeal);

/// <summary>
/// T089 — CRUD de artículos (RF-013 a RF-019).
/// </summary>
[ApiController]
[Route("api/articulos")]
public class ArticulosController : ControllerBase
{
    private readonly ArticuloService _servicio;
    private readonly StockDbContext _db;

    public ArticulosController(ArticuloService servicio, StockDbContext db)
    {
        _servicio = servicio;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? descripcion, [FromQuery] string? codigo, CancellationToken ct)
    {
        var consulta = _db.Articulos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            // Coincidencia exacta, con la misma regla que la unicidad del Código: insensible a
            // mayúsculas y sensible a acentos (RF-017a). La aporta la collation de la columna, no
            // código propio, de modo que la búsqueda y la unicidad no puedan divergir.
            //
            // Un Código que no existe devuelve un arreglo vacío y no un 404: para la pantalla de
            // carga eso significa "no hay sugerencia", que no es un error (RF-020g). El 404 sigue
            // siendo el de RF-020e, al grabar el movimiento.
            var buscado = codigo.Trim();

            consulta = consulta.Where(a => a.Codigo == buscado);
        }

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            // Insensible a mayúsculas y acentos por la collation de la columna (RF-027a, R-06).
            consulta = consulta.Where(a => EF.Functions.Like(a.Descripcion, $"%{descripcion}%"));
        }

        // El tope de la constitución también rige acá: ninguna consulta sin límite.
        var articulos = await consulta
            .OrderBy(a => a.Codigo)
            .Take(LimitesDeConsulta.TopeDeFilas)
            .Select(a => Proyectar(a))
            .ToListAsync(ct);

        return Ok(articulos);
    }

    /// <summary>
    /// T162 — RF-025b: los extremos del catálogo, que la pantalla sugiere como rango.
    ///
    /// Es un recurso propio y no una lectura del listado porque el listado recorta en 10.000 filas
    /// (RF-027): su última fila no es el último Código del catálogo, y el rango sugerido dejaría
    /// afuera artículos justo en el caso que ese tope contempla.
    ///
    /// <c>MIN</c>/<c>MAX</c> se resuelven en el motor y no traen filas a memoria, y el orden que
    /// aplican es el de la collation de la columna — la misma que define RF-025a y que ordena el
    /// resto de las consultas—, así que no hay una segunda regla de orden que pueda divergir.
    /// </summary>
    [HttpGet("extremos")]
    public async Task<IActionResult> Extremos(CancellationToken ct)
    {
        var extremos = await _db.Articulos
            .GroupBy(_ => 1)
            .Select(g => new ExtremosResponse(g.Min(a => a.Codigo), g.Max(a => a.Codigo)))
            .FirstOrDefaultAsync(ct);

        // Catálogo vacío: no es un recurso que falte, así que 200 con ambos en nulo y no 404.
        return Ok(extremos ?? new ExtremosResponse(null, null));
    }

    [HttpGet("{articuloId:int}")]
    public async Task<IActionResult> Leer(int articuloId, CancellationToken ct)
    {
        var articulo = await _db.Articulos
            .Where(a => a.ArticuloId == articuloId)
            .Select(a => Proyectar(a))
            .FirstOrDefaultAsync(ct);

        return articulo is null ? NoEncontrado() : Ok(articulo);
    }

    [HttpPost]
    public async Task<IActionResult> Alta([FromBody] ArticuloRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.AltaAsync(ADominio(solicitud), ct);

        if (!resultado.Exito)
        {
            return Traducir(resultado);
        }

        var creado = Proyectar(resultado.Articulo!);

        return CreatedAtAction(nameof(Leer), new { articuloId = creado.ArticuloId }, creado);
    }

    [HttpPut("{articuloId:int}")]
    public async Task<IActionResult> Modificar(
        int articuloId, [FromBody] ArticuloRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.ModificarAsync(articuloId, ADominio(solicitud), ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    [HttpDelete("{articuloId:int}")]
    public async Task<IActionResult> Baja(int articuloId, CancellationToken ct)
    {
        var resultado = await _servicio.BajaAsync(articuloId, ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    private static ArticuloResponse Proyectar(Domain.Entities.Articulo a) => new(
        a.ArticuloId, a.Codigo, a.Descripcion, a.PrecioCosto, a.Margen, a.PrecioVenta,
        a.StockMinimo, a.PuntoPedido, a.StockIdeal);

    private static ArticuloAValidar ADominio(ArticuloRequest solicitud) => new(
        solicitud.Codigo?.Trim() ?? string.Empty,
        solicitud.Descripcion?.Trim() ?? string.Empty,
        solicitud.PrecioCosto,
        solicitud.Margen,
        solicitud.StockMinimo,
        solicitud.PuntoPedido,
        solicitud.StockIdeal);

    private IActionResult Traducir(OperacionArticulo resultado) => resultado.Fallo switch
    {
        FalloDeArticulo.NoEncontrado => NoEncontrado(),

        // 409: conflicto con el estado actual de los datos — código duplicado (RF-017) o baja
        // restringida (RF-014a). Se distingue del 400 de validación de campo a propósito.
        FalloDeArticulo.Conflicto => Problem(
            detail: resultado.Mensaje,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflicto"),

        _ => ProblemaDeValidacion(resultado.Errores!),
    };

    private IActionResult ProblemaDeValidacion(IReadOnlyList<ErrorDeValidacion> errores)
    {
        foreach (var error in errores)
        {
            ModelState.AddModelError(error.Campo, error.Mensaje);
        }

        return ValidationProblem(ModelState);
    }

    private IActionResult NoEncontrado() => Problem(
        detail: "El artículo no existe.",
        statusCode: StatusCodes.Status404NotFound,
        title: "No encontrado");
}
