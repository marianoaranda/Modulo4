namespace Stock.Api.Domain.Entities;

/// <summary>
/// Línea de detalle de un movimiento (RF-020).
/// </summary>
public class MovimientoDetalle
{
    public int MovimientoDetalleId { get; set; }

    public int MovimientoNumero { get; set; }

    public Movimiento? Movimiento { get; set; }

    public int ArticuloId { get; set; }

    public Articulo? Articulo { get; set; }

    /// <summary>Entero &gt; 0 y ≤ 1.000.000 (RF-023, RF-023a).</summary>
    public int Cantidad { get; set; }

    /// <summary>
    /// Precio informado por operación. No se valida contra el Precio de Costo ni el Precio de
    /// Venta del artículo: refleja la operación real (RF-023b). Sí debe ser ≥ 0 (RF-023c).
    /// </summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// Columna calculada PERSISTED: <c>Cantidad × PrecioUnitario</c>. Lo calcula el sistema, no lo
    /// carga el usuario (RF-020c).
    /// </summary>
    public decimal PrecioTotal { get; private set; }
}
