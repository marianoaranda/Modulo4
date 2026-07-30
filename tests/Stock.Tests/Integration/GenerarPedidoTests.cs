using System.Net;

namespace Stock.Tests.Integration;

/// <summary>
/// T046, T047, T048, T048a y T048b — Generar Pedido de punta a punta contra la base real.
///
/// Complementa a <c>PedidoCalculatorTests</c>: allá se verifica la fórmula como función pura, acá
/// que el saldo que la alimenta sale de <c>vw_StockActual</c> y que el pipeline
/// filtrar → ordenar → recortar → marcar se aplica en ese orden exacto.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class GenerarPedidoTests : IntegrationTestBase
{
    private const string Base = "/api/consultas/generar-pedido";

    private async Task<int> SembrarArticuloAsync(
        string codigo, int stockMinimo, int puntoPedido, int stockIdeal,
        string descripcion = "Artículo de prueba")
    {
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('{codigo}', N'{descripcion}', 10.00, 0, {stockMinimo}, {puntoPedido}, {stockIdeal});
            """);

        return await EscalarAsync<int>($"SELECT ArticuloId FROM dbo.Articulo WHERE Codigo = '{codigo}'");
    }

    private async Task SembrarMovimientoAsync(int tipo, int articuloId, int cantidad)
    {
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Movimiento (Tipo, Fecha) VALUES ({tipo}, '2026-01-15');
            DECLARE @n int = SCOPE_IDENTITY();
            INSERT INTO dbo.MovimientoDetalle (MovimientoNumero, ArticuloId, Cantidad, PrecioUnitario)
            VALUES (@n, {articuloId}, {cantidad}, 10.00);
            """);
    }

    /// <summary>
    /// Conjunto de Datos de Referencia del spec: A-001 → 5, A-002 → 15, A-003 → 60, A-004 → 0.
    /// El stock se construye con movimientos reales, no se escribe: es el punto de RF-029.
    /// </summary>
    private async Task SembrarConjuntoDeReferenciaAsync()
    {
        var a001 = await SembrarArticuloAsync("A-001", 10, 20, 50);
        var a002 = await SembrarArticuloAsync("A-002", 10, 20, 50);
        var a003 = await SembrarArticuloAsync("A-003", 10, 20, 50);
        await SembrarArticuloAsync("A-004", 0, 0, 0);

        await SembrarMovimientoAsync(tipo: 1, a001, 5);
        await SembrarMovimientoAsync(tipo: 1, a002, 15);
        await SembrarMovimientoAsync(tipo: 1, a003, 60);
        // A-004 no tiene movimientos: su Stock Actual es 0 por RF-030.
    }

    private async Task<ResultadoGenerarPedido> ConsultarAsync(
        bool soloBajoMinimo, string modo, string? descripcion = null)
    {
        var url = $"{Base}?soloBajoMinimo={soloBajoMinimo.ToString().ToLowerInvariant()}&modoPedido={modo}";

        if (descripcion is not null)
        {
            url += $"&descripcion={Uri.EscapeDataString(descripcion)}";
        }

        var respuesta = await Client.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();

        return await Json.LeerAsync<ResultadoGenerarPedido>(respuesta);
    }

    // -------------------------------------------------------------------------------------
    // V-1 / CE-003 — la matriz de 6 × 4 celdas de punta a punta.
    // -------------------------------------------------------------------------------------

    [TestCase("HastaStockMinimo", 5, 0, 0, 0)]
    [TestCase("HastaPuntoPedido", 15, 5, 0, 0)]
    [TestCase("HastaStockIdeal", 45, 35, 0, 0)]
    public async Task Sin_soloBajoMinimo_se_listan_los_cuatro_con_su_cantidad(
        string modo, int a001, int a002, int a003, int a004)
    {
        await SembrarConjuntoDeReferenciaAsync();

        var resultado = await ConsultarAsync(soloBajoMinimo: false, modo);
        var porCodigo = resultado.Filas.ToDictionary(f => f.Codigo, f => f.CantidadAPedir);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Filas, Has.Count.EqualTo(4));
            Assert.That(porCodigo["A-001"], Is.EqualTo(a001));
            Assert.That(porCodigo["A-002"], Is.EqualTo(a002));
            Assert.That(porCodigo["A-003"], Is.EqualTo(a003));
            Assert.That(porCodigo["A-004"], Is.EqualTo(a004));
        });
    }

    [TestCase("HastaStockMinimo", 5)]
    [TestCase("HastaPuntoPedido", 15)]
    [TestCase("HastaStockIdeal", 45)]
    public async Task Con_soloBajoMinimo_solo_aparece_A001(string modo, int cantidadEsperada)
    {
        // Las 9 exclusiones de la matriz, verificadas como ausencia de fila y no como cantidad 0.
        await SembrarConjuntoDeReferenciaAsync();

        var resultado = await ConsultarAsync(soloBajoMinimo: true, modo);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "A-001" }));
            Assert.That(resultado.Filas[0].CantidadAPedir, Is.EqualTo(cantidadEsperada));
        });
    }

    [Test]
    public async Task Ninguna_cantidad_es_negativa_en_ninguna_de_las_seis_corridas()
    {
        await SembrarConjuntoDeReferenciaAsync();

        foreach (var soloBajoMinimo in new[] { false, true })
        {
            foreach (var modo in new[] { "HastaStockMinimo", "HastaPuntoPedido", "HastaStockIdeal" })
            {
                var resultado = await ConsultarAsync(soloBajoMinimo, modo);

                Assert.That(
                    resultado.Filas.Select(f => f.CantidadAPedir),
                    Is.All.GreaterThanOrEqualTo(0),
                    $"soloBajoMinimo={soloBajoMinimo}, modo={modo}");
            }
        }
    }

    // -------------------------------------------------------------------------------------
    // T046 — V-7: un artículo sin movimientos debe poder pedirse.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_articulo_sin_movimientos_aparece_con_stock_cero_y_pide_su_stock_minimo()
    {
        // RF-030. Es el caso que un INNER JOIN en la vista rompería en silencio: el artículo
        // nuevo desaparecería del resultado justo cuando más falta hace pedirlo.
        await SembrarArticuloAsync("N-001", stockMinimo: 10, puntoPedido: 10, stockIdeal: 10);

        var resultado = await ConsultarAsync(soloBajoMinimo: true, "HastaStockMinimo");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Filas, Has.Count.EqualTo(1));
            Assert.That(resultado.Filas[0].Codigo, Is.EqualTo("N-001"));
            Assert.That(resultado.Filas[0].CantidadAPedir, Is.EqualTo(10));
        });
    }

    // -------------------------------------------------------------------------------------
    // T047 — con soloBajoMinimo = No se listan todos, incluidos los de cantidad 0.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Con_soloBajoMinimo_en_No_las_filas_de_cantidad_cero_se_muestran_igual()
    {
        // RF-026: no se omiten. Que un artículo aparezca con 0 es información — dice "de éste no
        // hace falta pedir" — y es distinto de que no aparezca.
        await SembrarConjuntoDeReferenciaAsync();

        var resultado = await ConsultarAsync(soloBajoMinimo: false, "HastaStockMinimo");
        var enCero = resultado.Filas.Where(f => f.CantidadAPedir == 0).Select(f => f.Codigo);

        Assert.That(enCero, Is.EquivalentTo(new[] { "A-002", "A-003", "A-004" }));
    }

    // -------------------------------------------------------------------------------------
    // T048 — resultado vacío: cero filas, sin error.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Un_resultado_vacio_devuelve_cero_filas_sin_error()
    {
        // RF-032. El catálogo está vacío: la respuesta es 200 con lista vacía, no un 404 ni un
        // error. El texto exacto del mensaje lo asierta el test de la vista (T050).
        var respuesta = await Client.GetAsync($"{Base}?soloBajoMinimo=true&modoPedido=HastaStockIdeal");

        respuesta.EnsureSuccessStatusCode();
        var resultado = await Json.LeerAsync<ResultadoGenerarPedido>(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(resultado.Filas, Is.Empty);
            Assert.That(resultado.Truncado, Is.False);
        });
    }

    // -------------------------------------------------------------------------------------
    // T048a — tope, orden y determinismo, que RF-027 exige a AMBAS consultas.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Sobre_mas_de_diez_mil_articulos_recorta_ordena_y_marca_truncado()
    {
        await SembrarArticulosEnMasaAsync(10_500);

        var primera = await ConsultarAsync(soloBajoMinimo: false, "HastaStockIdeal");
        var segunda = await ConsultarAsync(soloBajoMinimo: false, "HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(primera.Filas, Has.Count.EqualTo(10_000), "RF-027: tope de 10.000 filas.");
            Assert.That(primera.Truncado, Is.True, "RF-027c: la UI debe poder informarlo.");

            Assert.That(
                primera.Filas.Select(f => f.Codigo),
                Is.Ordered.Ascending,
                "RF-027b: ordenado por Código ascendente.");

            // Dos corridas devuelven el mismo conjunto: sin el orden previo al recorte, *cuáles*
            // 10.000 filas vuelven quedaría a criterio del plan de ejecución.
            Assert.That(
                segunda.Filas.Select(f => f.Codigo),
                Is.EqualTo(primera.Filas.Select(f => f.Codigo)),
                "El recorte tiene que ser reproducible entre corridas.");
        });
    }

    [Test]
    public async Task El_recorte_se_aplica_despues_de_filtrar_y_ordenar()
    {
        // RF-027b. Se siembran 10.500 artículos "de volumen" y un puñado que sí coincide con el
        // filtro, con códigos que caen DESPUÉS de los primeros 10.000 en orden alfabético.
        //
        // Si el motor recortara antes de filtrar, estos artículos quedarían fuera del recorte y el
        // resultado filtrado sería vacío. Que aparezcan es la prueba de que el orden del pipeline
        // es filtrar → ordenar → recortar y no otro.
        await SembrarArticulosEnMasaAsync(10_500);
        await SembrarArticuloAsync("Z-001", 0, 0, 0, descripcion: "Tornillo especial");
        await SembrarArticuloAsync("Z-002", 0, 0, 0, descripcion: "Tornillo especial");

        var resultado = await ConsultarAsync(
            soloBajoMinimo: false, "HastaStockIdeal", descripcion: "Tornillo");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "Z-001", "Z-002" }));
            Assert.That(resultado.Truncado, Is.False, "10.000 no se alcanzó tras filtrar.");
        });
    }

    // -------------------------------------------------------------------------------------
    // T048b — filtro por descripción, insensible a mayúsculas y acentos.
    // -------------------------------------------------------------------------------------

    [TestCase("valvula")]
    [TestCase("VÁLVULA")]
    [TestCase("Válvula")]
    [TestCase("bronce")]
    [TestCase("BRONCE")]
    public async Task El_filtro_por_descripcion_es_insensible_a_mayusculas_y_acentos(string filtro)
    {
        // RF-027a, V-8. La insensibilidad la aporta la collation Modern_Spanish_CI_AI de la
        // columna (R-06): se resuelve en el motor, sin normalizar cadenas en la aplicación.
        await SembrarArticuloAsync("V-001", 0, 0, 0, descripcion: "Válvula de bronce");
        await SembrarArticuloAsync("O-001", 0, 0, 0, descripcion: "Otra cosa");

        var resultado = await ConsultarAsync(
            soloBajoMinimo: false, "HastaStockIdeal", descripcion: filtro);

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "V-001" }));
    }

    [Test]
    public async Task El_filtro_encuentra_la_coincidencia_en_cualquier_posicion()
    {
        // RF-027a: coincidencia parcial, no "empieza con".
        await SembrarArticuloAsync("V-001", 0, 0, 0, descripcion: "Válvula de bronce");

        var resultado = await ConsultarAsync(
            soloBajoMinimo: false, "HastaStockIdeal", descripcion: "de bron");

        Assert.That(resultado.Filas, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Un_filtro_vacio_no_acota_el_resultado()
    {
        // RF-027a, última cláusula.
        await SembrarConjuntoDeReferenciaAsync();

        var resultado = await ConsultarAsync(
            soloBajoMinimo: false, "HastaStockIdeal", descripcion: "");

        Assert.That(resultado.Filas, Has.Count.EqualTo(4));
    }

    // -------------------------------------------------------------------------------------
    // RF-033 — la consulta no persiste resultados.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task El_resultado_refleja_los_parametros_vigentes_al_momento_de_ejecutar()
    {
        // RF-033: modificar los parámetros de reposición se refleja en la siguiente ejecución.
        await SembrarArticuloAsync("A-001", stockMinimo: 5, puntoPedido: 5, stockIdeal: 5);

        var antes = await ConsultarAsync(soloBajoMinimo: false, "HastaStockIdeal");

        await EjecutarSqlAsync(
            "UPDATE dbo.Articulo SET StockMinimo = 30, PuntoPedido = 30, StockIdeal = 30 WHERE Codigo = 'A-001'");

        var despues = await ConsultarAsync(soloBajoMinimo: false, "HastaStockIdeal");

        Assert.Multiple(() =>
        {
            Assert.That(antes.Filas[0].CantidadAPedir, Is.EqualTo(5));
            Assert.That(despues.Filas[0].CantidadAPedir, Is.EqualTo(30));
        });
    }
}
