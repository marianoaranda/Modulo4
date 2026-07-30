using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

public enum TipoMovimientoWeb
{
    Compra = 1,
    Venta = 2,
}

public sealed class LineaDetalleViewModel
{
    [Display(Name = "Artículo")]
    public int ArticuloId { get; set; }

    public string? Codigo { get; set; }

    public int Cantidad { get; set; }

    [Display(Name = "Precio Unitario")]
    public decimal PrecioUnitario { get; set; }

    [Display(Name = "Precio Total")]
    public decimal PrecioTotal { get; set; }
}

/// <summary>
/// T078 — Carga y edición de un movimiento.
///
/// <c>Numero</c> es de sólo lectura en la pantalla: lo asigna el sistema y no es editable por el
/// usuario (RF-020a).
/// </summary>
public sealed class MovimientoViewModel
{
    public int Numero { get; set; }

    public TipoMovimientoWeb Tipo { get; set; } = TipoMovimientoWeb.Compra;

    [DataType(DataType.Date)]
    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public List<LineaDetalleViewModel> Detalle { get; set; } = [];

    /// <summary>
    /// Mensaje de rechazo devuelto por la API (400 o 422). Se muestra en la misma pantalla de
    /// carga, con los datos del usuario todavía puestos: un rechazo por stock insuficiente es un
    /// resultado previsto del sistema, no una falla, y no corresponde mandarlo a una página de
    /// error.
    /// </summary>
    public string? MensajeDeRechazo { get; set; }
}
