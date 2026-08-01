using System.Text.RegularExpressions;

namespace Stock.Tests.Web;

/// <summary>
/// T159 — Rango sugerido en la Consulta de Stock Actual (RF-025b).
///
/// La pantalla abre mostrando sobre qué universo va a consultar. Es una comodidad: los campos
/// quedan editables y el resultado no cambia —el rango completo y el rango vacío devuelven las
/// mismas filas—, así que lo que hay que verificar es de dónde salen los extremos, que no pisen lo
/// que el usuario escribió y que abrir la pantalla siga sin consultar.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class StockActualRangoTests : WebTestBase
{
    private const string ExtremosJson = """{"codigoDesde":"A-001","codigoHasta":"Z-999"}""";

    private const string CatalogoVacioJson = """{"codigoDesde":null,"codigoHasta":null}""";

    [Test]
    public async Task Al_abrir_la_pantalla_el_rango_trae_los_extremos_del_catalogo()
    {
        var html = await PantallaAsync(ExtremosJson, "/StockActual");

        Assert.Multiple(() =>
        {
            Assert.That(ValorDe(html, "codigoDesde"), Is.EqualTo("A-001"));
            Assert.That(ValorDe(html, "codigoHasta"), Is.EqualTo("Z-999"));
        });
    }

    [Test]
    public async Task Los_extremos_se_piden_al_recurso_que_los_calcula_y_no_al_listado()
    {
        // El listado recorta en 10.000 filas, así que su última fila no es el último Código del
        // catálogo (RF-027). Pedirle los extremos daría un rango sutilmente equivocado justo en el
        // caso que ese tope contempla.
        await PantallaAsync(ExtremosJson, "/StockActual");

        Assert.That(Api.UltimaSolicitud.RequestUri!.AbsolutePath,
            Is.EqualTo("/api/articulos/extremos"));
    }

    [Test]
    public async Task Con_el_catalogo_vacio_los_dos_campos_quedan_en_blanco_y_sin_error()
    {
        var html = await PantallaAsync(CatalogoVacioJson, "/StockActual");

        Assert.Multiple(() =>
        {
            Assert.That(ValorDe(html, "codigoDesde"), Is.Empty);
            Assert.That(ValorDe(html, "codigoHasta"), Is.Empty);
            Assert.That(html, Does.Not.Contain("alert-danger"), "No es un error.");
        });
    }

    [Test]
    public async Task Abrir_la_pantalla_no_ejecuta_la_consulta()
    {
        // Pedir los extremos no es consultar: sigue valiendo la distinción entre el primer ingreso
        // y una consulta ejecutada sin filtros, que es la que evita informarle al usuario que su
        // búsqueda no tuvo resultados antes de que buscara nada.
        var html = await PantallaAsync(ExtremosJson, "/StockActual");

        var rutas = Api.Recibidas.Select(r => r.RequestUri!.AbsolutePath).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rutas, Does.Not.Contain("/api/consultas/stock-actual"));
            Assert.That(html, Does.Not.Contain(Stock.Web.Resources.MensajesDeConsulta.ResultadoVacio));
        });
    }

    [Test]
    public async Task Un_rango_tecleado_por_el_usuario_no_se_pisa_con_el_sugerido()
    {
        Api.Responder(solicitud => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(
                solicitud.RequestUri!.AbsolutePath.EndsWith("/extremos", StringComparison.Ordinal)
                    ? ExtremosJson
                    : """{"filas":[],"truncado":false}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        });

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/StockActual?codigoDesde=M-100&codigoHasta=M-200"))
            .Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(ValorDe(html, "codigoDesde"), Is.EqualTo("M-100"));
            Assert.That(ValorDe(html, "codigoHasta"), Is.EqualTo("M-200"));
        });
    }

    private async Task<string> PantallaAsync(string json, string ruta)
    {
        Api.ResponderJson(json);

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync(ruta)).Content.ReadAsStringAsync();
    }

    /// <summary>El `value` del campo con ese `id`, o vacío si no lo tiene.</summary>
    private static string ValorDe(string html, string id)
    {
        var etiqueta = BuscadorArticulosTests.EtiquetaConId(html, id);
        var valor = Regex.Match(etiqueta, @"\bvalue=""([^""]*)""");

        return valor.Success ? valor.Groups[1].Value : string.Empty;
    }
}
