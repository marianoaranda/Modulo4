using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Tests.Integration;

/// <summary>
/// T017 — Restricciones de <c>MovimientoDetalle</c> a nivel de base.
///
/// Cubre RF-014a (NO ACTION hacia Articulo), RF-020c (PrecioTotal calculado), RF-021 (cascada
/// desde el encabezado), RF-023 y RF-023a (rango de Cantidad) y RF-023c (signo del Precio
/// Unitario). Igual que T016, se escribe antes de las configuraciones y debe fallar contra la
/// migración desnuda.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class EsquemaMovimientoTests : IntegrationTestBase
{
    private async Task<int> SembrarArticuloAsync(string codigo = "A-001")
    {
        await using var db = NuevoContexto();
        var articulo = new Articulo
        {
            Codigo = codigo,
            Descripcion = "Artículo de prueba",
            PrecioCosto = 100m,
            Margen = 50m,
            StockMinimo = 0,
            PuntoPedido = 0,
            StockIdeal = 0,
        };
        db.Articulos.Add(articulo);
        await db.SaveChangesAsync();
        return articulo.ArticuloId;
    }

    private async Task<int> SembrarMovimientoAsync(
        TipoMovimiento tipo = TipoMovimiento.Compra)
    {
        await using var db = NuevoContexto();
        var movimiento = new Movimiento
        {
            Tipo = tipo,
            Fecha = new DateOnly(2026, 1, 15),
        };
        db.Movimientos.Add(movimiento);
        await db.SaveChangesAsync();
        return movimiento.Numero;
    }

    [Test]
    public async Task PrecioTotal_lo_calcula_el_motor_como_cantidad_por_precio_unitario()
    {
        // RF-020c: el Precio Total no lo carga el usuario.
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        await using (var db = NuevoContexto())
        {
            db.MovimientoDetalles.Add(new MovimientoDetalle
            {
                MovimientoNumero = numero,
                ArticuloId = articuloId,
                Cantidad = 3,
                PrecioUnitario = 12.50m,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuevoContexto())
        {
            var detalle = await db.MovimientoDetalles.SingleAsync();
            Assert.That(detalle.PrecioTotal, Is.EqualTo(37.50m));
        }
    }

    [TestCase(0, TestName = "Cantidad cero")]
    [TestCase(-1, TestName = "Cantidad negativa")]
    [TestCase(1_000_001, TestName = "Cantidad por encima del tope de 1.000.000")]
    public async Task Cantidad_fuera_del_rango_admitido_se_rechaza(int cantidad)
    {
        // RF-023 (entero > 0) y RF-023a (tope de 1.000.000 de unidades).
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        var db = NuevoContexto();
        db.MovimientoDetalles.Add(new MovimientoDetalle
        {
            MovimientoNumero = numero,
            ArticuloId = articuloId,
            Cantidad = cantidad,
            PrecioUnitario = 10m,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task PrecioUnitario_negativo_se_rechaza()
    {
        // RF-023c: el precio de una operación real nunca es menor que cero.
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        var db = NuevoContexto();
        db.MovimientoDetalles.Add(new MovimientoDetalle
        {
            MovimientoNumero = numero,
            ArticuloId = articuloId,
            Cantidad = 1,
            PrecioUnitario = -0.01m,
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task PrecioUnitario_cero_se_acepta()
    {
        // RF-023c, extremo inferior inclusive: una bonificación es un precio unitario 0 válido.
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        var db = NuevoContexto();
        db.MovimientoDetalles.Add(new MovimientoDetalle
        {
            MovimientoNumero = numero,
            ArticuloId = articuloId,
            Cantidad = 1,
            PrecioUnitario = 0m,
        });

        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task Borrar_el_encabezado_arrastra_su_detalle_en_cascada()
    {
        // RF-021: la baja del movimiento es de encabezado y detalle.
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        await using (var db = NuevoContexto())
        {
            db.MovimientoDetalles.Add(new MovimientoDetalle
            {
                MovimientoNumero = numero,
                ArticuloId = articuloId,
                Cantidad = 5,
                PrecioUnitario = 10m,
            });
            await db.SaveChangesAsync();
        }

        // Se borra por SQL directo para verificar la cascada del motor y no la que EF Core
        // podría simular en memoria siguiendo sus propias reglas de seguimiento.
        await EjecutarSqlAsync($"DELETE FROM dbo.Movimiento WHERE Numero = {numero}");

        await using (var db = NuevoContexto())
        {
            Assert.That(await db.MovimientoDetalles.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task No_se_puede_borrar_un_articulo_con_detalle_asociado()
    {
        // RF-014a: baja restringida. La FK hacia Articulo es NO ACTION, de modo que el histórico
        // de movimientos y el Stock Actual derivado se preservan íntegros.
        var articuloId = await SembrarArticuloAsync();
        var numero = await SembrarMovimientoAsync();

        await using (var db = NuevoContexto())
        {
            db.MovimientoDetalles.Add(new MovimientoDetalle
            {
                MovimientoNumero = numero,
                ArticuloId = articuloId,
                Cantidad = 5,
                PrecioUnitario = 10m,
            });
            await db.SaveChangesAsync();
        }

        Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
            await EjecutarSqlAsync($"DELETE FROM dbo.Articulo WHERE ArticuloId = {articuloId}"));

        await using (var final = NuevoContexto())
        {
            Assert.That(await final.Articulos.CountAsync(), Is.EqualTo(1));
        }
    }

    [TestCase(0, TestName = "Tipo 0 no admitido")]
    [TestCase(3, TestName = "Tipo 3 no admitido")]
    public void Tipo_de_movimiento_fuera_del_conjunto_cerrado_se_rechaza(int tipo)
    {
        // RF-020b: el Tipo admite exclusivamente Compra (1) y Venta (2).
        Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
            await EjecutarSqlAsync(
                $"INSERT INTO dbo.Movimiento (Tipo, Fecha) VALUES ({tipo}, '2026-01-15')"));
    }
}
