namespace Stock.Api.Configuration;

/// <summary>
/// Lectura de configuración sin valores por defecto (Principio IV).
///
/// Los valores sensibles quedan vacíos en <c>appsettings.json</c> y se inyectan por variable de
/// entorno. Si alguno falta, la aplicación falla al arrancar con un mensaje explícito en vez de
/// caer en un valor por defecto que enmascare la omisión.
/// </summary>
public static class ConfiguracionRequerida
{
    public static string Leer(IConfiguration configuracion, string clave, string variableDeEntorno)
    {
        var valor = configuracion[clave];

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"Falta la configuración obligatoria '{clave}'. Definila en la variable de entorno " +
                $"'{variableDeEntorno}' (ver .env.example en la raíz del repositorio). " +
                "No existe valor por defecto: Principio IV de la constitución.");
        }

        return valor;
    }
}
