using System.Net;
using System.Text.Json;

namespace Stock.Tests.Web;

/// <summary>
/// T160a — Qué viaja a la API cuando el usuario deja líneas sin completar (RF-020j).
///
/// Con el botón "Agregar Línea", una línea de más es lo normal: se agrega y se deja en blanco. Eso
/// no puede invalidar el Movimiento ni desplazar a las demás. Lo que sí tiene que viajar es la
/// línea que el usuario **empezó** a completar, para que la API la rechace por RF-023 en vez de
/// que la pantalla la descarte por su cuenta y grabe algo distinto de lo que se ve.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoLineasEnBlancoTests : WebTestBase
{
    [Test]
    public async Task Una_linea_del_medio_en_blanco_no_se_envia_y_las_otras_llegan_completas()
    {
        Api.ResponderJson("""{"numero":1,"tipo":"Compra","fecha":"2026-01-15","detalle":[]}""",
            HttpStatusCode.Created);

        var cliente = ClienteConSesion();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Tipo"] = "Compra",
            ["Fecha"] = "2026-01-15",
            ["Detalle[0].Codigo"] = "A-001",
            ["Detalle[0].Cantidad"] = "10",
            ["Detalle[0].PrecioUnitario"] = "12.50",
            ["Detalle[1].Codigo"] = "",
            ["Detalle[1].Cantidad"] = "0",
            ["Detalle[1].PrecioUnitario"] = "0",
            ["Detalle[2].Codigo"] = "A-003",
            ["Detalle[2].Cantidad"] = "5",
            ["Detalle[2].PrecioUnitario"] = "7.25",
        });

        await cliente.PostAsync("/Movimientos/Create", formulario);

        using var enviado = JsonDocument.Parse(
            await Api.UltimaSolicitud.Content!.ReadAsStringAsync());

        var detalle = enviado.RootElement.GetProperty("detalle");

        Assert.Multiple(() =>
        {
            Assert.That(detalle.GetArrayLength(), Is.EqualTo(2), "La línea vacía no viaja.");
            Assert.That(detalle[0].GetProperty("codigo").GetString(), Is.EqualTo("A-001"));
            Assert.That(detalle[1].GetProperty("codigo").GetString(), Is.EqualTo("A-003"),
                "La tercera línea llega entera y no se pierde por el hueco del medio.");
            Assert.That(detalle[1].GetProperty("cantidad").GetInt32(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Una_linea_con_codigo_y_cantidad_cero_si_viaja_para_que_la_API_la_rechace()
    {
        // La diferencia con la anterior es haberla empezado a completar. Descartarla acá sería
        // grabar en silencio un movimiento distinto del que el usuario tiene en pantalla; el
        // rechazo de RF-023 vive en la API y tiene que poder llegar.
        Api.ResponderProblema(HttpStatusCode.BadRequest, "La Cantidad debe ser mayor que 0.");

        var cliente = ClienteConSesion();

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Tipo"] = "Compra",
            ["Fecha"] = "2026-01-15",
            ["Detalle[0].Codigo"] = "A-001",
            ["Detalle[0].Cantidad"] = "0",
            ["Detalle[0].PrecioUnitario"] = "10",
        });

        var respuesta = await cliente.PostAsync("/Movimientos/Create", formulario);
        var html = await respuesta.Content.ReadAsStringAsync();

        using var enviado = JsonDocument.Parse(
            await Api.UltimaSolicitud.Content!.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(enviado.RootElement.GetProperty("detalle").GetArrayLength(), Is.EqualTo(1));
            Assert.That(html, Does.Contain("La Cantidad debe ser mayor que 0."),
                "El rechazo de la API vuelve a la pantalla de carga.");
        });
    }
}
