using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T107a — El sistema no puede quedarse sin administrador (RF-005a, CE-007a).
///
/// Es la mitad del invariante que más fácil se olvida: prohibir la baja del último administrador
/// pero permitir cambiarle el perfil deja exactamente el mismo agujero, sólo que por otra puerta.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class UltimoAdministradorTests : SeguridadTestBase
{
    private async Task<int> AdminIdAsync() =>
        await EscalarAsync<int>("SELECT UsuarioId FROM dbo.Usuario WHERE NombreUsuario = 'admin'");

    [Test]
    public async Task Con_un_solo_administrador_su_baja_se_rechaza_con_409_sin_grabar()
    {
        var adminId = await AdminIdAsync();

        var respuesta = await Client.DeleteAsync($"{Usuarios}/{adminId}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(await CantidadDeUsuariosAsync(), Is.EqualTo(1), "El admin sigue existiendo.");
        });
    }

    [Test]
    public async Task Con_un_solo_administrador_cambiarle_el_perfil_se_rechaza_con_409()
    {
        // La otra puerta al mismo agujero: si esto se permitiera, el sistema quedaría sin nadie
        // capaz de operar RF-004 a RF-006 y no habría forma de recuperarlo desde la aplicación.
        var adminId = await AdminIdAsync();
        var perfilVendedor = await PerfilIdDeAsync("vendedor");

        var respuesta = await Client.PutAsJsonAsync($"{Usuarios}/{adminId}", new
        {
            nombreUsuario = "admin",
            nombreCompleto = "Administrador del sistema",
            perfilId = perfilVendedor,
        });

        var perfilVigente = await EscalarAsync<int>(
            $"SELECT PerfilId FROM dbo.Usuario WHERE UsuarioId = {adminId}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(perfilVigente, Is.EqualTo(await PerfilAdministradorIdAsync()),
                "Conserva su perfil administrador.");
        });
    }

    [Test]
    public async Task Con_dos_administradores_la_baja_de_uno_se_acepta()
    {
        var adminId = await AdminIdAsync();
        var perfilAdmin = await PerfilAdministradorIdAsync();
        await CrearUsuarioAsync("segundoAdmin", perfilAdmin);

        var respuesta = await Client.DeleteAsync($"{Usuarios}/{adminId}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await EscalarAsync<int>("""
                SELECT COUNT(*)
                FROM   dbo.Usuario u
                JOIN   dbo.Perfil  p ON p.PerfilId = u.PerfilId
                WHERE  p.EsAdministrador = 1
                """), Is.EqualTo(1), "Queda uno.");
        });
    }

    [Test]
    public async Task Con_dos_administradores_cambiarle_el_perfil_a_uno_se_acepta()
    {
        var adminId = await AdminIdAsync();
        var perfilAdmin = await PerfilAdministradorIdAsync();
        var perfilVendedor = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync("segundoAdmin", perfilAdmin);

        var respuesta = await Client.PutAsJsonAsync($"{Usuarios}/{adminId}", new
        {
            nombreUsuario = "admin",
            nombreCompleto = "Administrador del sistema",
            perfilId = perfilVendedor,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Dar_de_baja_a_un_no_administrador_nunca_se_restringe_por_esta_regla()
    {
        var perfilVendedor = await PerfilIdDeAsync("vendedor");
        var vendedorId = await CrearUsuarioAsync("vendedor1", perfilVendedor);

        var respuesta = await Client.DeleteAsync($"{Usuarios}/{vendedorId}");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Dos_bajas_concurrentes_no_pueden_eliminar_a_los_dos_ultimos_administradores()
    {
        // RF-005a exige que el conteo de administradores restantes se verifique DENTRO de la misma
        // transacción que la escritura. Si se contara antes de abrir la transacción, dos bajas
        // simultáneas verían dos administradores cada una, ambas se creerían seguras y el sistema
        // quedaría sin ninguno.
        var adminId = await AdminIdAsync();
        var perfilAdmin = await PerfilAdministradorIdAsync();
        var segundoId = await CrearUsuarioAsync("segundoAdmin", perfilAdmin);

        using var clienteA = ClienteAutenticado();
        using var clienteB = ClienteAutenticado();

        await Task.WhenAll(
            clienteA.DeleteAsync($"{Usuarios}/{adminId}"),
            clienteB.DeleteAsync($"{Usuarios}/{segundoId}"));

        var administradoresRestantes = await EscalarAsync<int>("""
            SELECT COUNT(*)
            FROM   dbo.Usuario u
            JOIN   dbo.Perfil  p ON p.PerfilId = u.PerfilId
            WHERE  p.EsAdministrador = 1
            """);

        Assert.That(administradoresRestantes, Is.GreaterThanOrEqualTo(1),
            "Alguna de las dos bajas tiene que haber sido rechazada.");
    }
}
