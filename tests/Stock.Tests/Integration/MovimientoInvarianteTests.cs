using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T061 — El invariante Stock Actual ≥ 0 (V-2, CE-005, RF-024a).
///
/// RF-024a lo generaliza a <b>toda</b> operación, no sólo a la venta: la baja o la reducción de una
/// compra ya consumida por ventas posteriores dejaría el saldo en negativo y se rechaza igual. Ése
/// es el caso que un sistema que sólo valide las ventas deja pasar en silencio.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoInvarianteTests : MovimientosTestBase
{
    [Test]
    public async Task Una_venta_que_dejaria_el_stock_negativo_se_rechaza_con_422_sin_grabar_nada()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await AltaAsync("Venta", Linea(articulo, 15));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(10), "El saldo no se tocó.");
            Assert.That(await CantidadDeMovimientosAsync(), Is.EqualTo(1), "Sólo quedó la compra.");
        });
    }

    [Test]
    public async Task El_rechazo_de_stock_viaja_como_problem_json_con_un_mensaje_para_el_usuario()
    {
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await AltaAsync("Venta", Linea(articulo, 1));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            Assert.That(cuerpo, Does.Contain("A-001"),
                "El mensaje tiene que decir de qué artículo falta stock para ser accionable.");
        });
    }

    [Test]
    public async Task Vender_exactamente_todo_el_stock_se_acepta_y_deja_el_saldo_en_cero()
    {
        // El invariante es "≥ 0", no "> 0": quedarse sin stock es un estado válido.
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await AltaAsync("Venta", Linea(articulo, 10));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(await StockDeAsync(articulo), Is.Zero);
        });
    }

    [Test]
    public async Task Dar_de_baja_una_compra_ya_consumida_por_ventas_se_rechaza_con_422()
    {
        // V-2, pasos 3 y 4. Es el caso que sólo queda cubierto desde la corrección de RF-024a:
        // la baja no es una operación "de lectura" sobre el stock, lo modifica igual que una venta.
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));
        await AltaExitosaAsync("Venta", Linea(articulo, 10));

        var respuesta = await BajaAsync(compra);

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity),
                "La baja dejaría el stock en −10.");
            Assert.That(await StockDeAsync(articulo), Is.Zero, "El saldo quedó en 0.");
            Assert.That(await CantidadDeMovimientosAsync(), Is.EqualTo(2), "La compra sigue existiendo.");
        });
    }

    [Test]
    public async Task Dar_de_baja_una_compra_no_consumida_se_acepta()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await BajaAsync(compra);

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await StockDeAsync(articulo), Is.Zero);
            Assert.That(await CantidadDeLineasAsync(), Is.Zero, "RF-021: la baja arrastra el detalle.");
        });
    }

    [Test]
    public async Task Dar_de_baja_una_venta_siempre_se_acepta_porque_solo_puede_subir_el_saldo()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));
        var venta = await AltaExitosaAsync("Venta", Linea(articulo, 4));

        var respuesta = await BajaAsync(venta);

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(10));
        });
    }

    [Test]
    public async Task La_baja_de_un_movimiento_inexistente_devuelve_404()
    {
        var respuesta = await BajaAsync(999_999);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
