using System.Text.RegularExpressions;

namespace Stock.Tests.Web;

/// <summary>
/// T168/T168a — Mensajes de validación de la pantalla en español (RF-035).
///
/// Los que emite la capa web viajan en los atributos <c>data-val-*</c> y los muestra la validación
/// del cliente sin ir al servidor. Son, junto con los de la API, los que hoy están en inglés: nadie
/// los escribió, los genera el marco de trabajo a partir del tipo y de la obligatoriedad del campo.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MensajesEnEspanolTests : WebTestBase
{
    /// <summary>
    /// Marcadores del texto por omisión del marco de trabajo. Se buscan como subcadenas del HTML
    /// renderizado: es lo que hace verificable el "en toda la aplicación" de RF-035, en vez de
    /// revisar los tres mensajes que uno se acuerda de mirar.
    /// </summary>
    private static readonly string[] Marcadores =
        ["is required", "must be a number", "The value", "The field"];

    private const string ArticuloJson = """
        {"articuloId":1,"codigo":"A-001","descripcion":"Válvula","precioCosto":100.00,
         "margen":50.00,"precioVenta":150.00,"stockMinimo":10,"puntoPedido":20,"stockIdeal":50}
        """;

    [Test]
    public async Task El_campo_obligatorio_se_anuncia_en_espanol_con_el_rotulo_de_la_pantalla()
    {
        var html = await PantallaAsync("/Articulos/Create");

        Assert.Multiple(() =>
        {
            Assert.That(AtributoDe(html, "Codigo", "data-val-required"),
                Is.EqualTo("El campo Código es obligatorio."));
            Assert.That(AtributoDe(html, "Descripcion", "data-val-required"),
                Is.EqualTo("El campo Descripción es obligatorio."));
        });
    }

    [Test]
    public async Task El_campo_numerico_se_anuncia_en_espanol()
    {
        var html = await PantallaAsync("/Articulos/Create");

        Assert.That(AtributoDe(html, "PrecioCosto", "data-val-number"),
            Is.EqualTo("El campo Precio de Costo debe ser un número."));
    }

    [Test]
    public async Task El_texto_del_cliente_es_el_mismo_que_el_del_servidor()
    {
        // RF-035: un mismo rechazo no puede decirse de dos maneras según dónde se detecte. Ambos
        // salen de la misma plantilla, así que basta comparar el del cliente con el esperado.
        var html = await PantallaAsync("/Articulos/Create");

        Assert.That(
            AtributoDe(html, "Codigo", "data-val-required"),
            Is.EqualTo(string.Format(
                Stock.Web.Resources.MensajesDeValidacion.Obligatorio, "Código")));
    }

    // -------------------------------------------------------------------------------------
    // T168a — el barrido: "todos los mensajes" sólo es verificable si se recorre todo.
    // -------------------------------------------------------------------------------------

    [TestCase("/Articulos/Create")]
    [TestCase("/Articulos/Edit/1")]
    [TestCase("/Movimientos/Create")]
    [TestCase("/Usuarios/Create")]
    [TestCase("/Perfiles/Create")]
    public async Task Ninguna_pantalla_emite_mensajes_de_validacion_en_ingles(string ruta)
    {
        var html = await PantallaAsync(ruta);

        var encontrados = Marcadores.Where(m => html.Contains(m, StringComparison.Ordinal)).ToList();

        Assert.That(encontrados, Is.Empty,
            $"{ruta} emite texto en inglés: {string.Join(", ", encontrados)}");
    }

    private async Task<string> PantallaAsync(string ruta)
    {
        // Una respuesta que sirve para cualquiera de las pantallas del barrido: las de artículos
        // leen un artículo, las demás listan y no miran el cuerpo.
        Api.ResponderJson(ArticuloJson);

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync(ruta)).Content.ReadAsStringAsync();
    }

    /// <summary>El valor de ese atributo en la etiqueta que lleva ese `id`.</summary>
    private static string AtributoDe(string html, string id, string atributo)
    {
        var etiqueta = BuscadorArticulosTests.EtiquetaConId(html, id);
        var valor = Regex.Match(etiqueta, $@"\b{Regex.Escape(atributo)}=""([^""]*)""");

        Assert.That(valor.Success, Is.True, $"El campo {id} declara {atributo}.");

        return valor.Groups[1].Value;
    }
}
