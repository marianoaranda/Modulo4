using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// T169 — Mensajes de validación de la API en español (RF-035, RF-035a).
///
/// Es el origen del "The Codigo field is required. The Descripcion field is required." que ve el
/// usuario: la API los devuelve en <c>errors</c> y la pantalla de carga los muestra tal cual. Los
/// produce el marco de trabajo —el obligatorio implícito de una propiedad no anulable y el
/// deserializador ante un valor de otro tipo—, así que no aparecen en ninguna cadena del código
/// propio y no alcanza con revisar los mensajes escritos a mano.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ValidacionEnEspanolTests : IntegrationTestBase
{
    /// <summary>Marcadores del texto por omisión del marco de trabajo, en inglés.</summary>
    private static readonly string[] Marcadores =
        ["is required", "must be a number", "The value", "The field", "System."];

    [Test]
    public async Task Un_campo_obligatorio_en_nulo_se_informa_en_espanol_con_el_rotulo_de_negocio()
    {
        // Éste es el camino del marco de trabajo, el que producía el "The Codigo field is
        // required." que veía el usuario: con el valor en nulo, el obligatorio implícito de la
        // propiedad no anulable rechaza el cuerpo **antes** de que la acción corra, así que el
        // validador de dominio nunca llega a opinar.
        var respuesta = await Client.PostAsJsonAsync("/api/articulos", new
        {
            codigo = (string?)null,
            descripcion = (string?)null,
            precioCosto = 100m,
            margen = 50m,
            stockMinimo = 0,
            puntoPedido = 0,
            stockIdeal = 0,
        });

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problema, Does.Contain("El campo Código es obligatorio."));
            Assert.That(problema, Does.Contain("El campo Descripción es obligatorio."));
        });
    }

    [Test]
    public async Task Un_campo_obligatorio_vacio_lo_rechaza_el_dominio_con_su_propio_mensaje()
    {
        // La otra mitad, y la que explica por qué el defecto era difícil de ver: con la cadena
        // vacía el cuerpo es válido para el marco de trabajo, corre la acción y contesta el
        // validador de dominio, que **ya estaba en español**. Según por dónde entrara el rechazo,
        // el mismo campo vacío se informaba en un idioma o en el otro.
        var respuesta = await Client.PostAsJsonAsync("/api/articulos", new
        {
            codigo = "",
            descripcion = "",
            precioCosto = 100m,
            margen = 50m,
            stockMinimo = 0,
            puntoPedido = 0,
            stockIdeal = 0,
        });

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problema, Does.Contain("El Código es obligatorio."));
            Assert.That(SinMarcadoresEnIngles(problema), Is.True);
        });
    }

    [Test]
    public async Task Una_cantidad_no_entera_se_informa_en_espanol_sin_nombres_de_tipos()
    {
        // RF-035a: lo produce el deserializador, antes de cualquier regla de negocio (RF-018a). Su
        // texto por omisión nombra tipos de la plataforma, que no significan nada para quien está
        // cargando un movimiento.
        var cuerpo = """
            {"tipo":"Compra","fecha":"2026-01-15",
             "detalle":[{"codigo":"A-001","cantidad":1.5,"precioUnitario":10.00}]}
            """;

        var respuesta = await Client.PostAsync(
            "/api/movimientos", new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(
                respuesta.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"),
                "La traducción no cambia la forma del contrato.");
            Assert.That(problema, Does.Contain("cantidad").IgnoreCase,
                "Sigue identificando el campo ofensor (RF-018a).");
            Assert.That(SinMarcadoresEnIngles(problema), Is.True,
                $"El problema quedó con texto en inglés: {problema}");

            // El cuerpo que no se pudo deserializar deja además al parámetro de la acción en nulo,
            // y el marco de trabajo se queja de él por su nombre interno. Esa segunda queja no le
            // dice nada a quien carga el movimiento y no debe salir (RF-035).
            Assert.That(problema, Does.Not.Contain("solicitud"),
                "El mensaje no nombra el parámetro interno de la acción.");
        });
    }

    [Test]
    public async Task Una_fecha_con_formato_invalido_tambien_se_informa_en_espanol()
    {
        var cuerpo = """
            {"tipo":"Compra","fecha":"no-es-una-fecha",
             "detalle":[{"codigo":"A-001","cantidad":1,"precioUnitario":10.00}]}
            """;

        var respuesta = await Client.PostAsync(
            "/api/movimientos", new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        var problema = await respuesta.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(SinMarcadoresEnIngles(problema), Is.True,
                $"El problema quedó con texto en inglés: {problema}");
        });
    }

    [Test]
    public async Task Los_mensajes_de_regla_de_negocio_no_se_tocan()
    {
        // Ya estaban en español y son más específicos que los genéricos de RF-035: traducirlos de
        // nuevo sólo podría empeorarlos.
        var articulo = await SembrarArticuloAsync();

        var respuesta = await Client.PostAsJsonAsync("/api/movimientos", new
        {
            tipo = "Compra",
            fecha = "2026-01-15",
            detalle = new[] { new { codigo = articulo, cantidad = 0, precioUnitario = 10m } },
        });

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        var mensajes = documento.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(c => c.Value.EnumerateArray())
            .Select(m => m.GetString()!)
            .ToList();

        Assert.That(mensajes, Has.Some.Contains("La Cantidad debe ser un número entero mayor que 0."));
    }

    private static bool SinMarcadoresEnIngles(string texto) =>
        !Marcadores.Any(m => texto.Contains(m, StringComparison.OrdinalIgnoreCase));

    private async Task<string> SembrarArticuloAsync()
    {
        await EjecutarSqlAsync("""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('A-001', N'Artículo de prueba', 100.00, 50, 0, 0, 0);
            """);

        return "A-001";
    }
}
