using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;

namespace Stock.Tests.Unit;

/// <summary>
/// T059/T060 — Validación de movimiento como lógica pura.
///
/// <b>No hay caso de "cantidad no entera"</b>, y su ausencia es deliberada: el validador recibe la
/// cantidad ya tipada como <c>int</c>, así que un valor no entero no puede alcanzarlo y el caso
/// sería vacuo. Ese rechazo es un requisito del <i>contrato</i> —ocurre al deserializar, en el
/// borde de la solicitud (RF-018a)— y se verifica en T070a, donde efectivamente se puede observar.
/// </summary>
[TestFixture]
[Category(TestCategories.Unit)]
public class MovimientoValidatorTests
{
    private static readonly DateOnly Hoy = new(2026, 7, 30);

    private static MovimientoAValidar Movimiento(
        TipoMovimiento tipo = TipoMovimiento.Compra,
        DateOnly? fecha = null,
        params LineaAValidar[] detalle) =>
        new(tipo,
            fecha ?? Hoy,
            detalle.Length > 0 ? detalle : [new LineaAValidar(Codigo: "A-001", Cantidad: 1, PrecioUnitario: 10m)]);

    private static IReadOnlyList<ErrorDeValidacion> Validar(MovimientoAValidar movimiento) =>
        MovimientoValidator.Validar(movimiento, Hoy);

    [Test]
    public void Un_movimiento_valido_no_arroja_errores() =>
        Assert.That(Validar(Movimiento()), Is.Empty);

    // --------------------------------------------------------------------------------------
    // Cantidad — RF-023 y RF-023a.
    // --------------------------------------------------------------------------------------

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-1000)]
    public void Cantidad_menor_o_igual_a_cero_se_rechaza(int cantidad) =>
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", cantidad, 10m))), Is.Not.Empty);

    [Test]
    public void Cantidad_por_encima_de_un_millon_se_rechaza() =>
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1_000_001, 1m))), Is.Not.Empty);

    [Test]
    public void Cantidad_de_exactamente_un_millon_se_acepta() =>
        // El tope de RF-023a es inclusive: "mayor a 1.000.000" es lo que se rechaza.
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1_000_000, 1m))), Is.Empty);

    // --------------------------------------------------------------------------------------
    // Precio Unitario — RF-023a y RF-023c.
    // --------------------------------------------------------------------------------------

    [Test]
    public void Precio_unitario_negativo_se_rechaza() =>
        // RF-023c: fija el extremo inferior que RF-023a dejaba abierto.
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1, -0.01m))), Is.Not.Empty);

    [Test]
    public void Precio_unitario_cero_se_acepta() =>
        // Una bonificación es una operación real con precio 0.
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1, 0m))), Is.Empty);

    [Test]
    public void Precio_unitario_por_encima_del_tope_se_rechaza() =>
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1, 10_000_000m))), Is.Not.Empty);

    [Test]
    public void Precio_unitario_de_exactamente_el_tope_se_acepta() =>
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 1, 9_999_999.99m))), Is.Empty);

    // --------------------------------------------------------------------------------------
    // Precio Total — RF-023a, tercer límite.
    // --------------------------------------------------------------------------------------

    [Test]
    public void Precio_total_por_encima_del_tope_se_rechaza()
    {
        // 1.000.000 × 9.999.999,99 ≈ 10^13, por encima de 999.999.999.999,99. Cada factor está
        // dentro de su propio límite: el tope del producto es una regla aparte, no un corolario.
        var linea = new LineaAValidar("A-001", 1_000_000, 9_999_999.99m);

        Assert.That(Validar(Movimiento(detalle: linea)), Is.Not.Empty);
    }

    [Test]
    public void Precio_total_dentro_del_tope_se_acepta() =>
        Assert.That(Validar(Movimiento(detalle: new LineaAValidar("A-001", 100, 1_000m))), Is.Empty);

    // --------------------------------------------------------------------------------------
    // Encabezado — RF-020b y RF-020d.
    // --------------------------------------------------------------------------------------

    [Test]
    public void Fecha_futura_se_rechaza() =>
        // RF-020d. Se valida acá y no con un CHECK porque la condición depende del momento de
        // evaluación: lo que hoy es futuro mañana no lo es.
        Assert.That(Validar(Movimiento(fecha: Hoy.AddDays(1))), Is.Not.Empty);

    [Test]
    public void Fecha_de_hoy_se_acepta() =>
        Assert.That(Validar(Movimiento(fecha: Hoy)), Is.Empty);

    [Test]
    public void Fecha_pasada_se_acepta() =>
        // RF-029: el inventario de apertura se carga con compras fechadas en el pasado.
        Assert.That(Validar(Movimiento(fecha: Hoy.AddYears(-2))), Is.Empty);

    [Test]
    public void Tipo_fuera_del_conjunto_cerrado_se_rechaza() =>
        // RF-020b: sólo Compra y Venta.
        Assert.That(Validar(Movimiento(tipo: (TipoMovimiento)7)), Is.Not.Empty);

    [Test]
    public void Un_movimiento_sin_lineas_se_rechaza() =>
        Assert.That(MovimientoValidator.Validar(
            new MovimientoAValidar(TipoMovimiento.Compra, Hoy, []), Hoy), Is.Not.Empty);

    // --------------------------------------------------------------------------------------
    // T060 — RF-023b: ninguna validación cruzada contra el catálogo.
    // --------------------------------------------------------------------------------------

    [Test]
    public void Un_precio_deliberadamente_distinto_al_del_catalogo_se_acepta()
    {
        // Caso de regresión de RF-023b. El precio se informa por movimiento y refleja la operación
        // real: una compra en oferta o una venta con descuento son válidas y no tienen por qué
        // coincidir con el Precio de Costo ni con el Precio de Venta del artículo.
        //
        // El validador ni siquiera recibe el artículo: la ausencia de esa dependencia es la forma
        // más fuerte de garantizar que la validación cruzada no puede colarse.
        var muyPorDebajo = new LineaAValidar(Codigo: "A-001", Cantidad: 5, PrecioUnitario: 0.01m);
        var muyPorEncima = new LineaAValidar(Codigo: "A-001", Cantidad: 5, PrecioUnitario: 999_999m);

        Assert.Multiple(() =>
        {
            Assert.That(Validar(Movimiento(detalle: muyPorDebajo)), Is.Empty);
            Assert.That(Validar(Movimiento(detalle: muyPorEncima)), Is.Empty);
        });
    }

    [Test]
    public void Se_informan_todas_las_lineas_invalidas_y_no_solo_la_primera()
    {
        var movimiento = Movimiento(detalle:
        [
            new LineaAValidar("A-001", 0, 10m),
            new LineaAValidar("A-002", 5, -1m),
        ]);

        Assert.That(Validar(movimiento), Has.Count.GreaterThanOrEqualTo(2));
    }
}
