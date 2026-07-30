using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

/// <summary>
/// T117 — Alta y modificación de perfil.
///
/// <c>EsAdministrador</c> está para <b>mostrar</b>, nunca para editar: la marca se establece
/// exclusivamente en la siembra y ningún DTO de la API la acepta (RF-003a). La vista no expone
/// ningún control que permita cambiarla.
/// </summary>
public sealed class PerfilViewModel
{
    public int PerfilId { get; set; }

    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Display(Name = "Es administrador")]
    public bool EsAdministrador { get; set; }

    public string? MensajeDeRechazo { get; set; }
}
