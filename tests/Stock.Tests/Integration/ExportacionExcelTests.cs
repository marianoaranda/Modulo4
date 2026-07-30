using ClosedXML.Excel;

namespace Stock.Tests.Integration;

/// <summary>
/// T049 — La exportación de Generar Pedido replica exactamente la respuesta JSON (RF-031, V-10).
///
/// El Excel se genera en la API y no en <c>Stock.Web</c> justamente para que esto se cumpla por
/// construcción: una sola implementación de la consulta, del filtro y del tope, en vez de dos que
/// podrían divergir (R-05).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ExportacionExcelTests : IntegrationTestBase
{
    private const string Json = "/api/consultas/generar-pedido";
    private const string Excel = "/api/consultas/generar-pedido/excel";

    private async Task SembrarArticuloAsync(
        string codigo, int stockIdeal, string descripcion = "Artículo de prueba")
    {
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('{codigo}', N'{descripcion}', 10.00, 0, 0, 0, {stockIdeal});
            """);
    }

    private async Task<IXLWorksheet> DescargarHojaAsync(string url)
    {
        var respuesta = await Client.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();

        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        var libro = new XLWorkbook(new MemoryStream(bytes));

        return libro.Worksheets.First();
    }

    [Test]
    public async Task El_archivo_es_un_xlsx_real_y_no_un_csv_renombrado()
    {
        // R-05: RF-031 habla de un archivo Excel. Que ClosedXML pueda abrirlo es la prueba de que
        // es OpenXML nativo y no un CSV con la extensión cambiada, que dispararía advertencias de
        // formato al abrirlo.
        await SembrarArticuloAsync("A-001", stockIdeal: 10);

        var respuesta = await Client.GetAsync($"{Excel}?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

            Assert.DoesNotThrow(() =>
            {
                var bytes = respuesta.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                using var _ = new XLWorkbook(new MemoryStream(bytes));
            });
        });
    }

    [Test]
    public async Task El_excel_replica_filas_y_orden_de_la_respuesta_json()
    {
        await SembrarArticuloAsync("C-003", stockIdeal: 3);
        await SembrarArticuloAsync("A-001", stockIdeal: 1);
        await SembrarArticuloAsync("B-002", stockIdeal: 2);

        const string parametros = "?soloBajoMinimo=false&modoPedido=HastaStockIdeal";

        var respuestaJson = await Client.GetAsync(Json + parametros);
        var resultado = await Integration.Json.LeerAsync<ResultadoGenerarPedido>(respuestaJson);

        var hoja = await DescargarHojaAsync(Excel + parametros);

        // Fila 1 = encabezados, los datos empiezan en la 2.
        var codigosEnExcel = Enumerable
            .Range(2, resultado.Filas.Count)
            .Select(f => hoja.Cell(f, 1).GetString())
            .ToList();

        var cantidadesEnExcel = Enumerable
            .Range(2, resultado.Filas.Count)
            .Select(f => hoja.Cell(f, 3).GetValue<int>())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(codigosEnExcel, Is.EqualTo(resultado.Filas.Select(f => f.Codigo).ToList()));
            Assert.That(cantidadesEnExcel, Is.EqualTo(resultado.Filas.Select(f => f.CantidadAPedir).ToList()));
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(resultado.Filas.Count + 1),
                "El Excel no puede traer filas de más ni de menos que la pantalla.");
        });
    }

    [Test]
    public async Task El_excel_respeta_el_mismo_filtro_que_la_pantalla()
    {
        await SembrarArticuloAsync("V-001", 10, descripcion: "Válvula de bronce");
        await SembrarArticuloAsync("O-001", 10, descripcion: "Otra cosa");

        var hoja = await DescargarHojaAsync(
            $"{Excel}?soloBajoMinimo=false&modoPedido=HastaStockIdeal&descripcion=valvula");

        Assert.Multiple(() =>
        {
            Assert.That(hoja.Cell(2, 1).GetString(), Is.EqualTo("V-001"));
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(2), "Sólo el encabezado y una fila.");
        });
    }

    [Test]
    public async Task El_excel_replica_el_recorte_de_la_pantalla()
    {
        // RF-031: mismas filas, mismo orden y mismo recorte.
        await SembrarArticulosEnMasaAsync(10_500);

        var hoja = await DescargarHojaAsync($"{Excel}?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(10_001),
            "10.000 filas de datos más el encabezado.");
    }

    [Test]
    public async Task Un_resultado_vacio_exporta_solo_los_encabezados()
    {
        // RF-031, última cláusula. El catálogo está vacío: el archivo igual debe existir y abrirse,
        // con la fila de encabezados y ninguna de datos.
        var hoja = await DescargarHojaAsync($"{Excel}?soloBajoMinimo=true&modoPedido=HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(hoja.Cell(1, 1).GetString(), Is.EqualTo("Código"));
            Assert.That(hoja.Cell(1, 2).GetString(), Is.EqualTo("Descripción"));
            Assert.That(hoja.Cell(1, 3).GetString(), Is.EqualTo("Cantidad a Pedir"));
            Assert.That(hoja.LastRowUsed()!.RowNumber(), Is.EqualTo(1), "Sólo la fila de encabezados.");
        });
    }
}
