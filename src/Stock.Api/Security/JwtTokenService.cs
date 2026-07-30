using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Stock.Api.Configuration;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Security;

public sealed record TokenEmitido(string Token, DateTimeOffset ExpiraEn);

/// <summary>
/// T098 — Emisión de tokens (R-04).
///
/// HS256, vigencia de 8 horas, sin <i>refresh token</i>: ocho horas cubren un turno completo del
/// comercio, y un refresh token agregaría almacenamiento, rotación y revocación para un sistema de
/// uno a cinco usuarios sin requisito que lo pida.
/// </summary>
public class JwtTokenService
{
    /// <summary>
    /// La <b>única</b> base de las decisiones de autorización. Se deriva de la marca inmutable
    /// <c>Perfil.EsAdministrador</c>, que la siembra fija y el ABM no expone (RF-003a).
    /// </summary>
    public const string ClaimEsAdmin = "es_admin";

    private readonly OpcionesDeArranque _opciones;

    public JwtTokenService(OpcionesDeArranque opciones) => _opciones = opciones;

    public SymmetricSecurityKey ClaveDeFirma =>
        new(Encoding.UTF8.GetBytes(_opciones.ClaveDeFirmaJwt));

    public TokenEmitido Emitir(Usuario usuario, Perfil perfil)
    {
        var expiraEn = DateTimeOffset.UtcNow.AddHours(_opciones.VigenciaHoras);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.UsuarioId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", usuario.NombreUsuario),

            // `role` lleva la Descripción del perfil y existe SÓLO para mostrar. Usarlo como base
            // de la política haría que renombrar el perfil administrador dejara al sistema sin
            // administrador, y que renombrar otro perfil a "administrador" concediera el
            // privilegio — exactamente lo que RF-003a prohíbe.
            new("role", perfil.Descripcion),

            new(ClaimEsAdmin, perfil.EsAdministrador ? "true" : "false"),
        };

        var token = new JwtSecurityToken(
            issuer: _opciones.Issuer,
            audience: _opciones.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiraEn.UtcDateTime,
            signingCredentials: new SigningCredentials(ClaveDeFirma, SecurityAlgorithms.HmacSha256));

        return new TokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }

    /// <summary>
    /// Parámetros de validación. <c>ClockSkew</c> en cero para que la expiración sea exacta: con
    /// la tolerancia por defecto de cinco minutos, un token vencido seguiría siendo aceptado un
    /// rato más, y la vigencia declarada no sería la real.
    /// </summary>
    public TokenValidationParameters ParametrosDeValidacion() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _opciones.Issuer,
        ValidateAudience = true,
        ValidAudience = _opciones.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = ClaveDeFirma,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role",
        NameClaimType = "name",
    };
}
