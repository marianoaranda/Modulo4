using System.Net;
using Microsoft.Data.SqlClient;

namespace Stock.Tests.Web;

/// <summary>
/// T121 — Una excepción no controlada en la capa MVC también queda registrada (RF-028, CE-008).
///
/// El middleware de <c>Stock.Api</c> no ve estas excepciones: nunca lo atraviesan. Sin un
/// manejador propio en <c>Stock.Web</c> habría una clase entera de errores invisible, y CE-008
/// —el 100% de los errores registrados— sería inalcanzable de forma silenciosa.
///
/// Es la <b>única</b> excepción a la regla de que la capa web no toca la base: sólo diagnóstico,
/// sólo escritura, ninguna entidad de negocio. Queda registrada en Complexity Tracking del plan.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ErrorLogWebTests : WebTestBase
{
    private string _nombreDeBase = string.Empty;
    private string _cadena = string.Empty;

    protected override void ConfigurarAplicacion(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:StockDb", _cadena);

    /// <summary>
    /// La capa web escribe la bitácora con su propia conexión, así que necesita una base real con
    /// la tabla ya creada. Se crea una efímera propia del fixture: este test no comparte estado
    /// con los de la API.
    ///
    /// Va en <c>OneTimeSetUp</c> para que la cadena ya esté disponible cuando el <c>SetUp</c> de
    /// la base construya la <c>WebApplicationFactory</c>.
    /// </summary>
    [OneTimeSetUp]
    public async Task CrearBaseDeBitacoraAsync()
    {
        _nombreDeBase = $"StockWebTest_{Guid.NewGuid():N}";
        _cadena = await BaseEfimera.CrearAsync(_nombreDeBase);
    }

    [OneTimeTearDown]
    public async Task DerribarBaseAsync() => await BaseEfimera.EliminarAsync(_nombreDeBase);

    [SetUp]
    public async Task VaciarLaBitacoraAsync() =>
        await BaseEfimera.EjecutarAsync(_cadena, "DELETE FROM dbo.ErrorLog");

    [Test]
    public async Task Una_excepcion_no_controlada_del_MVC_queda_registrada()
    {
        // La API responde 500; el controlador hace EnsureSuccessStatusCode y la excepción sube sin
        // que nadie la maneje. Es el caso realista: un fallo de la API que el front no previó.
        Api.Responder(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        var filas = await BaseEfimera.EscalarAsync(_cadena, "SELECT COUNT(*) FROM dbo.ErrorLog");

        Assert.Multiple(() =>
        {
            Assert.That((int)respuesta.StatusCode, Is.GreaterThanOrEqualTo(500));
            Assert.That(filas, Is.EqualTo(1), "La capa MVC tiene su propio manejador global.");
        });
    }

    [Test]
    public async Task El_registro_de_la_capa_web_trae_las_columnas_de_RF_028()
    {
        Api.Responder(_ => throw new InvalidOperationException("Falla simulada en la capa web."));

        var cliente = ClienteConSesion();
        await cliente.GetAsync("/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        var mensaje = await BaseEfimera.EscalarTextoAsync(
            _cadena, "SELECT TOP 1 Message FROM dbo.ErrorLog");
        var detalle = await BaseEfimera.EscalarTextoAsync(
            _cadena, "SELECT TOP 1 FullException FROM dbo.ErrorLog");
        var maquina = await BaseEfimera.EscalarTextoAsync(
            _cadena, "SELECT TOP 1 MachineName FROM dbo.ErrorLog");

        Assert.Multiple(() =>
        {
            Assert.That(mensaje, Does.Contain("Falla simulada en la capa web."));
            Assert.That(detalle, Does.Contain("InvalidOperationException"));
            Assert.That(maquina, Is.Not.Empty);
        });
    }

    [Test]
    public async Task Una_sesion_vencida_no_se_registra_como_error()
    {
        // El 401 de la API es un resultado previsto —el token venció— y se resuelve redirigiendo
        // al login. Registrarlo llenaría la bitácora de ruido operativo (R-08).
        Api.Responder(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        var filas = await BaseEfimera.EscalarAsync(_cadena, "SELECT COUNT(*) FROM dbo.ErrorLog");

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(filas, Is.Zero);
        });
    }
}

/// <summary>
/// Creación y borrado de bases efímeras para los tests de la capa web, que necesitan una base real
/// sólo para la bitácora.
/// </summary>
public static class BaseEfimera
{
    public static async Task<string> CrearAsync(string nombre)
    {
        var cadena = Stock.Tests.Integration.IntegrationTestBase.CadenaHacia(nombre);

        await EjecutarEnMasterAsync($"CREATE DATABASE [{nombre}]");

        // La tabla la crea la misma migración que el resto del esquema (R-08): acá se aplica el
        // historial completo, igual que en producción.
        await Stock.Tests.Integration.IntegrationTestBase.MigrarAsync(cadena);

        return cadena;
    }

    public static async Task EliminarAsync(string nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            return;
        }

        await EjecutarEnMasterAsync(
            $"ALTER DATABASE [{nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombre}]");
    }

    public static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<int> EscalarAsync(string cadena, string sql)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);

        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    public static async Task<string> EscalarTextoAsync(string cadena, string sql)
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);

        return (await comando.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private static async Task EjecutarEnMasterAsync(string sql)
    {
        await using var conexion = new SqlConnection(
            Stock.Tests.Integration.IntegrationTestBase.CadenaHacia("master"));

        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
