using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stock.Web.Models;
using Stock.Web.Services;

namespace Stock.Web.Controllers;

/// <summary>
/// T105 — Inicio y cierre de sesión de la capa MVC (R-09).
///
/// El JWT que devuelve la API se guarda como claim dentro de la cookie de autenticación, que se
/// emite <c>HttpOnly</c> y cifrada con Data Protection. Así el token queda fuera del alcance de
/// JavaScript —a diferencia de guardarlo en <c>localStorage</c>, que lo expondría a XSS— y la API
/// permanece <i>stateless</i>, validando el token en cada request como exige RF-012.
/// </summary>
[AllowAnonymous]
public class CuentaController : Controller
{
    private readonly StockApiClient _api;

    public CuentaController(StockApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;

        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vista, string? returnUrl, CancellationToken ct)
    {
        var respuesta = await _api.Http.PostAsJsonAsync(
            "/api/auth/login",
            new { usuario = vista.Usuario, password = vista.Password },
            ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            // Se muestra el mensaje tal como lo redacta la API, sin distinguir usuario inexistente
            // de contraseña incorrecta (RF-011).
            vista.Mensaje = await RespuestaDeLaApi.LeerDetalleDelProblemaAsync(respuesta, ct);
            vista.Password = string.Empty;

            return View(vista);
        }

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var raiz = documento.RootElement;

        var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, vista.Usuario),
                new Claim(BearerTokenHandler.ClaimDelToken, raiz.GetProperty("token").GetString()!),

                // Se copia del token la marca de administrador, para que el menú pueda ocultar los
                // ABM de seguridad sin volver a preguntarle a la API en cada render. La
                // autorización real la sigue haciendo la API contra el claim del JWT: esto es
                // presentación, no control de acceso.
                new Claim(
                    Stock.Web.Services.ClaimsDeSesion.EsAdmin,
                    LeerEsAdminDelToken(raiz.GetProperty("token").GetString()!)),

                new Claim("perfil", raiz.GetProperty("perfil").GetString() ?? string.Empty),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = raiz.GetProperty("expiraEn").GetDateTimeOffset(),
            });

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Extrae <c>es_admin</c> del JWT sin validarlo: la validación la hace la API en cada llamada,
    /// y acá el claim se usa sólo para decidir qué entradas de menú mostrar. Un token manipulado
    /// mostraría el menú pero recibiría 403 de la API igual (RF-010).
    /// </summary>
    private static string LeerEsAdminDelToken(string token)
    {
        try
        {
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

            return jwt.Claims.FirstOrDefault(c => c.Type == "es_admin")?.Value ?? "false";
        }
        catch (Exception)
        {
            return "false";
        }
    }
}
