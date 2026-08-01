using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Resources;
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
        var articulos = await ListarAsync(descripcion, codigo: null, ct);

        ViewData["Descripcion"] = descripcion;

        return View(articulos);
    }

    /// <summary>
    /// Única lectura del listado de artículos: la comparten la pantalla y la puerta JSON del
    /// buscador, de modo que las dos vean exactamente el mismo conjunto y el mismo tope.
    /// </summary>
    private async Task<List<ArticuloViewModel>> ListarAsync(
        string? descripcion, string? codigo, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(
            $"{Recurso}?descripcion={Uri.EscapeDataString(descripcion ?? string.Empty)}" +
            $"&codigo={Uri.EscapeDataString(codigo ?? string.Empty)}", ct);

        respuesta.EnsureSuccessStatusCode();

        return await RespuestaDeLaApi.LeerAsync<List<ArticuloViewModel>>(respuesta, ct) ?? [];
    }

    /// <summary>
    /// T141a — La puerta JSON del buscador de artículos (RF-034a).
    ///
    /// Existe porque el script del navegador <b>no puede</b> consumir <c>Stock.Api</c>: el JWT vive
    /// en un claim de la cookie de sesión y lo adjunta <c>BearerTokenHandler</c> a las llamadas
    /// salientes del servidor. Mandarlo al navegador para que llamara directo sería la
    /// alternativa, y expondría al cliente una credencial que hoy nunca sale de acá.
    ///
    /// Devuelve sólo Código y Descripción —lo que la grilla muestra— y el aviso de recorte ya
    /// resuelto, con el texto exacto de RF-032a: dejar que el script lo arme sería una segunda
    /// copia de una cadena que el spec fija al carácter.
    /// </summary>
    /// <remarks>
    /// El parámetro <c>codigo</c> resuelve un Código puntual y devuelve, además de la Descripción,
    /// los dos precios del catálogo: es la <b>única consulta por Código</b> de la pantalla de
    /// movimientos (RF-020g), la misma que sincroniza la Descripción de RF-034b. Dos consultas
    /// separadas podrían mostrar un artículo y sugerir el precio de otro.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Buscar(string? descripcion, string? codigo, CancellationToken ct)
    {
        var articulos = await ListarAsync(descripcion, codigo, ct);

        var truncado = articulos.Count >= LimitesDeConsulta.TopeDeFilas;

        return Json(new
        {
            filas = articulos.Select(a => new
            {
                codigo = a.Codigo,
                descripcion = a.Descripcion,
                precioCosto = a.PrecioCosto,
                precioVenta = a.PrecioVenta,
            }),
            truncado,
            aviso = truncado ? MensajesDeConsulta.ResultadoRecortado : null,
        });
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
