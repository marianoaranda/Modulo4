namespace Stock.Web.Services;

/// <summary>
/// Cliente tipado contra <c>Stock.Api</c>.
///
/// <c>Stock.Web</c> no accede a la base de negocio: toda regla vive detrás de esta API y la capa
/// MVC es un consumidor más. La única excepción, acotada y registrada en Complexity Tracking, es la
/// escritura de la bitácora de errores (R-08), que no pasa por acá.
///
/// Los métodos concretos se agregan con cada historia; esta clase existe desde la fase fundacional
/// para fijar el punto de acceso y el registro del <c>HttpClient</c>.
/// </summary>
public class StockApiClient
{
    private readonly HttpClient _http;

    public StockApiClient(HttpClient http) => _http = http;

    /// <summary>
    /// Acceso al <c>HttpClient</c> subyacente, ya configurado con la dirección base y el
    /// <c>DelegatingHandler</c> que adjunta el token.
    /// </summary>
    public HttpClient Http => _http;
}
