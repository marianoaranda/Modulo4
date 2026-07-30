using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Stock.Web.Services;

namespace Stock.Web;

/// <summary>
/// Punto de entrada de la capa MVC.
///
/// Igual que <c>Stock.Api.Program</c>, evita los <em>top-level statements</em> para que
/// <c>Stock.Api.Program</c> y <c>Stock.Web.Program</c> no colisionen en el proyecto de tests, que
/// referencia a ambos (R-10).
/// </summary>
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // R-09: cookie propia de la capa web, HttpOnly y cifrada con Data Protection, que
        // transporta el JWT como claim. Deja el token fuera del alcance de JavaScript y evita
        // el almacén de sesión que haría falta con estado de servidor.
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.LoginPath = "/Cuenta/Login";
                cookie.AccessDeniedPath = "/Cuenta/Login";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddControllersWithViews(mvc =>
        {
            // T105b — Filtro de autorización GLOBAL (RF-012): ninguna vista distinta del login se
            // renderiza sin sesión. `CuentaController` se exceptúa con `[AllowAnonymous]`.
            //
            // Que sea global y no atributo por controlador es lo que hace que una pantalla nueva
            // nazca protegida, en vez de depender de que alguien recuerde el atributo. Sin este
            // filtro, la vista se renderizaría y sólo fallaría la llamada a la API: el usuario
            // vería una pantalla rota en lugar del login.
            mvc.Filters.Add(new AuthorizeFilter(
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

            // Convierte el 401 de la API en una redirección al login (R-09).
            mvc.Filters.Add<RedirigirAlLoginFilter>();
        });

        // Por defecto Razor codifica como entidades numéricas todo lo que no sea ASCII básico, de
        // modo que "descripción" llegaría al navegador como "descripci&#xF3;n". Se ve bien, pero
        // hace que el HTML no contenga literalmente los textos que RF-032 y RF-032a fijan al
        // carácter. Ampliar el rango es lo correcto para una aplicación en español.
        builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(opciones =>
            opciones.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
                System.Text.Unicode.UnicodeRanges.All));

        var direccionDeLaApi = builder.Configuration["StockApi:BaseUrl"];

        builder.Services.AddTransient<BearerTokenHandler>();

        builder.Services.AddHttpClient<StockApiClient>(cliente =>
        {
            if (!string.IsNullOrWhiteSpace(direccionDeLaApi))
            {
                cliente.BaseAddress = new Uri(direccionDeLaApi);
            }
        }).AddHttpMessageHandler<BearerTokenHandler>();

        var app = builder.Build();

        app.UseExceptionHandler("/Home/Error");
        app.UseStatusCodePagesWithReExecute("/Home/Error", "?codigo={0}");

        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
