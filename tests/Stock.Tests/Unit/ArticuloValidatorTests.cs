using Stock.Api.Domain.Validation;

namespace Stock.Tests.Unit;

/// <summary>
/// T082 — Validación de artículo como lógica pura (RF-018, RF-019).
///
/// <b>Sin caso de "parámetro no entero"</b>, por el mismo motivo que en
/// <c>MovimientoValidatorTests</c>: los tres parámetros de reposición llegan tipados como
/// <c>int</c>, así que un valor no entero no puede alcanzar al validador. Ese rechazo ocurre en el
/// borde de la solicitud (RF-018a) y se verifica en T085a.
/// </summary>
[TestFixture]
[Category(TestCategories.Unit)]
public class ArticuloValidatorTests
{
    private static ArticuloAValidar Articulo(
        string codigo = "A-001",
        string descripcion = "Artículo de prueba",
        decimal precioCosto = 100m,
        decimal margen = 50m,
        int stockMinimo = 10,
        int puntoPedido = 20,
        int stockIdeal = 50) =>
        new(codigo, descripcion, precioCosto, margen, stockMinimo, puntoPedido, stockIdeal);

    [Test]
    public void Un_articulo_valido_no_arroja_errores() =>
        Assert.That(ArticuloValidator.Validar(Articulo()), Is.Empty);

    [TestCase("")]
    [TestCase("   ")]
    public void Codigo_vacio_se_rechaza(string codigo) =>
        Assert.That(ArticuloValidator.Validar(Articulo(codigo: codigo)), Is.Not.Empty);

    [Test]
    public void Descripcion_vacia_se_rechaza() =>
        Assert.That(ArticuloValidator.Validar(Articulo(descripcion: "")), Is.Not.Empty);

    [Test]
    public void PrecioCosto_negativo_se_rechaza() =>
        Assert.That(ArticuloValidator.Validar(Articulo(precioCosto: -0.01m)), Is.Not.Empty);

    [Test]
    public void Margen_negativo_se_rechaza() =>
        Assert.That(ArticuloValidator.Validar(Articulo(margen: -1m)), Is.Not.Empty);

    [Test]
    public void PrecioCosto_y_margen_en_cero_se_aceptan() =>
        // RF-018 prohíbe los negativos, no el cero: un artículo de costo 0 y margen 0 es válido.
        Assert.That(ArticuloValidator.Validar(Articulo(precioCosto: 0m, margen: 0m)), Is.Empty);

    [TestCase(-1, 20, 50, TestName = "StockMinimo negativo")]
    [TestCase(10, -1, 50, TestName = "PuntoPedido negativo")]
    [TestCase(10, 20, -1, TestName = "StockIdeal negativo")]
    public void Parametros_de_reposicion_negativos_se_rechazan(int minimo, int punto, int ideal) =>
        Assert.That(
            ArticuloValidator.Validar(Articulo(stockMinimo: minimo, puntoPedido: punto, stockIdeal: ideal)),
            Is.Not.Empty);

    [TestCase(30, 20, 50, TestName = "Minimo mayor que Punto de Pedido")]
    [TestCase(10, 60, 50, TestName = "Punto de Pedido mayor que Ideal")]
    [TestCase(50, 20, 10, TestName = "Orden completamente invertido")]
    public void Se_rechaza_el_incumplimiento_del_orden_de_los_tres_stocks(
        int minimo, int punto, int ideal) =>
        // RF-019: Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal.
        Assert.That(
            ArticuloValidator.Validar(Articulo(stockMinimo: minimo, puntoPedido: punto, stockIdeal: ideal)),
            Is.Not.Empty);

    [Test]
    public void Los_tres_parametros_iguales_se_aceptan() =>
        // Caso límite del spec: RF-019 admite la igualdad, y entonces las tres modalidades de
        // pedido arrojan el mismo resultado.
        Assert.That(
            ArticuloValidator.Validar(Articulo(stockMinimo: 7, puntoPedido: 7, stockIdeal: 7)),
            Is.Empty);

    [Test]
    public void Los_tres_parametros_en_cero_se_aceptan() =>
        // Es el caso de A-004 del Conjunto de Datos de Referencia.
        Assert.That(
            ArticuloValidator.Validar(Articulo(stockMinimo: 0, puntoPedido: 0, stockIdeal: 0)),
            Is.Empty);

    [Test]
    public void Se_informan_todos_los_errores_y_no_solo_el_primero()
    {
        var muyMal = Articulo(codigo: "", precioCosto: -1m, stockMinimo: 99, puntoPedido: 1, stockIdeal: 2);

        Assert.That(ArticuloValidator.Validar(muyMal), Has.Count.GreaterThanOrEqualTo(3));
    }
}
