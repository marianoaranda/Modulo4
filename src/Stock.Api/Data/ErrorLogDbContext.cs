using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data;

/// <summary>
/// T124 — Contexto de escritura de la bitácora, con <b>conexión independiente</b> y <b>sin
/// migraciones propias</b> (R-08).
///
/// Los dos rasgos responden a razones distintas y conviene no confundirlas:
///
/// <list type="bullet">
///   <item><b>Conexión independiente</b>: si el error se registrara con el <c>DbContext</c> de la
///   operación fallida, el <c>ROLLBACK</c> de esa transacción borraría también el registro del
///   error. La bitácora perdería exactamente los fallos que ocurren dentro de una transacción, en
///   silencio, y CE-008 sería inalcanzable sin que nadie lo notara.</item>
///
///   <item><b>Sin migraciones propias</b>: el esquema de <c>dbo.ErrorLog</c> lo declara y versiona
///   <c>StockDbContext</c>, junto al resto de las tablas. Hay una sola base y un solo historial de
///   migraciones; separarlo obligaría a un segundo historial sobre la misma base sin ganancia
///   alguna. Este contexto <b>mapea una tabla que ya existe</b>.</item>
/// </list>
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
