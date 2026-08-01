using System.Text.RegularExpressions;
using Stock.Web.Resources;

namespace Stock.Tests.Web;

/// <summary>
/// T136/T137 — La ventana de búsqueda de artículos y su uso desde las pantallas
/// (RF-034, RF-034a, RF-034b, RF-034c).
///
/// La lógica de cliente se verifica por su <b>contrato renderizado</b>, según fija la
/// reevaluación post-Fase 1 del plan: no se introduce un runner de JavaScript. Lo que el spec
/// exige verificar es que la pantalla quede cableada —el botón con la lupa junto a cada campo de
/// Código, el diálogo con sus dos columnas y su altura acotada, la Descripción sincronizada por
/// una sola ruta— y todo eso es asertable sobre el HTML que la aplicación emite.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class BuscadorArticulosTests : WebTestBase
{
    /// <summary>Delimitadores del bloque del buscador, que hacen comparable su marcado.</summary>
    private const string Inicio = "buscador-articulos:inicio";

    private const string Fin = "buscador-articulos:fin";

    // -------------------------------------------------------------------------------------
    // T136 — RF-034 y RF-034a: la ventana y el botón que la abre.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task La_ventana_pide_una_descripcion_y_un_boton_buscar()
    {
        var html = await PantallaDeCargaAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("buscadorArticulosDescripcion"),
                "La ventana pide un campo Descripción.");
            Assert.That(html, Does.Contain("buscadorArticulosBuscar"),
                "…y tiene su botón Buscar.");
        });
    }

    [Test]
    public async Task La_grilla_de_la_ventana_trae_las_dos_columnas_codigo_y_descripcion()
    {
        var dialogo = BloqueDelBuscador(await PantallaDeCargaAsync());

        // `<th(?:\s…)?>` y no `<th[^>]*>`: lo segundo también matchea la apertura de `<thead>`.
        var encabezados = Regex.Matches(dialogo, @"<th(?:\s[^>]*)?>(.*?)</th>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        Assert.That(encabezados, Is.EqualTo(new[] { "Código", "Descripción" }));
    }

    [Test]
    public async Task La_ventana_no_supera_los_600_pixeles_de_alto()
    {
        // RF-034a lo fija al píxel, así que el test lee el número y no la mera presencia del
        // estilo: un `max-height: 900px` pasaría cualquier aserción por subcadena.
        var dialogo = BloqueDelBuscador(await PantallaDeCargaAsync());

        var alturas = Regex.Matches(dialogo, @"max-height:\s*(\d+)px")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(alturas, Is.Not.Empty, "La ventana declara una altura máxima.");
            Assert.That(alturas, Has.All.LessThanOrEqualTo(600));
        });
    }

    [Test]
    public async Task La_ventana_declara_el_aviso_de_recorte_con_el_texto_exacto_de_RF_032a()
    {
        // Una Descripción vacía no libera del tope de 10.000 (RF-034a). El aviso es el mismo que
        // el de las consultas y sale del recurso compartido: dos copias divergirían.
        var dialogo = BloqueDelBuscador(await PantallaDeCargaAsync());

        Assert.That(dialogo, Does.Contain(MensajesDeConsulta.ResultadoRecortado));
    }

    [Test]
    public async Task Cada_campo_de_codigo_tiene_al_lado_un_boton_identificado_solo_con_una_lupa()
    {
        var html = await PantallaDeCargaAsync();

        var boton = Regex.Match(
            html, "<button[^>]*data-buscador-destino[^>]*>(.*?)</button>", RegexOptions.Singleline);

        Assert.Multiple(() =>
        {
            Assert.That(boton.Success, Is.True, "El campo de Código tiene su botón de búsqueda.");
            Assert.That(boton.Value, Does.Contain("icono-lupa"), "…con el ícono de lupa…");
            Assert.That(
                Regex.Replace(boton.Groups[1].Value, "<[^>]*>", string.Empty).Trim(),
                Is.Empty,
                "…y sin ningún texto: el botón se identifica sólo con el ícono (RF-034).");
        });
    }

    // -------------------------------------------------------------------------------------
    // T137 — RF-034b y RF-034c: equivalencia con la carga manual y componente único.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Las_dos_pantallas_con_campo_de_codigo_incluyen_el_mismo_buscador()
    {
        // RF-034c. Comparar el bloque renderizado y no sólo su presencia es lo que detecta la
        // copia: dos diálogos duplicados empiezan idénticos y divergen en el primer arreglo que
        // alguien haga sobre uno solo.
        var carga = BloqueDelBuscador(await PantallaDeCargaAsync());
        var consulta = BloqueDelBuscador(await PantallaDeStockActualAsync());

        Assert.That(consulta, Is.EqualTo(carga),
            "Las dos pantallas rinden exactamente el mismo diálogo.");
    }

    [Test]
    public async Task El_dialogo_aparece_una_sola_vez_por_pantalla()
    {
        foreach (var html in new[] { await PantallaDeCargaAsync(), await PantallaDeStockActualAsync() })
        {
            Assert.That(Regex.Matches(html, Inicio), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Cada_pantalla_solo_declara_cual_es_su_campo_de_destino()
    {
        // La pantalla no repite el marcado ni el script: sólo dice a qué campo va el Código
        // elegido y dónde mostrar su Descripción.
        var consulta = await PantallaDeStockActualAsync();

        var destinos = Regex.Matches(consulta, @"data-buscador-destino=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(destinos, Is.EqualTo(new[] { "codigoDesde", "codigoHasta" }),
                "Los dos extremos del rango tienen su botón, cada uno apuntando a su campo.");
            // El `src` lleva la huella de versión que agrega la vista, así que se cuenta el
            // archivo y no la cadena exacta.
            Assert.That(Regex.Matches(consulta, @"src=""[^""]*buscador-articulos\.js[^""]*"""),
                Has.Count.EqualTo(1),
                "El script se referencia una sola vez, desde la partial compartida.");
        });
    }

    [Test]
    public async Task Cada_campo_de_codigo_declara_donde_mostrar_su_descripcion()
    {
        // RF-034b: la pantalla muestra la Descripción del Código vigente, y la mantiene
        // sincronizada tanto al elegir desde la búsqueda como al editar a mano. El marcado lo hace
        // posible por una sola vía: el destino del buscador y el campo que el usuario tipea son
        // el mismo elemento, y de él cuelga el lugar donde va la Descripción.
        foreach (var html in new[] { await PantallaDeCargaAsync(), await PantallaDeStockActualAsync() })
        {
            var destinos = Regex.Matches(html, @"data-buscador-destino=""([^""]+)""")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.That(destinos, Is.Not.Empty);

            foreach (var destino in destinos)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(EtiquetaConId(html, destino), Does.Contain("data-articulo-codigo"),
                        $"El campo {destino} es el que el script observa cuando se tipea a mano.");
                    Assert.That(html, Does.Contain($@"data-descripcion-de=""{destino}"""),
                        $"…y tiene dónde mostrar la Descripción del Código vigente.");
                });
            }
        }
    }

    private async Task<string> PantallaDeCargaAsync()
    {
        Api.ResponderJson("""{"numero":8}""");

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync("/Movimientos/Create")).Content.ReadAsStringAsync();
    }

    private async Task<string> PantallaDeStockActualAsync()
    {
        Api.ResponderJson("""{"filas":[],"truncado":false}""");

        var cliente = ClienteConSesion();

        return await (await cliente.GetAsync("/StockActual")).Content.ReadAsStringAsync();
    }

    /// <summary>
    /// La etiqueta que lleva ese <c>id</c>, para poder asertar sus atributos sin depender del
    /// orden en que la tag helper los emite —que no es el del código fuente de la vista—.
    /// </summary>
    internal static string EtiquetaConId(string html, string id)
    {
        var etiqueta = Regex.Match(html, $@"<[a-z]+[^>]*\bid=""{Regex.Escape(id)}""[^>]*>");

        Assert.That(etiqueta.Success, Is.True, $"La pantalla tiene un elemento con id {id}.");

        return etiqueta.Value;
    }

    private static string BloqueDelBuscador(string html)
    {
        var desde = html.IndexOf(Inicio, StringComparison.Ordinal);
        var hasta = html.IndexOf(Fin, StringComparison.Ordinal);

        Assert.That(desde, Is.GreaterThanOrEqualTo(0), "La pantalla incluye el buscador.");
        Assert.That(hasta, Is.GreaterThan(desde), "…con su marca de cierre.");

        return html[desde..hasta];
    }
}
