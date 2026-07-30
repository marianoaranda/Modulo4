using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T109 — Contrato de los dos ABM de seguridad (RF-001 a RF-005, RF-010, RF-010a).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class SeguridadContractTests : SeguridadTestBase
{
    [Test]
    public async Task Recorrido_completo_del_ABM_de_perfiles()
    {
        var alta = await Client.PostAsJsonAsync(Perfiles, new { descripcion = "depósito" });
        Assert.That(alta.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var creado = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var perfilId = creado.RootElement.GetProperty("perfilId").GetInt32();

        Assert.Multiple(() =>
        {
            Assert.That(perfilId, Is.GreaterThan(0));
            Assert.That(creado.RootElement.GetProperty("esAdministrador").GetBoolean(), Is.False);
        });

        // Lectura por el listado (RF-001).
        var listado = await Client.GetAsync(Perfiles);
        listado.EnsureSuccessStatusCode();

        using var perfiles = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.That(perfiles.RootElement.GetArrayLength(), Is.EqualTo(4));

        // Modificación (RF-003).
        var modificacion = await Client.PutAsJsonAsync(
            $"{Perfiles}/{perfilId}", new { descripcion = "depósito central" });
        Assert.That(modificacion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Baja (RF-002).
        var baja = await Client.DeleteAsync($"{Perfiles}/{perfilId}");
        Assert.That(baja.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Recorrido_completo_del_ABM_de_usuarios()
    {
        var perfilId = await PerfilIdDeAsync("vendedor");

        var alta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Juan Pérez",
            perfilId,
            password = PasswordValida,
        });

        Assert.That(alta.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var creado = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var usuarioId = creado.RootElement.GetProperty("usuarioId").GetInt32();

        // Lectura (RF-004).
        var lectura = await Client.GetAsync($"{Usuarios}/{usuarioId}");
        Assert.That(lectura.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Modificación (RF-006).
        var modificacion = await Client.PutAsJsonAsync($"{Usuarios}/{usuarioId}", new
        {
            nombreUsuario = "jperez",
            nombreCompleto = "Juan Pérez González",
            perfilId,
        });
        Assert.That(modificacion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Baja (RF-005).
        var baja = await Client.DeleteAsync($"{Usuarios}/{usuarioId}");
        Assert.Multiple(async () =>
        {
            Assert.That(baja.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await Client.GetAsync($"{Usuarios}/{usuarioId}")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    // -------------------------------------------------------------------------------------
    // RF-010a — la restricción alcanza a los DOS recursos, no sólo a usuarios.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_perfil_no_administrador_recibe_403_en_los_dos_recursos()
    {
        // RF-010a: dejar el ABM de perfiles abierto permitiría alterar indirectamente el control
        // de acceso de RF-010, porque el perfil es lo que determina quién accede a los usuarios.
        using var vendedor = await ClienteComoVendedorAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await vendedor.GetAsync(Usuarios)).StatusCode,
                Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That((await vendedor.GetAsync(Perfiles)).StatusCode,
                Is.EqualTo(HttpStatusCode.Forbidden), "RF-010a, la mitad que se olvida.");
        });
    }

    [Test]
    public async Task Un_perfil_no_administrador_recibe_403_en_toda_operacion_de_escritura()
    {
        using var vendedor = await ClienteComoVendedorAsync();
        var perfilId = await PerfilIdDeAsync("vendedor");

        Assert.Multiple(async () =>
        {
            Assert.That((await vendedor.PostAsJsonAsync(Perfiles, new { descripcion = "x" })).StatusCode,
                Is.EqualTo(HttpStatusCode.Forbidden));

            Assert.That((await vendedor.DeleteAsync($"{Perfiles}/{perfilId}")).StatusCode,
                Is.EqualTo(HttpStatusCode.Forbidden));

            Assert.That((await vendedor.PostAsJsonAsync(Usuarios, new
            {
                nombreUsuario = "otro",
                nombreCompleto = "Otro",
                perfilId,
                password = PasswordValida,
            })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task Un_perfil_no_administrador_si_accede_al_resto_de_las_funcionalidades()
    {
        // El alcance cerrado del PRD: la restricción por perfil alcanza EXCLUSIVAMENTE a los dos
        // ABM de seguridad. Todo usuario autenticado usa artículos, movimientos y las consultas.
        using var vendedor = await ClienteComoVendedorAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await vendedor.GetAsync("/api/articulos")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await vendedor.GetAsync("/api/movimientos")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await vendedor.GetAsync("/api/consultas/stock-actual")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await vendedor.GetAsync(
                "/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task El_403_viaja_como_problem_json()
    {
        using var vendedor = await ClienteComoVendedorAsync();

        var respuesta = await vendedor.GetAsync(Usuarios);

        Assert.That(
            respuesta.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json"));
    }
}
