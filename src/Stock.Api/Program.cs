using Microsoft.EntityFrameworkCore;
using Stock.Api.Configuration;
using Stock.Api.Data;
using Stock.Api.Data.Seed;

namespace Stock.Api;

/// <summary>
/// Punto de entrada de la API.
///
/// No usa <em>top-level statements</em> a propósito: éstos generan un <c>Program</c> en el
/// namespace global, y el proyecto de tests referencia tanto a <c>Stock.Api</c> como a
/// <c>Stock.Web</c>. Con dos <c>Program</c> globales el tipo sería ambiguo y el proyecto de tests
/// no compilaría. Declararlo dentro de su propio namespace hace que
/// <c>WebApplicationFactory&lt;Stock.Api.Program&gt;</c> sea inequívoco (R-10).
/// </summary>
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Falla acá, al arrancar, si falta cualquiera de los tres secretos (Principio IV).
        var opciones = OpcionesDeArranque.Leer(builder.Configuration);

        builder.Services.AddSingleton(opciones);

        builder.Services.AddDbContext<StockDbContext>(o =>
            o.UseSqlServer(opciones.CadenaDeConexion));

        builder.Services.AddControllers();

        // Todas las respuestas de error viajan como application/problem+json (RFC 7807), incluido
        // el 400 que produce el binder al recibir un no entero en un campo entero (RF-018a).
        builder.Services.AddProblemDetails();

        var app = builder.Build();

        // Sólo en compose: fuera de ahí las migraciones se aplican con `dotnet ef database update`
        // (ver AGENTS.md). Que sea un flag y no el comportamiento por defecto evita que un arranque
        // accidental modifique el esquema de una base que no le corresponde.
        if (opciones.AplicarMigracionesAlArrancar)
        {
            AplicarMigracionesYSembrar(app);
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapControllers();
        app.Run();
    }

    private static void AplicarMigracionesYSembrar(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();

        db.Database.Migrate();
        DbSeeder.SembrarPerfilesAsync(db).GetAwaiter().GetResult();
    }
}
