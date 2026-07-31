namespace Stock.Tests.Integration;

/// <summary>
/// T122 — Rendimiento secuencial sobre el volumen de referencia (V-5, CE-002).
///
/// Es el escenario que <b>cierra el riesgo abierto que el spec dejó marcado</b>: el Stock Actual se
/// calcula por agregación de los movimientos y no se persiste en un campo. Si este fixture no
/// entrara en el presupuesto, la decisión R-01 tendría que revisarse antes de denormalizar nada —de
/// ahí que mida el peor caso y no un caso cómodo.
///
/// Categoría <c>Volumen</c>: el <c>.runsettings</c> lo excluye de la corrida por defecto, porque
/// sembrar 110.000 filas y ejecutar 110 consultas sobre ellas no puede estar en el camino de cada
/// <c>dotnet test</c>. Se corre con
/// <c>dotnet test StockModulo.sln --filter TestCategory=Volumen</c>.
/// </summary>
[Category(TestCategories.Volumen)]
public class RendimientoTests : RendimientoTestBase
{
    private const int CorridasDeCalentamiento = 5;

    /// <summary>
    /// 50 corridas medidas es el mínimo con el que un p95 significa algo: con menos, el percentil
    /// queda determinado por una o dos observaciones y una sola corrida lenta —un checkpoint del
    /// motor, otro proceso de la máquina— decide el resultado del test.
    /// </summary>
    private const int CorridasMedidas = 50;

    [Test]
    public async Task El_volumen_de_referencia_esta_completo_antes_de_medir()
    {
        await VerificarVolumenAsync();
    }

    [Test]
    public async Task La_consulta_de_stock_actual_responde_dentro_del_presupuesto()
    {
        await VerificarVolumenAsync();

        var muestras = await MedirAsync(
            Client, RutaStockActual, CorridasDeCalentamiento, CorridasMedidas);

        Informar("Consulta de Stock Actual", muestras);

        Assert.That(P95(muestras), Is.LessThan(PresupuestoP95),
            "CE-002 fija 3 segundos de p95 para la Consulta de Stock Actual sobre 10.000 " +
            "artículos y 100.000 líneas de detalle.");
    }

    [Test]
    public async Task La_consulta_de_generar_pedido_responde_dentro_del_presupuesto()
    {
        await VerificarVolumenAsync();

        var muestras = await MedirAsync(
            Client, RutaGenerarPedido, CorridasDeCalentamiento, CorridasMedidas);

        Informar("Generar Pedido", muestras);

        Assert.That(P95(muestras), Is.LessThan(PresupuestoP95),
            "CE-002 fija 3 segundos de p95 para Generar Pedido sobre 10.000 artículos y " +
            "100.000 líneas de detalle.");
    }
}
