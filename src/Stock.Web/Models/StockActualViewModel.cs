namespace Stock.Web.Models;

public sealed class FilaStockViewModel
{
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public int Cantidad { get; set; }
}

/// <summary>Forma exacta de la respuesta de <c>GET /api/consultas/stock-actual</c>.</summary>
public sealed class RespuestaStockActual
{
    public List<FilaStockViewModel> Filas { get; set; } = [];

    public bool Truncado { get; set; }
}

/// <summary>
/// T078 — Estado de la Consulta de Stock Actual.
///
/// A diferencia de Generar Pedido, acá todos los parámetros son opcionales: sin rango ni filtro la
/// consulta devuelve el catálogo entero, sujeto al tope de RF-027. Por eso <c>Consultada</c> se
/// activa explícitamente y no se deduce de que haya parámetros.
/// </summary>
public sealed class StockActualViewModel
{
    public string? CodigoDesde { get; set; }

    public string? CodigoHasta { get; set; }

    public string? Descripcion { get; set; }

    public bool Consultada { get; set; }

    public List<FilaStockViewModel> Filas { get; set; } = [];

    public bool Truncado { get; set; }

    public bool MostrarMensajeDeResultadoVacio => Consultada && Filas.Count == 0;
}

/// <summary>
/// Respuesta de <c>GET /api/articulos/extremos</c>: el rango que la pantalla sugiere al abrirse
/// (RF-025b). Ambos son nulos con el catálogo vacío.
/// </summary>
public sealed class ExtremosViewModel
{
    public string? CodigoDesde { get; set; }

    public string? CodigoHasta { get; set; }
}
