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
/// T105a agregó el <b>fixture de sesión</b>: <see cref="ClienteConSesion"/> emite la cookie de
/// autenticación con el JWT de prueba ya adentro, de modo que cada test elija explícitamente si
/// corre con sesión o sin ella. Es la contraparte web de lo que T100 hace del lado de la API, y
/// existe porque T105b registró el filtro de autorización global: sin el fixture, toda pantalla
/// respondería una redirección al login.
/// </summary>
public abstract class WebTestBase
{
    /// <summary>
    /// JWT que la sesión simulada transporta. No se valida en la capa web —la valida la API— así
    /// que alcanza con que lleve los claims que el menú consulta.
    /// </summary>
    /// <remarks>
    /// Sin puntos a propósito: es el tercer segmento del JWT, y un valor con puntos produciría un
    /// token de cinco segmentos que la capa web no podría leer para sacarle el claim
    /// <c>es_admin</c>.
    /// </remarks>
    protected const string TokenDePrueba = "firmaDePrueba";

    protected const string UsuarioDePrueba = "admin";

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

    /// <summary>
    /// Cliente con sesión iniciada.
    ///
    /// La sesión se obtiene haciendo el <b>login real</b> contra la pantalla, con la API simulada
    /// devolviendo un token: así la cookie que queda es la que la aplicación emite de verdad, con
    /// su cifrado y sus claims, y no una fabricada por el test que podría diferir de la real justo
    /// en lo que se quiere verificar.
    ///
    /// Al terminar restaura la respuesta programada y olvida la llamada de login, para que el test
    /// asierte sobre su propia solicitud y no sobre la del fixture.
    /// </summary>
    protected HttpClient ClienteConSesion(bool esAdmin = true)
    {
        var cliente = NuevoCliente();
        var respuestaOriginal = Api.RespuestaProgramada;

        Api.ResponderJson($$"""
            {
              "token": "{{JwtDePrueba(esAdmin)}}",
              "expiraEn": "2099-01-01T00:00:00+00:00",
              "perfil": "administrador"
            }
            """);

        var login = cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Usuario"] = UsuarioDePrueba,
                ["Password"] = "NoImporta1",
            })).GetAwaiter().GetResult();

        if (login.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"El fixture de sesión no pudo iniciar sesión: la app respondió {(int)login.StatusCode}.");
        }

        Api.Responder(respuestaOriginal);
        Api.Olvidar();

        return cliente;
    }

    /// <summary>
    /// JWT sin firmar de verdad: la capa web no lo valida —eso lo hace la API— pero sí le lee el
    /// claim <c>es_admin</c> para decidir qué entradas de menú mostrar.
    /// </summary>
    protected static string JwtDePrueba(bool esAdmin = true)
    {
        static string Base64Url(string json) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var cabecera = Base64Url("""{"alg":"HS256","typ":"JWT"}""");
        var cuerpo = Base64Url($$"""
            {"name":"admin","role":"administrador","es_admin":"{{(esAdmin ? "true" : "false")}}"}
            """);

        return $"{cabecera}.{cuerpo}.{TokenDePrueba}";
    }
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

    /// <summary>La respuesta programada, para poder guardarla y restaurarla.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage> RespuestaProgramada => _responder;

    /// <summary>Olvida lo recibido hasta ahora, para que el fixture no contamine las aserciones.</summary>
    public void Olvidar() => _recibidas.Clear();

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
