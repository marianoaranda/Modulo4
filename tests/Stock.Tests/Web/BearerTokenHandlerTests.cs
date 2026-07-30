using System.Net;

namespace Stock.Tests.Web;

/// <summary>
/// T096 — <c>BearerTokenHandler</c> y filtro de autorización global de la capa MVC (R-09, RF-012).
///
/// El manejador contiene lógica real —adjunta el encabezado y decide qué hacer ante un 401—, así
/// que el Principio I le aplica sin excepción por ser "de infraestructura".
///
/// Este test es el rojo que T105b viene a poner en verde, y al hacerlo <b>rompe deliberadamente</b>
/// los tests de capa web de US1–US3. Esa rotura está planificada: T105a introduce el fixture de
/// sesión y T105c los repara.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class BearerTokenHandlerTests : WebTestBase
{
    [Test]
    public async Task Adjunta_el_encabezado_Authorization_en_las_llamadas_salientes()
    {
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = ClienteConSesion();
        await cliente.GetAsync("/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        var autorizacion = Api.UltimaSolicitud.Headers.Authorization;

        Assert.Multiple(() =>
        {
            Assert.That(autorizacion, Is.Not.Null,
                "Sin esto, cada controlador tendría que acordarse de adjuntar el token.");
            Assert.That(autorizacion!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(autorizacion.Parameter, Is.EqualTo(JwtDePrueba()),
                "Tiene que viajar el JWT completo que la sesión guardó, no un fragmento.");
        });
    }

    [Test]
    public async Task Una_vista_protegida_no_se_renderiza_sin_sesion()
    {
        // RF-012: toda funcionalidad salvo el login exige sesión válida. Sin el filtro global, la
        // pantalla se renderizaría y sólo fallaría la llamada a la API — el usuario vería una
        // pantalla rota en vez del login.
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(respuesta.Headers.Location?.ToString(), Does.Contain("/Cuenta/Login"));
        });
    }

    [TestCase("/Articulos")]
    [TestCase("/Movimientos")]
    [TestCase("/StockActual")]
    [TestCase("/Usuarios")]
    [TestCase("/Perfiles")]
    public async Task Ninguna_pantalla_distinta_del_login_es_accesible_sin_sesion(string ruta)
    {
        Api.ResponderJson("[]");

        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync(ruta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect),
            $"{ruta} se renderizó sin sesión.");
    }

    [Test]
    public async Task La_pantalla_de_login_si_es_accesible_sin_sesion()
    {
        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync("/Cuenta/Login");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Ante_un_401_de_la_API_cierra_la_sesion_y_redirige_al_login()
    {
        // R-09: cuando el token vence, la API responde 401. La capa web no puede quedarse con una
        // cookie que ya no sirve: la borra y manda al login, para que el usuario no quede en un
        // limbo donde parece logueado pero nada funciona.
        Api.Responder(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(respuesta.Headers.Location?.ToString(), Does.Contain("/Cuenta/Login"));

            var cookies = respuesta.Headers.TryGetValues("Set-Cookie", out var valores)
                ? string.Join(" ", valores)
                : string.Empty;

            Assert.That(cookies, Does.Contain("expires=Thu, 01 Jan 1970").IgnoreCase
                .Or.Contain("max-age=0"),
                "La cookie de sesión tiene que quedar invalidada.");
        });
    }

    [Test]
    public async Task El_login_exitoso_guarda_el_token_y_habilita_las_pantallas()
    {
        Api.ResponderJson($$"""
            {"token":"{{TokenDePrueba}}","expiraEn":"2030-01-01T00:00:00Z","perfil":"administrador"}
            """);

        var cliente = NuevoCliente();

        var respuesta = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Usuario"] = "admin",
                ["Password"] = "Admin1234",
            }));

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));

            var cookies = respuesta.Headers.TryGetValues("Set-Cookie", out var valores)
                ? string.Join(" ", valores)
                : string.Empty;

            Assert.That(cookies, Does.Contain("httponly").IgnoreCase,
                "R-09: la cookie se emite HttpOnly para dejar el JWT fuera del alcance de JavaScript.");
        });
    }

    [Test]
    public async Task El_login_fallido_muestra_el_mensaje_generico_sin_iniciar_sesion()
    {
        Api.ResponderProblema(HttpStatusCode.Unauthorized, "Usuario o contraseña incorrectos.");

        var cliente = NuevoCliente();

        var respuesta = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Usuario"] = "admin",
                ["Password"] = "malísima",
            }));

        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("Usuario o contraseña incorrectos"));
        });
    }
}
