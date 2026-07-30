using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// Utilidades compartidas por los tests de movimientos: siembra de artículos y armado de las
/// solicitudes del CRUD. Sin esto, cada archivo repetiría el mismo andamiaje y las diferencias
/// entre escenarios quedarían enterradas en el ruido.
/// </summary>
public abstract class MovimientosTestBase : IntegrationTestBase
{
    protected const string Movimientos = "/api/movimientos";

    protected async Task<int> SembrarArticuloAsync(
        string codigo, string descripcion = "Artículo de prueba")
    {
        await EjecutarSqlAsync($"""
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            VALUES ('{codigo}', N'{descripcion}', 100.00, 50, 0, 0, 0);
            """);

        return await EscalarAsync<int>($"SELECT ArticuloId FROM dbo.Articulo WHERE Codigo = '{codigo}'");
    }

    protected static object Linea(int articuloId, int cantidad, decimal precioUnitario = 10m) =>
        new { articuloId, cantidad, precioUnitario };

    protected static object Cuerpo(string tipo, params object[] detalle) =>
        new { tipo, fecha = "2026-01-15", detalle };

    protected Task<HttpResponseMessage> AltaAsync(string tipo, params object[] detalle) =>
        Client.PostAsJsonAsync(Movimientos, Cuerpo(tipo, detalle));

    protected Task<HttpResponseMessage> ModificarAsync(int numero, string tipo, params object[] detalle) =>
        Client.PutAsJsonAsync($"{Movimientos}/{numero}", Cuerpo(tipo, detalle));

    protected Task<HttpResponseMessage> BajaAsync(int numero) =>
        Client.DeleteAsync($"{Movimientos}/{numero}");

    /// <summary>Da de alta un movimiento que se espera válido y devuelve su Número asignado.</summary>
    protected async Task<int> AltaExitosaAsync(string tipo, params object[] detalle)
    {
        var respuesta = await AltaAsync(tipo, detalle);

        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"Se esperaba un alta exitosa y la API respondió {(int)respuesta.StatusCode}: " +
                        await respuesta.Content.ReadAsStringAsync());
        }

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.GetProperty("numero").GetInt32();
    }

    /// <summary>Saldo del artículo según <c>vw_StockActual</c>, la única fuente del stock.</summary>
    protected Task<int> StockDeAsync(int articuloId) =>
        EscalarAsync<int>($"SELECT StockActual FROM dbo.vw_StockActual WHERE ArticuloId = {articuloId}");

    protected Task<int> CantidadDeMovimientosAsync() =>
        EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Movimiento");

    protected Task<int> CantidadDeLineasAsync() =>
        EscalarAsync<int>("SELECT COUNT(*) FROM dbo.MovimientoDetalle");
}
