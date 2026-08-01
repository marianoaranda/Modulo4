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

    // -------------------------------------------------------------------------------------
    // T148 — RF-020g: la resolución de un Código puntual, que alimenta la sugerencia de precio.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task El_filtro_por_codigo_devuelve_el_articulo_con_sus_dos_precios()
    {
        // La pantalla de carga necesita, para el Código vigente, la Descripción y los dos precios
        // del catálogo. Salen de una única consulta: dos consultas separadas podrían mostrar un
        // artículo y sugerir el precio de otro.
        await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "A-001", descripcion: "Válvula"));
        await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "A-002", descripcion: "Codo"));

        var respuesta = await Client.GetAsync($"{Recurso}?codigo=A-001");
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.That(documento.RootElement.GetArrayLength(), Is.EqualTo(1),
            "Coincidencia exacta: no devuelve también A-002.");

        var articulo = documento.RootElement[0];

        Assert.Multiple(() =>
        {
            Assert.That(articulo.GetProperty("descripcion").GetString(), Is.EqualTo("Válvula"));
            Assert.That(articulo.GetProperty("precioCosto").GetDecimal(), Is.EqualTo(100m));
            Assert.That(articulo.GetProperty("precioVenta").GetDecimal(), Is.EqualTo(150m),
                "El Precio de Venta es el calculado por RF-016, el que se sugiere en una venta.");
        });
    }

    [Test]
    public async Task El_filtro_por_codigo_usa_la_regla_de_comparacion_de_RF_017a()
    {
        await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "A-001"));
        await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "PAÑO-1"));

        var minusculas = await Client.GetAsync($"{Recurso}?codigo=a-001");
        var sinAcento = await Client.GetAsync($"{Recurso}?codigo=PANO-1");

        using var resueltoMinusculas = JsonDocument.Parse(await minusculas.Content.ReadAsStringAsync());
        using var resueltoSinAcento = JsonDocument.Parse(await sinAcento.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(resueltoMinusculas.RootElement.GetArrayLength(), Is.EqualTo(1),
                "Insensible a mayúsculas: `a-001` resuelve `A-001`.");
            Assert.That(resueltoSinAcento.RootElement.GetArrayLength(), Is.Zero,
                "Sensible a acentos: `PANO-1` no resuelve `PAÑO-1`.");
        });
    }

    [Test]
    public async Task Un_codigo_inexistente_devuelve_200_con_arreglo_vacio_y_no_404()
    {
        // Para la pantalla de carga, un Código que no existe significa "no hay sugerencia", que no
        // es un error (RF-020g). El 404 aparece recién al grabar el movimiento (RF-020e).
        var respuesta = await Client.GetAsync($"{Recurso}?codigo=NO-EXISTE");

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(documento.RootElement.GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public async Task El_codigo_y_la_descripcion_se_combinan_sin_contradecirse()
    {
        await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "A-001", descripcion: "Válvula"));

        var coinciden = await Client.GetAsync($"{Recurso}?codigo=A-001&descripcion=álvu");
        var noCoinciden = await Client.GetAsync($"{Recurso}?codigo=A-001&descripcion=zzz");

        using var conCoincidencia = JsonDocument.Parse(await coinciden.Content.ReadAsStringAsync());
        using var sinCoincidencia = JsonDocument.Parse(await noCoinciden.Content.ReadAsStringAsync());

        Assert.Multiple(() =>
        {
            Assert.That(conCoincidencia.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(sinCoincidencia.RootElement.GetArrayLength(), Is.Zero,
                "Los dos filtros se acumulan: no hay uno que gane sobre el otro.");
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
