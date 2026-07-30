namespace Stock.Web.Resources;

/// <summary>
/// Los dos mensajes informativos de las consultas, con el <b>texto literal</b> que fijan RF-032 y
/// RF-032a.
///
/// Viven acá y no incrustados en cada vista por dos razones. La primera es que las dos pantallas
/// —Generar Pedido y Consulta de Stock Actual— tienen que decir exactamente lo mismo: si cada
/// vista trajera su propia copia, alcanzaría con que alguien corrigiera una para que divergieran.
/// La segunda es que el spec fija el texto <i>al carácter</i> para que sea verificable: el test
/// asierta esta constante contra la cadena del spec, y las vistas la consumen, de modo que vista y
/// test no puedan separarse.
///
/// Ambos son mensajes <b>informativos</b>, no errores, y son distinguibles entre sí.
/// </summary>
public static class MensajesDeConsulta
{
    /// <summary>RF-032 — se muestra en el lugar de la grilla cuando no hay filas.</summary>
    public const string ResultadoVacio = "No hay artículos que cumplan los criterios de la consulta.";

    /// <summary>RF-032a — se muestra junto a la grilla cuando se alcanzó el tope de RF-027.</summary>
    public const string ResultadoRecortado =
        "Se muestran las primeras 10.000 filas. Acote la búsqueda con el filtro por descripción.";
}
