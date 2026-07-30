using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

/// <summary>
/// T090 — Alta, modificación y consulta de un artículo.
/// </summary>
public sealed class ArticuloViewModel
{
    public int ArticuloId { get; set; }

    [Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Display(Name = "Precio de Costo")]
    public decimal PrecioCosto { get; set; }

    [Display(Name = "Margen (%)")]
    public decimal Margen { get; set; }

    /// <summary>
    /// Sólo lectura: lo calcula el motor a partir del costo y el margen (RF-016). La vista lo
    /// muestra deshabilitado y el controlador nunca lo envía a la API.
    /// </summary>
    [Display(Name = "Precio de Venta")]
    public decimal PrecioVenta { get; set; }

    [Display(Name = "Stock Mínimo")]
    public int StockMinimo { get; set; }

    [Display(Name = "Punto de Pedido")]
    public int PuntoPedido { get; set; }

    [Display(Name = "Stock Ideal")]
    public int StockIdeal { get; set; }

    /// <summary>Motivo del rechazo devuelto por la API (400 de validación o 409 de conflicto).</summary>
    public string? MensajeDeRechazo { get; set; }
}
