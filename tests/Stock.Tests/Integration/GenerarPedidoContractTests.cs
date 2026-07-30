using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T045 — Contrato de <c>GET /api/consultas/generar-pedido</c>.
///
/// Verifica lo que el contrato <b>niega</b> tanto como lo que ofrece: los dos parámetros de
/// reposición son obligatorios y sin valor por defecto (RF-026b), y no existe parámetro de rango
/// (RF-026a).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class GenerarPedidoContractTests : IntegrationTestBase
{
    [Test]
    public async Task Omitir_soloBajoMinimo_devuelve_400_sin_aplicar_un_valor_por_defecto()
    {
        // RF-026b: un valor por defecto silencioso produciría una lista de pedido que el usuario
        // no pidió y no puede distinguir de la que sí.
        var respuesta = await Client.GetAsync(
            "/api/consultas/generar-pedido?modoPedido=HastaStockMinimo");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Omitir_modoPedido_devuelve_400()
    {
        var respuesta = await Client.GetAsync(
            "/api/consultas/generar-pedido?soloBajoMinimo=false");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Omitir_ambos_parametros_devuelve_400()
    {
        var respuesta = await Client.GetAsync("/api/consultas/generar-pedido");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Un_modoPedido_invalido_devuelve_400()
    {
        var respuesta = await Client.GetAsync(
            "/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaLaLuna");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task El_error_de_validacion_viaja_como_problem_json()
    {
        var respuesta = await Client.GetAsync("/api/consultas/generar-pedido");

        Assert.That(
            respuesta.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task Con_ambos_parametros_validos_responde_200()
    {
        var respuesta = await Client.GetAsync(
            "/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task El_endpoint_no_acepta_parametros_de_rango()
    {
        // RF-026a: Generar Pedido no tiene rango de artículos. Si un rango se colara, acotaría el
        // resultado en silencio y CE-001 dejaría de cumplirse: el usuario recibiría una lista de
        // pedido incompleta creyéndola completa.
        //
        // Se siembran dos artículos y se envían parámetros de rango que, de ser respetados,
        // dejarían fuera al segundo. La consulta debe devolver los dos igual.
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("Z-999");

        var respuesta = await Client.GetAsync(
            "/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal" +
            "&codigoDesde=A-001&codigoHasta=A-001");

        respuesta.EnsureSuccessStatusCode();
        var resultado = await Json.LeerAsync<ResultadoGenerarPedido>(respuesta);

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EquivalentTo(new[] { "A-001", "Z-999" }));
    }

    private async Task SembrarArticuloAsync(string codigo) =>
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('{codigo}', 'Artículo {codigo}', 10.00, 0, 0, 0, 0);
            """);
}
