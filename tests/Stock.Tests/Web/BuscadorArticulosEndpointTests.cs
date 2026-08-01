using System.Net;
using System.Text.Json;
using Stock.Web.Resources;

namespace Stock.Tests.Web;

/// <summary>
/// T136a — La puerta JSON del buscador (RF-034a).
///
/// El script del navegador no puede pedirle datos a <c>Stock.Api</c>: el JWT vive en un claim de
/// la cookie de sesión y lo adjunta <c>BearerTokenHandler</c> a las llamadas salientes del
/// <b>servidor</b>. Por eso el buscador consume una acción del mismo origen que la página, que
/// proxea la consulta con la sesión ya establecida. Sin este test, un buscador que apuntara
/// directo a la API pasaría los tests de marcado y fallaría recién en el navegador.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class BuscadorArticulosEndpointTests : WebTestBase
{
    private const string DosArticulosJson = """
        [{"articuloId":1,"codigo":"A-001","descripcion":"Válvula","precioCosto":100.00,
          "margen":50.00,"precioVenta":150.00,"stockMinimo":0,"puntoPedido":0,"stockIdeal":0},
         {"articuloId":2,"codigo":"A-002","descripcion":"Codo de bronce","precioCosto":80.00,
          "margen":25.00,"precioVenta":100.00,"stockMinimo":0,"puntoPedido":0,"stockIdeal":0}]
        """;

    [Test]
    public async Task Devuelve_json_con_el_codigo_y_la_descripcion_de_cada_fila()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?descripcion=bronce");

        Assert.That(
            respuesta.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var filas = documento.RootElement.GetProperty("filas");

        Assert.Multiple(() =>
        {
            Assert.That(filas.GetArrayLength(), Is.EqualTo(2));
            Assert.That(filas[0].GetProperty("codigo").GetString(), Is.EqualTo("A-001"));
            Assert.That(filas[0].GetProperty("descripcion").GetString(), Is.EqualTo("Válvula"));
        });
    }

    [Test]
    public async Task Reenvia_el_filtro_por_descripcion_a_la_API()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        await cliente.GetAsync("/Articulos/Buscar?descripcion=bronce");

        var url = Api.UltimaSolicitud.RequestUri!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("/api/articulos"));
            Assert.That(url, Does.Contain("descripcion=bronce"));
        });
    }

    [Test]
    public async Task La_respuesta_no_expone_el_token_de_la_sesion()
    {
        // El token nunca sale del servidor: si viajara en el cuerpo o en un encabezado, cualquier
        // script de la página podría leerlo y la cookie dejaría de ser la única custodia.
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?descripcion=");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cuerpo, Does.Not.Contain(TokenDePrueba));
            Assert.That(
                respuesta.Headers.Concat(respuesta.Content.Headers)
                    .SelectMany(h => h.Value)
                    .Any(v => v.Contains(TokenDePrueba)),
                Is.False,
                "Ningún encabezado de la respuesta transporta el token.");
        });
    }

    [Test]
    public async Task Sin_sesion_no_devuelve_datos()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?descripcion=");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Redirect),
                "El filtro global de autorización manda al login.");
            Assert.That(await respuesta.Content.ReadAsStringAsync(), Does.Not.Contain("A-001"));
        });
    }

    [Test]
    public async Task Una_descripcion_vacia_no_libera_del_tope_y_avisa_del_recorte()
    {
        // RF-034a: la búsqueda sin filtro no lista sin límite. El aviso viaja en la respuesta y no
        // lo inventa el script: así el texto exacto de RF-032a queda del lado del servidor, donde
        // ya vive para las dos consultas.
        Api.ResponderJson(JsonDeArticulos(10_000));

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?descripcion=");

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(documento.RootElement.GetProperty("truncado").GetBoolean(), Is.True);
            Assert.That(
                documento.RootElement.GetProperty("aviso").GetString(),
                Is.EqualTo(MensajesDeConsulta.ResultadoRecortado));
        });
    }

    [Test]
    public async Task Un_resultado_por_debajo_del_tope_no_avisa_de_recorte()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?descripcion=");

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.That(documento.RootElement.GetProperty("truncado").GetBoolean(), Is.False);
    }

    // -------------------------------------------------------------------------------------
    // T150a — RF-020g: la misma puerta resuelve un Código puntual, con sus dos precios.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Por_codigo_devuelve_la_descripcion_y_los_dos_precios_en_una_sola_respuesta()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?codigo=A-001");

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var fila = documento.RootElement.GetProperty("filas")[0];

        Assert.Multiple(() =>
        {
            Assert.That(fila.GetProperty("descripcion").GetString(), Is.EqualTo("Válvula"));
            Assert.That(fila.GetProperty("precioCosto").GetDecimal(), Is.EqualTo(100.00m));
            Assert.That(fila.GetProperty("precioVenta").GetDecimal(), Is.EqualTo(150.00m));
        });
    }

    [Test]
    public async Task Reenvia_el_codigo_a_la_API_sin_convertirlo_en_filtro_por_descripcion()
    {
        Api.ResponderJson(DosArticulosJson);

        var cliente = ClienteConSesion();
        await cliente.GetAsync("/Articulos/Buscar?codigo=A-001");

        var url = Api.UltimaSolicitud.RequestUri!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("codigo=A-001"));
            Assert.That(url, Does.Not.Contain("descripcion=A-001"));
        });
    }

    [Test]
    public async Task Un_codigo_inexistente_responde_200_con_el_resultado_vacio()
    {
        // Para la pantalla es "no hay sugerencia", no un error: el 404 llega recién al grabar
        // (RF-020e). Un error acá interrumpiría la carga por un Código a medio tipear.
        Api.ResponderJson("[]");

        var cliente = ClienteConSesion();
        var respuesta = await cliente.GetAsync("/Articulos/Buscar?codigo=NO-EXISTE");

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(documento.RootElement.GetProperty("filas").GetArrayLength(), Is.Zero);
        });
    }

    private static string JsonDeArticulos(int cantidad) =>
        "[" + string.Join(",", Enumerable.Range(1, cantidad).Select(i => $$"""
            {"articuloId":{{i}},"codigo":"A-{{i:D5}}","descripcion":"Artículo {{i}}",
             "precioCosto":10.00,"margen":0.00,"precioVenta":10.00,
             "stockMinimo":0,"puntoPedido":0,"stockIdeal":0}
            """)) + "]";
}
