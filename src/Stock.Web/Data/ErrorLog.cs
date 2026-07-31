using Microsoft.EntityFrameworkCore;

namespace Stock.Web.Data;

/// <summary>
/// T125a — Copia propia de la entidad de bitácora para <c>Stock.Web</c> (RF-028).
///
/// <b>La duplicación con <c>Stock.Api.Domain.Entities.ErrorLog</c> es deliberada.</b>
/// <c>Stock.Web</c> no referencia a <c>Stock.Api</c>: reusar la clase obligaría a que la capa de
/// presentación tomara una dependencia sobre el ensamblado completo de la API —con sus entidades
/// de negocio, sus servicios y su acceso a datos— para ahorrarse cinco propiedades. La
/// duplicación es el mal menor, y está acotada a una tabla de diagnóstico que no es entidad de
/// negocio.
/// </summary>
public class ErrorLog
{
    public int ErrorId { get; set; }

    public DateTime ErrorDateTime { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? FullException { get; set; }
}

/// <summary>
/// Contexto de escritura de la bitácora desde la capa MVC: conexión independiente y <b>sin
/// migraciones propias</b>, porque mapea la tabla que ya creó la migración inicial de
/// <c>Stock.Api</c> (R-08).
///
/// Es la <b>única</b> excepción a la regla de que <c>Stock.Web</c> no accede a la base: sólo
/// diagnóstico, sólo escritura, ninguna entidad de negocio. Registrada en Complexity Tracking del
/// plan. La alternativa evaluada —un endpoint <c>POST /api/errores</c>— se descartó porque un
/// sumidero de escritura anónimo es superficie de abuso, y protegerlo exigiría un secreto
/// compartido y su rotación: más complejidad que la que evita.
/// </summary>
public class ErrorLogDbContext : DbContext
{
    public ErrorLogDbContext(DbContextOptions<ErrorLogDbContext> options) : base(options)
    {
    }

    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entidad = modelBuilder.Entity<ErrorLog>();

        entidad.ToTable("ErrorLog");
        entidad.HasKey(e => e.ErrorId);
        entidad.Property(e => e.ErrorDateTime).HasColumnType("datetime2");
        entidad.Property(e => e.MachineName).HasMaxLength(100);
        entidad.Property(e => e.Message).HasColumnType("nvarchar(max)");
        entidad.Property(e => e.FullException).HasColumnType("nvarchar(max)");
    }
}
