namespace Stock.Api.Services;

/// <summary>
/// Tope común a las dos consultas (RF-027).
///
/// La constitución prohíbe consultas de stock/pedido sin límite ni filtro, así que el tope no es
/// una optimización opcional: es una restricción de arquitectura. Vive en un solo lugar para que
/// no pueda desincronizarse entre <c>GenerarPedidoQueryService</c> y <c>StockActualQueryService</c>.
/// </summary>
public static class LimitesDeConsulta
{
    public const int TopeDeFilas = 10_000;

    /// <summary>
    /// Se pide una fila de más para saber si hubo recorte. Contar el total con un
    /// <c>COUNT(*)</c> aparte costaría una segunda pasada sobre la agregación para responder una
    /// pregunta de sí o no.
    /// </summary>
    public const int TopeDeSondeo = TopeDeFilas + 1;
}
