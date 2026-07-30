using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T107 — ABM de usuarios (RF-006, RF-007, RF-009, RF-010).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class UsuariosTests : SeguridadTestBase
{
    [Test]
    public async Task Un_perfil_no_administrador_recibe_403_en_la_carga_de_usuarios()
    {
        // RF-010: se distingue del 401 de RF-012. Acá hay sesión válida; lo que falta es el
        // privilegio.
        using var vendedor = await ClienteComoVendedorAsync();

        var respuesta = await vendedor.GetAsync(Usuarios);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase("corta1")]
    [TestCase("12345678")]
    [TestCase("sinDigitos")]
    public async Task Una_contrasena_que_incumple_la_politica_devuelve_400_sin_grabar(string password)
    {
        // RF-009. Se valida ANTES de derivar el hash: no tiene sentido gastar 210.000 iteraciones
        // en una contraseña que se va a rechazar.
        var antes = await CantidadDeUsuariosAsync();
        var perfilId = await PerfilIdDeAsync("vendedor");

        var respuesta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario = "nuevo",
            nombreCompleto = "Usuario nuevo",
            perfilId,
            password,
        });

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await CantidadDeUsuariosAsync(), Is.EqualTo(antes));
        });
    }

    [Test]
    public async Task Ninguna_respuesta_incluye_Hash_ni_Salt()
    {
        // RF-007. Se revisan las tres formas en que un usuario sale de la API: el alta, la lectura
        // individual y el listado.
        var perfilId = await PerfilIdDeAsync("vendedor");

        var alta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Juan Pérez",
            perfilId,
            password = PasswordValida,
        });

        var cuerpoDelAlta = await alta.Content.ReadAsStringAsync();
        var cuerpoDelListado = await (await Client.GetAsync(Usuarios)).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cuerpoDelAlta, Does.Not.Contain("hash").IgnoreCase);
            Assert.That(cuerpoDelAlta, Does.Not.Contain("salt").IgnoreCase);
            Assert.That(cuerpoDelListado, Does.Not.Contain("hash").IgnoreCase);
            Assert.That(cuerpoDelListado, Does.Not.Contain("salt").IgnoreCase);
        });
    }

    [Test]
    public async Task Una_modificacion_sin_contrasena_no_re_deriva_el_hash()
    {
        // RF-006: la contraseña es opcional en la modificación. Si el hash se re-derivara con una
        // cadena vacía, cambiarle el nombre completo a alguien le cambiaría la contraseña sin
        // avisarle, y quedaría afuera del sistema.
        var perfilId = await PerfilIdDeAsync("vendedor");
        var usuarioId = await CrearUsuarioAsync("jperez", perfilId);

        var hashAntes = await EscalarAsync<string>(
            $"SELECT CONVERT(varchar(max), Hash, 2) FROM dbo.Usuario WHERE UsuarioId = {usuarioId}");

        var respuesta = await Client.PutAsJsonAsync($"{Usuarios}/{usuarioId}", new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Juan Pérez González",
            perfilId,
            // Sin password.
        });

        var hashDespues = await EscalarAsync<string>(
            $"SELECT CONVERT(varchar(max), Hash, 2) FROM dbo.Usuario WHERE UsuarioId = {usuarioId}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(hashDespues, Is.EqualTo(hashAntes));

            // Y el usuario sigue pudiendo entrar con su contraseña original.
            Assert.That(async () => await ObtenerTokenAsync("jperez", PasswordValida), Throws.Nothing);
        });
    }

    [Test]
    public async Task Una_modificacion_con_contrasena_si_re_deriva_el_hash()
    {
        var perfilId = await PerfilIdDeAsync("vendedor");
        var usuarioId = await CrearUsuarioAsync("jperez", perfilId);

        var hashAntes = await EscalarAsync<string>(
            $"SELECT CONVERT(varchar(max), Hash, 2) FROM dbo.Usuario WHERE UsuarioId = {usuarioId}");

        await Client.PutAsJsonAsync($"{Usuarios}/{usuarioId}", new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Juan Pérez",
            perfilId,
            password = "OtraClave456",
        });

        var hashDespues = await EscalarAsync<string>(
            $"SELECT CONVERT(varchar(max), Hash, 2) FROM dbo.Usuario WHERE UsuarioId = {usuarioId}");

        Assert.Multiple(async () =>
        {
            Assert.That(hashDespues, Is.Not.EqualTo(hashAntes));
            Assert.That(async () => await ObtenerTokenAsync("jperez", "OtraClave456"), Throws.Nothing);
        });
    }

    [Test]
    public async Task Dos_usuarios_con_la_misma_contrasena_tienen_hash_y_salt_distintos_en_base()
    {
        // V-11, paso 1 — CE-006 verificado contra lo que efectivamente quedó persistido, no sólo
        // contra la función de derivación.
        var perfilId = await PerfilIdDeAsync("vendedor");

        await CrearUsuarioAsync("uno", perfilId, "MismaClave1");
        await CrearUsuarioAsync("dos", perfilId, "MismaClave1");

        var distintos = await EscalarAsync<int>("""
            SELECT COUNT(DISTINCT CONVERT(varchar(max), Hash, 2) + CONVERT(varchar(max), Salt, 2))
            FROM   dbo.Usuario
            WHERE  NombreUsuario IN ('uno', 'dos')
            """);

        Assert.That(distintos, Is.EqualTo(2));
    }

    [Test]
    public async Task Un_nombre_de_usuario_duplicado_se_rechaza_con_409()
    {
        var perfilId = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync("jperez", perfilId);

        var respuesta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Otro Juan",
            perfilId,
            password = PasswordValida,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task El_alta_sin_contrasena_se_rechaza()
    {
        // La contraseña es obligatoria en el alta: un usuario sin credencial no podría entrar y
        // dejaría una fila con hash vacío en la tabla.
        var perfilId = await PerfilIdDeAsync("vendedor");

        var respuesta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario = "sinclave",
            nombreCompleto = "Sin clave",
            perfilId,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
