using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stock.Web.Services;

namespace Stock.Tests.Web;

/// <summary>
/// Base de los tests de la capa MVC (R-10).
///
/// Hospeda <c>Stock.Web</c> in-process y <b>simula la API</b> con un manejador programable, en vez
/// de levantar <c>Stock.Api</c>: lo que se verifica acá es el comportamiento propio del front
/// —armado de la solicitud, render de la vista, propagación del error, manejo del 401— y no la
/// regla de negocio, que ya tiene sus propios tests de integración contra la base real.
///
/// Todavía <b>sin sesión simulada</b>: en esta fase la app web no exige autenticación. El fixture
/// de sesión lo agrega T105a, en paralelo exacto con lo que T100 hace del lado de la API.
/// </summary>
public abstract class WebTestBase
{
    protected ApiSimulada Api { get; private set; } = null!;

    protected WebApplicationFactory<Stock.Web.Program> Factory { get; private set; } = null!;

    [SetUp]
    public void LevantarAppWeb()
    {
        Api = new ApiSimulada();

        Factory = new WebApplicationFactory<Stock.Web.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("StockApi:BaseUrl", "http://api-simulada.local");

                builder.ConfigureServices(services =>
                {
                    // Reemplaza el manejador de red del HttpClient tipado por el simulado, de modo
                    // que el cliente conserve su configuración real (dirección base, y más
                    // adelante el BearerTokenHandler) pero no salga a la red.
                    services.AddHttpClient<StockApiClient>(cliente =>
                        cliente.BaseAddress = new Uri("http://api-simulada.local"))
                        .ConfigurePrimaryHttpMessageHandler(() => Api);
                });
            });
    }

    [TearDown]
    public void DerribarAppWeb()
    {
        Factory?.Dispose();
        Api?.Dispose();
    }

    protected HttpClient NuevoCliente(bool seguirRedirecciones = false) =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = seguirRedirecciones,
        });
}

/// <summary>
/// Doble de <c>Stock.Api</c>: devuelve lo que el test programe y registra lo que recibió, para
/// poder asertar tanto el render como la solicitud saliente.
/// </summary>
public sealed class ApiSimulada : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _recibidas = [];

    private Func<HttpRequestMessage, HttpResponseMessage> _responder =
        _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };

    public IReadOnlyList<HttpRequestMessage> Recibidas => _recibidas;

    public HttpRequestMessage UltimaSolicitud =>
        _recibidas.Count > 0
            ? _recibidas[^1]
            : throw new InvalidOperationException("La app web no llamó a la API.");

    public void Responder(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public void ResponderJson(string json, System.Net.HttpStatusCode estado = System.Net.HttpStatusCode.OK) =>
        Responder(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    public void ResponderProblema(System.Net.HttpStatusCode estado, string detalle) =>
        Responder(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(
                $$"""{"status":{{(int)estado}},"detail":{{System.Text.Json.JsonSerializer.Serialize(detalle)}}}""",
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _recibidas.Add(request);
        return Task.FromResult(_responder(request));
    }
}
