using Stock.Web.Data;
using Stock.Web.Services;

namespace Stock.Web.Middleware;

/// <summary>
/// T126 — Manejador global de excepciones de la capa MVC (RF-028, CE-008).
///
/// El middleware de <c>Stock.Api</c> no ve estas excepciones: nunca lo atraviesan. Como RF-028 y
/// CE-008 exigen el 100% de los errores registrados, sin este manejador habría una clase entera de
/// errores invisible — y el sistema parecería estar registrando todo mientras pierde en silencio
/// los fallos del front.
///
/// No registra la <see cref="SesionVencidaException"/>: un token vencido es un resultado previsto
/// que se resuelve redirigiendo al login, no un fallo. Registrarlo llenaría la bitácora de ruido
/// operativo y haría que los errores reales se pierdan entre él.
/// </summary>
public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ExceptionLoggingMiddleware> _log;

    public ExceptionLoggingMiddleware(
        RequestDelegate siguiente, ILogger<ExceptionLoggingMiddleware> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto, IServiceProvider servicios)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (SesionVencidaException)
        {
            // El filtro de MVC ya la convirtió en redirección cuando venía de una acción. Si llega
            // hasta acá es que ocurrió fuera del pipeline de MVC: se redirige igual, sin registrar.
            contexto.Response.Redirect("/Cuenta/Login");
        }
        catch (Exception excepcion)
        {
            _log.LogError(excepcion, "Error de ejecución no controlado en la capa web.");

            await RegistrarAsync(servicios, excepcion, contexto.RequestAborted);

            throw;
        }
    }

    private async Task RegistrarAsync(
        IServiceProvider servicios, Exception excepcion, CancellationToken ct)
    {
        try
        {
            await using var scope = servicios.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErrorLogDbContext>();

            db.ErrorLogs.Add(new ErrorLog
            {
                ErrorDateTime = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                Message = excepcion.Message,
                FullException = excepcion.ToString(),
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception alRegistrar)
        {
            // Un fallo al registrar no puede tapar el error original ni dejar al usuario sin
            // respuesta por culpa del diagnóstico.
            _log.LogError(alRegistrar, "No se pudo escribir en la bitácora de errores.");
        }
    }
}
