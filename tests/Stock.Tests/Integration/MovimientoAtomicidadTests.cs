using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T063 — Todo-o-nada (V-3, RF-024c).
///
/// El modo de fallo que esto vigila es el más natural de todos: procesar las líneas en un bucle y
/// abortar cuando una falla, dejando aplicadas las anteriores. El movimiento sería entonces
/// parcialmente cierto, que es peor que rechazado.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoAtomicidadTests : MovimientosTestBase
{
    [Test]
    public async Task Si_falla_la_tercera_linea_no_se_aplica_ninguna_de_las_tres()
    {
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");
        var tercero = await SembrarArticuloAsync("A-003");

        await AltaExitosaAsync("Compra",
            Linea(primero, 100), Linea(segundo, 100), Linea(tercero, 5));

        // La tercera línea excede el stock disponible de A-003; las dos primeras son válidas.
        var respuesta = await AltaAsync("Venta",
            Linea(primero, 10), Linea(segundo, 10), Linea(tercero, 50));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));

            Assert.That(await StockDeAsync(primero), Is.EqualTo(100),
                "La línea 1 era válida y aun así no se aplicó.");
            Assert.That(await StockDeAsync(segundo), Is.EqualTo(100),
                "La línea 2 era válida y aun así no se aplicó.");
            Assert.That(await StockDeAsync(tercero), Is.EqualTo(5));

            Assert.That(await CantidadDeMovimientosAsync(), Is.EqualTo(1),
                "Sólo quedó la compra inicial: la venta no generó encabezado.");
        });
    }

    [Test]
    public async Task Un_fallo_de_validacion_de_campo_tampoco_aplica_ninguna_linea()
    {
        // RF-024c aplica a cualquier validación, no sólo a la de stock: acá falla la Cantidad de
        // la segunda línea (RF-023), que es un 400 y no un 422.
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");

        var respuesta = await AltaAsync("Compra", Linea(primero, 10), Linea(segundo, 0));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await StockDeAsync(primero), Is.Zero);
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task Un_movimiento_multilinea_valido_se_aplica_completo()
    {
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");
        var tercero = await SembrarArticuloAsync("A-003");

        await AltaExitosaAsync("Compra",
            Linea(primero, 5), Linea(segundo, 10), Linea(tercero, 15));

        Assert.Multiple(async () =>
        {
            Assert.That(await StockDeAsync(primero), Is.EqualTo(5));
            Assert.That(await StockDeAsync(segundo), Is.EqualTo(10));
            Assert.That(await StockDeAsync(tercero), Is.EqualTo(15));
        });
    }

    [Test]
    public async Task Dos_lineas_del_mismo_articulo_se_evaluan_por_su_efecto_conjunto()
    {
        // Con 10 en stock, dos líneas de venta de 6 suman 12: cada una por separado "entra", pero
        // juntas violan el invariante. Validar línea por línea contra el saldo inicial sería
        // exactamente el error que RF-024a prohíbe.
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await AltaAsync("Venta", Linea(articulo, 6), Linea(articulo, 6));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(10));
        });
    }
}
