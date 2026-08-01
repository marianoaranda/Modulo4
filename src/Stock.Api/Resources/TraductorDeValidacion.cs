using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Stock.Api.Resources;

/// <summary>
/// T172 — Traduce al español los mensajes de validación que produce el marco de trabajo
/// (RF-035, RF-035a).
///
/// Hay dos orígenes y ninguno de los dos pasa por código propio:
///
/// <list type="bullet">
///   <item>el <b>obligatorio implícito</b> de una propiedad no anulable del cuerpo, que produce el
///   "The Codigo field is required." que la pantalla de carga muestra tal cual;</item>
///   <item>el <b>deserializador</b>, cuando el valor recibido no entra en el tipo del campo. Es el
///   rechazo del borde de la solicitud que fija RF-018a, y su texto por omisión nombra tipos de la
///   plataforma (<c>System.Int32</c>) que no significan nada para quien carga un movimiento.</item>
/// </list>
///
/// El tipo se deduce del propio mensaje y no se expone: lo único que sale es qué se esperaba en
/// castellano. Un mensaje que ya esté en español —los de regla de negocio— se devuelve intacto.
/// </summary>
public static class TraductorDeValidacion
{
    /// <summary>Marca del fallo del deserializador de <c>System.Text.Json</c>.</summary>
    private const string FalloDeConversion = "could not be converted";

    public static string Traducir(string campo, string mensaje)
    {
        var rotulo = Rotulo(campo);

        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return string.Format(MensajesDeValidacion.ValorNoValido, rotulo);
        }

        if (mensaje.Contains(FalloDeConversion, StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(PlantillaSegunElTipo(mensaje), rotulo);
        }

        // El obligatorio que genera el marco de trabajo. Se reconoce por su texto porque llega ya
        // formateado, con el nombre del campo adentro.
        if (mensaje.Contains("field is required", StringComparison.OrdinalIgnoreCase) ||
            mensaje.Contains("is required", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(MensajesDeValidacion.Obligatorio, rotulo);
        }

        if (mensaje.Contains("must be a number", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(MensajesDeValidacion.DebeSerUnNumero, rotulo);
        }

        if (mensaje.StartsWith("The value", StringComparison.OrdinalIgnoreCase) ||
            mensaje.StartsWith("The field", StringComparison.OrdinalIgnoreCase) ||
            mensaje.StartsWith("The JSON", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(MensajesDeValidacion.ValorNoValido, rotulo);
        }

        // Todo lo demás ya lo redactó el proyecto, en español y con más precisión que cualquier
        // texto genérico: se devuelve tal cual.
        return mensaje;
    }

    private static string PlantillaSegunElTipo(string mensaje) => mensaje switch
    {
        _ when Menciona(mensaje, "Int32", "Int64", "Int16") => MensajesDeValidacion.DebeSerUnNumeroEntero,
        _ when Menciona(mensaje, "Decimal", "Double", "Single") => MensajesDeValidacion.DebeSerUnNumero,
        _ when Menciona(mensaje, "DateOnly", "DateTime", "TimeOnly") => MensajesDeValidacion.DebeSerUnaFecha,
        _ => MensajesDeValidacion.ValorNoValido,
    };

    private static bool Menciona(string mensaje, params string[] tipos) =>
        tipos.Any(t => mensaje.Contains(t, StringComparison.Ordinal));

    /// <summary>
    /// El nombre legible del campo a partir de la clave del estado del modelo. Las claves del
    /// deserializador vienen como <c>$.detalle[0].cantidad</c>: se toma el último tramo, que es el
    /// nombre que el cliente escribió en el cuerpo, y no la ruta completa.
    /// </summary>
    private static string Rotulo(string campo)
    {
        if (string.IsNullOrWhiteSpace(campo))
        {
            return "ingresado";
        }

        var ultimo = campo.Split('.').Last().Trim();

        if (string.IsNullOrWhiteSpace(ultimo))
        {
            return campo;
        }

        // Con inicial mayúscula: en el cuerpo el campo viaja en minúscula por convención del
        // contrato, pero el mensaje lo lee una persona.
        return char.ToUpperInvariant(ultimo[0]) + ultimo[1..];
    }
}

/// <summary>
/// Completa en español el mensaje de las validaciones que no traen uno propio, incluido el
/// obligatorio implícito de las propiedades no anulables (RF-035). Es la contraparte de la API del
/// proveedor equivalente de <c>Stock.Web</c>.
/// </summary>
public sealed class ValidacionEnEspanol : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext contexto)
    {
        var validaciones = contexto.ValidationMetadata.ValidatorMetadata;

        foreach (var validacion in validaciones)
        {
            if (validacion is RequiredAttribute obligatorio &&
                string.IsNullOrEmpty(obligatorio.ErrorMessage) &&
                string.IsNullOrEmpty(obligatorio.ErrorMessageResourceName))
            {
                obligatorio.ErrorMessage = MensajesDeValidacion.Obligatorio;
            }
        }

        // Los tipos de valor no anulables no traen el atributo en la lista: su obligatoriedad viene
        // marcada en los metadatos y el atributo lo sintetiza después el marco de trabajo, con su
        // texto en inglés. Se agrega acá con el mensaje ya puesto.
        if (contexto.ValidationMetadata.IsRequired == true &&
            !validaciones.OfType<RequiredAttribute>().Any())
        {
            validaciones.Add(new RequiredAttribute
            {
                ErrorMessage = MensajesDeValidacion.Obligatorio,
            });
        }
    }
}
