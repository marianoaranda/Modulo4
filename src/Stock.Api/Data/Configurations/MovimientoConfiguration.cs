using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data.Configurations;

/// <summary>
/// T032 — Encabezado de movimiento.
/// </summary>
public class MovimientoConfiguration : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> builder)
    {
        // RF-020a / R-07: IDENTITY satisface los cuatro atributos del requisito con el mecanismo
        // más simple disponible — única globalmente, compartida entre compras y ventas, no
        // editable por el usuario y no reutilizable tras una baja, porque IDENTITY no reasigna
        // valores liberados.
        builder.HasKey(m => m.Numero);
        builder.Property(m => m.Numero).ValueGeneratedOnAdd();

        builder.Property(m => m.Tipo).HasColumnType("tinyint");

        builder.Property(m => m.Fecha).HasColumnType("date");

        builder.ToTable(t =>
            // RF-020b: conjunto cerrado. 1 = Compra (suma), 2 = Venta (resta).
            t.HasCheckConstraint("CK_Movimiento_Tipo", "[Tipo] IN (1, 2)"));
    }
}
