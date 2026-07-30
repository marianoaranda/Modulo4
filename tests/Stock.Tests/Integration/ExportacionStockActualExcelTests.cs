using ClosedXML.Excel;

namespace Stock.Tests.Integration;

/// <summary>
/// T069a — Exportación de la <b>Consulta de Stock Actual</b> (RF-031).
///
/// RF-031 exige la réplica exacta en las <b>dos</b> exportaciones. Tener verificada sólo la de
/// Generar Pedido dejaría la mitad del requisito sostenida por analogía: son dos endpoints
/// distintos, con su propio pipeline de rango y filtro, y nada garantiza que el segundo herede la
/// corrección del primero.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ExportacionStockActualExcelTests : IntegrationTestBase
{
    private const string Json = "/api/consultas/stock-actual";
    private const string Excel = "/api/consultas/stock-actual/excel";

    private async Task SembrarArticuloAsync(string codigo, string descripcion = "Artículo de prueba") =>
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES (N'{codigo}', N'{descripcion}', 10.00, 0, 0, 0, 0);
            """);

    private async Task<IXLWorksheet> DescargarHojaAsync(string url)
    {
        var respuesta = await Client.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();

        var libro = new XLWorkbook(new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync()));

        return libro.Worksheets.First();
    }

    [Test]
    public async Task El_excel_replica_filas_orden_y_cantidades_de_la_respuesta_json()
    {
        await SembrarArticuloAsync("C-003");
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("B-002");

        var articuloId = await EscalarAsync<int>("SELECT ArticuloId FROM dbo.Articulo WHERE Codigo = 'B-002'");
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Movimiento (Tipo, Fecha) VALUES (1, '2026-01-15');
            INSERT INTO dbo.MovimientoDetalle (MovimientoNumero, ArticuloId, Cantidad, PrecioUnitario)
            VALUES (SCOPE_IDENTITY(), {articuloId}, 42, 10.00);
            """);

        var respuestaJson = await Client.GetAsync(Json);
        var resultado = await Integration.Json.LeerAsync<ResultadoStockActual>(respuestaJson);

        var hoja = await DescargarHojaAsync(Excel);

        var codigos = Enumerable.Range(2, resultado.Filas.Count)
            .Select(f => hoja.Cell(f, 1).GetString()).ToList();

        var cantidades = Enumerable.Range(2, resultado.Filas.Count)
            .Select(f => hoja.Cell(f, 3).GetValue<int>()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(codigos, Is.EqualTo(resultado.Filas.Select(f => f.Codigo).ToList()));
            Assert.That(cantidades, Is.EqualTo(resultado.Filas.Select(f => f.Cantidad).ToList()));
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(resultado.Filas.Count + 1));
        });
    }

    [Test]
    public async Task El_excel_respeta_el_mismo_rango_y_filtro_que_la_pantalla()
    {
        await SembrarArticuloAsync("A-001", "Válvula de bronce");
        await SembrarArticuloAsync("A-002", "Otra cosa");
        await SembrarArticuloAsync("Z-001", "Válvula de bronce");

        var hoja = await DescargarHojaAsync(
            $"{Excel}?codigoDesde=A-001&codigoHasta=A-999&descripcion=valvula");

        Assert.Multiple(() =>
        {
            Assert.That(hoja.Cell(2, 1).GetString(), Is.EqualTo("A-001"));
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task El_excel_replica_el_recorte_de_la_pantalla()
    {
        await SembrarArticulosEnMasaAsync(10_500);

        var hoja = await DescargarHojaAsync(Excel);

        Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(10_001));
    }

    [Test]
    public async Task Un_resultado_vacio_exporta_solo_los_encabezados()
    {
        var hoja = await DescargarHojaAsync(Excel);

        Assert.Multiple(() =>
        {
            Assert.That(hoja.Cell(1, 1).GetString(), Is.EqualTo("Código"));
            Assert.That(hoja.Cell(1, 2).GetString(), Is.EqualTo("Descripción"));
            Assert.That(hoja.Cell(1, 3).GetString(), Is.EqualTo("Cantidad"),
                "RF-025: en esta consulta la columna se rotula 'Cantidad'.");
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(1));
        });
    }
}
