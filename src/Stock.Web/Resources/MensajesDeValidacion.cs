namespace Stock.Web.Resources;

/// <summary>
/// Las plantillas de los mensajes de validación que **no escribe nadie**: los que el marco de
/// trabajo genera por un campo obligatorio vacío o por un valor de otro tipo, y que por omisión
/// están en inglés (RF-035).
///
/// El texto está fijado al carácter en el spec para que sea verificable, y <c>{0}</c> es el rótulo
/// de negocio del campo —el mismo que la pantalla muestra en su etiqueta—, nunca el nombre interno
/// de la propiedad.
///
/// Es una copia deliberada de las constantes equivalentes de <c>Stock.Api</c>, por el mismo motivo
/// que <see cref="LimitesDeConsulta"/>: los dos proyectos son ejecutables independientes y no
/// comparten ensamblado. Los mensajes de regla de negocio no viven acá: los redacta quien impone la
/// regla, ya están en español y son más específicos que estos dos.
/// </summary>
public static class MensajesDeValidacion
{
    public const string Obligatorio = "El campo {0} es obligatorio.";

    public const string DebeSerUnNumero = "El campo {0} debe ser un número.";

    public const string ValorNoValido = "El valor ingresado en el campo {0} no es válido.";
}
