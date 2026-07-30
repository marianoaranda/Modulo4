using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Tests.Integration;

/// <summary>
/// T019a — La unicidad del perfil administrador se garantiza <b>en el esquema</b>, no sólo en el
/// servicio, mediante un índice único filtrado <c>WHERE EsAdministrador = 1</c> (RF-003a).
///
/// La marca es la identidad de autorización del sistema: si dos perfiles pudieran tenerla, la
/// afirmación "existe siempre exactamente un perfil administrador" dejaría de ser cierta y el
/// privilegio pasaría a depender de cuál fila se lea primero.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class EsquemaPerfilTests : IntegrationTestBase
{
    [Test]
    public async Task No_puede_haber_dos_perfiles_marcados_como_administrador()
    {
        await using var db = NuevoContexto();
        db.Perfiles.Add(new Perfil { Descripcion = "administrador", EsAdministrador = true });
        await db.SaveChangesAsync();

        db.Perfiles.Add(new Perfil { Descripcion = "supervisor", EsAdministrador = true });

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public void Varios_perfiles_sin_la_marca_conviven_sin_problema()
    {
        // El índice es *filtrado*: sólo restringe las filas con EsAdministrador = 1.
        var db = NuevoContexto();
        db.Perfiles.Add(new Perfil { Descripcion = "administrativo", EsAdministrador = false });
        db.Perfiles.Add(new Perfil { Descripcion = "vendedor", EsAdministrador = false });
        db.Perfiles.Add(new Perfil { Descripcion = "depósito", EsAdministrador = false });

        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task EsAdministrador_tiene_valor_por_defecto_falso()
    {
        // RF-003a: la marca se establece exclusivamente en la siembra. Un alta que no la
        // mencione no puede crear un administrador por accidente.
        await EjecutarSqlAsync("INSERT INTO dbo.Perfil (Descripcion) VALUES ('recién creado')");

        await using var db = NuevoContexto();
        var perfil = await db.Perfiles.SingleAsync();

        Assert.That(perfil.EsAdministrador, Is.False);
    }

    [Test]
    public async Task No_se_puede_borrar_un_perfil_con_usuarios_asignados()
    {
        // RF-002a: baja restringida, garantizada por la FK NO ACTION antes de que el servicio
        // llegue a opinar.
        int perfilId;
        await using (var db = NuevoContexto())
        {
            var perfil = new Perfil { Descripcion = "vendedor", EsAdministrador = false };
            db.Perfiles.Add(perfil);
            await db.SaveChangesAsync();
            perfilId = perfil.PerfilId;

            db.Usuarios.Add(new Usuario
            {
                NombreUsuario = "jperez",
                NombreCompleto = "Juan Pérez",
                Hash = new byte[32],
                Salt = new byte[16],
                PerfilId = perfilId,
            });
            await db.SaveChangesAsync();
        }

        Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
            await EjecutarSqlAsync($"DELETE FROM dbo.Perfil WHERE PerfilId = {perfilId}"));
    }

    [Test]
    public async Task NombreUsuario_duplicado_se_rechaza()
    {
        // RF-004/RF-011: el nombre de usuario identifica la cuenta en el login.
        await using var db = NuevoContexto();
        var perfil = new Perfil { Descripcion = "vendedor", EsAdministrador = false };
        db.Perfiles.Add(perfil);
        await db.SaveChangesAsync();

        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "jperez",
            NombreCompleto = "Juan Pérez",
            Hash = new byte[32],
            Salt = new byte[16],
            PerfilId = perfil.PerfilId,
        });
        await db.SaveChangesAsync();

        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "jperez",
            NombreCompleto = "Otro Juan",
            Hash = new byte[32],
            Salt = new byte[16],
            PerfilId = perfil.PerfilId,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }
}
