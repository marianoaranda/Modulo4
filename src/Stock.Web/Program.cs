using Stock.Web.Services;

namespace Stock.Web;

/// <summary>
/// Punto de entrada de la capa MVC.
///
/// Igual que <c>Stock.Api.Program</c>, evita los <em>top-level statements</em> para que
/// <c>Stock.Api.Program</c> y <c>Stock.Web.Program</c> no colisionen en el proyecto de tests, que
/// referencia a ambos (R-10).
///
/// <b>Deliberadamente sin el filtro de autorización global.</b> Ese filtro es código de producción
/// de RF-012 y su test vive en la Fase 6 (T096): introducirlo acá sería implementar antes del rojo
/// y violaría el Principio I. Lo agrega T105b, después de que su test exista y falle.
/// </summary>
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();

        // Por defecto Razor codifica como entidades numéricas todo lo que no sea ASCII básico, de
        // modo que "descripción" llegaría al navegador como "descripci&#xF3;n". Se ve bien, pero
        // hace que el HTML no contenga literalmente los textos que RF-032 y RF-032a fijan al
        // carácter. Ampliar el rango es lo correcto para una aplicación en español.
        builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(opciones =>
            opciones.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
                System.Text.Unicode.UnicodeRanges.All));

        var direccionDeLaApi = builder.Configuration["StockApi:BaseUrl"];

        builder.Services.AddHttpClient<StockApiClient>(cliente =>
        {
            if (!string.IsNullOrWhiteSpace(direccionDeLaApi))
            {
                cliente.BaseAddress = new Uri(direccionDeLaApi);
            }
        });

        var app = builder.Build();

        app.UseExceptionHandler("/Home/Error");
        app.UseStatusCodePagesWithReExecute("/Home/Error", "?codigo={0}");

        app.UseStaticFiles();
        app.UseRouting();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
