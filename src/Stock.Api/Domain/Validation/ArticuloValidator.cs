namespace Stock.Api.Domain.Validation;

/// <summary>
/// Un artículo tal como llega a la validación. Los tres parámetros de reposición ya son
/// <c>int</c>: el rechazo del no entero ocurrió antes, al deserializar (RF-018a).
/// </summary>
public sealed record ArticuloAValidar(
    string Codigo,
    string Descripcion,
    decimal PrecioCosto,
    decimal Margen,
    int StockMinimo,
    int PuntoPedido,
    int StockIdeal);

/// <summary>
/// T087 — Validación de artículo (RF-018, RF-019).
///
/// Duplica a propósito las reglas que el esquema ya impone con <c>CHECK</c>. No es redundancia
/// inútil: el <c>CHECK</c> garantiza que el dato malo no entre pase lo que pase, y esta capa
/// existe para poder devolver un 400 con un mensaje que diga <i>qué</i> está mal, en lugar de una
/// violación de restricción convertida en 500.
/// </summary>
public static class ArticuloValidator
{
    public static IReadOnlyList<ErrorDeValidacion> Validar(ArticuloAValidar articulo)
    {
        var errores = new List<ErrorDeValidacion>();

        if (string.IsNullOrWhiteSpace(articulo.Codigo))
        {
            errores.Add(new ErrorDeValidacion("codigo", "El Código es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(articulo.Descripcion))
        {
            errores.Add(new ErrorDeValidacion("descripcion", "La Descripción es obligatoria."));
        }

        // RF-018: prohíbe los negativos, no el cero.
        if (articulo.PrecioCosto < 0)
        {
            errores.Add(new ErrorDeValidacion("precioCosto", "El Precio de Costo no puede ser negativo."));
        }

        if (articulo.Margen < 0)
        {
            errores.Add(new ErrorDeValidacion("margen", "El Margen no puede ser negativo."));
        }

        if (articulo.StockMinimo < 0)
        {
            errores.Add(new ErrorDeValidacion("stockMinimo", "El Stock Mínimo no puede ser negativo."));
        }

        if (articulo.PuntoPedido < 0)
        {
            errores.Add(new ErrorDeValidacion("puntoPedido", "El Punto de Pedido no puede ser negativo."));
        }

        if (articulo.StockIdeal < 0)
        {
            errores.Add(new ErrorDeValidacion("stockIdeal", "El Stock Ideal no puede ser negativo."));
        }

        // RF-019. La igualdad se admite: los tres iguales es un caso válido del spec, y entonces
        // las tres modalidades de pedido arrojan el mismo resultado.
        if (articulo.StockMinimo >= 0 && articulo.PuntoPedido >= 0 && articulo.StockIdeal >= 0 &&
            !(articulo.StockMinimo <= articulo.PuntoPedido && articulo.PuntoPedido <= articulo.StockIdeal))
        {
            errores.Add(new ErrorDeValidacion(
                "stockMinimo",
                "Debe cumplirse Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal."));
        }

        return errores;
    }
}
