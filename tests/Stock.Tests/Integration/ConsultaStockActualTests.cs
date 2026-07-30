namespace Stock.Tests.Integration;

/// <summary>
/// T066, T067, T068 y T069 — Consulta de Stock Actual (RF-025, RF-025a, RF-027).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class ConsultaStockActualTests : IntegrationTestBase
{
    private const string Base = "/api/consultas/stock-actual";

    private async Task SembrarArticuloAsync(string codigo, string descripcion = "Artículo de prueba") =>
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES (N'{codigo}', N'{descripcion}', 10.00, 0, 0, 0, 0);
            """);

    private async Task<ResultadoStockActual> ConsultarAsync(
        string? desde = null, string? hasta = null, string? descripcion = null)
    {
        var parametros = new List<string>();

        if (desde is not null) parametros.Add($"codigoDesde={Uri.EscapeDataString(desde)}");
        if (hasta is not null) parametros.Add($"codigoHasta={Uri.EscapeDataString(hasta)}");
        if (descripcion is not null) parametros.Add($"descripcion={Uri.EscapeDataString(descripcion)}");

        var url = parametros.Count > 0 ? $"{Base}?{string.Join('&', parametros)}" : Base;

        var respuesta = await Client.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();

        return await Json.LeerAsync<ResultadoStockActual>(respuesta);
    }

    // -------------------------------------------------------------------------------------
    // T066 — V-6: determinismo del recorte.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task Dos_corridas_sin_filtro_devuelven_el_mismo_conjunto_ordenado_y_truncado()
    {
        await SembrarArticulosEnMasaAsync(10_500);

        var primera = await ConsultarAsync();
        var segunda = await ConsultarAsync();

        Assert.Multiple(() =>
        {
            Assert.That(primera.Filas, Has.Count.EqualTo(10_000));
            Assert.That(primera.Truncado, Is.True);
            Assert.That(primera.Filas.Select(f => f.Codigo), Is.Ordered.Ascending);
            Assert.That(
                segunda.Filas.Select(f => f.Codigo),
                Is.EqualTo(primera.Filas.Select(f => f.Codigo)));
        });
    }

    [Test]
    public async Task Sin_alcanzar_el_tope_no_se_marca_truncado()
    {
        await SembrarArticuloAsync("A-001");

        var resultado = await ConsultarAsync();

        Assert.That(resultado.Truncado, Is.False);
    }

    [Test]
    public async Task La_cantidad_es_el_saldo_de_movimientos()
    {
        // RF-025: la columna "Cantidad" expone el Stock Actual, saldo de compras menos ventas.
        await SembrarArticuloAsync("A-001");
        var articuloId = await EscalarAsync<int>("SELECT ArticuloId FROM dbo.Articulo WHERE Codigo = 'A-001'");

        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Movimiento (Tipo, Fecha) VALUES (1, '2026-01-15');
            INSERT INTO dbo.MovimientoDetalle (MovimientoNumero, ArticuloId, Cantidad, PrecioUnitario)
            VALUES (SCOPE_IDENTITY(), {articuloId}, 40, 10.00);

            INSERT INTO dbo.Movimiento (Tipo, Fecha) VALUES (2, '2026-01-16');
            INSERT INTO dbo.MovimientoDetalle (MovimientoNumero, ArticuloId, Cantidad, PrecioUnitario)
            VALUES (SCOPE_IDENTITY(), {articuloId}, 15, 20.00);
            """);

        var resultado = await ConsultarAsync();

        Assert.That(resultado.Filas.Single().Cantidad, Is.EqualTo(25));
    }

    // -------------------------------------------------------------------------------------
    // T067 — V-9: el rango de códigos.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task El_rango_incluye_ambos_extremos()
    {
        // RF-025a: rango inclusive.
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("A-002");
        await SembrarArticuloAsync("A-003");
        await SembrarArticuloAsync("A-004");

        var resultado = await ConsultarAsync(desde: "A-001", hasta: "A-003");

        Assert.That(
            resultado.Filas.Select(f => f.Codigo),
            Is.EqualTo(new[] { "A-001", "A-002", "A-003" }));
    }

    [Test]
    public async Task Un_extremo_vacio_no_aplica_limite_por_ese_lado()
    {
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("M-500");
        await SembrarArticuloAsync("Z-999");

        var sinLimiteInferior = await ConsultarAsync(hasta: "M-500");
        var sinLimiteSuperior = await ConsultarAsync(desde: "M-500");
        var sinLimites = await ConsultarAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sinLimiteInferior.Filas.Select(f => f.Codigo),
                Is.EqualTo(new[] { "A-001", "M-500" }));
            Assert.That(sinLimiteSuperior.Filas.Select(f => f.Codigo),
                Is.EqualTo(new[] { "M-500", "Z-999" }));
            Assert.That(sinLimites.Filas, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task Un_rango_invertido_devuelve_vacio_sin_error()
    {
        // RF-025a, última cláusula: es un resultado vacío, no un error. Rechazarlo con un 400
        // sería técnicamente defendible y el spec lo prohíbe explícitamente.
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("Z-999");

        var respuesta = await Client.GetAsync($"{Base}?codigoDesde=Z-999&codigoHasta=A-001");
        respuesta.EnsureSuccessStatusCode();

        var resultado = await Json.LeerAsync<ResultadoStockActual>(respuesta);

        Assert.Multiple(() =>
        {
            Assert.That((int)respuesta.StatusCode, Is.EqualTo(200));
            Assert.That(resultado.Filas, Is.Empty);
        });
    }

    // -------------------------------------------------------------------------------------
    // T068 — RF-025a: la collation del Código.
    // -------------------------------------------------------------------------------------

    [Test]
    public async Task El_rango_del_codigo_es_insensible_a_mayusculas()
    {
        // Modern_Spanish_CI_AS. La distinción es observable: determina qué filas entran y en qué
        // posición quedan frente al tope de RF-027.
        await SembrarArticuloAsync("a-002");

        var resultado = await ConsultarAsync(desde: "A-001", hasta: "A-003");

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "a-002" }));
    }

    [Test]
    public async Task El_rango_del_codigo_es_sensible_a_acentos()
    {
        // La otra mitad de la regla: CI pero AS. Un código acentuado no es el mismo que el sin
        // acentuar, y por lo tanto no cae en el mismo punto del orden.
        await SembrarArticuloAsync("PAÑO-1");
        await SembrarArticuloAsync("PANO-1");

        var resultado = await ConsultarAsync(desde: "PANO-1", hasta: "PANO-1");

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "PANO-1" }));
    }

    // -------------------------------------------------------------------------------------
    // T069 — V-8: el filtro por descripción.
    // -------------------------------------------------------------------------------------

    [TestCase("valvula")]
    [TestCase("VÁLVULA")]
    [TestCase("Válvula")]
    [TestCase("bronce")]
    public async Task El_filtro_por_descripcion_es_insensible_a_mayusculas_y_acentos(string filtro)
    {
        await SembrarArticuloAsync("V-001", "Válvula de bronce");
        await SembrarArticuloAsync("O-001", "Otra cosa");

        var resultado = await ConsultarAsync(descripcion: filtro);

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "V-001" }));
    }

    [Test]
    public async Task Un_filtro_vacio_no_acota_el_resultado()
    {
        await SembrarArticuloAsync("A-001");
        await SembrarArticuloAsync("A-002");

        var resultado = await ConsultarAsync(descripcion: "");

        Assert.That(resultado.Filas, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task El_rango_y_el_filtro_se_combinan()
    {
        await SembrarArticuloAsync("A-001", "Válvula de bronce");
        await SembrarArticuloAsync("A-002", "Otra cosa");
        await SembrarArticuloAsync("Z-001", "Válvula de bronce");

        var resultado = await ConsultarAsync(desde: "A-001", hasta: "A-999", descripcion: "valvula");

        Assert.That(resultado.Filas.Select(f => f.Codigo), Is.EqualTo(new[] { "A-001" }));
    }

    [Test]
    public async Task Un_articulo_sin_movimientos_aparece_con_cantidad_cero()
    {
        // RF-030, también en esta consulta.
        await SembrarArticuloAsync("N-001");

        var resultado = await ConsultarAsync();

        Assert.That(resultado.Filas.Single().Cantidad, Is.Zero);
    }
}
