namespace Stock.Tests.Integration;

/// <summary>
/// T123 — Rendimiento bajo concurrencia (CE-004, segunda cláusula).
///
/// CE-004 tiene dos cláusulas y se verifican por separado porque miden cosas distintas:
/// <see cref="ConcurrenciaTests"/> cubre la primera —que ninguna operación concurrente puede violar
/// el invariante de stock no negativo— y este fixture cubre la segunda: que con hasta 5 usuarios
/// concurrentes las consultas <b>siguen</b> cumpliendo el presupuesto de CE-002.
///
/// Que el caso secuencial de T122 entre en presupuesto no implica éste. La agregación de
/// <c>vw_StockActual</c> recorre las 100.000 líneas en cada consulta, así que cinco consultas
/// simultáneas compiten por el mismo pool de conexiones, la misma caché de datos y la misma CPU: es
/// exactamente donde un plan que "alcanza justo" en solitario deja de alcanzar. Sin este fixture,
/// la mitad de CE-004 quedaría sostenida sólo por la inferencia de que si uno anda, cinco también.
/// </summary>
[Category(TestCategories.Volumen)]
public class RendimientoConcurrenteTests : RendimientoTestBase
{
    /// <summary>Tope de usuarios simultáneos que fija CE-004.</summary>
    private const int ClientesConcurrentes = 5;

    private const int CorridasDeCalentamiento = 2;

    /// <summary>
    /// 10 por cliente y por consulta: 5 × 10 × 2 = 100 muestras agregadas, suficientes para un p95
    /// con sentido sin multiplicar por cinco la duración del fixture.
    /// </summary>
    private const int CorridasMedidasPorCliente = 10;

    [Test]
    public async Task Las_dos_consultas_sostienen_el_presupuesto_con_cinco_usuarios_concurrentes()
    {
        await VerificarVolumenAsync();

        // Un HttpClient por cliente simulado: compartir uno solo mediría la cola interna de un
        // único cliente en vez de cinco usuarios reales pegándole al servidor a la vez.
        var clientes = Enumerable.Range(0, ClientesConcurrentes)
            .Select(_ => ClienteAutenticado())
            .ToList();

        try
        {
            // Las dos consultas se lanzan mezcladas y no una tanda después de la otra: es lo que
            // reproduce la situación real, donde los cinco usuarios no están mirando la misma
            // pantalla.
            var corridas = clientes.SelectMany(cliente => new[]
            {
                MedirAsync(cliente, RutaStockActual,
                    CorridasDeCalentamiento, CorridasMedidasPorCliente),
                MedirAsync(cliente, RutaGenerarPedido,
                    CorridasDeCalentamiento, CorridasMedidasPorCliente),
            });

            var resultados = await Task.WhenAll(corridas);

            var agregadas = resultados.SelectMany(m => m).ToList();

            Informar($"Ambas consultas con {ClientesConcurrentes} clientes concurrentes", agregadas);

            Assert.That(P95(agregadas), Is.LessThan(PresupuestoP95),
                $"CE-004 exige que con hasta {ClientesConcurrentes} usuarios concurrentes las " +
                "consultas sigan cumpliendo el presupuesto de latencia de CE-002.");
        }
        finally
        {
            foreach (var cliente in clientes)
            {
                cliente.Dispose();
            }
        }
    }
}
