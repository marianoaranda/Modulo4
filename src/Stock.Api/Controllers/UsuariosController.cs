using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Validation;
using Stock.Api.Security;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

public sealed class UsuarioRequest
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public int PerfilId { get; set; }

    /// <summary>
    /// Obligatoria en el alta. En la modificación, si se omite se conserva el hash existente
    /// (RF-006): re-derivarlo con una cadena vacía dejaría afuera del sistema a quien sólo quiso
    /// corregirse el nombre.
    /// </summary>
    public string? Password { get; set; }
}

/// <summary>
/// Respuesta de usuario. <b>No tiene <c>Hash</c> ni <c>Salt</c></b>: no existe forma de que se
/// filtren porque no hay propiedad que los transporte (RF-007).
/// </summary>
public sealed record UsuarioResponse(
    int UsuarioId, string NombreUsuario, string NombreCompleto, int PerfilId);

/// <summary>
/// T116 — ABM de usuarios (RF-004 a RF-006), restringido al perfil administrador (RF-010).
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = AuthorizationPolicies.SoloAdministrador)]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _servicio;
    private readonly StockDbContext _db;

    public UsuariosController(UsuarioService servicio, StockDbContext db)
    {
        _servicio = servicio;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await _db.Usuarios
            .OrderBy(u => u.UsuarioId)
            .Select(u => new UsuarioResponse(
                u.UsuarioId, u.NombreUsuario, u.NombreCompleto, u.PerfilId))
            .ToListAsync(ct));

    [HttpGet("{usuarioId:int}")]
    public async Task<IActionResult> Leer(int usuarioId, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .Where(u => u.UsuarioId == usuarioId)
            .Select(u => new UsuarioResponse(
                u.UsuarioId, u.NombreUsuario, u.NombreCompleto, u.PerfilId))
            .FirstOrDefaultAsync(ct);

        return usuario is null
            ? Traducir(OperacionSeguridad.NoEncontrado("El usuario no existe."))
            : Ok(usuario);
    }

    [HttpPost]
    public async Task<IActionResult> Alta([FromBody] UsuarioRequest solicitud, CancellationToken ct)
    {
        var (resultado, usuario) = await _servicio.AltaAsync(ADominio(solicitud), ct);

        if (!resultado.Exito)
        {
            return Traducir(resultado);
        }

        var creado = new UsuarioResponse(
            usuario!.UsuarioId, usuario.NombreUsuario, usuario.NombreCompleto, usuario.PerfilId);

        return CreatedAtAction(nameof(Leer), new { usuarioId = creado.UsuarioId }, creado);
    }

    [HttpPut("{usuarioId:int}")]
    public async Task<IActionResult> Modificar(
        int usuarioId, [FromBody] UsuarioRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.ModificarAsync(usuarioId, ADominio(solicitud), ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    [HttpDelete("{usuarioId:int}")]
    public async Task<IActionResult> Baja(int usuarioId, CancellationToken ct)
    {
        var resultado = await _servicio.BajaAsync(usuarioId, ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    private static UsuarioAGrabar ADominio(UsuarioRequest solicitud) => new(
        solicitud.NombreUsuario, solicitud.NombreCompleto, solicitud.PerfilId, solicitud.Password);

    private IActionResult Traducir(OperacionSeguridad resultado) => resultado.Fallo switch
    {
        FalloDeSeguridad.NoEncontrado => Problem(
            detail: resultado.Mensaje,
            statusCode: StatusCodes.Status404NotFound,
            title: "No encontrado"),

        // 409: nombre de usuario duplicado, o último administrador (RF-005a).
        FalloDeSeguridad.Conflicto => Problem(
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
}
