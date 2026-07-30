using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T064 — Numeración global de movimientos (RF-020a).
///
/// El requisito pide cuatro propiedades del Número: única en todo el sistema, compartida entre
/// compras y ventas, no editable por el usuario y no reutilizable tras una baja. Las cuatro salen
/// de que sea la clave primaria <c>IDENTITY</c> del encabezado (R-07); acá se verifican como
/// comportamiento observable y no como detalle de implementación.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoNumeracionTests : MovimientosTestBase
{
    [Test]
    public async Task Compras_y_ventas_comparten_una_unica_secuencia()
    {
        var articulo = await SembrarArticuloAsync("A-001");

        var compra = await AltaExitosaAsync("Compra", Linea(articulo, 10));
        var venta = await AltaExitosaAsync("Venta", Linea(articulo, 1));
        var otraCompra = await AltaExitosaAsync("Compra", Linea(articulo, 5));

        Assert.Multiple(() =>
        {
            Assert.That(new[] { compra, venta, otraCompra }, Is.Unique,
                "Ningún Número se repite, ni siquiera entre tipos distintos.");
            Assert.That(venta, Is.GreaterThan(compra));
            Assert.That(otraCompra, Is.GreaterThan(venta));
        });
    }

    [Test]
    public async Task El_numero_no_se_reutiliza_tras_una_baja()
    {
        // Es la propiedad que una numeración calculada como MAX(Numero) + 1 rompería: al borrar el
        // último movimiento, el siguiente alta reciclaría su Número y dos movimientos distintos
        // compartirían identidad a lo largo del tiempo.
        var articulo = await SembrarArticuloAsync("A-001");

        var primero = await AltaExitosaAsync("Compra", Linea(articulo, 10));
        await BajaAsync(primero);

        var segundo = await AltaExitosaAsync("Compra", Linea(articulo, 10));

        Assert.That(segundo, Is.GreaterThan(primero));
    }

    [Test]
    public async Task El_numero_enviado_por_el_cliente_se_ignora()
    {
        // RF-020a: lo genera el sistema, no el usuario.
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await Client.PostAsJsonAsync(Movimientos, new
        {
            numero = 777,
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { Linea(articulo, 1) },
        });

        respuesta.EnsureSuccessStatusCode();

        var existeElImpuesto = await EscalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Movimiento WHERE Numero = 777");

        Assert.That(existeElImpuesto, Is.Zero);
    }
}
