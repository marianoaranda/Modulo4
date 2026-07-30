using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Tests.Integration;

/// <summary>
/// T016 — Restricciones de <c>Articulo</c> verificadas <b>a nivel de base</b>, no en el servicio.
///
/// El esquema codifica reglas de negocio (RF-016 a RF-019), así que le corresponde su propio ciclo
/// rojo→verde: estos tests se escriben antes que <c>ArticuloConfiguration</c> y deben fallar contra
/// la migración desnuda de T029, cuando las tablas existen pero sin CHECK, sin columna calculada y
/// sin índice único.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class EsquemaArticuloTests : IntegrationTestBase
{
    private static Articulo NuevoArticulo(
        string codigo = "A-001",
        string descripcion = "Artículo de prueba",
        decimal precioCosto = 100m,
        decimal margen = 50m,
        int stockMinimo = 10,
        int puntoPedido = 20,
        int stockIdeal = 50) => new()
        {
            Codigo = codigo,
            Descripcion = descripcion,
            PrecioCosto = precioCosto,
            Margen = margen,
            StockMinimo = stockMinimo,
            PuntoPedido = puntoPedido,
            StockIdeal = stockIdeal,
        };

    [Test]
    public async Task PrecioVenta_lo_calcula_el_motor_a_partir_de_costo_y_margen()
    {
        // RF-016: PrecioVenta = PrecioCosto × (1 + Margen / 100), como columna calculada
        // PERSISTED. Al calcularla el motor es imposible que diverja de sus insumos.
        await using (var db = NuevoContexto())
        {
            db.Articulos.Add(NuevoArticulo(precioCosto: 100m, margen: 50m));
            await db.SaveChangesAsync();
        }

        await using (var db = NuevoContexto())
        {
            var articulo = await db.Articulos.SingleAsync();
            Assert.That(articulo.PrecioVenta, Is.EqualTo(150.00m));
        }
    }

    [Test]
    public async Task Codigo_duplicado_se_rechaza_por_el_indice_unico()
    {
        // RF-017.
        await using var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(codigo: "A-001"));
        await db.SaveChangesAsync();

        db.Articulos.Add(NuevoArticulo(codigo: "A-001"));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task Codigo_que_difiere_solo_en_mayusculas_es_el_mismo_Codigo()
    {
        // RF-017a: la unicidad usa la misma regla de comparación que el ordenamiento
        // (Modern_Spanish_CI_AS), insensible a mayúsculas. `A-001` y `a-001` colisionan.
        await using var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(codigo: "A-001"));
        await db.SaveChangesAsync();

        db.Articulos.Add(NuevoArticulo(codigo: "a-001"));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public async Task Codigos_que_diferen_en_un_acento_son_distintos()
    {
        // RF-017a, cara complementaria: la comparación es sensible a acentos, de modo que
        // dos códigos que sólo difieren en la tilde conviven sin violar la unicidad.
        await using var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(codigo: "PANO-1"));
        db.Articulos.Add(NuevoArticulo(codigo: "PAÑO-1"));

        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }

    [TestCase(-1, 20, 50, TestName = "StockMinimo negativo")]
    [TestCase(10, -1, 50, TestName = "PuntoPedido negativo")]
    [TestCase(10, 20, -1, TestName = "StockIdeal negativo")]
    public void Parametros_de_reposicion_negativos_se_rechazan(int minimo, int punto, int ideal)
    {
        // RF-018.
        var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(stockMinimo: minimo, puntoPedido: punto, stockIdeal: ideal));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public void PrecioCosto_negativo_se_rechaza()
    {
        // RF-018.
        var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(precioCosto: -1m));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public void Margen_negativo_se_rechaza()
    {
        // RF-018.
        var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(margen: -0.01m));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [TestCase(30, 20, 50, TestName = "StockMinimo mayor que PuntoPedido")]
    [TestCase(10, 60, 50, TestName = "PuntoPedido mayor que StockIdeal")]
    public void Se_rechaza_el_orden_incorrecto_de_los_tres_stocks(int minimo, int punto, int ideal)
    {
        // RF-019: CHECK (StockMinimo <= PuntoPedido AND PuntoPedido <= StockIdeal).
        var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(stockMinimo: minimo, puntoPedido: punto, stockIdeal: ideal));

        Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Test]
    public void Los_tres_stocks_iguales_se_aceptan()
    {
        // Caso límite del spec: Mínimo = Punto de Pedido = Ideal es válido por RF-019,
        // y hace que las tres modalidades de pedido arrojen el mismo resultado.
        var db = NuevoContexto();
        db.Articulos.Add(NuevoArticulo(stockMinimo: 7, puntoPedido: 7, stockIdeal: 7));

        Assert.DoesNotThrowAsync(async () => await db.SaveChangesAsync());
    }
}
