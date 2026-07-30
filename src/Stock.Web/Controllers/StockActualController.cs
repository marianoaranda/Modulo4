using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T081 — Consulta de Stock Actual (RF-025).
/// </summary>
public class StockActualController : Controller
{
    private readonly StockApiClient _api;

    public StockActualController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? codigoDesde, string? codigoHasta, string? descripcion, bool consultar,
        CancellationToken ct)
    {
        var vista = new StockActualViewModel
        {
            CodigoDesde = codigoDesde,
            CodigoHasta = codigoHasta,
            Descripcion = descripcion,
        };

        // Todos los parámetros son opcionales, así que no alcanza con mirar si vinieron: se
        // distingue el primer ingreso a la pantalla de una consulta ejecutada sin filtros. Sin esa
        // distinción, la pantalla informaría que no hubo resultados antes de que el usuario
        // buscara nada.
        var seConsulto = consultar
            || codigoDesde is not null || codigoHasta is not null || descripcion is not null;

        if (!seConsulto)
        {
            return View(vista);
        }

        var respuesta = await _api.Http.GetAsync(UrlDeConsulta(
            "/api/consultas/stock-actual", codigoDesde, codigoHasta, descripcion), ct);

        respuesta.EnsureSuccessStatusCode();

        var resultado = await RespuestaDeLaApi.LeerAsync<RespuestaStockActual>(respuesta, ct)
            ?? new RespuestaStockActual();

        vista.Consultada = true;
        vista.Filas = resultado.Filas;
        vista.Truncado = resultado.Truncado;

        return View(vista);
    }

    /// <summary>Retransmite el archivo que genera la API, sin regenerarlo (RF-031, R-05).</summary>
    [HttpGet]
    public async Task<IActionResult> Excel(
        string? codigoDesde, string? codigoHasta, string? descripcion, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(UrlDeConsulta(
            "/api/consultas/stock-actual/excel", codigoDesde, codigoHasta, descripcion), ct);

        respuesta.EnsureSuccessStatusCode();

        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        var tipo = respuesta.Content.Headers.ContentType?.MediaType
            ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return File(contenido, tipo, "stock-actual.xlsx");
    }

    private static string UrlDeConsulta(
        string ruta, string? codigoDesde, string? codigoHasta, string? descripcion) =>
        $"{ruta}?codigoDesde={Uri.EscapeDataString(codigoDesde ?? string.Empty)}" +
        $"&codigoHasta={Uri.EscapeDataString(codigoHasta ?? string.Empty)}" +
        $"&descripcion={Uri.EscapeDataString(descripcion ?? string.Empty)}";
}
