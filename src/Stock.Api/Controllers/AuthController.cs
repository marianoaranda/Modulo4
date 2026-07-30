using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Security;

namespace Stock.Api.Controllers;

public sealed class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string Token, DateTimeOffset ExpiraEn, string Perfil);

/// <summary>
/// T099 — Inicio de sesión (RF-011). Es el <b>único</b> endpoint público de la API.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    /// <summary>
    /// El mismo mensaje para usuario inexistente y para contraseña incorrecta. Si difirieran, el
    /// login funcionaría como oráculo de existencia de cuentas: probando nombres se sabría cuáles
    /// existen antes de intentar ninguna contraseña (RF-011).
    /// </summary>
    private const string MensajeGenerico = "Usuario o contraseña incorrectos.";

    private readonly StockDbContext _db;
    private readonly JwtTokenService _tokens;

    public AuthController(StockDbContext db, JwtTokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest solicitud, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.NombreUsuario == solicitud.Usuario, ct);

        // Se verifica siempre, incluso cuando el usuario no existe, contra una credencial
        // descartable. Así el costo en tiempo del login es parecido en ambos casos y no se puede
        // inferir la existencia de la cuenta midiendo cuánto tarda la respuesta.
        var coincide = usuario is null
            ? PasswordHasher.Verificar(solicitud.Password, new byte[32], new byte[16])
            : PasswordHasher.Verificar(solicitud.Password, usuario.Hash, usuario.Salt);

        if (usuario?.Perfil is null || !coincide)
        {
            return Problem(
                detail: MensajeGenerico,
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado");
        }

        var emitido = _tokens.Emitir(usuario, usuario.Perfil);

        return Ok(new LoginResponse(emitido.Token, emitido.ExpiraEn, usuario.Perfil.Descripcion));
    }
}
