using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Stock.Api.Data;
using Stock.Api.Data.Seed;

namespace Stock.Tests.Integration;

/// <summary>
/// Base de los tests de integración (R-10).
///
/// Hospeda <c>Stock.Api</c> <b>in-process</b> con <c>WebApplicationFactory</c> y usa de
/// <c>docker compose</c> sólo el SQL Server. La base es real y no un doble en memoria porque los
/// tres puntos de mayor riesgo del diseño —bloqueos <c>UPDLOCK</c>, collations acento-insensibles y
/// planes de agregación— son comportamiento específico del motor: SQLite o InMemory darían una
/// señal verde falsa.
///
/// Cada fixture crea su propia base efímera y la elimina al terminar, de modo que dos fixtures no
/// se pisen y una corrida no dependa del estado dejado por la anterior.
/// </summary>
public abstract class IntegrationTestBase
{
    /// <summary>
    /// Servidor de compose. Se puede apuntar a otro con la variable de entorno
    /// <c>STOCK_TEST_SQLSERVER</c> sin tocar código.
    /// </summary>
    private static string ServidorDePruebas =>
        Environment.GetEnvironmentVariable("STOCK_TEST_SQLSERVER")
        ?? "Server=localhost,1433;User Id=sa;Password=" + PasswordSa + ";TrustServerCertificate=True";

    private static string PasswordSa =>
        Environment.GetEnvironmentVariable("SA_PASSWORD")
        ?? LeerDelEnvLocal("SA_PASSWORD")
        ?? throw new InvalidOperationException(
            "No se encontró SA_PASSWORD. Los tests de integración necesitan el SQL Server de " +
            "`docker compose up -d sqlserver` y el archivo .env de la raíz (ver .env.example).");

    /// <summary>Cadena de conexión hacia una base concreta del servidor de pruebas.</summary>
    public static string CadenaHacia(string nombreDeBase) =>
        new SqlConnectionStringBuilder(ServidorDePruebas)
        {
            InitialCatalog = nombreDeBase,
            MultipleActiveResultSets = true,
        }.ConnectionString;

    /// <summary>
    /// Aplica el historial completo de migraciones sobre la base indicada. Lo usan también los
    /// tests de la capa web, que necesitan la tabla <c>ErrorLog</c> creada por esa misma migración.
    /// </summary>
    public static async Task MigrarAsync(string cadenaDeConexion)
    {
        var options = new DbContextOptionsBuilder<StockDbContext>()
            .UseSqlServer(cadenaDeConexion)
            .Options;

        await using var db = new StockDbContext(options);
        await db.Database.MigrateAsync();
    }

    private string _nombreDeBase = string.Empty;

    protected string CadenaDeConexion { get; private set; } = string.Empty;

    protected WebApplicationFactory<Stock.Api.Program> Factory { get; private set; } = null!;

    /// <summary>Cliente HTTP contra la API hospedada in-process.</summary>
    protected HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task LevantarBaseYApiAsync()
    {
        _nombreDeBase = $"StockTest_{Guid.NewGuid():N}";
        CadenaDeConexion = new SqlConnectionStringBuilder(ServidorDePruebas)
        {
            InitialCatalog = _nombreDeBase,
            MultipleActiveResultSets = true,
        }.ConnectionString;

        await EjecutarEnServidorAsync($"CREATE DATABASE [{_nombreDeBase}]");

        Factory = new WebApplicationFactory<Stock.Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:StockDb", CadenaDeConexion);
                // La API falla al arrancar si falta cualquiera de los tres secretos (Principio IV),
                // así que el host de pruebas los provee explícitamente.
                builder.UseSetting("Jwt:SigningKey", ClaveDeFirmaDePrueba);
                builder.UseSetting("SEED_ADMIN_PASSWORD", PasswordAdminDePrueba);
                builder.UseSetting("ApplyMigrationsOnStartup", "false");

                // Sin esto, una excepción no controlada de la API llega al test convertida en un
                // 500 genérico y sin rastro: el middleware la registra por ILogger y los
                // proveedores por defecto no escriben a la salida de NUnit. Diagnosticar un fallo
                // se vuelve entonces adivinar.
                builder.ConfigureServices(services =>
                    services.AddLogging(logging => logging.AddProvider(new ProveedorParaNUnit())));
            });

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
            await db.Database.MigrateAsync();
        }

        Client = Factory.CreateClient();
        await SembrarSeguridadYAutenticarAsync();

        await PrepararFixtureAsync();
    }

    [OneTimeTearDown]
    public async Task DerribarBaseYApiAsync()
    {
        Client?.Dispose();
        Factory?.Dispose();

        if (!string.IsNullOrEmpty(_nombreDeBase))
        {
            // SINGLE_USER WITH ROLLBACK IMMEDIATE: sin esto el DROP se cuelga si quedó abierta
            // alguna conexión del pool.
            await EjecutarEnServidorAsync(
                $"ALTER DATABASE [{_nombreDeBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_nombreDeBase}]");
        }
    }

    [SetUp]
    public async Task LimpiarDatosAsync()
    {
        // El orden respeta las dependencias: el detalle antes que sus padres, y el usuario antes
        // que su perfil.
        await EjecutarSqlAsync("""
            DELETE FROM dbo.MovimientoDetalle;
            DELETE FROM dbo.Movimiento;
            DELETE FROM dbo.Articulo;
            DELETE FROM dbo.Usuario;
            DELETE FROM dbo.Perfil;
            DELETE FROM dbo.ErrorLog;
            """);

        // La limpieza se lleva puestos los perfiles y el usuario admin, así que hay que
        // reponerlos y volver a autenticar antes de cada test.
        await SembrarSeguridadYAutenticarAsync();

        await LimpiarFixtureAsync();
    }

    /// <summary>
    /// T100 — Fixture autenticado.
    ///
    /// Sirve los perfiles base y el usuario <c>admin</c>, pide un token al propio endpoint de
    /// login y lo deja puesto en <see cref="Client"/>. Que <see cref="Client"/> venga autenticado
    /// por omisión es deliberado: cuando T101 aplicó <c>[Authorize]</c> a todos los controladores,
    /// los tests de US1, US2 y US3 volvieron a verde sin tocar una línea, porque la autenticación
    /// es una precondición de esos escenarios y no parte de lo que verifican. Los tests que sí
    /// verifican el acceso usan <see cref="ClienteSinToken"/> explícitamente.
    /// </summary>
    private async Task SembrarSeguridadYAutenticarAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();

            await DbSeeder.SembrarPerfilesAsync(db);
            await DbSeeder.SembrarAdministradorAsync(db, PasswordAdminDePrueba);
        }

        Token = await ObtenerTokenAsync("admin", PasswordAdminDePrueba);

        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token);
    }

    /// <summary>Token del usuario administrador de la corrida.</summary>
    protected string Token { get; private set; } = string.Empty;

    /// <summary>
    /// Cliente sin credenciales. Lo usan los tests que verifican que sin sesión no se accede a
    /// nada (RF-012).
    /// </summary>
    protected HttpClient ClienteSinToken() => Factory.CreateClient();

    /// <summary>
    /// Cliente propio con el token de la corrida. Lo usan los tests de concurrencia, donde cada
    /// operación simultánea representa a un usuario distinto y necesita su propia conexión.
    /// </summary>
    protected HttpClient ClienteAutenticado()
    {
        var cliente = Factory.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token);

        return cliente;
    }

    /// <summary>Cliente autenticado como otro usuario, para los tests de 403 (RF-010).</summary>
    protected async Task<HttpClient> ClienteComoAsync(string usuario, string password)
    {
        var cliente = Factory.CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await ObtenerTokenAsync(usuario, password));

        return cliente;
    }

    protected async Task<string> ObtenerTokenAsync(string usuario, string password)
    {
        using var sinToken = Factory.CreateClient();

        var respuesta = await sinToken.PostAsJsonAsync(
            "/api/auth/login", new { usuario, password });

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"El login de '{usuario}' falló con {(int)respuesta.StatusCode}: " +
                await respuesta.Content.ReadAsStringAsync());
        }

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.GetProperty("token").GetString()!;
    }

    /// <summary>
    /// Token con los claims correctos y la firma correcta, pero ya vencido. Verifica que el
    /// <c>ClockSkew</c> en cero de R-04 hace que la expiración sea exacta.
    /// </summary>
    protected string TokenExpirado() => ArmarToken(
        ClaveDeFirmaDePrueba, expiracion: DateTime.UtcNow.AddMinutes(-1));

    /// <summary>Token bien formado pero firmado con otra clave.</summary>
    protected string TokenFirmadoCon(string clave) => ArmarToken(
        clave.PadRight(32, '.'), expiracion: DateTime.UtcNow.AddHours(1));

    private static string ArmarToken(string clave, DateTime expiracion)
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "Stock.Api",
            audience: "Stock.Web",
            claims: [new Claim("name", "admin"), new Claim("es_admin", "true")],
            notBefore: expiracion.AddHours(-8),
            expires: expiracion,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Gancho para que un fixture siembre lo que necesite una sola vez.</summary>
    protected virtual Task PrepararFixtureAsync() => Task.CompletedTask;

    /// <summary>Gancho para re-sembrar después de cada limpieza.</summary>
    protected virtual Task LimpiarFixtureAsync() => Task.CompletedTask;

    /// <summary>
    /// Contexto nuevo contra la base efímera. Se devuelve uno por uso —y no uno compartido— para
    /// que un test pueda verificar lo que quedó <b>en la base</b> y no lo que sigue cacheado en el
    /// seguimiento de cambios de EF Core.
    /// </summary>
    protected StockDbContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<StockDbContext>()
            .UseSqlServer(CadenaDeConexion)
            .Options;

        return new StockDbContext(options);
    }

    /// <summary>
    /// Siembra artículos en masa para los tests de tope y orden. Se hace con un
    /// <c>INSERT ... SELECT</c> sobre una tabla de números y no fila por fila con EF Core, porque
    /// 10.001 <c>SaveChanges</c> tardarían minutos y esto es sólo preparación del escenario.
    ///
    /// Los códigos se rellenan con ceros a la izquierda para que el orden alfabético coincida con
    /// el numérico: sin eso, "B-10" iría antes que "B-9" y el test de determinismo del recorte
    /// verificaría algo distinto de lo que dice verificar.
    /// </summary>
    protected async Task SembrarArticulosEnMasaAsync(
        int cantidad, string prefijo = "B-", string descripcion = "Artículo de volumen")
    {
        await EjecutarSqlAsync($"""
            WITH Numeros AS (
                SELECT TOP ({cantidad})
                       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                FROM   sys.all_objects a
                CROSS JOIN sys.all_objects b
            )
            INSERT INTO dbo.Articulo
                (Codigo, Descripcion, PrecioCosto, Margen, StockMinimo, PuntoPedido, StockIdeal)
            SELECT '{prefijo}' + RIGHT('00000' + CAST(n AS varchar(6)), 6),
                   '{descripcion} ' + CAST(n AS varchar(6)),
                   10.00, 0, 0, 0, 0
            FROM   Numeros;
            """);
    }

    protected async Task<int> EjecutarSqlAsync(string sql)
    {
        await using var conexion = new SqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        return await comando.ExecuteNonQueryAsync();
    }

    protected async Task<T?> EscalarAsync<T>(string sql)
    {
        await using var conexion = new SqlConnection(CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        var resultado = await comando.ExecuteScalarAsync();

        return resultado is null or DBNull ? default : (T)Convert.ChangeType(resultado, typeof(T));
    }

    internal const string ClaveDeFirmaDePrueba =
        "clave-de-firma-solo-para-tests-no-es-un-secreto-real-0123456789";

    internal const string PasswordAdminDePrueba = "AdminDePrueba1";

    private static async Task EjecutarEnServidorAsync(string sql)
    {
        var alMaster = new SqlConnectionStringBuilder(ServidorDePruebas)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        await using var conexion = new SqlConnection(alMaster);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Lee una clave del <c>.env</c> de la raíz. El archivo está ignorado por git y no se
    /// commitea; esto sólo evita tener que exportar la variable a mano en cada consola.
    /// </summary>
    private static string? LeerDelEnvLocal(string clave)
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, ".env");

            if (File.Exists(candidato))
            {
                foreach (var linea in File.ReadAllLines(candidato))
                {
                    var separador = linea.IndexOf('=');
                    if (separador <= 0 || linea.TrimStart().StartsWith('#'))
                    {
                        continue;
                    }

                    if (linea[..separador].Trim() == clave)
                    {
                        return linea[(separador + 1)..].Trim();
                    }
                }
            }

            directorio = directorio.Parent;
        }

        return null;
    }
}
