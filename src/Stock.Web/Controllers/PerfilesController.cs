using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T118 — ABM de perfiles desde la pantalla.
///
/// El control de acceso real lo hace la API, que responde 403 a quien no tenga la marca de
/// administrador (RF-010a). Acá sólo se ocultan las entradas de menú: una interfaz que ofrece
/// pantallas destinadas a fallar es una interfaz que miente.
/// </summary>
public class PerfilesController : Controller
{
    private const string Recurso = "/api/perfiles";

    private readonly StockApiClient _api;

    public PerfilesController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(Recurso, ct);
        respuesta.EnsureSuccessStatusCode();

        return View(await RespuestaDeLaApi.LeerAsync<List<PerfilViewModel>>(respuesta, ct) ?? []);
    }

    [HttpGet]
    public IActionResult Create() => View(new PerfilViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(PerfilViewModel vista, CancellationToken ct)
    {
        // Sólo se envía la Descripción: la marca no es alcanzable desde la API (RF-003a).
        var respuesta = await _api.Http.PostAsJsonAsync(
            Recurso, new { descripcion = vista.Descripcion }, ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

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
    public async Task<IActionResult> Edit(int id, PerfilViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PutAsJsonAsync(
            $"{Recurso}/{id}", new { descripcion = vista.Descripcion }, ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        vista.PerfilId = id;
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

        // RF-002a (perfil con usuarios) o RF-002b (perfil administrador): dos rechazos previstos
        // con el mismo 409, distinguibles por el mensaje que redacta la API.
        var vista = await LeerAsync(id, ct) ?? new PerfilViewModel { PerfilId = id };
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    private async Task<PerfilViewModel?> LeerAsync(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/{id}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        respuesta.EnsureSuccessStatusCode();

        return await RespuestaDeLaApi.LeerAsync<PerfilViewModel>(respuesta, ct);
    }
}
