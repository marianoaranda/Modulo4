using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stock.Api.Data;

/// <summary>
/// Fábrica de diseño para las herramientas de EF Core (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>).
///
/// Existe porque el arranque de la API falla a propósito si falta cualquiera de los tres secretos
/// (Principio IV), y las herramientas de diseño no tienen por qué conocerlos: generar una migración
/// no necesita conectarse a nada. La cadena que se usa acá sirve sólo para que EF Core sepa que el
/// proveedor es SQL Server; para <c>database update</c> se toma la real de
/// <c>ConnectionStrings__StockDb</c>.
/// </summary>
public class StockDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
{
    private const string CadenaDeDiseno =
        "Server=(localdb)\\MSSQLLocalDB;Database=StockModulo;Trusted_Connection=True;";

    public StockDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__StockDb");

        var options = new DbContextOptionsBuilder<StockDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(cadena) ? CadenaDeDiseno : cadena)
            .Options;

        return new StockDbContext(options);
    }
}
