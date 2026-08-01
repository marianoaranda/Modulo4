using System.Text.RegularExpressions;

namespace Stock.Tests.Web;

/// <summary>
/// T149/T150 — La carga asistida del detalle de movimientos (RF-020g, RF-020h, RF-020i).
///
/// Como en la Fase 9, la lógica de cliente se verifica por su <b>contrato renderizado</b>: qué
/// columnas emite la grilla, qué expone la pantalla para que la sugerencia sea posible y con qué
/// rótulo aparece el total. Que el número sugerido sea el correcto lo deciden el catálogo y el
/// Tipo, y eso ya está verificado del lado de la API (T148).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoDetalleAsistidoTests : WebTestBase
{
    private const string MovimientoJson = """
        {"numero":7,"tipo":"Venta","fecha":"2026-01-15",
         "detalle":[{"codigo":"A-001","cantidad":2,"precioUnitario":10.00,"precioTotal":20.00}]}
        """;

    // -------------------------------------------------------------------------------------
    // T149 — RF-020g: la sugerencia del Precio Unitario según el Tipo.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task La_pantalla_expone_el_tipo_vigente_para_que_la_sugerencia_pueda_elegir_el_precio()
    {
        // La sugerencia es el Precio de Costo en una compra y el Precio de Venta en una venta, así
        // que el script necesita saber cuál es el Tipo vigente en el momento en que se carga el
        // Código. Sin esta marca, la pantalla no puede distinguirlos.
        var html = await PantallaDeCargaAsync();

        Assert.That(BuscadorArticulosTests.EtiquetaConId(html, "Tipo"),
            Does.Contain("data-tipo-movimiento"));
    }

    [Test]
    public async Task El_script_de_la_sugerencia_se_engancha_en_el_alta_y_en_la_edicion()
    {
        foreach (var html in new[] { await PantallaDeCargaAsync(), await PantallaDeEdicionAsync() })
        {
            Assert.That(Regex.Matches(html, @"src=""[^""]*movimiento-detalle\.js[^""]*"""),
                Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task La_pantalla_no_declara_un_segundo_origen_de_datos_del_articulo()
    {
        // RF-020g pide **una única consulta por Código**: la misma que sincroniza la Descripción
        // (RF-034b). Si la pantalla trajera una segunda ruta, lo que se muestra y lo que se
        // sugiere podrían salir de fuentes distintas y contradecirse.
        var html = await PantallaDeCargaAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("/api/articulos"),
                "El navegador nunca llama a la API directamente: no tiene el token.");
            Assert.That(Regex.Matches(html, @"src=""[^""]*buscador-articulos\.js[^""]*"""),
                Has.Count.EqualTo(1),
                "La resolución del Código vive en un solo script.");
        });
    }

    [Test]
    public async Task Al_editar_un_movimiento_las_lineas_grabadas_no_se_marcan_para_re_sugerir()
    {
        // RF-020g: el precio informado en un movimiento ya grabado refleja la operación real, no
        // el catálogo vigente. Volver a sugerir al abrir la edición lo pisaría.
        var html = await PantallaDeEdicionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("10.00").Or.Contain("10,00"),
                "La línea muestra el Precio Unitario que se grabó.");
            Assert.That(html, Does.Not.Contain("data-sugerir-precio"),
                "…y no lleva ninguna marca que dispare una sugerencia sobre ella.");
        });
    }

    // -------------------------------------------------------------------------------------
    // T150 — RF-020h y RF-020i: la grilla de cuatro columnas y el Total General.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task El_detalle_tiene_exactamente_las_cuatro_columnas_en_orden()
    {
        var html = await PantallaDeCargaAsync();

        var encabezados = Regex.Matches(
                GrillaDelDetalle(html), @"<th(?:\s[^>]*)?>(.*?)</th>", RegexOptions.Singleline)
            .Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]*>", string.Empty).Trim())
            .Where(t => t.Length > 0)
            .ToList();

        Assert.That(encabezados.Take(4),
            Is.EqualTo(new[] { "Código", "Cantidad", "Precio Unitario", "Precio Total" }));
    }

    [Test]
    public async Task La_descripcion_va_debajo_del_codigo_dentro_de_su_celda_y_no_como_quinta_columna()
    {
        var grilla = GrillaDelDetalle(await PantallaDeCargaAsync());

        // La primera celda de la primera fila del cuerpo: contiene el campo de Código y, dentro,
        // el lugar de la Descripción.
        var primeraCelda = Regex.Match(grilla, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline);

        Assert.Multiple(() =>
        {
            Assert.That(primeraCelda.Value, Does.Contain("data-articulo-codigo"));
            Assert.That(primeraCelda.Value, Does.Contain("data-descripcion-de"),
                "La Descripción vive dentro de la celda del Código, debajo de él.");
        });
    }

    [Test]
    public async Task El_precio_total_de_la_linea_no_es_editable()
    {
        // RF-020c: lo calcula el sistema. Un campo editable sugeriría que el valor tipeado va a
        // persistir, cuando el motor lo descarta.
        var grilla = GrillaDelDetalle(await PantallaDeCargaAsync());

        var celdasDeTotal = Regex.Matches(grilla, @"<[^>]*data-precio-total[^>]*>");

        Assert.Multiple(() =>
        {
            Assert.That(celdasDeTotal, Is.Not.Empty, "Cada línea muestra su Precio Total.");
            Assert.That(celdasDeTotal.All(c => !c.Value.StartsWith("<input")), Is.True,
                "…y no como campo de carga.");
        });
    }

    [Test]
    public async Task La_pantalla_muestra_un_total_general_rotulado_y_en_cero_sin_lineas()
    {
        var html = await PantallaDeCargaAsync();
        var grilla = GrillaDelDetalle(html);

        var total = Regex.Match(grilla, @"<[^>]*data-total-general[^>]*>(.*?)<", RegexOptions.Singleline);

        Assert.Multiple(() =>
        {
            Assert.That(grilla, Does.Contain("Total General"),
                "El total va rotulado con el texto exacto de RF-020i.");
            Assert.That(total.Success, Is.True, "…y tiene dónde escribirse.");
            Assert.That(total.Groups[1].Value.Trim(), Is.EqualTo("0"),
                "Un detalle sin líneas cargadas muestra 0.");
        });
    }

    [Test]
    public async Task Cada_linea_expone_la_cantidad_y_el_precio_que_alimentan_los_dos_totales()
    {
        var grilla = GrillaDelDetalle(await PantallaDeCargaAsync());

        Assert.Multiple(() =>
        {
            Assert.That(grilla, Does.Contain("data-cantidad"));
            Assert.That(grilla, Does.Contain("data-precio-unitario"));
        });
    }

    private async Task<string> PantallaDeCargaAsync()
    {
        Api.ResponderJson("""{"numero":8}""");

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync("/Movimientos/Create")).Content.ReadAsStringAsync();
    }

    private async Task<string> PantallaDeEdicionAsync()
    {
        Api.ResponderJson(MovimientoJson);

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync("/Movimientos/Edit/7")).Content.ReadAsStringAsync();
    }

    /// <summary>La tabla del detalle, para no confundir sus columnas con las del buscador.</summary>
    private static string GrillaDelDetalle(string html)
    {
        var desde = html.IndexOf("id=\"detalle\"", StringComparison.Ordinal);

        Assert.That(desde, Is.GreaterThanOrEqualTo(0), "La pantalla tiene la grilla del detalle.");

        var hasta = html.IndexOf("</table>", desde, StringComparison.Ordinal);

        return html[desde..hasta];
    }
}
