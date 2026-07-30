using System.Net;
using Stock.Web.Resources;

namespace Stock.Tests.Web;

/// <summary>
/// T050 — La pantalla de Generar Pedido.
///
/// Lo que se verifica acá es el comportamiento propio del front: que arme la solicitud con los dos
/// parámetros de reposición, que muestre los mensajes informativos con el texto exacto del spec y
/// que retransmita el Excel sin tocarlo. La regla de cálculo ya tiene sus tests en la API.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class GenerarPedidoControllerTests : WebTestBase
{
    private const string VacioJson = """{"filas":[],"truncado":false}""";

    private const string UnaFilaJson = """
        {"filas":[{"codigo":"A-001","descripcion":"Válvula","cantidadAPedir":5}],"truncado":false}
        """;

    private const string TruncadoJson = """
        {"filas":[{"codigo":"A-001","descripcion":"Válvula","cantidadAPedir":5}],"truncado":true}
        """;

    // ---------------------------------------------------------------------------------------
    // Los textos literales que fija el spec. Se asertan UNA vez, acá: el resto de los tests
    // compara contra la constante compartida, de modo que la vista y el test no puedan divergir
    // y a la vez el texto del spec quede fijado en un único lugar verificable.
    // ---------------------------------------------------------------------------------------

    [Test]
    public void Los_mensajes_informativos_dicen_exactamente_lo_que_fija_el_spec()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MensajesDeConsulta.ResultadoVacio,
                Is.EqualTo("No hay artículos que cumplan los criterios de la consulta."),
                "RF-032.");

            Assert.That(MensajesDeConsulta.ResultadoRecortado,
                Is.EqualTo("Se muestran las primeras 10.000 filas. Acote la búsqueda con el filtro por descripción."),
                "RF-032a.");
        });
    }

    [Test]
    public async Task Envia_a_la_API_los_dos_parametros_de_reposicion()
    {
        Api.ResponderJson(UnaFilaJson);

        var cliente = NuevoCliente();
        await cliente.GetAsync("/GenerarPedido?soloBajoMinimo=true&modoPedido=HastaPuntoPedido");

        var url = Api.UltimaSolicitud.RequestUri!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("soloBajoMinimo=true"));
            Assert.That(url, Does.Contain("modoPedido=HastaPuntoPedido"));
        });
    }

    [Test]
    public async Task Muestra_el_aviso_de_recorte_cuando_la_API_marca_truncado()
    {
        // RF-032a / RF-027c: el usuario tiene que enterarse de que está viendo una lista parcial.
        Api.ResponderJson(TruncadoJson);

        var cliente = NuevoCliente();
        var html = await (await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal")).Content.ReadAsStringAsync();

        Assert.That(html, Does.Contain(MensajesDeConsulta.ResultadoRecortado));
    }

    [Test]
    public async Task No_muestra_el_aviso_de_recorte_cuando_no_hubo_recorte()
    {
        Api.ResponderJson(UnaFilaJson);

        var cliente = NuevoCliente();
        var html = await (await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal")).Content.ReadAsStringAsync();

        Assert.That(html, Does.Not.Contain(MensajesDeConsulta.ResultadoRecortado));
    }

    [Test]
    public async Task Muestra_el_mensaje_de_resultado_vacio_sin_indicador_de_error()
    {
        // RF-032: grilla vacía con mensaje informativo, no un error.
        Api.ResponderJson(VacioJson);

        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=true&modoPedido=HastaStockIdeal");
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain(MensajesDeConsulta.ResultadoVacio));
            Assert.That(html, Does.Not.Contain(MensajesDeConsulta.ResultadoRecortado),
                "Los dos mensajes tienen que ser distinguibles entre sí.");
        });
    }

    [Test]
    public async Task Los_dos_mensajes_no_se_muestran_cuando_hay_filas()
    {
        Api.ResponderJson(UnaFilaJson);

        var cliente = NuevoCliente();
        var html = await (await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain(MensajesDeConsulta.ResultadoVacio));
            Assert.That(html, Does.Contain("A-001"));
        });
    }

    [Test]
    public async Task Retransmite_el_excel_de_la_API_sin_tocarlo()
    {
        // R-05: el archivo lo genera la API; la capa web sólo lo pasa al navegador. Si lo
        // regenerara, RF-031 pasaría a depender de que dos implementaciones coincidan.
        var contenido = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x11, 0x22 };

        Api.Responder(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(contenido)
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
                },
            },
        });

        var cliente = NuevoCliente();
        var respuesta = await cliente.GetAsync(
            "/GenerarPedido/Excel?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        var recibido = await respuesta.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(recibido, Is.EqualTo(contenido));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            Assert.That(Api.UltimaSolicitud.RequestUri!.AbsolutePath, Does.EndWith("/excel"));
        });
    }
}
