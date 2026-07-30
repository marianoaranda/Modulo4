namespace Stock.Api.Domain.Entities;

/// <summary>
/// Bitácora de errores de ejecución (RF-028).
///
/// El <b>esquema</b> lo declara y versiona <c>StockDbContext</c>, junto al resto de las tablas y en
/// la misma migración inicial. La <b>escritura en runtime</b>, en cambio, se hace con
/// <c>ErrorLogDbContext</c>, que abre su propia conexión fuera de la transacción que está fallando,
/// para que el registro sobreviva al rollback (R-08, CE-008). Son dos cosas distintas y conviene
/// no confundirlas.
///
/// Registra <b>sólo errores de ejecución no controlados</b>: los rechazos de negocio esperados
/// (stock insuficiente, código duplicado, contraseña corta) son resultados previstos, no fallos.
/// </summary>
public class ErrorLog
{
    public int ErrorId { get; set; }

    public DateTime ErrorDateTime { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? FullException { get; set; }
}
