namespace Stock.Api.Domain.Entities;

/// <summary>
/// Artículo del catálogo (RF-013).
///
/// El <b>Stock Actual no es un campo de esta entidad</b>: es siempre el saldo de los movimientos,
/// calculado por <c>vw_StockActual</c> (RF-029, Principio III).
/// </summary>
public class Articulo
{
    /// <summary>
    /// Clave sustituta, no el Código: así modificar el Código de un artículo —permitido por
    /// RF-015— no obliga a propagar el cambio al histórico de movimientos.
    /// </summary>
    public int ArticuloId { get; set; }

    /// <summary>Identidad de negocio, texto. Único con comparación CI_AS (RF-013, RF-017a).</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public decimal PrecioCosto { get; set; }

    /// <summary>Porcentaje.</summary>
    public decimal Margen { get; set; }

    /// <summary>
    /// Columna calculada PERSISTED: <c>PrecioCosto × (1 + Margen / 100)</c>. La calcula el motor,
    /// de modo que es imposible que diverja de sus insumos (RF-016).
    /// </summary>
    public decimal PrecioVenta { get; private set; }

    // Los tres parámetros de reposición son enteros por RF-013a. En consecuencia el Stock Actual
    // y la Cantidad a Pedir resultan enteros por construcción, sin regla de redondeo.
    public int StockMinimo { get; set; }

    public int PuntoPedido { get; set; }

    public int StockIdeal { get; set; }

    public ICollection<MovimientoDetalle> Detalle { get; } = new List<MovimientoDetalle>();
}
