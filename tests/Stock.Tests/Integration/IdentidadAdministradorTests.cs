using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T108a — El privilegio sigue a la marca, no al texto (RF-003a, RF-010, CE-007a, V-13).
///
/// Es el test que justifica que exista <c>Perfil.EsAdministrador</c> en vez de comparar la
/// Descripción contra "administrador". Sin la marca, RF-003 —que permite renombrar libremente un
/// perfil— sería una vía para conceder y quitar privilegios sin pasar por el ABM de usuarios.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class IdentidadAdministradorTests : SeguridadTestBase
{
    [Test]
    public async Task Renombrar_el_perfil_administrador_conserva_el_acceso_de_sus_usuarios()
    {
        // V-13, paso 1. El admin sigue siendo admin aunque su perfil pase a llamarse "operador".
        var perfilAdmin = await PerfilAdministradorIdAsync();

        var renombre = await Client.PutAsJsonAsync(
            $"{Perfiles}/{perfilAdmin}", new { descripcion = "operador" });

        renombre.EnsureSuccessStatusCode();

        // Se pide un token nuevo: el anterior se emitió antes del renombre.
        using var despues = await ClienteComoAsync("admin", PasswordAdminDePrueba);
        var respuesta = await despues.GetAsync(Usuarios);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "El privilegio no dependía del texto, así que renombrar no se lo quitó.");
    }

    [Test]
    public async Task Renombrar_otro_perfil_a_administrador_no_concede_el_privilegio()
    {
        // V-13, paso 2. Es la cara peligrosa: si la autorización mirara la Descripción, cualquiera
        // con acceso al ABM de perfiles se autoconcedería el privilegio renombrando el suyo.
        var perfilVendedor = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync("vendedor1", perfilVendedor);

        var renombre = await Client.PutAsJsonAsync(
            $"{Perfiles}/{perfilVendedor}", new { descripcion = "administrador" });

        renombre.EnsureSuccessStatusCode();

        using var vendedor = await ClienteComoAsync("vendedor1", PasswordValida);
        var respuesta = await vendedor.GetAsync(Usuarios);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
            "Llamarse 'administrador' no alcanza: la marca sigue en el otro perfil.");
    }

    [Test]
    public async Task El_alta_de_perfil_ignora_el_campo_EsAdministrador_del_cuerpo()
    {
        // V-13, paso 3. El DTO no lo declara, así que el campo enviado no tiene dónde aterrizar.
        var respuesta = await Client.PostAsJsonAsync(Perfiles, new
        {
            descripcion = "intruso",
            esAdministrador = true,
        });

        var esAdmin = await EscalarAsync<int>(
            "SELECT CAST(EsAdministrador AS int) FROM dbo.Perfil WHERE Descripcion = N'intruso'");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.IsSuccessStatusCode, Is.True,
                "El campo se ignora; no convierte la solicitud en inválida.");
            Assert.That(esAdmin, Is.Zero);
            Assert.That(await CantidadDePerfilesAdministradoresAsync(), Is.EqualTo(1),
                "Sigue habiendo un solo perfil administrador.");
        });
    }

    [Test]
    public async Task La_modificacion_de_perfil_ignora_el_campo_EsAdministrador_del_cuerpo()
    {
        var perfilVendedor = await PerfilIdDeAsync("vendedor");

        await Client.PutAsJsonAsync($"{Perfiles}/{perfilVendedor}", new
        {
            descripcion = "vendedor",
            esAdministrador = true,
        });

        var esAdmin = await EscalarAsync<int>(
            $"SELECT CAST(EsAdministrador AS int) FROM dbo.Perfil WHERE PerfilId = {perfilVendedor}");

        Assert.Multiple(async () =>
        {
            Assert.That(esAdmin, Is.Zero);
            Assert.That(await CantidadDePerfilesAdministradoresAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Quitarle_la_marca_al_perfil_administrador_tampoco_es_alcanzable()
    {
        var perfilAdmin = await PerfilAdministradorIdAsync();

        await Client.PutAsJsonAsync($"{Perfiles}/{perfilAdmin}", new
        {
            descripcion = "administrador",
            esAdministrador = false,
        });

        var esAdmin = await EscalarAsync<int>(
            $"SELECT CAST(EsAdministrador AS int) FROM dbo.Perfil WHERE PerfilId = {perfilAdmin}");

        Assert.That(esAdmin, Is.EqualTo(1), "La marca no se puede apagar desde la API.");
    }

    [Test]
    public async Task El_claim_es_admin_refleja_la_marca_y_no_la_descripcion()
    {
        // La verificación en el token, que es donde la política de autorización mira.
        var perfilVendedor = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync("vendedor1", perfilVendedor);
        await Client.PutAsJsonAsync($"{Perfiles}/{perfilVendedor}", new { descripcion = "administrador" });

        var token = await ObtenerTokenAsync("vendedor1", PasswordValida);
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(token).Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Multiple(() =>
        {
            Assert.That(claims["role"], Is.EqualTo("administrador"),
                "`role` lleva la Descripción, que ahora dice 'administrador'…");
            Assert.That(claims["es_admin"], Is.EqualTo("false"),
                "…pero `es_admin` sigue la marca, que no cambió. La política mira este claim.");
        });
    }
}
