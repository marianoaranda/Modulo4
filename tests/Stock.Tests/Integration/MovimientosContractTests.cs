using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T070/T070a — Contrato del CRUD de <c>/api/movimientos</c>.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientosContractTests : MovimientosTestBase
{
    [Test]
    public async Task Recorrido_completo_alta_lectura_modificacion_baja()
    {
        var articulo = await SembrarArticuloAsync("A-001");

        // Alta.
        var alta = await AltaAsync("Compra", Linea(articulo, 10, 12.50m));
        Assert.That(alta.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var creado = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var numero = creado.RootElement.GetProperty("numero").GetInt32();

        Assert.Multiple(() =>
        {
            Assert.That(numero, Is.GreaterThan(0), "RF-020a: el sistema asigna el Número.");
            Assert.That(creado.RootElement.GetProperty("tipo").GetString(), Is.EqualTo("Compra"));
            Assert.That(
                creado.RootElement.GetProperty("detalle")[0].GetProperty("precioTotal").GetDecimal(),
                Is.EqualTo(125.00m),
                "RF-020c: el Precio Total lo calcula el sistema.");
        });

        // Lectura.
        var lectura = await Client.GetAsync($"{Movimientos}/{numero}");
        Assert.That(lectura.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var leido = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(leido.RootElement.GetProperty("numero").GetInt32(), Is.EqualTo(numero));
            Assert.That(
                leido.RootElement.GetProperty("detalle")[0].GetProperty("codigo").GetString(),
                Is.EqualTo("A-001"),
                "El detalle expone el Código, que es la identidad de negocio que ve el usuario.");
        });

        // Modificación.
        var modificacion = await ModificarAsync(numero, "Compra", Linea(articulo, 20, 12.50m));
        Assert.That(modificacion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await StockDeAsync(articulo), Is.EqualTo(20));

        // Baja.
        var baja = await BajaAsync(numero);
        Assert.Multiple(async () =>
        {
            Assert.That(baja.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await Client.GetAsync($"{Movimientos}/{numero}")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task El_listado_devuelve_los_movimientos_cargados()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));
        await AltaExitosaAsync("Venta", Linea(articulo, 3));

        var respuesta = await Client.GetAsync(Movimientos);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.That(documento.RootElement.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task Leer_un_movimiento_inexistente_devuelve_404()
    {
        var respuesta = await Client.GetAsync($"{Movimientos}/999999");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Un_tipo_fuera_del_conjunto_cerrado_devuelve_400()
    {
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await AltaAsync("Devolucion", Linea(articulo, 1));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Un_articulo_inexistente_en_el_detalle_devuelve_400()
    {
        var respuesta = await AltaAsync("Compra", Linea(articuloId: 999_999, cantidad: 1));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero);
        });
    }

    // -------------------------------------------------------------------------------------
    // T070a — RF-018a: el no entero se rechaza en el borde de la solicitud.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Una_cantidad_no_entera_devuelve_400_problem_json_identificando_el_campo()
    {
        // RF-018a. El rechazo ocurre al deserializar, antes de llegar a ninguna regla de negocio:
        // por eso el validador de dominio no tiene —ni podría tener— un caso para esto.
        //
        // Se asierta el CUERPO del problema y no sólo el código, para distinguir este rechazo del
        // 400 genérico del framework: sin esa distinción, el test pasaría igual si el no entero se
        // truncara silenciosamente a 1 y fallara después por otro motivo.
        var articulo = await SembrarArticuloAsync("A-001");

        var cuerpo = $$"""
            {"tipo":"Compra","fecha":"2026-01-15",
             "detalle":[{"articuloId":{{articulo}},"cantidad":1.5,"precioUnitario":10.00}]}
            """;

        var respuesta = await Client.PostAsync(
            Movimientos, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
            Assert.That(problema, Does.Contain("cantidad").IgnoreCase,
                "El problema tiene que identificar el campo ofensor.");
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero, "No se grabó nada.");
        });
    }

    [Test]
    public async Task Un_articuloId_no_entero_tambien_devuelve_400()
    {
        var cuerpo = """
            {"tipo":"Compra","fecha":"2026-01-15",
             "detalle":[{"articuloId":1.7,"cantidad":1,"precioUnitario":10.00}]}
            """;

        var respuesta = await Client.PostAsync(
            Movimientos, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Una_cantidad_entera_expresada_como_decimal_exacto_tambien_se_rechaza()
    {
        // "2.0" es matemáticamente entero pero sintácticamente decimal. Aceptarlo obligaría al
        // borde a razonar sobre el valor y no sobre el tipo, que es justo lo que RF-018a evita.
        var articulo = await SembrarArticuloAsync("A-001");

        var cuerpo = $$"""
            {"tipo":"Compra","fecha":"2026-01-15",
             "detalle":[{"articuloId":{{articulo}},"cantidad":2.0,"precioUnitario":10.00}]}
            """;

        var respuesta = await Client.PostAsync(
            Movimientos, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Una_fecha_futura_devuelve_400_sin_grabar()
    {
        // RF-020d.
        var articulo = await SembrarArticuloAsync("A-001");
        var manana = DateOnly.FromDateTime(DateTime.Today.AddDays(1)).ToString("yyyy-MM-dd");

        var respuesta = await Client.PostAsJsonAsync(Movimientos, new
        {
            tipo = "Compra",
            fecha = manana,
            detalle = new[] { Linea(articulo, 1) },
        });

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero);
        });
    }
}
