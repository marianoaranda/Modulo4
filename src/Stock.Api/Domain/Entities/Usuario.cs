namespace Stock.Api.Domain.Entities;

/// <summary>
/// Usuario del sistema (RF-004).
/// </summary>
public class Usuario
{
    public int UsuarioId { get; set; }

    public string NombreUsuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>
    /// Subclave derivada con PBKDF2-HMAC-SHA256 (R-03). Columna <b>separada</b> del salt, según la
    /// forma que exige el PRD (RF-007).
    /// </summary>
    public byte[] Hash { get; set; } = [];

    /// <summary>
    /// Salt aleatorio de 16 bytes, propio de cada usuario, para que dos usuarios con la misma
    /// contraseña tengan representaciones distintas (RF-008).
    /// </summary>
    public byte[] Salt { get; set; } = [];

    public int PerfilId { get; set; }

    public Perfil? Perfil { get; set; }
}
