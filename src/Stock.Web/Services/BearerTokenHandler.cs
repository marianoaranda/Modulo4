using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Stock.Web.Services;

/// <summary>
/// T104 — Adjunta el token a cada llamada saliente y maneja el 401 (R-09).
///
/// Va como <c>DelegatingHandler</c> del <c>HttpClient</c> tipado y no como código en cada
/// controlador por dos motivos. El primero es que un controlador nuevo queda cubierto sin que
/// nadie tenga que acordarse de adjuntar el encabezado. El segundo es el manejo del 401: cuando el
/// token vence, la respuesta llega en cualquier llamada, y la reacción —cerrar la sesión y
/// mandar al login— tiene que ser la misma siempre.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    /// <summary>Nombre del claim donde la cookie guarda el JWT.</summary>
    public const string ClaimDelToken = "stock_api_token";

    private readonly IHttpContextAccessor _contexto;

    public BearerTokenHandler(IHttpContextAccessor contexto) => _contexto = contexto;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var contexto = _contexto.HttpContext;

        var token = contexto?.User.FindFirst(ClaimDelToken)?.Value;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var respuesta = await base.SendAsync(request, cancellationToken);

        // El login es la única llamada que puede recibir un 401 legítimo sin que haya sesión que
        // cerrar: es precisamente el 401 de "usuario o contraseña incorrectos".
        var esElLogin = request.RequestUri?.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal) == true;

        if (respuesta.StatusCode == HttpStatusCode.Unauthorized && contexto is not null && !esElLogin)
        {
            // El token venció o dejó de ser válido. Quedarse con la cookie dejaría al usuario en
            // un limbo: la aplicación lo trataría como logueado y ninguna pantalla funcionaría.
            await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Se señaliza con una excepción propia para que el filtro global la convierta en una
            // redirección al login. Devolver el 401 tal cual haría que el controlador reventara
            // en EnsureSuccessStatusCode y el usuario viera un error de servidor en lugar de la
            // pantalla de ingreso.
            throw new SesionVencidaException();
        }

        return respuesta;
    }
}

/// <summary>
/// Señala que la API rechazó la llamada por falta de sesión válida. La convierte en una
/// redirección al login el filtro <see cref="RedirigirAlLoginFilter"/>.
/// </summary>
public sealed class SesionVencidaException : Exception
{
    public SesionVencidaException() : base("La sesión venció o no es válida.")
    {
    }
}
