using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data.Configurations;

/// <summary>
/// T034 — Perfil y Usuario.
/// </summary>
public class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.Property(p => p.Descripcion)
            .HasMaxLength(100)
            .IsRequired();

        // RF-003a: la marca se establece exclusivamente en la siembra, así que un alta que no la
        // mencione no puede crear un administrador por accidente.
        builder.Property(p => p.EsAdministrador)
            .HasDefaultValue(false);

        // Índice único FILTRADO: garantiza en el esquema que exista a lo sumo un perfil
        // administrador. Sin el filtro, la unicidad alcanzaría también a los perfiles sin la
        // marca y sólo podría existir un perfil no administrador, que es lo contrario de lo que
        // se quiere. (RF-003a)
        builder.HasIndex(p => p.EsAdministrador)
            .IsUnique()
            .HasFilter("[EsAdministrador] = 1")
            .HasDatabaseName("UX_Perfil_EsAdministrador");
    }
}

/// <summary>
/// T034 — Usuario. <c>Hash</c> y <c>Salt</c> son columnas separadas por la forma que exige el PRD
/// (RF-007, RF-008); ningún DTO de respuesta las expone.
/// </summary>
public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.Property(u => u.NombreUsuario)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.NombreCompleto)
            .HasMaxLength(200)
            .IsRequired();

        // Subclave derivada de 32 bytes y salt aleatorio de 16 bytes (R-03).
        builder.Property(u => u.Hash).HasColumnType("varbinary(32)").IsRequired();
        builder.Property(u => u.Salt).HasColumnType("varbinary(16)").IsRequired();

        builder.HasIndex(u => u.NombreUsuario)
            .IsUnique()
            .HasDatabaseName("UX_Usuario_NombreUsuario");

        // RF-002a: baja restringida del perfil. Igual que con Articulo, el servicio verifica antes
        // para devolver un 409 legible en vez de una violación de FK.
        builder.HasOne(u => u.Perfil)
            .WithMany(p => p.Usuarios)
            .HasForeignKey(u => u.PerfilId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(u => u.PerfilId).HasDatabaseName("IX_Usuario_PerfilId");
    }
}
