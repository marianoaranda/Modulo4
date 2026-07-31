using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Stock.Api.Data;

namespace Stock.Tests.Integration;

/// <summary>
/// Repositorio de bloqueo que falla después de haber tomado el bloqueo, es decir con la
/// transacción de movimientos ya abierta. Es el escenario exacto de V-12.
/// </summary>
public class BloqueoQueFalla : ArticuloLockRepository
{
    public BloqueoQueFalla(StockDbContext db) : base(db)
    {
    }

    public override Task BloquearAsync(IEnumerable<int> articuloIds, CancellationToken ct = default) =>
        throw new InvalidOperationException("Falla simulada dentro de la transacción.");
}

/// <summary>
/// T120 — La bitácora sobrevive al rollback (V-12, CE-008, RF-028).
///
/// Es el punto de diseño que más fácilmente se implementa mal. Si el registro del error se
/// escribiera con el mismo <c>DbContext</c> de la operación fallida, el <c>ROLLBACK</c> de esa
/// transacción se llevaría puesto también el registro del error — y el fallo sería silencioso,
/// porque el sistema parecería estar registrando errores mientras pierde exactamente los que
/// ocurren dentro de una transacción.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ErrorLogTests : IntegrationTestBase
{
    private Task<int> FilasEnErrorLogAsync() =>
        EscalarAsync<int>("SELECT COUNT(*) FROM dbo.ErrorLog");

    private async Task<int> SembrarArticuloAsync()
    {
        await EjecutarSqlAsync("""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('A-001', 'Artículo de prueba', 10.00, 0, 0, 0, 0);
            """);

        return await EscalarAsync<int>("SELECT ArticuloId FROM dbo.Articulo WHERE Codigo = 'A-001'");
    }

    [Test]
    public async Task Una_excepcion_dentro_de_la_transaccion_queda_registrada_pese_al_rollback()
    {
        var articuloId = await SembrarArticuloAsync();

        // Se sustituye el repositorio de bloqueo por uno que falla con la transacción ya abierta.
        using var factoryQueFalla = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<ArticuloLockRepository, BloqueoQueFalla>()));

        using var cliente = factoryQueFalla.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId, cantidad = 10, precioUnitario = 5m } },
        });

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Movimiento"), Is.Zero,
                "La transacción hizo rollback: el movimiento no quedó.");

            Assert.That(await FilasEnErrorLogAsync(), Is.EqualTo(1),
                "…y aun así el error quedó registrado. Ésa es la razón de la conexión aparte.");
        });
    }

    [Test]
    public async Task El_registro_trae_las_columnas_que_exige_RF_028()
    {
        var articuloId = await SembrarArticuloAsync();

        using var factoryQueFalla = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<ArticuloLockRepository, BloqueoQueFalla>()));

        using var cliente = factoryQueFalla.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

        await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId, cantidad = 10, precioUnitario = 5m } },
        });

        var maquina = await EscalarAsync<string>("SELECT TOP 1 MachineName FROM dbo.ErrorLog");
        var mensaje = await EscalarAsync<string>("SELECT TOP 1 Message FROM dbo.ErrorLog");
        var detalle = await EscalarAsync<string>("SELECT TOP 1 FullException FROM dbo.ErrorLog");
        var fecha = await EscalarAsync<DateTime>("SELECT TOP 1 ErrorDateTime FROM dbo.ErrorLog");

        Assert.Multiple(() =>
        {
            Assert.That(maquina, Is.Not.Empty);
            Assert.That(mensaje, Does.Contain("Falla simulada"));
            Assert.That(detalle, Does.Contain("InvalidOperationException"));
            Assert.That(fecha, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)));
        });
    }

    [Test]
    public async Task La_respuesta_al_usuario_no_expone_detalle_interno()
    {
        // RF-028: mensaje genérico. El detalle va a la bitácora, no al cliente.
        var articuloId = await SembrarArticuloAsync();

        using var factoryQueFalla = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<ArticuloLockRepository, BloqueoQueFalla>()));

        using var cliente = factoryQueFalla.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId, cantidad = 10, precioUnitario = 5m } },
        });

        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cuerpo, Does.Not.Contain("InvalidOperationException"));
            Assert.That(cuerpo, Does.Not.Contain("Falla simulada"));
            Assert.That(cuerpo, Does.Not.Contain("Stock.Api.Data"), "Ni rastros de la pila.");
        });
    }

    // -------------------------------------------------------------------------------------
    // CE-008, cara complementaria: la bitácora es para fallos, no para rechazos esperados.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_422_de_negocio_no_genera_fila_en_la_bitacora()
    {
        // Una venta sin stock es un resultado previsto del sistema. Registrarla llenaría la
        // bitácora de ruido y haría que los errores reales se pierdan entre rechazos normales.
        var articuloId = await SembrarArticuloAsync();

        var respuesta = await Client.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Venta",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId, cantidad = 10, precioUnitario = 5m } },
        });

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(await FilasEnErrorLogAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task Ni_un_400_ni_un_404_ni_un_409_generan_fila_en_la_bitacora()
    {
        var articuloId = await SembrarArticuloAsync();

        // 400: cantidad inválida.
        await Client.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId, cantidad = 0, precioUnitario = 5m } },
        });

        // 404: movimiento inexistente.
        await Client.DeleteAsync("/api/movimientos/999999");

        // 409: código de artículo duplicado.
        await Client.PostAsJsonAsync("/api/articulos", new
        {
            codigo = "A-001",
            descripcion = "Duplicado",
            precioCosto = 1m,
            margen = 0m,
            stockMinimo = 0,
            puntoPedido = 0,
            stockIdeal = 0,
        });

        // 401: sin token.
        using var sinToken = ClienteSinToken();
        await sinToken.GetAsync("/api/articulos");

        Assert.That(await FilasEnErrorLogAsync(), Is.Zero,
            "Los rechazos esperados no son fallos: sólo el 500 se registra.");
    }
}
