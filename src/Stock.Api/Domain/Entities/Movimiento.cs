namespace Stock.Api.Domain.Entities;

/// <summary>
/// Tipo de movimiento. Conjunto cerrado: una compra suma al Stock Actual y una venta resta
/// (RF-020b). Los valores son los que persiste la columna <c>Tipo</c>.
/// </summary>
public enum TipoMovimiento
{
    Compra = 1,
    Venta = 2,
}

/// <summary>
/// Encabezado de movimiento (RF-020).
/// </summary>
public class Movimiento
{
    /// <summary>
    /// Clave primaria <c>IDENTITY</c>: única globalmente, compartida entre compras y ventas, no
    /// editable por el usuario y no reutilizable tras una baja, porque <c>IDENTITY</c> no reasigna
    /// valores liberados (RF-020a, R-07).
    /// </summary>
    public int Numero { get; set; }

    public TipoMovimiento Tipo { get; set; }

    /// <summary>
    /// No puede ser posterior a hoy. Se valida en el servicio y no con un <c>CHECK</c>, porque la
    /// condición depende del momento de evaluación (RF-020d).
    /// </summary>
    public DateOnly Fecha { get; set; }

    public ICollection<MovimientoDetalle> Detalle { get; } = new List<MovimientoDetalle>();
}
