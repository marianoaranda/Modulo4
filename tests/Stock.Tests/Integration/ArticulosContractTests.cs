using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T085/T085a — Contrato del CRUD de <c>/api/articulos</c> (RF-013, RF-014, RF-015, RF-018a).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ArticulosContractTests : IntegrationTestBase
{
    private const string Recurso = "/api/articulos";

    private static object Articulo(string codigo = "A-001", string descripcion = "Artículo de prueba") =>
        new
        {
            codigo,
            descripcion,
            precioCosto = 100m,
            margen = 50m,
            stockMinimo = 10,
            puntoPedido = 20,
            stockIdeal = 50,
        };

    [Test]
    public async Task Recorrido_completo_alta_lectura_modificacion_baja()
    {
        // Alta.
        var alta = await Client.PostAsJsonAsync(Recurso, Articulo());
        Assert.That(alta.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var creado = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var id = creado.RootElement.GetProperty("articuloId").GetInt32();

        Assert.That(id, Is.GreaterThan(0));

        // Lectura.
        var lectura = await Client.GetAsync($"{Recurso}/{id}");
        Assert.That(lectura.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var leido = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(leido.RootElement.GetProperty("codigo").GetString(), Is.EqualTo("A-001"));
            Assert.That(leido.RootElement.GetProperty("precioVenta").GetDecimal(), Is.EqualTo(150.00m));
        });

        // Modificación (RF-015).
        var modificacion = await Client.PutAsJsonAsync(
            $"{Recurso}/{id}", Articulo(descripcion: "Descripción modificada"));
        Assert.That(modificacion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Baja (RF-014).
        var baja = await Client.DeleteAsync($"{Recurso}/{id}");
        Assert.Multiple(async () =>
        {
            Assert.That(baja.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await Client.GetAsync($"{Recurso}/{id}")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task El_listado_admite_filtro_opcional_por_descripcion()
    {
        await Client.PostAsJsonAsync(Recurso, Articulo("V-001", "Válvula de bronce"));
        await Client.PostAsJsonAsync(Recurso, Articulo("O-001", "Otra cosa"));

        var respuesta = await Client.GetAsync($"{Recurso}?descripcion=valvula");
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(documento.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(documento.RootElement[0].GetProperty("codigo").GetString(), Is.EqualTo("V-001"));
        });
    }

    [Test]
    public async Task Leer_modificar_o_dar_de_baja_un_articulo_inexistente_devuelve_404()
    {
        Assert.Multiple(async () =>
        {
            Assert.That((await Client.GetAsync($"{Recurso}/999999")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await Client.PutAsJsonAsync($"{Recurso}/999999", Articulo())).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await Client.DeleteAsync($"{Recurso}/999999")).StatusCode,
                Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Un_valor_negativo_devuelve_400_sin_grabar()
    {
        var respuesta = await Client.PostAsJsonAsync(Recurso, new
        {
            codigo = "A-001",
            descripcion = "Artículo de prueba",
            precioCosto = -1m,
            margen = 50m,
            stockMinimo = 10,
            puntoPedido = 20,
            stockIdeal = 50,
        });

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Articulo"), Is.Zero);
        });
    }

    [Test]
    public async Task El_incumplimiento_del_orden_de_stocks_devuelve_400()
    {
        var respuesta = await Client.PostAsJsonAsync(Recurso, new
        {
            codigo = "A-001",
            descripcion = "Artículo de prueba",
            precioCosto = 100m,
            margen = 50m,
            stockMinimo = 30,
            puntoPedido = 20,
            stockIdeal = 50,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // -------------------------------------------------------------------------------------
    // T085a — RF-018a: el no entero se rechaza en el borde.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_stockMinimo_no_entero_devuelve_400_problem_json_identificando_el_campo()
    {
        var cuerpo = """
            {"codigo":"A-001","descripcion":"Artículo de prueba","precioCosto":100.00,
             "margen":50.00,"stockMinimo":2.5,"puntoPedido":20,"stockIdeal":50}
            """;

        var respuesta = await Client.PostAsync(
            Recurso, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
            Assert.That(problema, Does.Contain("stockMinimo").IgnoreCase,
                "El problema tiene que identificar el campo ofensor.");
            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Articulo"), Is.Zero);
        });
    }

    [TestCase("puntoPedido")]
    [TestCase("stockIdeal")]
    public async Task Cualquiera_de_los_tres_parametros_no_entero_devuelve_400(string campo)
    {
        var valores = new Dictionary<string, string>
        {
            ["stockMinimo"] = "10",
            ["puntoPedido"] = "20",
            ["stockIdeal"] = "50",
        };

        valores[campo] = "7.5";

        var cuerpo = $$"""
            {"codigo":"A-001","descripcion":"Artículo de prueba","precioCosto":100.00,"margen":50.00,
             "stockMinimo":{{valores["stockMinimo"]}},
             "puntoPedido":{{valores["puntoPedido"]}},
             "stockIdeal":{{valores["stockIdeal"]}}}
            """;

        var respuesta = await Client.PostAsync(
            Recurso, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Un_precio_de_costo_decimal_si_se_acepta()
    {
        // RF-018a alcanza sólo a los campos enteros. El Precio de Costo es decimal por diseño:
        // rechazar "33.33" acá sería un falso positivo del mismo requisito.
        var cuerpo = """
            {"codigo":"A-001","descripcion":"Artículo de prueba","precioCosto":33.33,
             "margen":10.50,"stockMinimo":0,"puntoPedido":0,"stockIdeal":0}
            """;

        var respuesta = await Client.PostAsync(
            Recurso, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }
}
