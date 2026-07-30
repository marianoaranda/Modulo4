namespace Stock.Api.Domain.Entities;

/// <summary>
/// Perfil de seguridad (RF-001).
/// </summary>
public class Perfil
{
    public int PerfilId { get; set; }

    /// <summary>
    /// Rótulo editable por RF-003. <b>No</b> es base de ninguna decisión de autorización:
    /// renombrar un perfil no otorga ni quita privilegios (RF-003a).
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Marca interna inmutable que identifica al perfil administrador. Es la <b>única</b> base de
    /// las decisiones de autorización. Se establece exclusivamente en la siembra inicial y ningún
    /// DTO del ABM la acepta, de modo que la Descripción puede cambiar libremente sin efecto sobre
    /// los privilegios (RF-003a).
    /// </summary>
    public bool EsAdministrador { get; set; }

    public ICollection<Usuario> Usuarios { get; } = new List<Usuario>();
}
