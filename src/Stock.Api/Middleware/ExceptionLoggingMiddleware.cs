using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Middleware;

/// <summary>
/// T125 — Manejador global de excepciones de la API (RF-028, CE-008).
///
/// Registra <b>sólo errores de ejecución no controlados</b>. Los rechazos de negocio esperados
/// —stock insuficiente, código duplicado, contraseña corta, rango inválido— llegan acá como
/// respuestas 4xx ya construidas por los controladores y nunca como excepciones, de modo que la
/// bitácora no se llena de resultados previstos. Si lo hiciera, los errores reales quedarían
/// sepultados entre rechazos normales.
///
/// Al usuario le devuelve un mensaje genérico: el detalle va a la bitácora, no al cliente.
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
        catch (Exception excepcion)
        {
            _log.LogError(excepcion, "Error de ejecución no controlado.");

            await RegistrarAsync(servicios, excepcion, contexto.RequestAborted);

            if (contexto.Response.HasStarted)
            {
                // Ya se empezó a escribir la respuesta: no se puede reemplazar por un problem+json
                // sin corromperla. El error igual quedó registrado, que es lo que exige CE-008.
                throw;
            }

            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Response.ContentType = "application/problem+json";

            await contexto.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                Detail = "Ocurrió un error inesperado. El incidente quedó registrado.",
            });
        }
    }

    /// <summary>
    /// Escribe con <c>ErrorLogDbContext</c>, que abre su propia conexión fuera de la transacción
    /// que está fallando. Un fallo al registrar no puede propagarse: dejaría al usuario sin
    /// respuesta por culpa del diagnóstico.
    /// </summary>
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
            _log.LogError(alRegistrar, "No se pudo escribir en la bitácora de errores.");
        }
    }
}
