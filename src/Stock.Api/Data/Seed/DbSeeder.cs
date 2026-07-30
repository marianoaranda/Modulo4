using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;
using Stock.Api.Security;

namespace Stock.Api.Data.Seed;

/// <summary>
/// Siembra de los datos necesarios para que el sistema sea operable en el primer arranque.
///
/// Es el <b>único</b> lugar del sistema que establece <c>Perfil.EsAdministrador</c>. Ningún DTO del
/// ABM de perfiles acepta esa marca, de modo que la Descripción puede cambiar libremente (RF-003)
/// sin efecto sobre los privilegios y no existe forma de fabricar un segundo administrador desde
/// la API (RF-003a).
/// </summary>
public static class DbSeeder
{
    public const string PerfilAdministrador = "administrador";
    public const string PerfilAdministrativo = "administrativo";
    public const string PerfilVendedor = "vendedor";

    public static async Task SembrarPerfilesAsync(StockDbContext db, CancellationToken ct = default)
    {
        if (await db.Perfiles.AnyAsync(ct))
        {
            return;
        }

        db.Perfiles.AddRange(
            new Perfil { Descripcion = PerfilAdministrador, EsAdministrador = true },
            new Perfil { Descripcion = PerfilAdministrativo, EsAdministrador = false },
            new Perfil { Descripcion = PerfilVendedor, EsAdministrador = false });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// T103 — Usuario administrador inicial, para que el sistema sea operable en el primer
    /// arranque (supuesto del spec).
    ///
    /// El perfil se localiza por la <b>marca</b> <c>EsAdministrador</c> y no por la Descripción:
    /// buscarlo por el texto "administrador" haría que renombrar el perfil —operación válida por
    /// RF-003— rompiera la siembra. RF-002b y RF-005a garantizan después que este punto de entrada
    /// no pueda perderse por operación del propio ABM.
    ///
    /// La contraseña sale de <c>SEED_ADMIN_PASSWORD</c>, sin valor por defecto embebido: la API ya
    /// falló al arrancar si la variable no está definida (Principio IV).
    /// </summary>
    public static async Task SembrarAdministradorAsync(
        StockDbContext db, string password, CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(ct))
        {
            return;
        }

        var perfil = await db.Perfiles.FirstOrDefaultAsync(p => p.EsAdministrador, ct)
            ?? throw new InvalidOperationException(
                "No existe el perfil administrador. Hay que sembrar los perfiles antes que el usuario.");

        var credencial = PasswordHasher.Derivar(password);

        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            NombreCompleto = "Administrador del sistema",
            Hash = credencial.Hash,
            Salt = credencial.Salt,
            PerfilId = perfil.PerfilId,
        });

        await db.SaveChangesAsync(ct);
    }
}
