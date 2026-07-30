using Microsoft.AspNetCore.Mvc;
using Stock.Api.Domain.Pedido;
using Stock.Api.Export;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

/// <summary>
/// T055 — Las dos consultas de pantalla, y sus exportaciones (RF-025, RF-026, RF-031).
/// </summary>
[ApiController]
[Route("api/consultas")]
public class ConsultasController : ControllerBase
{
    private readonly GenerarPedidoQueryService _pedidos;
    private readonly ExcelExporter _excel;

    public ConsultasController(GenerarPedidoQueryService pedidos, ExcelExporter excel)
    {
        _pedidos = pedidos;
        _excel = excel;
    }

    [HttpGet("generar-pedido")]
    public async Task<IActionResult> GenerarPedido(
        [FromQuery] bool? soloBajoMinimo,
        [FromQuery] ModoPedido? modoPedido,
        [FromQuery] string? descripcion,
        CancellationToken ct)
    {
        // RF-026b: ambos parámetros de reposición son obligatorios y sin valor por defecto.
        //
        // Se declaran nullables y se validan a mano, en vez de confiar en el enlace de modelos:
        // para un parámetro de tipo valor no anulable, ASP.NET Core enlaza el default sin
        // reportar error, y eso es exactamente el "valor por defecto silencioso" que el requisito
        // prohíbe. El usuario recibiría una lista de pedido que no pidió y no podría distinguirla
        // de la que sí.
        if (soloBajoMinimo is null)
        {
            ModelState.AddModelError(
                nameof(soloBajoMinimo),
                "El parámetro de reposición 'soloBajoMinimo' es obligatorio.");
        }

        if (modoPedido is null)
        {
            ModelState.AddModelError(
                nameof(modoPedido),
                "El parámetro de reposición 'modoPedido' es obligatorio.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _pedidos.ConsultarAsync(
            soloBajoMinimo!.Value, modoPedido!.Value, descripcion, ct);

        return Ok(resultado);
    }

    [HttpGet("generar-pedido/excel")]
    public async Task<IActionResult> GenerarPedidoExcel(
        [FromQuery] bool? soloBajoMinimo,
        [FromQuery] ModoPedido? modoPedido,
        [FromQuery] string? descripcion,
        CancellationToken ct)
    {
        if (soloBajoMinimo is null || modoPedido is null)
        {
            ModelState.AddModelError(
                "parametrosDeReposicion",
                "Los parámetros de reposición 'soloBajoMinimo' y 'modoPedido' son obligatorios.");

            return ValidationProblem(ModelState);
        }

        // Se consulta con exactamente los mismos parámetros que la pantalla y se exportan las
        // filas ya recortadas: RF-031 se cumple por construcción (R-05).
        var resultado = await _pedidos.ConsultarAsync(
            soloBajoMinimo.Value, modoPedido.Value, descripcion, ct);

        var contenido = _excel.Exportar(
            "Generar Pedido",
            ["Código", "Descripción", "Cantidad a Pedir"],
            resultado.Filas,
            fila => [fila.Codigo, fila.Descripcion, fila.CantidadAPedir]);

        return File(contenido, ExcelExporter.TipoDeContenido, "generar-pedido.xlsx");
    }
}
