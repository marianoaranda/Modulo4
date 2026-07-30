using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Tests.Integration;

/// <summary>
/// T018 — <c>vw_StockActual</c> es el único lugar del sistema donde se calcula el saldo
/// (Principio III). Verifica las dos propiedades que la definen: las compras suman y las ventas
/// restan, y el <c>LEFT JOIN</c> con <c>ISNULL(...,0)</c> hace que un artículo sin movimientos
/// aparezca con 0 en vez de desaparecer del resultado (RF-030).
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class VistaStockActualTests : IntegrationTestBase
{
    private async Task<int> SembrarArticuloAsync(string codigo)
    {
        await using var db = NuevoContexto();
        var articulo = new Articulo
        {
            Codigo = codigo,
            Descripcion = $"Artículo {codigo}",
            PrecioCosto = 10m,
            Margen = 0m,
            StockMinimo = 0,
            PuntoPedido = 0,
            StockIdeal = 0,
        };
        db.Articulos.Add(articulo);
        await db.SaveChangesAsync();
        return articulo.ArticuloId;
    }

    private async Task SembrarMovimientoAsync(TipoMovimiento tipo, int articuloId, int cantidad)
    {
        await using var db = NuevoContexto();
        db.Movimientos.Add(new Movimiento
        {
            Tipo = tipo,
            Fecha = new DateOnly(2026, 1, 15),
            Detalle =
            {
                new MovimientoDetalle
                {
                    ArticuloId = articuloId,
                    Cantidad = cantidad,
                    PrecioUnitario = 10m,
                },
            },
        });
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Un_articulo_sin_movimientos_aparece_con_stock_cero()
    {
        // RF-030: el artículo nuevo debe poder pedirse, así que no puede faltar del resultado.
        await SembrarArticuloAsync("A-001");

        await using var db = NuevoContexto();
        var fila = await db.StockActual.SingleAsync(f => f.Codigo == "A-001");

        Assert.That(fila.StockActual, Is.Zero);
    }

    [Test]
    public async Task El_stock_es_el_saldo_de_compras_menos_ventas()
    {
        var articuloId = await SembrarArticuloAsync("A-001");

        await SembrarMovimientoAsync(TipoMovimiento.Compra, articuloId, 100);
        await SembrarMovimientoAsync(TipoMovimiento.Compra, articuloId, 20);
        await SembrarMovimientoAsync(TipoMovimiento.Venta, articuloId, 30);

        await using var db = NuevoContexto();
        var fila = await db.StockActual.SingleAsync(f => f.Codigo == "A-001");

        Assert.That(fila.StockActual, Is.EqualTo(90));
    }

    [Test]
    public async Task Cada_articulo_agrega_solo_sus_propios_movimientos()
    {
        var primero = await SembrarArticuloAsync("A-001");
        var segundo = await SembrarArticuloAsync("A-002");
        await SembrarArticuloAsync("A-003");

        await SembrarMovimientoAsync(TipoMovimiento.Compra, primero, 10);
        await SembrarMovimientoAsync(TipoMovimiento.Compra, segundo, 7);
        await SembrarMovimientoAsync(TipoMovimiento.Venta, segundo, 2);

        await using var db = NuevoContexto();
        var porCodigo = await db.StockActual.ToDictionaryAsync(f => f.Codigo, f => f.StockActual);

        Assert.Multiple(() =>
        {
            Assert.That(porCodigo["A-001"], Is.EqualTo(10));
            Assert.That(porCodigo["A-002"], Is.EqualTo(5));
            Assert.That(porCodigo["A-003"], Is.Zero);
        });
    }
}
