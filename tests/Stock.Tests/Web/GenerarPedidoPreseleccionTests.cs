using System.Text.RegularExpressions;

namespace Stock.Tests.Web;

/// <summary>
/// T158/T158a — Preselección de los parámetros de "Generar Pedido" (RF-026c).
///
/// La preselección vive **en la pantalla**, no en el servidor. La distinción no es cosmética: si
/// se implementara como un valor por defecto de la API, RF-026b quedaría roto y el usuario podría
/// recibir una lista de pedido que nunca eligió, indistinguible de la que sí. Por eso acá se
/// verifican las dos mitades: que los valores aparezcan elegidos en el formulario, y que **abrir
/// la pantalla no consulte nada**.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class GenerarPedidoPreseleccionTests : WebTestBase
{
    [Test]
    public async Task Al_abrir_la_pantalla_los_dos_parametros_vienen_sugeridos()
    {
        var html = await PantallaAsync("/GenerarPedido");

        Assert.Multiple(() =>
        {
            Assert.That(OpcionElegida(html, "soloBajoMinimo"), Is.EqualTo("false"),
                "\"Solo bajo mínimo\" viene en No.");
            Assert.That(OpcionElegida(html, "modoPedido"), Is.EqualTo("HastaStockIdeal"),
                "…y el Modo de Pedido en Hasta Stock Ideal.");
        });
    }

    [Test]
    public async Task Abrir_la_pantalla_no_ejecuta_la_consulta()
    {
        // Lo que separa "sugerir" de "decidir por el usuario". Además, si abrir consultara, la
        // pantalla mostraría el mensaje de resultado vacío antes de que nadie pidiera nada.
        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/GenerarPedido")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Api.Recibidas, Is.Empty, "No hubo ninguna llamada a la API.");
            Assert.That(html, Does.Not.Contain(Stock.Web.Resources.MensajesDeConsulta.ResultadoVacio));
        });
    }

    [Test]
    public async Task Lo_que_elige_el_usuario_gana_sobre_lo_sugerido()
    {
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync(
            "/GenerarPedido?soloBajoMinimo=true&modoPedido=HastaStockMinimo"))
            .Content.ReadAsStringAsync();

        var url = Api.UltimaSolicitud.RequestUri!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(OpcionElegida(html, "soloBajoMinimo"), Is.EqualTo("true"));
            Assert.That(OpcionElegida(html, "modoPedido"), Is.EqualTo("HastaStockMinimo"));
            Assert.That(url, Does.Contain("soloBajoMinimo=true"));
            Assert.That(url, Does.Contain("modoPedido=HastaStockMinimo"));
        });
    }

    [Test]
    public async Task Una_solicitud_con_un_solo_parametro_no_se_completa_con_el_sugerido()
    {
        // T158a — la protección de RF-026b del lado de la pantalla. Si el parámetro que falta se
        // rellenara con el sugerido, la web estaría inventando el valor por defecto que el
        // requisito prohíbe, y el rechazo de la API (verificado en GenerarPedidoContractTests)
        // nunca llegaría a ocurrir porque la solicitud saldría completa.
        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/GenerarPedido?soloBajoMinimo=true"))
            .Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Api.Recibidas, Is.Empty,
                "Con un parámetro de reposición ausente no se consulta.");
            Assert.That(OpcionElegida(html, "soloBajoMinimo"), Is.EqualTo("true"),
                "Se conserva lo que el usuario sí eligió.");
        });
    }

    private async Task<string> PantallaAsync(string ruta)
    {
        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync(ruta)).Content.ReadAsStringAsync();
    }

    /// <summary>El `value` de la opción marcada como `selected` dentro de ese `select`.</summary>
    private static string OpcionElegida(string html, string idDelSelect)
    {
        var select = Regex.Match(
            html,
            $@"<select[^>]*\bid=""{Regex.Escape(idDelSelect)}""[^>]*>(.*?)</select>",
            RegexOptions.Singleline);

        Assert.That(select.Success, Is.True, $"La pantalla tiene el select {idDelSelect}.");

        var elegida = Regex.Match(
            select.Groups[1].Value, @"<option[^>]*\bvalue=""([^""]*)""[^>]*\bselected\b");

        Assert.That(elegida.Success, Is.True, $"…con una opción elegida en {idDelSelect}.");

        return elegida.Groups[1].Value;
    }
}
