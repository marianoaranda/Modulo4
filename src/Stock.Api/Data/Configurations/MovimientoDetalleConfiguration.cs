using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data.Configurations;

/// <summary>
/// T033 — Línea de detalle: el punto donde el esquema protege el saldo.
/// </summary>
public class MovimientoDetalleConfiguration : IEntityTypeConfiguration<MovimientoDetalle>
{
    public void Configure(EntityTypeBuilder<MovimientoDetalle> builder)
    {
        builder.Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");

        // RF-020c: lo calcula el sistema, no lo carga el usuario.
        builder.Property(d => d.PrecioTotal)
            .HasColumnType("decimal(18,2)")
            .HasComputedColumnSql(
                "CAST([Cantidad] * [PrecioUnitario] AS decimal(18,2))",
                stored: true);

        // RF-021: la baja del movimiento arrastra su detalle.
        builder.HasOne(d => d.Movimiento)
            .WithMany(m => m.Detalle)
            .HasForeignKey(d => d.MovimientoNumero)
            .OnDelete(DeleteBehavior.Cascade);

        // RF-014a: baja restringida del artículo. NO ACTION preserva el histórico de movimientos
        // y, con él, el Stock Actual derivado. El servicio verifica antes para devolver un 409
        // legible en vez de una violación de FK.
        builder.HasOne(d => d.Articulo)
            .WithMany(a => a.Detalle)
            .HasForeignKey(d => d.ArticuloId)
            .OnDelete(DeleteBehavior.NoAction);

        // Índice de cobertura que sostiene la agregación de vw_StockActual (R-01). Es lo que hace
        // que calcular el saldo en cada consulta —en vez de persistirlo— quepa holgadamente en el
        // presupuesto de 3 s de CE-002.
        builder.HasIndex(d => d.ArticuloId)
            .HasDatabaseName("IX_MovimientoDetalle_ArticuloId")
            .IncludeProperties(d => new { d.Cantidad, d.MovimientoNumero });

        builder.HasIndex(d => d.MovimientoNumero)
            .HasDatabaseName("IX_MovimientoDetalle_MovimientoNumero");

        builder.ToTable(t =>
        {
            // RF-023 (entero > 0) y RF-023a (tope de 1.000.000 de unidades). El esquema protege el
            // signo y el rango de la Cantidad, donde un valor absurdo corrompería el saldo.
            t.HasCheckConstraint(
                "CK_MovimientoDetalle_Cantidad",
                "[Cantidad] > 0 AND [Cantidad] <= 1000000");

            // RF-023c: acota el signo, no vincula el precio al catálogo (RF-023b). El cero se
            // admite: una bonificación es una operación real. Los techos monetarios de RF-023a,
            // en cambio, viven sólo en MovimientoValidator: son política de negocio, más propensa
            // a cambiar, y decimal(18,2) cubre con holgura el producto máximo.
            t.HasCheckConstraint(
                "CK_MovimientoDetalle_PrecioUnitario",
                "[PrecioUnitario] >= 0");
        });
    }
}
