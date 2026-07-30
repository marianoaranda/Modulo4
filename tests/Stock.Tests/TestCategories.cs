namespace Stock.Tests;

/// <summary>
/// Categorías de prueba de R-10. Se usan como <c>[Category(TestCategories.Unit)]</c> para que el
/// nombre no se repita como cadena suelta en cada archivo.
/// </summary>
public static class TestCategories
{
    /// <summary>Lógica pura, sin base de datos.</summary>
    public const string Unit = "Unit";

    /// <summary>Contra el SQL Server real de docker compose, con base efímera por corrida.</summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Rendimiento con volumen (CE-002, CE-004). Excluida de la corrida por defecto
    /// por el <c>.runsettings</c>, porque siembra 10.000 artículos y 100.000 líneas.
    /// </summary>
    public const string Volumen = "Volumen";
}
