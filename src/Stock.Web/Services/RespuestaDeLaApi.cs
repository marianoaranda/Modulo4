using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stock.Web.Services;

/// <summary>
/// Traducción de las respuestas de error de la API a algo que la vista pueda mostrar.
///
/// La API devuelve <c>application/problem+json</c> (RFC 7807) con un <c>detail</c> apto para el
/// usuario final. La capa web lo extrae tal cual y no lo reescribe: el mensaje ya viene redactado
/// desde donde vive la regla, así que reformularlo acá sólo abriría la puerta a que las dos
/// versiones digan cosas distintas.
/// </summary>
public static class RespuestaDeLaApi
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,

        // La API serializa el Tipo de Movimiento como texto —"Compra" / "Venta"—, que es lo que
        // fija el contrato y lo que hace legible la respuesta. Sin este convertidor, cualquier
        // pantalla que lea un movimiento ya grabado (el listado, la edición, la confirmación de
        // baja) rompe al deserializar. Se descubrió con el test de la pantalla de edición: los
        // tests anteriores devolvían listas vacías y nunca llegaban a leer un Tipo.
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<string> LeerDetalleDelProblemaAsync(
        HttpResponseMessage respuesta, CancellationToken ct = default)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return MensajeGenerico(respuesta);
        }

        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            var raiz = documento.RootElement;

            if (raiz.TryGetProperty("detail", out var detalle) &&
                detalle.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detalle.GetString()))
            {
                return detalle.GetString()!;
            }

            // Los 400 de validación traen los mensajes en `errors`, agrupados por campo.
            if (raiz.TryGetProperty("errors", out var errores) &&
                errores.ValueKind == JsonValueKind.Object)
            {
                var mensajes = errores.EnumerateObject()
                    .SelectMany(campo => campo.Value.EnumerateArray())
                    .Select(m => m.GetString())
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();

                if (mensajes.Count > 0)
                {
                    return string.Join(" ", mensajes);
                }
            }
        }
        catch (JsonException)
        {
            // Una respuesta de error que no sea JSON no debería tumbar la pantalla de carga.
        }

        return MensajeGenerico(respuesta);
    }

    public static async Task<T?> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct = default)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        return string.IsNullOrWhiteSpace(cuerpo)
            ? default
            : JsonSerializer.Deserialize<T>(cuerpo, Opciones);
    }

    private static string MensajeGenerico(HttpResponseMessage respuesta) =>
        $"La operación no pudo completarse (código {(int)respuesta.StatusCode}).";
}
