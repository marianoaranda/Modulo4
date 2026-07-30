namespace Stock.Api.Domain.Pedido;

/// <summary>
/// Modo de Pedido: determina hasta qué Nivel se repone (RF-026).
///
/// Es uno de los dos parámetros de reposición, y es obligatorio en cada ejecución: no tiene valor
/// por defecto, porque un default silencioso produciría una lista de pedido que el usuario no pidió
/// y no puede distinguir de la que sí (RF-026b). Por eso el enum no declara un miembro con valor 0.
/// </summary>
public enum ModoPedido
{
    HastaStockMinimo = 1,
    HastaPuntoPedido = 2,
    HastaStockIdeal = 3,
}
