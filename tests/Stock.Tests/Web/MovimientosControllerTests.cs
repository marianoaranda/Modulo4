using System.Net;
using Stock.Web.Resources;

namespace Stock.Tests.Web;

/// <summary>
/// T071 — Capa web de movimientos y de la Consulta de Stock Actual.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientosControllerTests : WebTestBase
{
    [Test]
    public async Task El_alta_envia_a_la_API_todas_las_lineas_de_detalle()
    {
        // Un movimiento multilínea cargado desde la pantalla tiene que llegar completo: si el
        // front enviara sólo la primera línea, el todo-o-nada de la API sería irrelevante porque
        // las demás nunca habrían existido.
        Api.ResponderJson("""{"numero":1,"tipo":"Compra","fecha":"2026-01-15","detalle":[]}""",
            HttpStatusCode.Created);

        var cliente = NuevoCliente();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Tipo"] = "Compra",
            ["Fecha"] = "2026-01-15",
            ["Detalle[0].ArticuloId"] = "1",
            ["Detalle[0].Cantidad"] = "10",
            ["Detalle[0].PrecioUnitario"] = "12.50",
            ["Detalle[1].ArticuloId"] = "2",
            ["Detalle[1].Cantidad"] = "5",
            ["Detalle[1].PrecioUnitario"] = "7.25",
        });

        await cliente.PostAsync("/Movimientos/Create", formulario);

        var enviado = await Api.UltimaSolicitud.Content!.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Api.UltimaSolicitud.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(enviado, Does.Contain("\"cantidad\":10"));
            Assert.That(enviado, Does.Contain("\"cantidad\":5"));
        });
    }

    [Test]
    public async Task Un_422_de_la_API_se_propaga_a_la_vista_como_mensaje_para_el_usuario()
    {
        // El rechazo por stock insuficiente es un resultado previsto, no una falla: el usuario
        // tiene que ver el motivo en la pantalla de carga, con sus datos todavía cargados, y no
        // una página de error genérica.
        Api.ResponderProblema(
            HttpStatusCode.UnprocessableEntity, "Stock insuficiente para el artículo A-001.");

        var cliente = NuevoCliente();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Tipo"] = "Venta",
            ["Fecha"] = "2026-01-15",
            ["Detalle[0].ArticuloId"] = "1",
            ["Detalle[0].Cantidad"] = "999",
            ["Detalle[0].PrecioUnitario"] = "10",
        });

        var respuesta = await cliente.PostAsync("/Movimientos/Create", formulario);
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Se vuelve a mostrar el formulario, no se redirige ni se rompe.");
            Assert.That(html, Does.Contain("Stock insuficiente para el artículo A-001."));
            Assert.That(html, Does.Contain("999"), "Los datos cargados no se pierden.");
        });
    }

    [Test]
    public async Task La_consulta_de_stock_actual_envia_rango_y_filtro_a_la_API()
    {
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = NuevoCliente();
        await cliente.GetAsync("/StockActual?codigoDesde=A-001&codigoHasta=A-999&descripcion=valvula");

        var url = Api.UltimaSolicitud.RequestUri!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("codigoDesde=A-001"));
            Assert.That(url, Does.Contain("codigoHasta=A-999"));
            Assert.That(url, Does.Contain("descripcion=valvula"));
        });
    }

    [Test]
    public async Task La_consulta_de_stock_actual_muestra_los_mismos_dos_mensajes_que_generar_pedido()
    {
        // RF-032 y RF-032a valen para AMBAS pantallas, y con el mismo texto: por eso las dos
        // vistas consumen el mismo recurso compartido.
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = NuevoCliente();
        var vacio = await (await cliente.GetAsync("/StockActual?consultar=true")).Content.ReadAsStringAsync();

        Api.ResponderJson("""{"filas":[{"codigo":"A-001","descripcion":"X","cantidad":1}],"truncado":true}""");
        var recortado = await (await cliente.GetAsync("/StockActual?consultar=true")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vacio, Does.Contain(MensajesDeConsulta.ResultadoVacio));
            Assert.That(recortado, Does.Contain(MensajesDeConsulta.ResultadoRecortado));
        });
    }

    [Test]
    public async Task La_consulta_de_stock_actual_retransmite_el_excel()
    {
        var contenido = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x55 };

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
        var respuesta = await cliente.GetAsync("/StockActual/Excel?codigoDesde=A-001");

        Assert.Multiple(async () =>
        {
            Assert.That(await respuesta.Content.ReadAsByteArrayAsync(), Is.EqualTo(contenido));
            Assert.That(Api.UltimaSolicitud.RequestUri!.AbsolutePath, Does.EndWith("/excel"));
        });
    }
}
