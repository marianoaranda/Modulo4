using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T062 — Modificación de movimientos (RF-022), con el mismo invariante que el alta (RF-024a).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoModificacionTests : MovimientosTestBase
{
    [Test]
    public async Task Una_modificacion_exitosa_recalcula_el_stock_actual()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await ModificarAsync(compra, "Compra", Linea(articulo, 25));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(25),
                "El saldo es derivado: no hay nada que 'actualizar', se recalcula solo.");
        });
    }

    [Test]
    public async Task Una_modificacion_que_dejaria_el_saldo_negativo_se_rechaza_con_422()
    {
        // RF-024a: reducir una compra ya consumida por ventas posteriores dejaría el stock en −5.
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));
        await AltaExitosaAsync("Venta", Linea(articulo, 10));

        var respuesta = await ModificarAsync(compra, "Compra", Linea(articulo, 5));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(await StockDeAsync(articulo), Is.Zero, "Nada cambió.");
        });
    }

    [Test]
    public async Task Una_modificacion_rechazada_no_deja_el_detalle_a_medias()
    {
        // El servicio reemplaza el detalle completo: si el reemplazo se aplicara antes de validar
        // y el rollback fallara, quedaría un movimiento mutilado. Se verifica que el detalle
        // original sigue intacto, no sólo que el saldo coincide.
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));
        await AltaExitosaAsync("Venta", Linea(articulo, 10));

        await ModificarAsync(compra, "Compra", Linea(articulo, 1));

        var cantidadOriginal = await EscalarAsync<int>(
            $"SELECT Cantidad FROM dbo.MovimientoDetalle WHERE MovimientoNumero = {compra}");

        Assert.Multiple(async () =>
        {
            Assert.That(cantidadOriginal, Is.EqualTo(10));
            Assert.That(await CantidadDeLineasAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Se_puede_agregar_y_quitar_lineas_en_una_modificacion()
    {
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");
        var compra = await AltaExitosaAsync("Compra", Linea(primero, 10));

        var respuesta = await ModificarAsync(
            compra, "Compra", Linea(primero, 3), Linea(segundo, 7));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await StockDeAsync(primero), Is.EqualTo(3));
            Assert.That(await StockDeAsync(segundo), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Modificar_un_movimiento_inexistente_devuelve_404()
    {
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await ModificarAsync(999_999, "Compra", Linea(articulo, 1));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task El_numero_no_cambia_al_modificar()
    {
        // RF-020a: el Número no es editable por el usuario.
        var articulo = await SembrarArticuloAsync("A-001");
        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));

        await ModificarAsync(compra, "Compra", Linea(articulo, 11));

        var sigueExistiendo = await EscalarAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Movimiento WHERE Numero = {compra}");

        Assert.That(sigueExistiendo, Is.EqualTo(1));
    }
}
