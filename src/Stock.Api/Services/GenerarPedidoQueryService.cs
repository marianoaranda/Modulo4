using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Pedido;

namespace Stock.Api.Services;

public sealed record FilaPedido(string Codigo, string Descripcion, int CantidadAPedir);

public sealed record ResultadoPedido(IReadOnlyList<FilaPedido> Filas, bool Truncado);

/// <summary>
/// T053 — Consulta "Generar Pedido" (RF-026).
///
/// Aplica el pipeline de RF-027b en este orden exacto: <b>filtrar → ordenar por Código → recortar
/// a 10.000 → marcar</b>. El orden antes del recorte es lo que hace el resultado determinista: sin
/// él, <i>cuáles</i> 10.000 filas vuelven quedaría a criterio del plan de ejecución y dos corridas
/// idénticas podrían devolver conjuntos distintos.
/// </summary>
public class GenerarPedidoQueryService
{
    private readonly StockDbContext _db;

    public GenerarPedidoQueryService(StockDbContext db) => _db = db;

    public async Task<ResultadoPedido> ConsultarAsync(
        bool soloBajoMinimo,
        ModoPedido modo,
        string? descripcion,
        CancellationToken ct = default)
    {
        // El saldo sale de vw_StockActual, único lugar del sistema donde se calcula (Principio III).
        var consulta =
            from stock in _db.StockActual
            join articulo in _db.Articulos on stock.ArticuloId equals articulo.ArticuloId
            select new
            {
                stock.Codigo,
                stock.Descripcion,
                stock.StockActual,
                articulo.StockMinimo,
                articulo.PuntoPedido,
                articulo.StockIdeal,
            };

        // 1. Filtrar. Un filtro vacío no acota el resultado (RF-027a).
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            // La insensibilidad a mayúsculas y acentos la aporta la collation Modern_Spanish_CI_AI
            // de la columna, no esta línea (R-06).
            consulta = consulta.Where(x => EF.Functions.Like(x.Descripcion, $"%{descripcion}%"));
        }

        if (soloBajoMinimo)
        {
            // Contraparte en SQL de PedidoCalculator.Incluir. Tiene que evaluarse en el motor y no
            // en memoria, porque el tope de 10.000 se aplica DESPUÉS de filtrar (RF-027b): filtrar
            // después del recorte devolvería menos filas de las que corresponden.
            //
            // Que coincida con la función pura no queda librado a la disciplina: si divergiera,
            // la matriz del Conjunto de Datos de Referencia de GenerarPedidoTests fallaría.
            consulta = consulta.Where(x => x.StockActual < x.StockMinimo);
        }

        // 2. Ordenar y 3. recortar, pidiendo una fila de más para detectar el recorte.
        var crudas = await consulta
            .OrderBy(x => x.Codigo)
            .Take(LimitesDeConsulta.TopeDeSondeo)
            .ToListAsync(ct);

        // 4. Marcar.
        var truncado = crudas.Count > LimitesDeConsulta.TopeDeFilas;

        var filas = crudas
            .Take(LimitesDeConsulta.TopeDeFilas)
            .Select(x => new FilaPedido(
                x.Codigo,
                x.Descripcion,
                PedidoCalculator.CantidadAPedir(
                    modo,
                    new ParametrosDeReposicion(x.StockMinimo, x.PuntoPedido, x.StockIdeal),
                    x.StockActual)))
            .ToList();

        return new ResultadoPedido(filas, truncado);
    }
}
