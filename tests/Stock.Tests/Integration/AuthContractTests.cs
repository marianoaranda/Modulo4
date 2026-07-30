using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T095 — Contrato de <c>POST /api/auth/login</c> (RF-011, R-04).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class AuthContractTests : IntegrationTestBase
{
    private const string Login = "/api/auth/login";

    private async Task<HttpResponseMessage> LoginAsync(string usuario, string password)
    {
        using var sinToken = ClienteSinToken();

        return await sinToken.PostAsJsonAsync(Login, new { usuario, password });
    }

    [Test]
    public async Task El_login_valido_devuelve_200_con_token_vencimiento_y_perfil()
    {
        var respuesta = await LoginAsync("admin", PasswordAdminDePrueba);

        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(documento.RootElement.GetProperty("token").GetString(), Is.Not.Empty);
            Assert.That(documento.RootElement.GetProperty("expiraEn").GetDateTimeOffset(),
                Is.GreaterThan(DateTimeOffset.UtcNow));
            Assert.That(documento.RootElement.GetProperty("perfil").GetString(),
                Is.EqualTo("administrador"));
        });
    }

    [Test]
    public async Task El_login_fallido_devuelve_401_como_problem_json()
    {
        var respuesta = await LoginAsync("admin", "Incorrecta1");

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task El_token_lleva_los_claims_que_fija_R_04()
    {
        var respuesta = await LoginAsync("admin", PasswordAdminDePrueba);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(documento.RootElement.GetProperty("token").GetString());

        var claims = token.Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Multiple(() =>
        {
            Assert.That(claims, Does.ContainKey("sub"));
            Assert.That(claims["name"], Is.EqualTo("admin"));

            // `role` es la Descripción del perfil y existe SÓLO para mostrar.
            Assert.That(claims["role"], Is.EqualTo("administrador"));

            // `es_admin` se deriva de la marca inmutable Perfil.EsAdministrador y es la única
            // base de la autorización (RF-003a). La separación es lo que hace que renombrar el
            // perfil no otorgue ni quite privilegios.
            Assert.That(claims["es_admin"], Is.EqualTo("true"));
        });
    }

    [Test]
    public async Task El_token_vence_a_las_ocho_horas()
    {
        // R-04: cubre un turno completo de trabajo del comercio sin obligar a reautenticar en
        // medio de la operación, que es el patrón de uso real.
        var respuesta = await LoginAsync("admin", PasswordAdminDePrueba);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var vencimiento = documento.RootElement.GetProperty("expiraEn").GetDateTimeOffset();

        Assert.That(
            vencimiento - DateTimeOffset.UtcNow,
            Is.EqualTo(TimeSpan.FromHours(8)).Within(TimeSpan.FromMinutes(2)));
    }

    [Test]
    public async Task El_token_esta_firmado_con_HS256()
    {
        var respuesta = await LoginAsync("admin", PasswordAdminDePrueba);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(documento.RootElement.GetProperty("token").GetString());

        Assert.That(token.Header.Alg, Is.EqualTo("HS256"));
    }

    [Test]
    public async Task Un_token_firmado_con_otra_clave_se_rechaza()
    {
        // Sin esta verificación, un token con los claims correctos pero firmado por cualquiera
        // sería aceptado, y la autenticación no valdría nada.
        using var conFirmaAjena = ClienteSinToken();
        conFirmaAjena.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TokenFirmadoCon("otra-clave-completamente-distinta-de-32-o-mas-bytes"));

        var respuesta = await conFirmaAjena.GetAsync("/api/articulos");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [TestCase("", "Cualquiera1")]
    [TestCase("admin", "")]
    public async Task Credenciales_vacias_no_autorizan(string usuario, string password)
    {
        var respuesta = await LoginAsync(usuario, password);

        Assert.That(respuesta.StatusCode, Is.Not.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Un_cuerpo_sin_los_campos_obligatorios_devuelve_400_o_401()
    {
        using var sinToken = ClienteSinToken();

        var respuesta = await sinToken.PostAsync(
            Login, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.That(
            respuesta.StatusCode,
            Is.AnyOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized));
    }
}
