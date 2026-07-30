namespace Stock.Api.Data.Views;

/// <summary>
/// Proyección de <c>vw_StockActual</c>, el <b>único</b> lugar del sistema donde se calcula el
/// saldo de un artículo (Principio III). La consultan las dos consultas de pantalla y también la
/// validación del invariante de stock, de modo que no puede haber dos implementaciones del saldo
/// que diverjan.
///
/// Se mapea como entidad sin clave (<c>HasNoKey().ToView(...)</c>): no es una tabla y no se
/// escribe nunca.
/// </summary>
public class StockActualView
{
    public int ArticuloId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Saldo de movimientos: las compras suman y las ventas restan. Los artículos sin movimientos
    /// aparecen con 0 gracias al <c>LEFT JOIN</c> con <c>ISNULL(..., 0)</c> (RF-030).
    /// </summary>
    public int StockActual { get; set; }
}
