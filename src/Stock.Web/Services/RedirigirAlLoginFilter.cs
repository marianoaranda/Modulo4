using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Stock.Web.Services;

/// <summary>
/// Convierte una <see cref="SesionVencidaException"/> en una redirección al login.
///
/// Va como filtro y no como <c>try/catch</c> en cada acción porque el token puede vencer en
/// cualquier llamada de cualquier pantalla, y la reacción tiene que ser siempre la misma.
///
/// No es la bitácora de errores: una sesión vencida es un resultado previsto del sistema, no un
/// error de ejecución, y por lo tanto <b>no</b> se registra en <c>ErrorLog</c> (R-08).
/// </summary>
public class RedirigirAlLoginFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not SesionVencidaException)
        {
            return;
        }

        context.ExceptionHandled = true;
        context.Result = new RedirectToActionResult("Login", "Cuenta", null);
    }
}
