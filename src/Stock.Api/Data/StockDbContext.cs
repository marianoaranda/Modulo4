using Microsoft.EntityFrameworkCore;
using Stock.Api.Data.Views;
using Stock.Api.Domain.Entities;

namespace Stock.Api.Data;

/// <summary>
/// Contexto dueño del esquema de las <b>seis</b> tablas.
///
/// <c>ErrorLog</c> entra acá aunque en runtime se escriba por otra conexión: hay una sola base y un
/// solo historial de migraciones, y la propiedad del esquema es una cosa distinta de la conexión de
/// escritura (R-08). Si la tabla no naciera en esta migración, el primer error no controlado
/// fallaría al registrarse con <i>invalid object name</i>.
/// </summary>
public class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options) : base(options)
    {
    }

    public DbSet<Perfil> Perfiles => Set<Perfil>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Articulo> Articulos => Set<Articulo>();

    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    public DbSet<MovimientoDetalle> MovimientoDetalles => Set<MovimientoDetalle>();

    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    /// <summary>
    /// Objeto derivado, no una tabla: se consulta, nunca se escribe.
    /// </summary>
    public DbSet<StockActualView> StockActual => Set<StockActualView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Nombres en singular, como en data-model.md.
        modelBuilder.Entity<Perfil>().ToTable("Perfil");
        modelBuilder.Entity<Usuario>().ToTable("Usuario");
        modelBuilder.Entity<Articulo>().ToTable("Articulo");
        modelBuilder.Entity<Movimiento>().ToTable("Movimiento");
        modelBuilder.Entity<MovimientoDetalle>().ToTable("MovimientoDetalle");
        modelBuilder.Entity<ErrorLog>().ToTable("ErrorLog");

        // Claves primarias que no siguen la convención de nombre de EF Core. Son estructura, no
        // regla de negocio: sin ellas el modelo ni siquiera se construye, así que van acá y no en
        // las configuraciones que los tests del esquema ponen en rojo.
        modelBuilder.Entity<Movimiento>().HasKey(m => m.Numero);
        modelBuilder.Entity<ErrorLog>().HasKey(e => e.ErrorId);

        modelBuilder.Entity<StockActualView>().HasNoKey().ToView("vw_StockActual");

        // Las restricciones que codifican reglas de negocio —CHECK, columnas calculadas, índices
        // únicos, collations y comportamiento de borrado— viven en las configuraciones de
        // Data/Configurations, que se aplican acá.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockDbContext).Assembly);
    }
}
