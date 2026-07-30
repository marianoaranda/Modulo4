using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

/// <summary>
/// Línea de detalle de la solicitud.
///
/// <c>Cantidad</c> y <c>ArticuloId</c> son <c>int</c>: es el tipado lo que implementa RF-018a. Un
/// valor no entero se rechaza al deserializar, en el borde de la solicitud, con un 400 que
/// identifica el campo — antes de llegar a ninguna regla de negocio y sin grabar nada.
/// </summary>
public sealed class LineaRequest
{
    public int ArticuloId { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
}

public sealed class MovimientoRequest
{
    public TipoMovimiento Tipo { get; set; }

    public DateOnly Fecha { get; set; }

    public List<LineaRequest> Detalle { get; set; } = [];
}

public sealed record DetalleResponse(
    int ArticuloId, string Codigo, int Cantidad, decimal PrecioUnitario, decimal PrecioTotal);

public sealed record MovimientoResponse(
    int Numero, TipoMovimiento Tipo, DateOnly Fecha, IReadOnlyList<DetalleResponse> Detalle);

/// <summary>
/// T075 — CRUD de movimientos (RF-020 a RF-024c).
///
/// El controlador no contiene ninguna regla: traduce el resultado de <c>MovimientoService</c> al
/// código de estado que fija <c>contracts/README.md</c>. La distinción entre 400, 404 y 422 es
/// deliberada y hace testeable el comportamiento del spec — en particular, que el rechazo por
/// stock sea siempre 422 y nunca un 409 de conflicto que obligue a reintentar (RF-024b).
/// </summary>
[ApiController]
[Route("api/movimientos")]
public class MovimientosController : ControllerBase
{
    private readonly MovimientoService _servicio;
    private readonly StockDbContext _db;

    public MovimientosController(MovimientoService servicio, StockDbContext db)
    {
        _servicio = servicio;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MovimientoResponse>>> Listar(CancellationToken ct) =>
        Ok(await Proyectar(_db.Movimientos.OrderBy(m => m.Numero)).ToListAsync(ct));

    [HttpGet("{numero:int}")]
    public async Task<IActionResult> Leer(int numero, CancellationToken ct)
    {
        var movimiento = await Proyectar(_db.Movimientos.Where(m => m.Numero == numero))
            .FirstOrDefaultAsync(ct);

        return movimiento is null ? NoEncontrado() : Ok(movimiento);
    }

    [HttpPost]
    public async Task<IActionResult> Alta([FromBody] MovimientoRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.AltaAsync(ADominio(solicitud), ct);

        if (!resultado.Exito)
        {
            return Traducir(resultado);
        }

        var creado = await Proyectar(_db.Movimientos.Where(m => m.Numero == resultado.Numero))
            .FirstAsync(ct);

        return CreatedAtAction(nameof(Leer), new { numero = resultado.Numero }, creado);
    }

    [HttpPut("{numero:int}")]
    public async Task<IActionResult> Modificar(
        int numero, [FromBody] MovimientoRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.ModificarAsync(numero, ADominio(solicitud), ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    [HttpDelete("{numero:int}")]
    public async Task<IActionResult> Baja(int numero, CancellationToken ct)
    {
        var resultado = await _servicio.BajaAsync(numero, ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    /// <summary>
    /// Proyecta a la forma del contrato. Recibe la consulta <b>ya filtrada</b>: filtrar después de
    /// proyectar obligaría a EF Core a traducir un predicado sobre el tipo de respuesta, que no
    /// tiene equivalente en SQL.
    /// </summary>
    private static IQueryable<MovimientoResponse> Proyectar(IQueryable<Movimiento> consulta) =>
        consulta.Select(m => new MovimientoResponse(
            m.Numero,
            m.Tipo,
            m.Fecha,
            m.Detalle.Select(d => new DetalleResponse(
                d.ArticuloId,
                d.Articulo!.Codigo,
                d.Cantidad,
                d.PrecioUnitario,
                d.PrecioTotal)).ToList()));

    private static MovimientoAValidar ADominio(MovimientoRequest solicitud) =>
        new(solicitud.Tipo,
            solicitud.Fecha,
            solicitud.Detalle
                .Select(l => new LineaAValidar(l.ArticuloId, l.Cantidad, l.PrecioUnitario))
                .ToList());

    private IActionResult Traducir(OperacionMovimiento resultado) => resultado.Fallo switch
    {
        FalloDeMovimiento.NoEncontrado => NoEncontrado(),

        // 422: sintácticamente válido pero viola un invariante de negocio. Es también la respuesta
        // ante concurrencia perdida, evaluada contra el saldo ya actualizado (RF-024b).
        FalloDeMovimiento.StockInsuficiente => Problem(
            detail: resultado.Mensaje,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Stock insuficiente"),

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
        detail: "El Movimiento no existe.",
        statusCode: StatusCodes.Status404NotFound,
        title: "No encontrado");
}
