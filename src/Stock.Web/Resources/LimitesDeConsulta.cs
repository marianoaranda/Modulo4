namespace Stock.Web.Resources;

/// <summary>
/// El tope de filas de RF-027, del lado del front.
///
/// <b>Es una copia deliberada</b> del que aplica la API, no la fuente de verdad: los dos proyectos
/// son ejecutables independientes y no comparten ensamblado, y quien recorta el resultado es
/// siempre el motor. Acá el número se usa para una sola cosa: detectar que un listado volvió
/// completo hasta el tope y, por lo tanto, hay que avisar del recorte (RF-032a). El día que el
/// tope cambie, cambia en los dos lados o el aviso deja de aparecer — que es un defecto visible,
/// no un recorte silencioso.
/// </summary>
public static class LimitesDeConsulta
{
    public const int TopeDeFilas = 10_000;
}
