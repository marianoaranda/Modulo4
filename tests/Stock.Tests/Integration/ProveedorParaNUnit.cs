using Microsoft.Extensions.Logging;

namespace Stock.Tests.Integration;

/// <summary>
/// Redirige los registros de la API hospedada in-process a la salida de NUnit, filtrando a
/// advertencias y errores.
///
/// Existe por una razón concreta de diagnóstico: cuando la API lanza una excepción no controlada,
/// el middleware la convierte en un 500 con cuerpo genérico —que es lo correcto de cara al
/// usuario— y el test sólo ve ese 500. Sin este puente, averiguar qué falló obliga a instrumentar
/// el código de producción a mano en cada investigación.
/// </summary>
public sealed class ProveedorParaNUnit : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LoggerParaNUnit(categoryName);

    public void Dispose()
    {
    }

    private static readonly object Candado = new();

    private sealed class LoggerParaNUnit : ILogger
    {
        private readonly string _categoria;

        public LoggerParaNUnit(string categoria) => _categoria = categoria;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var texto = $"[{logLevel}] {_categoria}: {formatter(state, exception)}"
                + (exception is null ? string.Empty : Environment.NewLine + exception);

            TestContext.Progress.WriteLine(texto);

            // El corredor de pruebas no siempre muestra lo que va a Progress. Cuando se define
            // STOCK_TEST_LOG, se deja además en un archivo, que es lo que hace diagnosticable un
            // 500 sin tener que instrumentar el código de producción.
            var archivo = Environment.GetEnvironmentVariable("STOCK_TEST_LOG");

            if (!string.IsNullOrWhiteSpace(archivo))
            {
                lock (Candado)
                {
                    File.AppendAllText(archivo, texto + Environment.NewLine);
                }
            }
        }
    }
}
