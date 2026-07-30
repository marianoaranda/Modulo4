using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain.Entities;

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
}
