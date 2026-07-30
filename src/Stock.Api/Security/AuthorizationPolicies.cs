using Microsoft.AspNetCore.Authorization;

namespace Stock.Api.Security;

/// <summary>
/// T112 — Política de autorización de los dos ABM de seguridad (RF-010, RF-010a, RF-003a).
///
/// Exige el claim <c>es_admin = "true"</c> y <b>nada más</b>. En particular, no mira el claim
/// <c>role</c> ni ninguna cadena de Descripción: <c>role</c> lleva la Descripción del perfil, que
/// RF-003 permite cambiar libremente, así que basar la política en él haría que renombrar el
/// perfil administrador dejara al sistema sin administrador y que renombrar otro perfil a
/// "administrador" concediera el privilegio.
///
/// Al usuario autenticado que no tenga el claim se le responde 403, distinto del 401 de RF-012 que
/// corresponde a la ausencia de sesión.
/// </summary>
public static class AuthorizationPolicies
{
    public const string SoloAdministrador = "SoloAdministrador";

    public static void Agregar(AuthorizationOptions opciones) =>
        opciones.AddPolicy(SoloAdministrador, politica => politica
            .RequireAuthenticatedUser()
            .RequireClaim(JwtTokenService.ClaimEsAdmin, "true"));
}
