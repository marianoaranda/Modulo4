using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

/// <summary>
/// T117 — Alta y modificación de usuario.
///
/// No tiene <c>Hash</c> ni <c>Salt</c>: la API no los devuelve y la pantalla no los necesita
/// (RF-007). <c>Password</c> viaja sólo hacia la API, nunca de vuelta.
/// </summary>
public sealed class UsuarioViewModel
{
    public int UsuarioId { get; set; }

    [Display(Name = "Nombre de usuario")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Display(Name = "Perfil")]
    public int PerfilId { get; set; }

    /// <summary>
    /// Obligatoria en el alta; en la modificación, dejarla vacía conserva la contraseña actual
    /// (RF-006). La pantalla lo dice explícitamente para que nadie la complete "por las dudas".
    /// </summary>
    [Display(Name = "Contraseña")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public string? MensajeDeRechazo { get; set; }

    /// <summary>Perfiles disponibles para el desplegable.</summary>
    public List<PerfilViewModel> Perfiles { get; set; } = [];
}
