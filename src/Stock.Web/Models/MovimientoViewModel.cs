using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

public enum TipoMovimientoWeb
{
    Compra = 1,
    Venta = 2,
}

/// <summary>Respuesta de <c>/api/movimientos/proximo-numero</c> (RF-020f).</summary>
public sealed class ProximoNumeroViewModel
{
    public int Numero { get; set; }
}

public sealed class LineaDetalleViewModel
{
    /// <summary>
    /// Identidad de negocio de la línea (RF-020e). El identificador interno del artículo no
    /// aparece: ni la pantalla lo pide ni la API lo acepta.
    /// </summary>
    [Display(Name = "Código")]
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

    /// <summary>
    /// Correlativo que le tocaría a un movimiento nuevo, para mostrarlo en sólo lectura (RF-020f).
    /// Es <c>null</c> en la edición: ahí el Número ya existe y no hay nada que sugerir.
    /// </summary>
    public int? NumeroSugerido { get; set; }

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
