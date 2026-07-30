using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T108 — ABM de perfiles (RF-002a, RF-002b, RF-003).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class PerfilesTests : SeguridadTestBase
{
    [Test]
    public async Task La_baja_de_un_perfil_con_usuarios_asignados_se_rechaza_con_409()
    {
        // RF-002a: baja restringida. No hay baja lógica ni eliminación en cascada.
        var perfilVendedor = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync("vendedor1", perfilVendedor);

        var respuesta = await Client.DeleteAsync($"{Perfiles}/{perfilVendedor}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
                "409 legible, no una violación de FK convertida en 500.");
            Assert.That(await EscalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.Perfil WHERE PerfilId = {perfilVendedor}"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task La_baja_de_un_perfil_sin_usuarios_se_acepta()
    {
        var alta = await Client.PostAsJsonAsync(Perfiles, new { descripcion = "depósito" });
        alta.EnsureSuccessStatusCode();

        var perfilId = await PerfilIdDeAsync("depósito");

        var respuesta = await Client.DeleteAsync($"{Perfiles}/{perfilId}");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task La_baja_del_perfil_administrador_se_rechaza_aun_sin_usuarios_asignados()
    {
        // RF-002b. Se verifica ANTES que RF-002a: sin esta regla, bastaría con mover al último
        // administrador a otro perfil y después borrar el perfil administrador para dejar al
        // sistema sin quien habilite RF-004 a RF-006.
        var perfilAdmin = await PerfilAdministradorIdAsync();
        var perfilVendedor = await PerfilIdDeAsync("vendedor");

        // El escenario se arma por SQL y no por la API a propósito: RF-005a impide que el perfil
        // administrador se quede sin usuarios operando normalmente, así que por la API este estado
        // es inalcanzable. Justamente por eso hay que construirlo a mano — RF-002b tiene que valer
        // igual, y sin este montaje el test estaría verificando RF-005a por segunda vez en lugar
        // de RF-002b.
        await EjecutarSqlAsync(
            $"UPDATE dbo.Usuario SET PerfilId = {perfilVendedor} WHERE PerfilId = {perfilAdmin}");

        var sinUsuarios = await EscalarAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Usuario WHERE PerfilId = {perfilAdmin}");

        var respuesta = await Client.DeleteAsync($"{Perfiles}/{perfilAdmin}");

        Assert.Multiple(async () =>
        {
            Assert.That(sinUsuarios, Is.Zero, "Precondición: el perfil quedó sin usuarios.");
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(await CantidadDePerfilesAdministradoresAsync(), Is.EqualTo(1),
                "Siempre existe exactamente un perfil administrador.");
        });
    }

    [Test]
    public async Task La_modificacion_de_la_descripcion_se_persiste()
    {
        // RF-003: la Descripción es un rótulo editable.
        var perfilVendedor = await PerfilIdDeAsync("vendedor");

        var respuesta = await Client.PutAsJsonAsync(
            $"{Perfiles}/{perfilVendedor}", new { descripcion = "vendedor de salón" });

        var vigente = await EscalarAsync<string>(
            $"SELECT Descripcion FROM dbo.Perfil WHERE PerfilId = {perfilVendedor}");

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(vigente, Is.EqualTo("vendedor de salón"));
        });
    }

    [Test]
    public async Task El_alta_de_un_perfil_nuevo_nunca_lo_marca_como_administrador()
    {
        // RF-003a: la marca se establece exclusivamente en la siembra.
        await Client.PostAsJsonAsync(Perfiles, new { descripcion = "depósito" });

        var esAdmin = await EscalarAsync<int>(
            "SELECT CAST(EsAdministrador AS int) FROM dbo.Perfil WHERE Descripcion = N'depósito'");

        Assert.Multiple(async () =>
        {
            Assert.That(esAdmin, Is.Zero);
            Assert.That(await CantidadDePerfilesAdministradoresAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Una_descripcion_vacia_se_rechaza_con_400()
    {
        var respuesta = await Client.PostAsJsonAsync(Perfiles, new { descripcion = "" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Modificar_o_dar_de_baja_un_perfil_inexistente_devuelve_404()
    {
        Assert.Multiple(async () =>
        {
            Assert.That((await Client.PutAsJsonAsync($"{Perfiles}/999999", new { descripcion = "x" })).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await Client.DeleteAsync($"{Perfiles}/999999")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
        });
    }
}
