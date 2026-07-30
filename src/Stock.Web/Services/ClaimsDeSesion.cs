namespace Stock.Web.Services;

/// <summary>
/// Claims que la cookie de sesión de la capa web transporta.
/// </summary>
public static class ClaimsDeSesion
{
    /// <summary>
    /// Copia del claim <c>es_admin</c> del JWT. Sirve <b>sólo</b> para decidir qué entradas de
    /// menú mostrar: el control de acceso real lo hace la API contra el claim del token en cada
    /// llamada (RF-010, RF-010a). Nunca se compara la Descripción del perfil contra la cadena
    /// "administrador", que es justamente lo que RF-003a prohíbe.
    /// </summary>
    public const string EsAdmin = "es_admin";
}
