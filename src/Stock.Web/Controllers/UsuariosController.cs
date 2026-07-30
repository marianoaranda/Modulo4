using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T118 — ABM de usuarios desde la pantalla (RF-004 a RF-006, RF-010).
/// </summary>
public class UsuariosController : Controller
{
    private const string Recurso = "/api/usuarios";
    private const string RecursoPerfiles = "/api/perfiles";

    private readonly StockApiClient _api;

    public UsuariosController(StockApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(Recurso, ct);
        respuesta.EnsureSuccessStatusCode();

        return View(await RespuestaDeLaApi.LeerAsync<List<UsuarioViewModel>>(respuesta, ct) ?? []);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new UsuarioViewModel { Perfiles = await PerfilesAsync(ct) });

    [HttpPost]
    public async Task<IActionResult> Create(UsuarioViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PostAsJsonAsync(Recurso, new
        {
            nombreUsuario = vista.NombreUsuario,
            nombreCompleto = vista.NombreCompleto,
            perfilId = vista.PerfilId,
            password = vista.Password,
        }, ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        // Un 400 por contraseña que incumple RF-009, o un 409 por nombre duplicado. La contraseña
        // NO se devuelve a la vista: se limpia para que no viaje de vuelta al navegador.
        vista.Password = null;
        vista.Perfiles = await PerfilesAsync(ct);
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var vista = await LeerAsync(id, ct);

        if (vista is null)
        {
            return NotFound();
        }

        vista.Perfiles = await PerfilesAsync(ct);

        return View(vista);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UsuarioViewModel vista, CancellationToken ct)
    {
        var respuesta = await _api.Http.PutAsJsonAsync($"{Recurso}/{id}", new
        {
            nombreUsuario = vista.NombreUsuario,
            nombreCompleto = vista.NombreCompleto,
            perfilId = vista.PerfilId,

            // Vacía significa "conservar la actual" (RF-006): se manda null y no cadena vacía,
            // porque una cadena vacía es una contraseña que incumple la política y produciría un
            // 400 al usuario que sólo quiso corregirse el nombre.
            password = string.IsNullOrEmpty(vista.Password) ? null : vista.Password,
        }, ct);

        if (respuesta.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }

        vista.UsuarioId = id;
        vista.Password = null;
        vista.Perfiles = await PerfilesAsync(ct);
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

        // RF-005a: es el último administrador y la baja se rechaza con 409.
        var vista = await LeerAsync(id, ct) ?? new UsuarioViewModel { UsuarioId = id };
        vista.MensajeDeRechazo = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);

        return View(vista);
    }

    private async Task<UsuarioViewModel?> LeerAsync(int id, CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync($"{Recurso}/{id}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        respuesta.EnsureSuccessStatusCode();

        return await RespuestaDeLaApi.LeerAsync<UsuarioViewModel>(respuesta, ct);
    }

    private async Task<List<PerfilViewModel>> PerfilesAsync(CancellationToken ct)
    {
        var respuesta = await _api.Http.GetAsync(RecursoPerfiles, ct);

        return respuesta.IsSuccessStatusCode
            ? await RespuestaDeLaApi.LeerAsync<List<PerfilViewModel>>(respuesta, ct) ?? []
            : [];
    }
}
