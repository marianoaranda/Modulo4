using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;

namespace Stock.Api.Services;

public sealed record FilaStock(string Codigo, string Descripcion, int Cantidad);

public sealed record ResultadoStock(IReadOnlyList<FilaStock> Filas, bool Truncado);

/// <summary>
/// T076 — Consulta de Stock Actual (RF-025).
///
/// Mismo pipeline que Generar Pedido —filtrar → ordenar por Código → recortar a 10.000 → marcar—
/// y misma fuente del saldo, <c>vw_StockActual</c>. Lo que agrega es el rango de códigos de
/// RF-025a, que Generar Pedido explícitamente no tiene (RF-026a).
/// </summary>
public class StockActualQueryService
{
    private readonly StockDbContext _db;

    public StockActualQueryService(StockDbContext db) => _db = db;

    public async Task<ResultadoStock> ConsultarAsync(
        string? codigoDesde,
        string? codigoHasta,
        string? descripcion,
        CancellationToken ct = default)
    {
        var consulta = _db.StockActual.AsQueryable();

        // 1. Filtrar. Ambos extremos son opcionales: vacío = sin límite por ese lado (RF-025a).
        //
        // La comparación se delega al motor con la collation Modern_Spanish_CI_AS de la columna
        // Codigo: insensible a mayúsculas y sensible a acentos, que es el orden alfabético del
        // español y no un orden ordinal por punto de código. Compararlo en C# con string.Compare
        // daría un resultado distinto y observable: cambiaría qué filas entran y en qué posición
        // quedan frente al tope.
        //
        // Un rango invertido no necesita tratamiento especial: no hay Código que cumpla ambas
        // condiciones, así que el resultado sale vacío sin error, que es lo que pide RF-025a.
        if (!string.IsNullOrWhiteSpace(codigoDesde))
        {
            consulta = consulta.Where(s => string.Compare(s.Codigo, codigoDesde) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(codigoHasta))
        {
            consulta = consulta.Where(s => string.Compare(s.Codigo, codigoHasta) <= 0);
        }

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            consulta = consulta.Where(s => EF.Functions.Like(s.Descripcion, $"%{descripcion}%"));
        }

        // 2. Ordenar y 3. recortar, con una fila de sondeo para detectar el recorte.
        var crudas = await consulta
            .OrderBy(s => s.Codigo)
            .Take(LimitesDeConsulta.TopeDeSondeo)
            .Select(s => new FilaStock(s.Codigo, s.Descripcion, s.StockActual))
            .ToListAsync(ct);

        // 4. Marcar.
        var truncado = crudas.Count > LimitesDeConsulta.TopeDeFilas;

        return new ResultadoStock(crudas.Take(LimitesDeConsulta.TopeDeFilas).ToList(), truncado);
    }
}
