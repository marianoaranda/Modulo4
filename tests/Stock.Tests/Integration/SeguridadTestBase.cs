using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Tests.Integration;

/// <summary>
/// Utilidades compartidas por los tests de los dos ABM de seguridad: crear perfiles y usuarios, y
/// obtener clientes autenticados como usuarios no administradores.
/// </summary>
public abstract class SeguridadTestBase : IntegrationTestBase
{
    protected const string Usuarios = "/api/usuarios";
    protected const string Perfiles = "/api/perfiles";

    /// <summary>Contraseña válida según RF-009, para no repetirla en cada caso.</summary>
    protected const string PasswordValida = "Clave1234";

    protected async Task<int> PerfilIdDeAsync(string descripcion) =>
        await EscalarAsync<int>(
            $"SELECT PerfilId FROM dbo.Perfil WHERE Descripcion = N'{descripcion}'");

    protected async Task<int> PerfilAdministradorIdAsync() =>
        await EscalarAsync<int>("SELECT PerfilId FROM dbo.Perfil WHERE EsAdministrador = 1");

    /// <summary>Alta de usuario por la API, como administrador.</summary>
    protected async Task<int> CrearUsuarioAsync(
        string nombreUsuario, int perfilId, string password = PasswordValida)
    {
        var respuesta = await Client.PostAsJsonAsync(Usuarios, new
        {
            nombreUsuario,
            nombreCompleto = $"Usuario {nombreUsuario}",
            perfilId,
            password,
        });

        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"No se pudo crear el usuario '{nombreUsuario}': " +
                        $"{(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
        }

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.GetProperty("usuarioId").GetInt32();
    }

    /// <summary>Cliente autenticado como un usuario del perfil indicado (no administrador).</summary>
    protected async Task<HttpClient> ClienteComoVendedorAsync(string nombreUsuario = "vendedor1")
    {
        var perfilId = await PerfilIdDeAsync("vendedor");
        await CrearUsuarioAsync(nombreUsuario, perfilId);

        return await ClienteComoAsync(nombreUsuario, PasswordValida);
    }

    protected Task<int> CantidadDeUsuariosAsync() =>
        EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Usuario");

    protected Task<int> CantidadDePerfilesAdministradoresAsync() =>
        EscalarAsync<int>("SELECT COUNT(*) FROM dbo.Perfil WHERE EsAdministrador = 1");
}
