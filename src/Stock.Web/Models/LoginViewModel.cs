using System.ComponentModel.DataAnnotations;

namespace Stock.Web.Models;

public sealed class LoginViewModel
{
    [Display(Name = "Usuario")]
    public string Usuario { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje de credenciales inválidas. Es el mismo para usuario inexistente y para contraseña
    /// incorrecta, tal como lo devuelve la API (RF-011): la pantalla no lo reformula ni agrega
    /// detalle, porque hacerlo reintroduciría el oráculo de existencia de cuentas que la API
    /// evita.
    /// </summary>
    public string? Mensaje { get; set; }
}
