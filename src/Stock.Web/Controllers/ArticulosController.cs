using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T091 — ABM de artículos desde la pantalla.
/// </summary>
public class ArticulosController : Controller
{
    private const string Recurso = "/api/articulos";

    private readonly StockApiClient _api;

    public ArticulosController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(string? descripcion, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(
            $"{Recurso}?descripcion={Uri.EscapeDataString(descripcion ?? string.Empty)}", ct);

        respuesta.EnsureSuccessStatusCode();

        var articulos = await RespuestaDeLaApi.LeerAsync<List<ArticuloViewModel>>(respuesta, ct);

        ViewData["Descripcion"] = descripcion;

        return View(articulos ?? []);
    }

    [HttpGet]
    public IActionResult Create() => View(new ArticuloViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(ArticuloViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PostAsJsonAsync(Recurso, ASolicitud(vista), ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        // Un 400 de validación o un 409 de código duplicado son rechazos previstos: se vuelve al
        // formulario con el motivo y los datos ya cargados.
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var vista = await LeerAsync(id, ct);

        return vista is null ? NotFound() : View(vista);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ArticuloViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PutAsJsonAsync($"{Recurso}/{id}", ASolicitud(vista), ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        vista.ArticuloId = id;
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var vista = await LeerAsync(id, ct);

        return vista is null ? NotFound() : View(vista);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> ConfirmarBaja(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.DeleteAsync($"{Recurso}/{id}", ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        // RF-014a: el artículo tiene movimientos y la baja se rechaza con 409. El usuario tiene
        // que ver el motivo, no una página de error.
        var vista = await LeerAsync(id, ct) ?? new ArticuloViewModel { ArticuloId = id };
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    private async Task<ArticuloViewModel?> LeerAsync(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/{id}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        respuesta.EnsureSuccessStatusCode();

        return await RespuestaDeLaApi.LeerAsync<ArticuloViewModel>(respuesta, ct);
    }

    /// <summary>
    /// El Precio de Venta no se envía: lo calcula el motor (RF-016). Omitirlo acá es lo que hace
    /// que el campo de sólo lectura de la vista sea coherente con lo que efectivamente ocurre.
    /// </summary>
    private static object ASolicitud(ArticuloViewModel vista) => new
    {
        codigo = vista.Codigo,
        descripcion = vista.Descripcion,
        precioCosto = vista.PrecioCosto,
        margen = vista.Margen,
        stockMinimo = vista.StockMinimo,
        puntoPedido = vista.PuntoPedido,
        stockIdeal = vista.StockIdeal,
    };
}
