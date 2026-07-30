using Stock.Api.Domain.Pedido;

namespace Stock.Tests.Unit;

/// <summary>
/// T043/T044 — El corazón del módulo, contra el <b>Conjunto de Datos de Referencia</b> del spec.
///
/// La calculadora es una función pura sobre parámetros de reposición y saldo: no toca EF Core ni
/// ASP.NET, así que se desarrolla íntegramente en rojo→verde→refactor sin infraestructura, que es
/// donde el Principio I aporta más.
///
/// La matriz es de 6 combinaciones × 4 artículos = 24 celdas: <b>15 cantidades asertadas</b> y
/// <b>9 exclusiones</b>. Las exclusiones se verifican explícitamente como ausencia de fila y no
/// como cantidad 0: son cosas distintas y confundirlas dejaría pasar el error de listar artículos
/// que no corresponden (CE-003).
/// </summary>
[TestFixture]
[Category(TestCategories.Unit)]
public class PedidoCalculatorTests
{
    // Conjunto de Datos de Referencia (spec, Criterios de Éxito).
    private static readonly ParametrosDeReposicion A001 = new(StockMinimo: 10, PuntoPedido: 20, StockIdeal: 50);
    private static readonly ParametrosDeReposicion A002 = new(StockMinimo: 10, PuntoPedido: 20, StockIdeal: 50);
    private static readonly ParametrosDeReposicion A003 = new(StockMinimo: 10, PuntoPedido: 20, StockIdeal: 50);
    private static readonly ParametrosDeReposicion A004 = new(StockMinimo: 0, PuntoPedido: 0, StockIdeal: 0);

    private const int StockA001 = 5;   // por debajo del mínimo
    private const int StockA002 = 15;  // sobre el mínimo, bajo el punto de pedido
    private const int StockA003 = 60;  // por encima del stock ideal
    private const int StockA004 = 0;   // parámetros en cero, sin movimientos

    // ---------------------------------------------------------------------------------------
    // Las 15 cantidades asertadas de la matriz.
    // ---------------------------------------------------------------------------------------

    [TestCase(ModoPedido.HastaStockMinimo, 5)]
    [TestCase(ModoPedido.HastaPuntoPedido, 15)]
    [TestCase(ModoPedido.HastaStockIdeal, 45)]
    public void A001_cantidad_a_pedir(ModoPedido modo, int esperada) =>
        Assert.That(PedidoCalculator.CantidadAPedir(modo, A001, StockA001), Is.EqualTo(esperada));

    [TestCase(ModoPedido.HastaStockMinimo, 0)]
    [TestCase(ModoPedido.HastaPuntoPedido, 5)]
    [TestCase(ModoPedido.HastaStockIdeal, 35)]
    public void A002_cantidad_a_pedir(ModoPedido modo, int esperada) =>
        Assert.That(PedidoCalculator.CantidadAPedir(modo, A002, StockA002), Is.EqualTo(esperada));

    [TestCase(ModoPedido.HastaStockMinimo, 0)]
    [TestCase(ModoPedido.HastaPuntoPedido, 0)]
    [TestCase(ModoPedido.HastaStockIdeal, 0)]
    public void A003_cantidad_a_pedir(ModoPedido modo, int esperada) =>
        Assert.That(PedidoCalculator.CantidadAPedir(modo, A003, StockA003), Is.EqualTo(esperada));

    [TestCase(ModoPedido.HastaStockMinimo, 0)]
    [TestCase(ModoPedido.HastaPuntoPedido, 0)]
    [TestCase(ModoPedido.HastaStockIdeal, 0)]
    public void A004_cantidad_a_pedir(ModoPedido modo, int esperada) =>
        Assert.That(PedidoCalculator.CantidadAPedir(modo, A004, StockA004), Is.EqualTo(esperada));

    // ---------------------------------------------------------------------------------------
    // Las 9 exclusiones: con soloBajoMinimo = Sí, sólo A-001 entra al resultado.
    // ---------------------------------------------------------------------------------------

    [Test]
    public void Con_soloBajoMinimo_solo_A001_se_incluye()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PedidoCalculator.Incluir(true, A001, StockA001), Is.True,
                "A-001 tiene 5 < 10: está por debajo del mínimo.");

            Assert.That(PedidoCalculator.Incluir(true, A002, StockA002), Is.False,
                "A-002 tiene 15 >= 10: no está por debajo del mínimo.");

            Assert.That(PedidoCalculator.Incluir(true, A003, StockA003), Is.False,
                "A-003 tiene 60 >= 10.");

            Assert.That(PedidoCalculator.Incluir(true, A004, StockA004), Is.False,
                "A-004 queda fuera porque 0 < 0 es falso. Es comportamiento esperado, no un defecto.");
        });
    }

    [Test]
    public void Con_soloBajoMinimo_en_No_se_listan_los_cuatro_articulos()
    {
        // RF-026: las filas con Cantidad a Pedir 0 se muestran igual, no se omiten.
        Assert.Multiple(() =>
        {
            Assert.That(PedidoCalculator.Incluir(false, A001, StockA001), Is.True);
            Assert.That(PedidoCalculator.Incluir(false, A002, StockA002), Is.True);
            Assert.That(PedidoCalculator.Incluir(false, A003, StockA003), Is.True);
            Assert.That(PedidoCalculator.Incluir(false, A004, StockA004), Is.True);
        });
    }

    // ---------------------------------------------------------------------------------------
    // T044 — La cantidad nunca es negativa.
    // ---------------------------------------------------------------------------------------

    [TestCase(ModoPedido.HastaStockMinimo)]
    [TestCase(ModoPedido.HastaPuntoPedido)]
    [TestCase(ModoPedido.HastaStockIdeal)]
    public void La_cantidad_nunca_es_negativa_cuando_el_stock_supera_el_nivel(ModoPedido modo)
    {
        // CE-003, segunda cláusula. Es el MAX(0, ...) de RF-026: un artículo con stock de sobra
        // arroja 0, jamás un número negativo que se leería como "devolver mercadería".
        var conStockDeSobra = PedidoCalculator.CantidadAPedir(modo, A003, stockActual: 1_000);

        Assert.That(conStockDeSobra, Is.Zero);
    }

    [Test]
    public void El_nivel_lo_determina_el_modo_de_pedido()
    {
        var parametros = new ParametrosDeReposicion(StockMinimo: 3, PuntoPedido: 7, StockIdeal: 11);

        Assert.Multiple(() =>
        {
            Assert.That(PedidoCalculator.Nivel(ModoPedido.HastaStockMinimo, parametros), Is.EqualTo(3));
            Assert.That(PedidoCalculator.Nivel(ModoPedido.HastaPuntoPedido, parametros), Is.EqualTo(7));
            Assert.That(PedidoCalculator.Nivel(ModoPedido.HastaStockIdeal, parametros), Is.EqualTo(11));
        });
    }

    [Test]
    public void Con_los_tres_parametros_iguales_las_tres_modalidades_coinciden()
    {
        // Caso límite del spec: RF-019 admite Mínimo = Punto de Pedido = Ideal.
        var iguales = new ParametrosDeReposicion(5, 5, 5);

        Assert.Multiple(() =>
        {
            Assert.That(PedidoCalculator.CantidadAPedir(ModoPedido.HastaStockMinimo, iguales, 2), Is.EqualTo(3));
            Assert.That(PedidoCalculator.CantidadAPedir(ModoPedido.HastaPuntoPedido, iguales, 2), Is.EqualTo(3));
            Assert.That(PedidoCalculator.CantidadAPedir(ModoPedido.HastaStockIdeal, iguales, 2), Is.EqualTo(3));
        });
    }
}
