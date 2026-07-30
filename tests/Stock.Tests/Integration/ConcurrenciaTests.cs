using System.Net;
using System.Net.Http.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T065 — Concurrencia (V-4, CE-004, RF-024b).
///
/// Es el escenario que verifica el bloqueo pesimista de R-02. Lo que RF-024b pide es preciso y
/// tiene dos mitades: que el invariante se sostenga bajo concurrencia, y que la operación perdedora
/// reciba <b>stock insuficiente</b> evaluado contra el saldo ya actualizado — nunca un error de
/// conflicto que obligue al usuario a reintentar.
///
/// La segunda mitad es la que descarta el diseño optimista con <c>rowversion</c>: sostendría el
/// invariante, pero devolvería el error de reintento que el requisito prohíbe explícitamente. Por
/// eso el test no se conforma con contar cuántas ventas entraron: asierta también el código de
/// cada rechazo.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ConcurrenciaTests : MovimientosTestBase
{
    [Test]
    public async Task Cinco_ventas_simultaneas_de_cuatro_sobre_un_stock_de_diez_graban_a_lo_sumo_dos()
    {
        var articulo = await SembrarArticuloAsync("A-001");
        await AltaExitosaAsync("Compra", Linea(articulo, 10));

        // Cada cliente tiene su propia conexión, como cinco usuarios distintos del comercio.
        var clientes = Enumerable.Range(0, 5).Select(_ => ClienteAutenticado()).ToList();

        var cuerpo = Cuerpo("Venta", Linea(articulo, 4));

        var respuestas = await Task.WhenAll(
            clientes.Select(c => c.PostAsJsonAsync(Movimientos, cuerpo)));

        var grabadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rechazadas = respuestas.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();

        var stockFinal = await StockDeAsync(articulo);

        Assert.Multiple(() =>
        {
            Assert.That(grabadas, Is.LessThanOrEqualTo(2),
                "10 unidades no alcanzan para tres ventas de 4.");

            Assert.That(stockFinal, Is.GreaterThanOrEqualTo(0),
                "CE-005: el saldo nunca queda negativo, pase lo que pase.");

            Assert.That(stockFinal, Is.EqualTo(10 - (grabadas * 4)),
                "El saldo tiene que ser exactamente el de las ventas efectivamente grabadas.");

            // La mitad de RF-024b que un esquema optimista incumpliría.
            Assert.That(
                rechazadas.Select(r => r.StatusCode),
                Is.All.EqualTo(HttpStatusCode.UnprocessableEntity),
                "Todo rechazo tiene que ser 'stock insuficiente' (422), nunca un conflicto de " +
                "concurrencia (409) ni un error de servidor que pida reintentar.");
        });

        foreach (var cliente in clientes)
        {
            cliente.Dispose();
        }
    }

    [Test]
    public async Task Ventas_concurrentes_de_articulos_distintos_no_se_bloquean_entre_si()
    {
        // El bloqueo es por artículo: la fila de Articulo funciona como mutex. Dos ventas de
        // artículos distintos no compiten y ambas deben entrar.
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");

        await AltaExitosaAsync("Compra", Linea(primero, 10), Linea(segundo, 10));

        using var clienteA = ClienteAutenticado();
        using var clienteB = ClienteAutenticado();

        var respuestas = await Task.WhenAll(
            clienteA.PostAsJsonAsync(Movimientos, Cuerpo("Venta", Linea(primero, 10))),
            clienteB.PostAsJsonAsync(Movimientos, Cuerpo("Venta", Linea(segundo, 10))));

        Assert.That(
            respuestas.Select(r => r.StatusCode),
            Is.All.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Movimientos_multilinea_concurrentes_que_comparten_articulos_no_generan_deadlock()
    {
        // Dos movimientos que tocan {A, B} y {B, A}. Si el bloqueo no se tomara siempre en orden
        // ascendente de ArticuloId, ésta es la forma canónica de producir un deadlock, y un
        // deadlock se manifiesta como error de concurrencia — justo lo que RF-024b prohíbe
        // exponer.
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");

        await AltaExitosaAsync("Compra", Linea(primero, 1000), Linea(segundo, 1000));

        var enUnOrden = Cuerpo("Venta", Linea(primero, 1), Linea(segundo, 1));
        var enElOtro = Cuerpo("Venta", Linea(segundo, 1), Linea(primero, 1));

        var tandas = Enumerable.Range(0, 8).Select(async i =>
        {
            using var cliente = ClienteAutenticado();
            return await cliente.PostAsJsonAsync(Movimientos, i % 2 == 0 ? enUnOrden : enElOtro);
        });

        var respuestas = await Task.WhenAll(tandas);

        Assert.That(
            respuestas.Select(r => r.StatusCode),
            Is.All.EqualTo(HttpStatusCode.Created),
            "Ninguna debería fallar: hay stock de sobra y el orden de bloqueo evita el deadlock.");
    }
}
