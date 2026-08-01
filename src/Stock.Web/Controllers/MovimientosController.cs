using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T079 — Alta, modificación y baja de movimientos desde la pantalla.
/// </summary>
public class MovimientosController : Controller
{
    private const string Recurso = "/api/movimientos";

    private readonly StockApiClient _api;

    public MovimientosController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(Recurso, ct);
        respuesta.EnsureSuccessStatusCode();

        var movimientos = await RespuestaDeLaApi.LeerAsync<List<MovimientoViewModel>>(respuesta, ct);

        return View(movimientos ?? []);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) => View(new MovimientoViewModel
    {
        // Una línea vacía para que el formulario tenga por dónde empezar.
        Detalle = [new LineaDetalleViewModel()],

        // RF-020f: sugerencia informativa. Si la API no contesta, la pantalla se abre igual sin
        // el Número: es un dato de comodidad y no una condición para poder cargar.
        NumeroSugerido = await ProximoNumeroAsync(ct),
    });

    private async Task<int?> ProximoNumeroAsync(CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/proximo-numero", ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        var proximo = await RespuestaDeLaApi.LeerAsync<ProximoNumeroViewModel>(respuesta, ct);

        return proximo?.Numero;
    }

    [HttpPost]
    public async Task<IActionResult> Create(MovimientoViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PostAsJsonAsync(Recurso, ASolicitud(vista), ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        // Un 400 o un 422 son rechazos previstos: se vuelve al formulario con el motivo y los
        // datos que el usuario ya había cargado, no a una página de error.
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/{id}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        respuesta.EnsureSuccessStatusCode();

        var movimiento = await RespuestaDeLaApi.LeerAsync<MovimientoViewModel>(respuesta, ct);

        return movimiento is null ? NotFound() : View(movimiento);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, MovimientoViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PutAsJsonAsync($"{Recurso}/{id}", ASolicitud(vista), ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        vista.Numero = id;
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/{id}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        respuesta.EnsureSuccessStatusCode();

        var movimiento = await RespuestaDeLaApi.LeerAsync<MovimientoViewModel>(respuesta, ct);

        return movimiento is null ? NotFound() : View(movimiento);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> ConfirmarBaja(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.DeleteAsync($"{Recurso}/{id}", ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        // RF-024a: la baja de una compra ya consumida por ventas posteriores se rechaza con 422.
        // El usuario tiene que ver por qué, en la misma pantalla de confirmación.
        var vista = await RespuestaDeLaApi.LeerAsync<MovimientoViewModel>(
            await _api.Http.GetAsync($"{Recurso}/{id}", ct), ct) ?? new MovimientoViewModel();

        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    private static object ASolicitud(MovimientoViewModel vista) => new
    {
        tipo = vista.Tipo.ToString(),
        fecha = vista.Fecha.ToString("yyyy-MM-dd"),
        detalle = vista.Detalle
            // Las líneas que el usuario dejó en blanco no se envían: son filas del formulario que
            // nunca completó, no líneas con cantidad 0 que la API deba rechazar.
            .Where(l => !string.IsNullOrWhiteSpace(l.Codigo))
            .Select(l => new
            {
                codigo = l.Codigo!.Trim(),
                cantidad = l.Cantidad,
                precioUnitario = l.PrecioUnitario,
            })
            .ToList(),
    };
}
