namespace Stock.Api.Domain.Pedido;

/// <summary>
/// Los tres parámetros de reposición de un artículo. Enteros por RF-013a, lo que hace que la
/// Cantidad a Pedir resulte entera por construcción y no haga falta regla de redondeo.
/// </summary>
public readonly record struct ParametrosDeReposicion(int StockMinimo, int PuntoPedido, int StockIdeal);

/// <summary>
/// La regla de negocio central del módulo (RF-026).
///
/// Es una <b>función pura</b>: sin EF Core, sin ASP.NET, sin estado. Esa es la razón por la que
/// puede desarrollarse íntegramente test-first contra el Conjunto de Datos de Referencia del spec,
/// y por la que vive en <c>Domain/</c> y no en un servicio.
///
/// Nótese que acá no hay ninguna inferencia ni estimación: la "inferencia" de qué pedir es esta
/// resta determinista sobre movimientos reales. Toda cantidad es trazable a compras y ventas
/// registradas (Principio III).
/// </summary>
public static class PedidoCalculator
{
    /// <summary>Nivel objetivo de reposición según el Modo de Pedido.</summary>
    public static int Nivel(ModoPedido modo, ParametrosDeReposicion parametros) => modo switch
    {
        ModoPedido.HastaStockMinimo => parametros.StockMinimo,
        ModoPedido.HastaPuntoPedido => parametros.PuntoPedido,
        ModoPedido.HastaStockIdeal => parametros.StockIdeal,
        _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de Pedido no admitido."),
    };

    /// <summary>
    /// Con <c>soloBajoMinimo = No</c> se listan TODOS los artículos, incluidos los de cantidad 0:
    /// que un artículo aparezca con 0 es información, y es distinto de que no aparezca (RF-026).
    ///
    /// Con <c>Sí</c>, sólo los que están estrictamente por debajo del mínimo. La comparación es
    /// estricta, de modo que un artículo con Stock Mínimo 0 nunca entra —<c>0 &lt; 0</c> es falso—:
    /// es comportamiento esperado del spec, no un defecto.
    /// </summary>
    public static bool Incluir(bool soloBajoMinimo, ParametrosDeReposicion parametros, int stockActual) =>
        !soloBajoMinimo || stockActual < parametros.StockMinimo;

    /// <summary>
    /// <c>MAX(0, Nivel − Stock Actual)</c>.
    ///
    /// El <c>MAX(0, …)</c> es lo que garantiza que la cantidad nunca sea negativa (CE-003): un
    /// artículo con stock de sobra arroja 0, no un número negativo que se leería como "devolver
    /// mercadería". En la rama <c>soloBajoMinimo = Sí</c> es redundante —RF-019 garantiza
    /// StockMinimo ≤ Nivel y el filtro garantiza stock &lt; StockMinimo— pero se aplica igual por
    /// uniformidad.
    /// </summary>
    public static int CantidadAPedir(ModoPedido modo, ParametrosDeReposicion parametros, int stockActual) =>
        Math.Max(0, Nivel(modo, parametros) - stockActual);
}
