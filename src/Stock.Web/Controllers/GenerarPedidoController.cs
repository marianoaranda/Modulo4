using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T057 — Pantalla de Generar Pedido. Consume la API y retransmite el Excel.
/// </summary>
public class GenerarPedidoController : Controller
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly StockApiClient _api;

    public GenerarPedidoController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(
        bool? soloBajoMinimo, ModoPedidoWeb? modoPedido, string? descripcion, CancellationToken ct)
    {
        var vista = new GenerarPedidoViewModel
        {
            SoloBajoMinimo = soloBajoMinimo,
            ModoPedido = modoPedido,
            Descripcion = descripcion,
        };

        // Los dos parámetros de reposición son obligatorios (RF-026b). En el primer ingreso a la
        // pantalla todavía no se eligieron: se muestra el formulario sin consultar ni informar
        // nada, que es distinto de haber consultado y no haber obtenido filas.
        if (soloBajoMinimo is null || modoPedido is null)
        {
            return View(vista);
        }

        var respuesta = await _api.Http.GetAsync(
            $"/api/consultas/generar-pedido?soloBajoMinimo={soloBajoMinimo.Value.ToString().ToLowerInvariant()}" +
            $"&modoPedido={modoPedido.Value}" +
            $"&descripcion={Uri.EscapeDataString(descripcion ?? string.Empty)}",
            ct);

        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        var resultado = JsonSerializer.Deserialize<RespuestaGenerarPedido>(cuerpo, Json)
            ?? new RespuestaGenerarPedido();

        vista.Consultada = true;
        vista.Filas = resultado.Filas;
        vista.Truncado = resultado.Truncado;

        return View(vista);
    }

    /// <summary>
    /// Retransmite al navegador el archivo que genera la API, sin regenerarlo.
    ///
    /// Es lo que hace que RF-031 se cumpla por construcción: si la capa web armara su propio
    /// Excel, el requisito pasaría a depender de que dos implementaciones de la consulta, el filtro
    /// y el tope coincidieran (R-05).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Excel(
        bool? soloBajoMinimo, ModoPedidoWeb? modoPedido, string? descripcion, CancellationToken ct)
    {
        if (soloBajoMinimo is null || modoPedido is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var respuesta = await _api.Http.GetAsync(
            $"/api/consultas/generar-pedido/excel?soloBajoMinimo={soloBajoMinimo.Value.ToString().ToLowerInvariant()}" +
            $"&modoPedido={modoPedido.Value}" +
            $"&descripcion={Uri.EscapeDataString(descripcion ?? string.Empty)}",
            ct);

        respuesta.EnsureSuccessStatusCode();

        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        var tipo = respuesta.Content.Headers.ContentType?.MediaType
            ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return File(contenido, tipo, "generar-pedido.xlsx");
    }
}
