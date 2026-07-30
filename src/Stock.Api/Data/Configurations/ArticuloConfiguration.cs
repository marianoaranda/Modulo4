using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data.Configurations;

/// <summary>
/// T031 — Reglas de negocio del artículo codificadas en el esquema (RF-016 a RF-019).
///
/// Van en la base y no sólo en el servicio porque son invariantes del dato: ninguna ruta de
/// escritura, presente o futura, puede evadirlas.
/// </summary>
public class ArticuloConfiguration : IEntityTypeConfiguration<Articulo>
{
    /// <summary>
    /// Insensible a mayúsculas y <b>sensible</b> a acentos: es la regla de ordenamiento
    /// alfabético del español que fija RF-025a, y la misma que gobierna la unicidad del Código
    /// por RF-017a. Que sean la misma es el punto: `A-001` y `a-001` son el mismo Código.
    /// </summary>
    private const string CollationCodigo = "Modern_Spanish_CI_AS";

    /// <summary>
    /// Insensible a mayúsculas <b>y</b> a acentos, que es lo que hace que el filtro por
    /// descripción de RF-027a encuentre "Válvula" buscando "valvula" sin normalizar cadenas en la
    /// aplicación ni mantener una columna espejo (R-06).
    /// </summary>
    private const string CollationDescripcion = "Modern_Spanish_CI_AI";

    public void Configure(EntityTypeBuilder<Articulo> builder)
    {
        builder.Property(a => a.Codigo)
            .HasMaxLength(50)
            .IsRequired()
            .UseCollation(CollationCodigo);

        builder.Property(a => a.Descripcion)
            .HasMaxLength(200)
            .IsRequired()
            .UseCollation(CollationDescripcion);

        builder.HasIndex(a => a.Codigo)
            .IsUnique()
            .HasDatabaseName("UX_Articulo_Codigo");

        builder.Property(a => a.PrecioCosto).HasColumnType("decimal(18,2)");
        builder.Property(a => a.Margen).HasColumnType("decimal(9,4)");

        // RF-016. Al calcularla el motor es imposible que diverja de costo y margen: refuerzo del
        // Principio III. El CAST a decimal(18,2) aplica el redondeo del motor (mitad hacia arriba
        // en valor absoluto), decisión documentada en data-model.md porque el spec no la fija.
        builder.Property(a => a.PrecioVenta)
            .HasColumnType("decimal(18,2)")
            .HasComputedColumnSql(
                "CAST([PrecioCosto] * (1 + [Margen] / 100.0) AS decimal(18,2))",
                stored: true);

        builder.ToTable(t =>
        {
            // RF-018.
            t.HasCheckConstraint(
                "CK_Articulo_ValoresNoNegativos",
                "[PrecioCosto] >= 0 AND [Margen] >= 0 AND [StockMinimo] >= 0 " +
                "AND [PuntoPedido] >= 0 AND [StockIdeal] >= 0");

            // RF-019. Admite los tres iguales, que es un caso límite válido del spec.
            t.HasCheckConstraint(
                "CK_Articulo_OrdenDeStocks",
                "[StockMinimo] <= [PuntoPedido] AND [PuntoPedido] <= [StockIdeal]");
        });
    }
}
