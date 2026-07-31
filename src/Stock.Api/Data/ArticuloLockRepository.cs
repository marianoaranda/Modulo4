using Microsoft.EntityFrameworkCore;

namespace Stock.Api.Data;

/// <summary>
/// T073 — Bloqueo pesimista del protocolo de escritura de movimientos (R-02).
///
/// La fila de <c>Articulo</c> funciona como <b>mutex por artículo</b>. No impide por sí sola que
/// otra transacción inserte detalle: la corrección depende de que <i>todas</i> las rutas de
/// escritura tomen el bloqueo antes de leer el saldo. Por eso es un invariante de arquitectura y
/// no una optimización, y por eso <c>MovimientoService</c> es el único lugar que escribe
/// movimientos.
///
/// Se eligió bloqueo pesimista y no concurrencia optimista porque RF-024b exige que la operación
/// perdedora reciba <i>stock insuficiente</i> evaluado contra el saldo ya actualizado, y prohíbe
/// explícitamente devolver un error de conflicto que obligue a reintentar. Con <c>UPDLOCK</c> la
/// segunda transacción espera, re-lee el saldo confirmado y falla —si falla— por el motivo
/// correcto.
/// </summary>
public class ArticuloLockRepository
{
    private readonly StockDbContext _db;

    public ArticuloLockRepository(StockDbContext db) => _db = db;

    /// <summary>
    /// Toma <c>UPDLOCK, HOLDLOCK</c> sobre las filas indicadas, <b>en orden ascendente de
    /// ArticuloId</b>.
    ///
    /// El orden no es cosmético: es lo que evita deadlocks entre dos movimientos multilínea que
    /// comparten artículos y los recorren en sentidos opuestos. Y un deadlock se le presentaría al
    /// usuario como un error de concurrencia que pide reintentar, que es justo lo que RF-024b
    /// prohíbe.
    /// </summary>
    /// <remarks>
    /// Es <c>virtual</c> para que el test de la bitácora (V-12) pueda sustituirlo por uno que
    /// falle <b>dentro</b> de la transacción ya abierta. Sin ese punto de sustitución, forzar una
    /// excepción no controlada en medio de una operación transaccional exigiría un endpoint de
    /// prueba en el código de producción, que es peor: quedaría expuesto en la aplicación real.
    /// </remarks>
    public virtual async Task BloquearAsync(IEnumerable<int> articuloIds, CancellationToken ct = default)
    {
        var ordenados = articuloIds.Distinct().OrderBy(id => id).ToList();

        if (ordenados.Count == 0)
        {
            return;
        }

        // Los identificadores son enteros ya materializados, así que interpolarlos no abre
        // superficie de inyección; se hace así porque el hint de bloqueo no admite parámetros.
        var lista = string.Join(", ", ordenados);

        await _db.Database.ExecuteSqlRawAsync(
            $"""
             SELECT ArticuloId
             FROM   dbo.Articulo WITH (UPDLOCK, HOLDLOCK)
             WHERE  ArticuloId IN ({lista})
             ORDER  BY ArticuloId;
             """,
            ct);
    }
}
