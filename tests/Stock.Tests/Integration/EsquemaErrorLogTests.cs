using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Tests.Integration;

/// <summary>
/// T019 — La tabla <c>dbo.ErrorLog</c> tiene que nacer con la migración inicial, junto al resto.
///
/// Es el modo de fallo natural del diseño de R-08: como en runtime se escribe con un
/// <c>DbContext</c> aparte, es fácil olvidar que el <b>esquema</b> igual lo declara
/// <c>StockDbContext</c>. Si la tabla no entrara en la migración, el primer error no controlado
/// fallaría al registrarse con <i>invalid object name</i>, incumpliendo CE-008 justo cuando más se
/// lo necesita.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class EsquemaErrorLogTests : IntegrationTestBase
{
    [Test]
    public async Task La_tabla_ErrorLog_existe_tras_aplicar_las_migraciones()
    {
        var existe = await EscalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'ErrorLog' AND SCHEMA_NAME(schema_id) = 'dbo'");

        Assert.That(existe, Is.EqualTo(1));
    }

    [Test]
    public async Task La_tabla_ErrorLog_admite_inserciones_con_las_columnas_de_RF_028()
    {
        await using (var db = NuevoContexto())
        {
            db.ErrorLogs.Add(new ErrorLog
            {
                ErrorDateTime = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                MachineName = "maquina-de-prueba",
                Message = "Mensaje de prueba",
                FullException = "System.InvalidOperationException: detalle completo",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuevoContexto())
        {
            var registro = await db.ErrorLogs.SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(registro.ErrorId, Is.GreaterThan(0));
                Assert.That(registro.MachineName, Is.EqualTo("maquina-de-prueba"));
                Assert.That(registro.Message, Is.EqualTo("Mensaje de prueba"));
                Assert.That(registro.FullException, Does.Contain("InvalidOperationException"));
            });
        }
    }

    [Test]
    public async Task FullException_admite_nulo()
    {
        // RF-028: el detalle de la excepción es el único campo opcional de la bitácora.
        await using var db = NuevoContexto();
        db.ErrorLogs.Add(new ErrorLog
        {
            ErrorDateTime = DateTime.UtcNow,
            MachineName = "maquina-de-prueba",
            Message = "Error sin detalle",
            FullException = null,
        });

        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }
}
