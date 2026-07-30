using System.Text.Json.Serialization;

namespace Stock.Web.Models;

/// <summary>Modo de Pedido, replicado del contrato de la API (RF-026).</summary>
public enum ModoPedidoWeb
{
    HastaStockMinimo = 1,
    HastaPuntoPedido = 2,
    HastaStockIdeal = 3,
}

public sealed class FilaPedidoViewModel
{
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("cantidadAPedir")]
    public int CantidadAPedir { get; set; }
}

/// <summary>
/// Forma exacta de la respuesta de <c>GET /api/consultas/generar-pedido</c>.
/// </summary>
public sealed class RespuestaGenerarPedido
{
    public List<FilaPedidoViewModel> Filas { get; set; } = [];

    public bool Truncado { get; set; }
}

/// <summary>
/// T056 — Estado de la pantalla de Generar Pedido.
///
/// <c>Consultada</c> distingue el primer ingreso —donde no hay que mostrar ningún mensaje— de una
/// consulta ejecutada que no arrojó filas, que sí debe mostrar el mensaje de RF-032. Sin esa
/// distinción, la pantalla saludaría al usuario diciéndole que su búsqueda no tuvo resultados
/// antes de que buscara nada.
/// </summary>
public sealed class GenerarPedidoViewModel
{
    public bool? SoloBajoMinimo { get; set; }

    public ModoPedidoWeb? ModoPedido { get; set; }

    public string? Descripcion { get; set; }

    public bool Consultada { get; set; }

    public List<FilaPedidoViewModel> Filas { get; set; } = [];

    public bool Truncado { get; set; }

    public bool MostrarMensajeDeResultadoVacio => Consultada && Filas.Count == 0;
}
