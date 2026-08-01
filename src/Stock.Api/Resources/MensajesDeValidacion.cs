namespace Stock.Api.Resources;

/// <summary>
/// Las plantillas de los mensajes de validación que genera el marco de trabajo (RF-035, RF-035a).
///
/// Copia deliberada de las de <c>Stock.Web</c>: los dos proyectos son ejecutables independientes y
/// no comparten ensamblado. Acá hacen falta igual porque la API también rechaza cuerpos incompletos
/// —y es su respuesta la que la pantalla de carga muestra tal cual—, además del rechazo del
/// deserializador que fija RF-035a, que no existe del lado de la web.
/// </summary>
public static class MensajesDeValidacion
{
    public const string Obligatorio = "El campo {0} es obligatorio.";

    public const string DebeSerUnNumero = "El campo {0} debe ser un número.";

    public const string DebeSerUnNumeroEntero = "El campo {0} debe ser un número entero.";

    public const string DebeSerUnaFecha = "El campo {0} debe ser una fecha válida.";

    public const string ValorNoValido = "El valor ingresado en el campo {0} no es válido.";
}
