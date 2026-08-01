using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T134 — Resolución del Código en la línea de detalle (RF-020e).
///
/// El Código es la identidad de negocio de la línea y el identificador interno no cruza el borde
/// de la API. Eso deja dos comportamientos observables que hay que fijar: qué pasa cuando el
/// Código no existe —404 nombrándolo, sin grabar nada— y con qué regla se lo compara contra el
/// catálogo, que es la misma de RF-017a: insensible a mayúsculas y sensible a acentos.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class MovimientoCodigoTests : MovimientosTestBase
{
    [Test]
    public async Task Un_codigo_inexistente_devuelve_404_problem_json_nombrando_el_codigo_ofensor()
    {
        await SembrarArticuloAsync("A-001");

        var respuesta = await AltaAsync("Compra", LineaDeCodigo("Z-999", 1));
        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));

            // Sin el Código en el cuerpo, quien carga un movimiento de diez líneas no sabe cuál
            // de todas rechazó el sistema.
            Assert.That(problema, Does.Contain("Z-999"),
                "El problema tiene que identificar el Código ofensor.");
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero);
            Assert.That(await CantidadDeLineasAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task Un_codigo_inexistente_en_una_linea_no_graba_ninguna_de_las_otras()
    {
        // RF-024c: el movimiento es todo-o-nada, y la resolución del Código no es la excepción.
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await AltaAsync(
            "Compra", Linea(articulo, 10), LineaDeCodigo("Z-999", 5));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await CantidadDeLineasAsync(), Is.Zero, "Ninguna línea quedó aplicada.");
            Assert.That(await StockDeAsync(articulo), Is.Zero, "El saldo no se movió.");
        });
    }

    [Test]
    public async Task El_codigo_se_resuelve_con_la_regla_insensible_a_mayusculas_de_RF_017a()
    {
        // El usuario puede cargar `a-001` donde el catálogo tiene `A-001`: es el mismo Código,
        // por la misma regla de comparación que sostiene la unicidad (RF-017a).
        var articulo = await SembrarArticuloAsync("A-001");

        var respuesta = await AltaAsync("Compra", LineaDeCodigo("a-001", 7));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(7),
                "Se resolvió al mismo artículo del catálogo.");
        });
    }

    [Test]
    public async Task La_comparacion_del_codigo_es_sensible_a_acentos()
    {
        // La otra mitad de RF-017a: dos códigos que difieren en un acento son artículos distintos,
        // así que `PANO-1` no puede resolver a `PAÑO-1`.
        await SembrarArticuloAsync("PAÑO-1");

        var respuesta = await AltaAsync("Compra", LineaDeCodigo("PANO-1", 1));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await CantidadDeMovimientosAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task La_modificacion_tambien_resuelve_el_codigo_y_rechaza_el_inexistente()
    {
        // La resolución vive en el protocolo de escritura, que comparten alta, baja y
        // modificación. Si sólo estuviera en el alta, modificar sería la puerta de atrás.
        var articulo = await SembrarArticuloAsync("A-001");
        var numero = await AltaExitosaAsync("Compra", Linea(articulo, 10));

        var respuesta = await ModificarAsync(numero, "Compra", LineaDeCodigo("Z-999", 3));

        Assert.Multiple(async () =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await StockDeAsync(articulo), Is.EqualTo(10),
                "El movimiento original quedó intacto.");
        });
    }
}
