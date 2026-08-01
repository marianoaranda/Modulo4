using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;
using Stock.Web.Resources;

namespace Stock.Web.Services;

/// <summary>
/// T171 — Pone en español los mensajes de validación que genera el marco de trabajo (RF-035).
///
/// Va como proveedor de metadatos y no como un <c>ErrorMessage</c> escrito en cada propiedad de
/// cada modelo por un motivo concreto: la propiedad que alguien agregue mañana y se olvide de
/// anotar volvería al inglés sin que nada avise. Acá el mensaje se completa <b>a toda</b> validación
/// que no traiga uno propio, incluido el <b>obligatorio implícito</b> de las propiedades no
/// anulables — que es el que produce el "The Codigo field is required." de hoy y que ninguna
/// anotación del proyecto podría alcanzar, porque no la escribió nadie.
///
/// Sólo completa lo que falta: una validación con su mensaje propio queda intacta, de modo que los
/// mensajes de regla de negocio —ya en español y más específicos— no se pisen.
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

        // Los **tipos de valor** no anulables —un `decimal`, un `int`— no llegan acá con un
        // `RequiredAttribute` en la lista: su obligatoriedad viene marcada en los metadatos y el
        // atributo lo sintetiza el marco de trabajo más tarde, con su texto por omisión en inglés.
        // Agregarlo acá, ya con el mensaje puesto, es lo que hace que "El campo Precio de Costo es
        // obligatorio." también valga para ellos. Sin esto, el recorrido de arriba deja en español
        // los campos de texto y en inglés los numéricos, que fue exactamente lo que pasó.
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

/// <summary>
/// Pone en español el mensaje del obligatorio **en el momento de crear el validador del cliente**.
///
/// Hace falta además del proveedor de metadatos por un motivo que costó encontrar: para un tipo de
/// valor no anulable —un <c>decimal</c>, un <c>int</c>— el marco de trabajo no usa el atributo que
/// haya en los metadatos, sino que <b>sintetiza uno nuevo</b> al armar la validación del cliente, y
/// ese trae el texto por omisión en inglés. El resultado era una pantalla a medias: "El campo
/// Código es obligatorio." junto a "The Precio de Costo field is required.".
///
/// Este punto de extensión los alcanza a los dos, porque el atributo sintetizado también pasa por
/// acá antes de convertirse en los atributos <c>data-val-*</c>.
/// </summary>
public sealed class AdaptadorDeValidacionEnEspanol : IValidationAttributeAdapterProvider
{
    /// <summary>El proveedor estándar, que sigue armando el adaptador que corresponda.</summary>
    private readonly IValidationAttributeAdapterProvider _estandar = new ValidationAttributeAdapterProvider();

    public IAttributeAdapter? GetAttributeAdapter(
        ValidationAttribute atributo, IStringLocalizer? localizador)
    {
        if (atributo is RequiredAttribute &&
            string.IsNullOrEmpty(atributo.ErrorMessage) &&
            string.IsNullOrEmpty(atributo.ErrorMessageResourceName))
        {
            atributo.ErrorMessage = MensajesDeValidacion.Obligatorio;
        }

        return _estandar.GetAttributeAdapter(atributo, localizador);
    }
}

/// <summary>
/// Los mensajes de los fallos de <b>enlazado</b>: los que no vienen de una validación sino de que
/// el valor recibido no entra en el tipo del campo. Son los que alimentan, además, los atributos
/// <c>data-val-*</c> que usa la validación del cliente, de modo que un mismo rechazo se diga igual
/// se detecte donde se detecte (RF-035).
/// </summary>
public static class MensajesDeEnlazado
{
    public static void EnEspanol(MvcOptions opciones)
    {
        var proveedor = opciones.ModelBindingMessageProvider;

        proveedor.SetValueMustNotBeNullAccessor(
            _ => MensajesDeValidacion.Obligatorio.Replace("{0}", "requerido"));

        proveedor.SetMissingBindRequiredValueAccessor(
            campo => string.Format(MensajesDeValidacion.Obligatorio, campo));

        proveedor.SetValueMustBeANumberAccessor(
            campo => string.Format(MensajesDeValidacion.DebeSerUnNumero, campo));

        proveedor.SetNonPropertyValueMustBeANumberAccessor(
            () => string.Format(MensajesDeValidacion.DebeSerUnNumero, "ingresado"));

        proveedor.SetAttemptedValueIsInvalidAccessor(
            (valor, campo) => string.Format(MensajesDeValidacion.ValorNoValido, campo) +
                              $" Valor recibido: '{valor}'.");

        proveedor.SetNonPropertyAttemptedValueIsInvalidAccessor(
            valor => $"El valor '{valor}' no es válido.");

        proveedor.SetUnknownValueIsInvalidAccessor(
            campo => string.Format(MensajesDeValidacion.ValorNoValido, campo));

        proveedor.SetNonPropertyUnknownValueIsInvalidAccessor(
            () => "El valor ingresado no es válido.");

        proveedor.SetMissingKeyOrValueAccessor(
            () => "Falta un dato obligatorio.");

        proveedor.SetMissingRequestBodyRequiredValueAccessor(
            () => "La solicitud no trae cuerpo y es obligatorio.");
    }
}
