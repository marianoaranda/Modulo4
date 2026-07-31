using System.Diagnostics;

namespace Stock.Tests.Integration;

/// <summary>
/// Base compartida por los dos fixtures de volumen (T122 y T123).
///
/// Siembra el <b>volumen de referencia</b> que fija CE-002 —10.000 artículos y 100.000 líneas de
/// detalle— y provee la instrumentación de medición. Existe para que T122 y T123 midan exactamente
/// sobre el mismo escenario: si cada uno sembrara el suyo, una diferencia en el poblado —cuántas
/// líneas por artículo, qué proporción entra en el resultado— haría incomparables sus números y el
/// presupuesto agregado de CE-004 dejaría de significar lo mismo que el secuencial de CE-002.
/// </summary>
public abstract class RendimientoTestBase : IntegrationTestBase
{
    protected const int CantidadDeArticulos = 10_000;

    /// <summary>10 líneas por artículo repartidas en 1.000 movimientos: 100.000 en total.</summary>
    protected const int LineasPorArticulo = 10;

    protected const int CantidadDeMovimientos = 1_000;

    /// <summary>Presupuesto de CE-002. Se mide contra el p95, no contra el promedio.</summary>
    protected static readonly TimeSpan PresupuestoP95 = TimeSpan.FromSeconds(3);

    protected const string RutaStockActual = "/api/consultas/stock-actual";

    /// <summary>
    /// Peor caso deliberado: sin filtro y con <c>soloBajoMinimo=false</c>, de modo que la consulta
    /// recorra el catálogo entero y devuelva las 10.000 filas. Medir con un filtro que recorte el
    /// resultado daría un número cómodo que no dice nada sobre el presupuesto.
    /// </summary>
    protected const string RutaGenerarPedido =
        "/api/consultas/generar-pedido?soloBajoMinimo=false&modoPedido=HastaStockIdeal";

    /// <summary>
    /// El <c>[SetUp]</c> de la base vacía las tablas antes de cada test, así que el volumen se
    /// repone acá y no en <c>PrepararFixtureAsync</c>. La siembra es enteramente basada en
    /// conjuntos —un <c>INSERT ... SELECT</c> por tabla, no 110.000 <c>SaveChanges</c>—, de modo que
    /// reponerla por test cuesta segundos y no minutos.
    /// </summary>
    protected override Task LimpiarFixtureAsync() => SembrarVolumenDeReferenciaAsync();

    private async Task SembrarVolumenDeReferenciaAsync()
    {
        // Los tres niveles de reposición se eligen para que TODO artículo quede por encima del
        // punto de pedido (saldo 50) y aun así tenga algo que pedir hasta el Stock Ideal (100).
        // Así el resultado no depende de cuál de los tres modos se consulte y, sobre todo, la
        // consulta devuelve las 10.000 filas en vez de un puñado: es lo que hace que la medición
        // ejerza el camino completo de agregación, proyección y serialización.
        await EjecutarSqlAsync($"""
            WITH Numeros AS (
                SELECT TOP ({CantidadDeArticulos})
                       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM   sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            SELECT 'V-' + RIGHT('00000' + CAST(n AS varchar(6)), 6),
                   'Artículo de volumen ' + CAST(n AS varchar(6)),
                   10.00, 25, 20, 40, 100
            FROM   Numeros;
            """);

        await EjecutarSqlAsync($"""
            WITH Numeros AS (
                SELECT TOP ({CantidadDeMovimientos})
                       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM   sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO dbo.Movimiento (Tipo, Fecha)
            SELECT 1, CAST('2026-01-02' AS date)
            FROM   Numeros;
            """);

        // El detalle se ata a los movimientos por número de fila y NO por el valor de la clave:
        // `Movimiento.Numero` es IDENTITY y el DELETE del `[SetUp]` no reinicia la semilla, así que
        // en la segunda siembra los números ya no arrancan en 1. Asumirlo dejaría el INSERT sin
        // filas coincidentes y el fixture mediría sobre una base vacía, en verde y sin sentido.
        await EjecutarSqlAsync($"""
            WITH Articulos AS (
                SELECT ArticuloId, ROW_NUMBER() OVER (ORDER BY ArticuloId) AS n
                FROM   dbo.Articulo
            ),
            Movimientos AS (
                SELECT Numero, ROW_NUMBER() OVER (ORDER BY Numero) AS m
                FROM   dbo.Movimiento
            ),
            Repeticiones AS (
                SELECT TOP ({LineasPorArticulo})
                       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS r
                FROM   sys.all_objects
            )
            INSERT INTO dbo.MovimientoDetalle
                (MovimientoNumero, ArticuloId, Cantidad, PrecioUnitario)
            SELECT mv.Numero, ar.ArticuloId, 5, 10.00
            FROM   Articulos ar
            CROSS JOIN Repeticiones rp
            JOIN   Movimientos mv
                   ON mv.m = ((ar.n - 1) * {LineasPorArticulo} + rp.r - 1) % {CantidadDeMovimientos} + 1;
            """);

        // Sin estadísticas frescas el optimizador planifica sobre un catálogo que todavía cree
        // vacío y elige planes que no son los que usaría en producción. Medir eso sería medir un
        // artefacto del fixture.
        await EjecutarSqlAsync("""
            UPDATE STATISTICS dbo.Articulo;
            UPDATE STATISTICS dbo.MovimientoDetalle;
            UPDATE STATISTICS dbo.Movimiento;
            """);
    }

    /// <summary>
    /// Verifica que el escenario es el que dice ser antes de medir sobre él. Una medición veloz
    /// sobre una base a medio sembrar es peor que un fallo: pasa en verde y certifica algo falso.
    /// </summary>
    protected async Task VerificarVolumenAsync()
    {
        var articulos = await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Articulo");
        var lineas = await EscalarAsync<int>("SELECT COUNT(*) FROM dbo.MovimientoDetalle");

        Assert.Multiple(() =>
        {
            Assert.That(articulos, Is.EqualTo(CantidadDeArticulos),
                "El volumen de referencia de CE-002 exige 10.000 artículos.");

            Assert.That(lineas, Is.EqualTo(CantidadDeArticulos * LineasPorArticulo),
                "El volumen de referencia de CE-002 exige 100.000 líneas de detalle.");
        });
    }

    /// <summary>
    /// Ejecuta la ruta indicada y devuelve los tiempos de las corridas medidas, descartando las de
    /// calentamiento.
    ///
    /// El calentamiento no es un truco para mejorar el número: la primera llamada carga el plan de
    /// consulta, el pool de conexiones y el JIT de la ruta MVC, costos que se pagan una vez por
    /// proceso y no en cada consulta que hace el usuario. Incluirlos mediría el arranque del
    /// servidor, no la consulta.
    /// </summary>
    protected static async Task<IReadOnlyList<TimeSpan>> MedirAsync(
        HttpClient cliente, string ruta, int calentamiento, int medidas)
    {
        for (var i = 0; i < calentamiento; i++)
        {
            await ConsumirAsync(cliente, ruta);
        }

        var muestras = new List<TimeSpan>(medidas);

        for (var i = 0; i < medidas; i++)
        {
            var reloj = Stopwatch.StartNew();
            await ConsumirAsync(cliente, ruta);
            reloj.Stop();

            muestras.Add(reloj.Elapsed);
        }

        return muestras;
    }

    /// <summary>
    /// Lee el cuerpo <b>completo</b>: el presupuesto de CE-002 es el tiempo hasta tener el
    /// resultado, no hasta recibir los encabezados. Con 10.000 filas la serialización y la
    /// transferencia no son despreciables, y cronometrar sólo los encabezados dejaría fuera de la
    /// medición justo la parte que crece con el volumen.
    /// </summary>
    private static async Task ConsumirAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, HttpCompletionOption.ResponseHeadersRead);

        respuesta.EnsureSuccessStatusCode();

        await respuesta.Content.LoadIntoBufferAsync();
        await respuesta.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// Percentil 95 por el método del rango más cercano: el menor valor observado que deja al menos
    /// el 95% de la muestra en o por debajo de él. Sin interpolación, porque interpolar inventaría
    /// un valor que nunca se midió.
    /// </summary>
    protected static TimeSpan P95(IReadOnlyList<TimeSpan> muestras)
    {
        if (muestras.Count == 0)
        {
            throw new InvalidOperationException("No hay muestras para calcular el percentil.");
        }

        var ordenadas = muestras.OrderBy(m => m).ToList();
        var indice = (int)Math.Ceiling(0.95 * ordenadas.Count) - 1;

        return ordenadas[Math.Clamp(indice, 0, ordenadas.Count - 1)];
    }

    /// <summary>
    /// Deja el detalle de la distribución en la salida de NUnit. Un fallo que sólo dice "3,4 s >
    /// 3 s" no permite distinguir una regresión sostenida de una única corrida arrastrada por otro
    /// proceso de la máquina; con la mediana y el máximo a la vista, sí.
    /// </summary>
    protected static void Informar(string consulta, IReadOnlyList<TimeSpan> muestras)
    {
        var ordenadas = muestras.OrderBy(m => m).ToList();

        TestContext.Progress.WriteLine(
            $"{consulta}: n={ordenadas.Count} " +
            $"mediana={ordenadas[ordenadas.Count / 2].TotalMilliseconds:F0} ms " +
            $"p95={P95(muestras).TotalMilliseconds:F0} ms " +
            $"máx={ordenadas[^1].TotalMilliseconds:F0} ms");
    }
}
