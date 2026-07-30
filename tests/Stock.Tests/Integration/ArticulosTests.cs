using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T083/T084 — ABM de artículos contra la base real (RF-014a, RF-016, RF-017, RF-017a, RF-033).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ArticulosTests : IntegrationTestBase
{
    private const string Recurso = "/api/articulos";

    private static object Articulo(
        string codigo = "A-001",
        string descripcion = "Artículo de prueba",
        decimal precioCosto = 100m,
        decimal margen = 50m,
        int stockMinimo = 10,
        int puntoPedido = 20,
        int stockIdeal = 50) => new
        {
            codigo,
            descripcion,
            precioCosto,
            margen,
            stockMinimo,
            puntoPedido,
            stockIdeal,
        };

    private async Task<int> AltaExitosaAsync(object articulo)
    {
        var respuesta = await Client.PostAsJsonAsync(Recurso, articulo);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.GetProperty("articuloId").GetInt32();
    }

    // -------------------------------------------------------------------------------------
    // RF-016 — Precio de Venta calculado.
    // -------------------------------------------------------------------------------------

    [TestCase(100, 50, 150.00)]
    [TestCase(100, 0, 100.00)]
    [TestCase(33.33, 10, 36.66)]
    [TestCase(0, 25, 0.00)]
    public async Task El_precio_de_venta_lo_calcula_el_sistema(
        decimal costo, decimal margen, decimal esperado)
    {
        var respuesta = await Client.PostAsJsonAsync(
            Recurso, Articulo(precioCosto: costo, margen: margen));

        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.That(
            documento.RootElement.GetProperty("precioVenta").GetDecimal(),
            Is.EqualTo(esperado));
    }

    [Test]
    public async Task El_precio_de_venta_enviado_por_el_cliente_se_ignora()
    {
        // Es columna calculada: si el cuerpo pudiera fijarla, existiría un camino por el que el
        // precio quedara desalineado de costo y margen, que es justo lo que RF-016 impide.
        var respuesta = await Client.PostAsJsonAsync(Recurso, new
        {
            codigo = "A-001",
            descripcion = "Artículo de prueba",
            precioCosto = 100m,
            margen = 50m,
            precioVenta = 9_999m,
            stockMinimo = 0,
            puntoPedido = 0,
            stockIdeal = 0,
        });

        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.That(documento.RootElement.GetProperty("precioVenta").GetDecimal(), Is.EqualTo(150.00m));
    }

    [Test]
    public async Task El_precio_de_venta_se_recalcula_al_modificar_costo_o_margen()
    {
        var id = await AltaExitosaAsync(Articulo(precioCosto: 100m, margen: 50m));

        await Client.PutAsJsonAsync($"{Recurso}/{id}", Articulo(precioCosto: 200m, margen: 10m));

        var precioVenta = await EscalarAsync<decimal>(
            $"SELECT PrecioVenta FROM dbo.Articulo WHERE ArticuloId = {id}");

        Assert.That(precioVenta, Is.EqualTo(220.00m));
    }

    // -------------------------------------------------------------------------------------
    // RF-017 / RF-017a — Código único, insensible a mayúsculas.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_codigo_duplicado_se_rechaza_con_409()
    {
        await AltaExitosaAsync(Articulo(codigo: "A-001"));

        var respuesta = await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "A-001"));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Articulo"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Un_codigo_que_difiere_solo_en_mayusculas_es_duplicado()
    {
        // RF-017a: la unicidad usa la misma regla de comparación que el ordenamiento.
        await AltaExitosaAsync(Articulo(codigo: "A-001"));

        var respuesta = await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "a-001"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Dos_codigos_que_difieren_en_un_acento_son_distintos()
    {
        // La otra mitad de RF-017a: sensible a acentos.
        await AltaExitosaAsync(Articulo(codigo: "PANO-1"));

        var respuesta = await Client.PostAsJsonAsync(Recurso, Articulo(codigo: "PAÑO-1"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Modificar_hacia_un_codigo_ya_usado_se_rechaza_con_409()
    {
        await AltaExitosaAsync(Articulo(codigo: "A-001"));
        var segundo = await AltaExitosaAsync(Articulo(codigo: "A-002"));

        var respuesta = await Client.PutAsJsonAsync($"{Recurso}/{segundo}", Articulo(codigo: "A-001"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Modificar_un_articulo_conservando_su_propio_codigo_se_acepta()
    {
        // El chequeo de duplicados tiene que excluir al propio registro; si no, ninguna
        // modificación que no cambie el Código sería posible.
        var id = await AltaExitosaAsync(Articulo(codigo: "A-001"));

        var respuesta = await Client.PutAsJsonAsync(
            $"{Recurso}/{id}", Articulo(codigo: "A-001", descripcion: "Descripción nueva"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    // -------------------------------------------------------------------------------------
    // RF-014a — Baja restringida.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task La_baja_de_un_articulo_con_movimientos_se_rechaza_con_409()
    {
        var id = await AltaExitosaAsync(Articulo(codigo: "A-001"));

        await Client.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { articuloId = id, cantidad = 10, precioUnitario = 5m } },
        });

        var respuesta = await Client.DeleteAsync($"{Recurso}/{id}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
                "409 legible, no una violación de FK convertida en 500.");

            Assert.That(await EscalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.Articulo WHERE ArticuloId = {id}"), Is.EqualTo(1),
                "El artículo sigue existiendo.");

            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.MovimientoDetalle"),
                Is.EqualTo(1), "Su histórico queda intacto.");
        });
    }

    [Test]
    public async Task La_baja_de_un_articulo_sin_movimientos_se_acepta()
    {
        var id = await AltaExitosaAsync(Articulo(codigo: "A-001"));

        var respuesta = await Client.DeleteAsync($"{Recurso}/{id}");

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Articulo"), Is.Zero);
        });
    }

    // -------------------------------------------------------------------------------------
    // T084 — RF-033: los parámetros vigentes se reflejan en la siguiente ejecución.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Modificar_los_parametros_de_reposicion_cambia_el_resultado_de_generar_pedido()
    {
        var id = await AltaExitosaAsync(
            Articulo(codigo: "A-001", stockMinimo: 5, puntoPedido: 5, stockIdeal: 5));

        var antes = await Json.LeerAsync<ResultadoGenerarPedido>(
            await Client.GetAsync("/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal"));

        await Client.PutAsJsonAsync($"{Recurso}/{id}",
            Articulo(codigo: "A-001", stockMinimo: 40, puntoPedido: 40, stockIdeal: 40));

        var despues = await Json.LeerAsync<ResultadoGenerarPedido>(
            await Client.GetAsync("/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal"));

        Assert.Multiple(() =>
        {
            Assert.That(antes.Filas.Single().CantidadAPedir, Is.EqualTo(5));
            Assert.That(despues.Filas.Single().CantidadAPedir, Is.EqualTo(40),
                "La consulta no conserva resultados previos: se recalcula contra el estado vigente.");
        });
    }
}
