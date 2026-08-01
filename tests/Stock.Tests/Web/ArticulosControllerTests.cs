using System.Net;

namespace Stock.Tests.Web;

/// <summary>
/// T086 — Capa web del ABM de artículos.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ArticulosControllerTests : WebTestBase
{
    private const string UnArticuloJson = """
        [{"articuloId":1,"codigo":"A-001","descripcion":"Válvula","precioCosto":100.00,
          "margen":50.00,"precioVenta":150.00,"stockMinimo":10,"puntoPedido":20,"stockIdeal":50}]
        """;

    private const string ArticuloJson = """
        {"articuloId":1,"codigo":"A-001","descripcion":"Válvula","precioCosto":100.00,
         "margen":50.00,"precioVenta":150.00,"stockMinimo":10,"puntoPedido":20,"stockIdeal":50}
        """;

    [Test]
    public async Task El_listado_muestra_los_articulos_de_la_API()
    {
        Api.ResponderJson(UnArticuloJson);

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/Articulos")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("A-001"));
            Assert.That(html, Does.Contain("150"));
        });
    }

    [Test]
    public async Task El_precio_de_venta_se_muestra_como_solo_lectura()
    {
        // RF-016: lo calcula el sistema. Un campo editable invitaría a cargarlo a mano y
        // sugeriría que el valor tipeado va a persistir, cuando el motor lo descarta.
        Api.ResponderJson(ArticuloJson);

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/Articulos/Edit/1")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("150"), "El precio de venta se muestra.");
            Assert.That(html, Does.Match(@"(?s)PrecioVenta.*?readonly|readonly.*?PrecioVenta"),
                "…pero como campo de sólo lectura.");
        });
    }

    [Test]
    public async Task Las_vistas_de_alta_y_edicion_recalculan_el_precio_de_venta_al_editar()
    {
        // T138 — RF-016a. El campo sigue siendo de sólo lectura (lo garantiza el caso de T086, que
        // debe seguir en verde): lo que se agrega es que el valor mostrado acompañe lo que el
        // usuario está tipeando, sin grabar ni recargar. Se asierta el cableado —los dos campos
        // que disparan el recálculo y el script que lo hace— porque eso es lo que la vista puede
        // prometer; que el número final sea el correcto lo sigue decidiendo el servidor (RF-016).
        Api.ResponderJson(ArticuloJson);

        var cliente = ClienteConSesion();

        var alta = await (await cliente.GetAsync("/Articulos/Create")).Content.ReadAsStringAsync();
        var edicion = await (await cliente.GetAsync("/Articulos/Edit/1")).Content.ReadAsStringAsync();

        foreach (var html in new[] { alta, edicion })
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    BuscadorArticulosTests.EtiquetaConId(html, "PrecioCosto"),
                    Does.Contain("data-precio-costo"),
                    "El Precio de Costo dispara el recálculo.");
                Assert.That(
                    BuscadorArticulosTests.EtiquetaConId(html, "Margen"),
                    Does.Contain("data-margen"),
                    "El Margen también.");
                Assert.That(
                    BuscadorArticulosTests.EtiquetaConId(html, "PrecioVenta"),
                    Does.Contain("data-precio-venta"),
                    "Y el Precio de Venta es el destino del cálculo.");
                Assert.That(html, Does.Contain("articulo-precio.js"));
            });
        }
    }

    [Test]
    public async Task Un_409_de_codigo_duplicado_se_propaga_a_la_vista()
    {
        // RF-017: es un rechazo previsto. El usuario tiene que volver al formulario con sus datos
        // y el motivo, no a una página de error.
        Api.ResponderProblema(HttpStatusCode.Conflict, "Ya existe un artículo con el código A-001.");

        var cliente = ClienteConSesion();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "A-001",
            ["Descripcion"] = "Válvula",
            ["PrecioCosto"] = "100",
            ["Margen"] = "50",
            ["StockMinimo"] = "10",
            ["PuntoPedido"] = "20",
            ["StockIdeal"] = "50",
        });

        var respuesta = await cliente.PostAsync("/Articulos/Create", formulario);
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("Ya existe un artículo con el código A-001."));
            Assert.That(html, Does.Contain("A-001"), "Los datos cargados no se pierden.");
        });
    }

    [Test]
    public async Task Un_409_de_baja_restringida_se_propaga_a_la_vista()
    {
        // RF-014a.
        Api.Responder(solicitud => solicitud.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"status":409,"detail":"El artículo tiene movimientos asociados y no puede eliminarse."}""",
                    System.Text.Encoding.UTF8,
                    "application/problem+json"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ArticuloJson, System.Text.Encoding.UTF8, "application/json"),
            });

        var cliente = ClienteConSesion();
        var respuesta = await cliente.PostAsync("/Articulos/Delete/1", new FormUrlEncodedContent([]));
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.That(html, Does.Contain("El artículo tiene movimientos asociados y no puede eliminarse."));
    }

    [Test]
    public async Task El_alta_envia_a_la_API_los_parametros_de_reposicion()
    {
        Api.ResponderJson(ArticuloJson, HttpStatusCode.Created);

        var cliente = ClienteConSesion();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "A-001",
            ["Descripcion"] = "Válvula",
            ["PrecioCosto"] = "100",
            ["Margen"] = "50",
            ["StockMinimo"] = "10",
            ["PuntoPedido"] = "20",
            ["StockIdeal"] = "50",
        });

        await cliente.PostAsync("/Articulos/Create", formulario);

        var enviado = await Api.UltimaSolicitud.Content!.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(enviado, Does.Contain("\"stockMinimo\":10"));
            Assert.That(enviado, Does.Contain("\"puntoPedido\":20"));
            Assert.That(enviado, Does.Contain("\"stockIdeal\":50"));
            Assert.That(enviado, Does.Not.Contain("precioVenta"),
                "El precio de venta no se envía: lo calcula el motor (RF-016).");
        });
    }
}
