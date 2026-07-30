using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data.Configurations;

/// <summary>
/// T035 — Bitácora de errores (RF-028). Sin relaciones: se escribe desde una conexión
/// independiente, fuera de la transacción que está fallando (R-08).
/// </summary>
public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.HasKey(e => e.ErrorId);

        builder.Property(e => e.ErrorDateTime).HasColumnType("datetime2").IsRequired();

        builder.Property(e => e.MachineName).HasMaxLength(100).IsRequired();

        builder.Property(e => e.Message).HasColumnType("nvarchar(max)").IsRequired();

        // Único campo opcional de la bitácora.
        builder.Property(e => e.FullException).HasColumnType("nvarchar(max)");
    }
}
