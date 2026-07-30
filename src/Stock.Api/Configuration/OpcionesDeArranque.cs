namespace Stock.Api.Configuration;

/// <summary>
/// Los tres secretos que la API exige al arrancar, resueltos de una sola vez para que la falta de
/// cualquiera se detecte antes de escuchar en el puerto y no en la primera petición que lo use.
/// </summary>
public sealed record OpcionesDeArranque(
    string CadenaDeConexion,
    string ClaveDeFirmaJwt,
    string PasswordAdminInicial,
    string Issuer,
    string Audience,
    int VigenciaHoras,
    bool AplicarMigracionesAlArrancar)
{
    public static OpcionesDeArranque Leer(IConfiguration configuracion) => new(
        CadenaDeConexion: ConfiguracionRequerida.Leer(
            configuracion, "ConnectionStrings:StockDb", "ConnectionStrings__StockDb"),
        ClaveDeFirmaJwt: ConfiguracionRequerida.Leer(
            configuracion, "Jwt:SigningKey", "Jwt__SigningKey"),
        PasswordAdminInicial: ConfiguracionRequerida.Leer(
            configuracion, "SEED_ADMIN_PASSWORD", "SEED_ADMIN_PASSWORD"),
        Issuer: configuracion["Jwt:Issuer"] ?? "Stock.Api",
        Audience: configuracion["Jwt:Audience"] ?? "Stock.Web",
        VigenciaHoras: configuracion.GetValue("Jwt:VigenciaHoras", 8),
        AplicarMigracionesAlArrancar: configuracion.GetValue("ApplyMigrationsOnStartup", false));
}
