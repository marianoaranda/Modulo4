using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// Proyecciones de las respuestas de las dos consultas, declaradas del lado del test.
///
/// Se definen acá y no se reusan los DTO de la API a propósito: si el test deserializara con el
/// mismo tipo que serializa el controlador, un cambio de nombre de campo pasaría inadvertido y el
/// contrato de <c>openapi.yaml</c> dejaría de estar verificado.
/// </summary>
public sealed record FilaGenerarPedido(string Codigo, string Descripcion, int CantidadAPedir);

public sealed record ResultadoGenerarPedido(IReadOnlyList<FilaGenerarPedido> Filas, bool Truncado);

public sealed record FilaStockActual(string Codigo, string Descripcion, int Cantidad);

public sealed record ResultadoStockActual(IReadOnlyList<FilaStockActual> Filas, bool Truncado);

public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(cuerpo, Opciones)
            ?? throw new InvalidOperationException($"No se pudo deserializar la respuesta: {cuerpo}");
    }
}
