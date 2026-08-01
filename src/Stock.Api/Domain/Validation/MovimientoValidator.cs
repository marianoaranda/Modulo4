using Stock.Api.Domain.Entities;

namespace Stock.Api.Domain.Validation;

/// <summary>Un rechazo de validación, con el campo que lo produjo para poder mostrarlo.</summary>
public sealed record ErrorDeValidacion(string Campo, string Mensaje);

/// <summary>
/// Una línea tal como llega a la validación: la <c>Cantidad</c> ya es <c>int</c>.
///
/// Nótese que <b>no incluye el artículo</b>, sólo su Código. Es deliberado: RF-023b prohíbe
/// validar el Precio Unitario contra el Precio de Costo o el de Venta del catálogo, y la forma más
/// fuerte de garantizarlo es que el validador no tenga acceso a esos datos. El Código es la
/// identidad de negocio de la línea (RF-020e) y se resuelve a su identificador interno recién en
/// el servicio, dentro de la transacción.
/// </summary>
public sealed record LineaAValidar(string Codigo, int Cantidad, decimal PrecioUnitario);

public sealed record MovimientoAValidar(
    TipoMovimiento Tipo, DateOnly Fecha, IReadOnlyList<LineaAValidar> Detalle);

/// <summary>
/// T072 — Validación de campo de un movimiento (RF-020b, RF-020d, RF-023, RF-023a, RF-023c).
///
/// Todo lo que rechaza acá es un <b>400</b>: entrada que viola una regla de validación de campo.
/// El invariante de stock, en cambio, es un <b>422</b> y lo evalúa <c>MovimientoService</c> dentro
/// de la transacción, porque depende del estado de la base y no del contenido de la solicitud.
///
/// Devuelve <b>todos</b> los errores y no el primero: quien carga un movimiento de diez líneas
/// prefiere enterarse de una vez de las tres que están mal.
/// </summary>
public static class MovimientoValidator
{
    public const int CantidadMaxima = 1_000_000;
    public const decimal PrecioUnitarioMaximo = 9_999_999.99m;
    public const decimal PrecioTotalMaximo = 999_999_999_999.99m;

    public static IReadOnlyList<ErrorDeValidacion> Validar(MovimientoAValidar movimiento, DateOnly hoy)
    {
        var errores = new List<ErrorDeValidacion>();

        // RF-020b: conjunto cerrado.
        if (movimiento.Tipo is not (TipoMovimiento.Compra or TipoMovimiento.Venta))
        {
            errores.Add(new ErrorDeValidacion(
                "tipo", "El Tipo de Movimiento debe ser Compra o Venta."));
        }

        // RF-020d. Se compara contra la fecha que recibe y no contra DateTime.Today: así la regla
        // es determinista y testeable sin depender de cuándo corra la suite.
        if (movimiento.Fecha > hoy)
        {
            errores.Add(new ErrorDeValidacion(
                "fecha", "La Fecha del Movimiento no puede ser posterior a la fecha actual."));
        }

        if (movimiento.Detalle.Count == 0)
        {
            errores.Add(new ErrorDeValidacion(
                "detalle", "El Movimiento debe tener al menos una línea de detalle."));
        }

        for (var i = 0; i < movimiento.Detalle.Count; i++)
        {
            var linea = movimiento.Detalle[i];
            var campo = $"detalle[{i}]";

            // RF-023: entero mayor que 0. El "entero" ya lo garantizó el borde de la solicitud
            // (RF-018a); acá se verifica el signo.
            if (linea.Cantidad <= 0)
            {
                errores.Add(new ErrorDeValidacion(
                    $"{campo}.cantidad", "La Cantidad debe ser un número entero mayor que 0."));
            }

            // RF-023a.
            if (linea.Cantidad > CantidadMaxima)
            {
                errores.Add(new ErrorDeValidacion(
                    $"{campo}.cantidad",
                    $"La Cantidad no puede superar {CantidadMaxima:N0} unidades."));
            }

            // RF-023c: el extremo inferior. El cero se admite (una bonificación).
            if (linea.PrecioUnitario < 0)
            {
                errores.Add(new ErrorDeValidacion(
                    $"{campo}.precioUnitario", "El Precio Unitario no puede ser negativo."));
            }

            // RF-023a.
            if (linea.PrecioUnitario > PrecioUnitarioMaximo)
            {
                errores.Add(new ErrorDeValidacion(
                    $"{campo}.precioUnitario",
                    $"El Precio Unitario no puede superar {PrecioUnitarioMaximo:N2}."));
            }

            // RF-023a, tercer límite. Es una regla propia y no un corolario de las dos anteriores:
            // cada factor puede estar dentro de su tope y el producto excederlo igual.
            if (linea.Cantidad > 0 &&
                linea.PrecioUnitario >= 0 &&
                (decimal)linea.Cantidad * linea.PrecioUnitario > PrecioTotalMaximo)
            {
                errores.Add(new ErrorDeValidacion(
                    $"{campo}.precioTotal",
                    $"El Precio Total de la línea no puede superar {PrecioTotalMaximo:N2}."));
            }
        }

        return errores;
    }
}
