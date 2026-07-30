using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T094 — Protección del acceso (V-11, CE-007, RF-011, RF-012).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class SeguridadTests : IntegrationTestBase
{
    [TestCase("/api/articulos")]
    [TestCase("/api/movimientos")]
    [TestCase("/api/usuarios")]
    [TestCase("/api/perfiles")]
    [TestCase("/api/consultas/stock-actual")]
    [TestCase("/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal")]
    public async Task Sin_token_toda_llamada_devuelve_401(string ruta)
    {
        // RF-012 / CE-007: ninguna funcionalidad distinta del login es accesible sin sesión.
        // Se recorren las seis familias de endpoints y no una de muestra, porque el olvido típico
        // es dejar un controlador sin el atributo.
        using var sinToken = ClienteSinToken();

        var respuesta = await sinToken.GetAsync(ruta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task El_login_es_el_unico_endpoint_publico()
    {
        using var sinToken = ClienteSinToken();

        var respuesta = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "admin", password = PasswordAdminDePrueba });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Un_token_invalido_devuelve_401()
    {
        using var conBasura = ClienteSinToken();
        conBasura.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "esto.no.es.un.token");

        var respuesta = await conBasura.GetAsync("/api/articulos");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Un_token_expirado_devuelve_401()
    {
        // R-04 fija ClockSkew en cero justamente para que la expiración sea exacta. Con la
        // tolerancia por defecto de 5 minutos, un token recién vencido seguiría siendo aceptado y
        // este test no podría distinguir "expira" de "no expira".
        using var conVencido = ClienteSinToken();
        conVencido.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenExpirado());

        var respuesta = await conVencido.GetAsync("/api/articulos");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Con_token_valido_se_autoriza_el_ingreso()
    {
        var respuesta = await Client.GetAsync("/api/articulos");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // -------------------------------------------------------------------------------------
    // RF-011 — el mismo mensaje ante usuario inexistente y contraseña incorrecta.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Usuario_inexistente_y_contrasena_incorrecta_devuelven_el_mismo_mensaje()
    {
        // Si los mensajes difirieran, el login funcionaría como oráculo de existencia de cuentas:
        // probando nombres se sabría cuáles existen antes de intentar ninguna contraseña.
        using var sinToken = ClienteSinToken();

        var inexistente = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "no-existe", password = "Cualquiera1" });

        var incorrecta = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "admin", password = "Incorrecta1" });

        var mensajeInexistente = await inexistente.Content.ReadAsStringAsync();
        var mensajeIncorrecta = await incorrecta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(incorrecta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            Assert.That(mensajeInexistente, Does.Contain("Usuario o contraseña incorrectos"));
            Assert.That(mensajeIncorrecta, Does.Contain("Usuario o contraseña incorrectos"));
        });
    }

    [Test]
    public async Task La_respuesta_del_login_fallido_no_revela_si_la_cuenta_existe()
    {
        using var sinToken = ClienteSinToken();

        var inexistente = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "no-existe", password = "Cualquiera1" });

        var cuerpo = await inexistente.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cuerpo, Does.Not.Contain("no-existe").IgnoreCase);
            Assert.That(cuerpo, Does.Not.Contain("no existe").IgnoreCase);
        });
    }

    [Test]
    public async Task El_login_exitoso_devuelve_un_token_y_su_vencimiento()
    {
        using var sinToken = ClienteSinToken();

        var respuesta = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "admin", password = PasswordAdminDePrueba });

        respuesta.EnsureSuccessStatusCode();

        using var documento = System.Text.Json.JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(documento.RootElement.GetProperty("token").GetString(), Is.Not.Empty);
            Assert.That(documento.RootElement.TryGetProperty("expiraEn", out _), Is.True);
        });
    }

    [Test]
    public async Task El_login_nunca_devuelve_el_hash_ni_el_salt()
    {
        using var sinToken = ClienteSinToken();

        var respuesta = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario = "admin", password = PasswordAdminDePrueba });

        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cuerpo, Does.Not.Contain("hash").IgnoreCase);
            Assert.That(cuerpo, Does.Not.Contain("salt").IgnoreCase);
        });
    }
}
