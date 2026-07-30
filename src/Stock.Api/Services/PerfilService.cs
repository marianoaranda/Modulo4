using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;

namespace Stock.Api.Services;

public enum FalloDeSeguridad
{
    /// <summary>400.</summary>
    Validacion,

    /// <summary>404.</summary>
    NoEncontrado,

    /// <summary>409 — baja restringida o último administrador.</summary>
    Conflicto,
}

public sealed record OperacionSeguridad(
    bool Exito,
    FalloDeSeguridad? Fallo = null,
    string? Mensaje = null,
    IReadOnlyList<ErrorDeValidacion>? Errores = null)
{
    public static readonly OperacionSeguridad Correcta = new(true);

    public static OperacionSeguridad Invalida(string campo, string mensaje) =>
        new(false, FalloDeSeguridad.Validacion, Errores: [new ErrorDeValidacion(campo, mensaje)]);

    public static OperacionSeguridad NoEncontrado(string mensaje) =>
        new(false, FalloDeSeguridad.NoEncontrado, mensaje);

    public static OperacionSeguridad Conflicto(string mensaje) =>
        new(false, FalloDeSeguridad.Conflicto, mensaje);
}

/// <summary>
/// T113 — ABM de perfiles (RF-001, RF-002a, RF-002b, RF-003).
///
/// El alta y la modificación reciben <b>sólo la Descripción</b>. La marca <c>EsAdministrador</c> no
/// es alcanzable desde la API: no existe parámetro que la transporte, así que no hace falta código
/// defensivo que la ignore (RF-003a).
/// </summary>
public class PerfilService
{
    private readonly StockDbContext _db;

    public PerfilService(StockDbContext db) => _db = db;

    public async Task<(OperacionSeguridad Resultado, Perfil? Perfil)> AltaAsync(
        string descripcion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return (OperacionSeguridad.Invalida("descripcion", "La Descripción es obligatoria."), null);
        }

        // EsAdministrador no se toca: queda en su DEFAULT 0.
        var perfil = new Perfil { Descripcion = descripcion.Trim() };

        _db.Perfiles.Add(perfil);
        await _db.SaveChangesAsync(ct);

        return (OperacionSeguridad.Correcta, perfil);
    }

    public async Task<OperacionSeguridad> ModificarAsync(
        int perfilId, string descripcion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return OperacionSeguridad.Invalida("descripcion", "La Descripción es obligatoria.");
        }

        var perfil = await _db.Perfiles.FirstOrDefaultAsync(p => p.PerfilId == perfilId, ct);

        if (perfil is null)
        {
            return OperacionSeguridad.NoEncontrado("El perfil no existe.");
        }

        // RF-003: sólo la Descripción. Renombrar el perfil administrador es una operación válida
        // y no altera los privilegios de sus usuarios, porque éstos siguen la marca (RF-003a).
        perfil.Descripcion = descripcion.Trim();
        await _db.SaveChangesAsync(ct);

        return OperacionSeguridad.Correcta;
    }

    public async Task<OperacionSeguridad> BajaAsync(int perfilId, CancellationToken ct)
    {
        var perfil = await _db.Perfiles.FirstOrDefaultAsync(p => p.PerfilId == perfilId, ct);

        if (perfil is null)
        {
            return OperacionSeguridad.NoEncontrado("El perfil no existe.");
        }

        // RF-002b se verifica ANTES que RF-002a, y el orden importa: sin esta regla bastaría con
        // mover al último administrador a otro perfil y después borrar el perfil administrador
        // —que ya no tendría usuarios— para dejar al sistema sin quien habilite RF-004 a RF-006.
        if (perfil.EsAdministrador)
        {
            return OperacionSeguridad.Conflicto(
                "El perfil administrador no puede eliminarse, tenga o no usuarios asignados.");
        }

        // RF-002a: baja restringida. La FK NO ACTION es la garantía última; esta verificación
        // existe para devolver un 409 legible en vez de una violación de restricción.
        var tieneUsuarios = await _db.Usuarios.AnyAsync(u => u.PerfilId == perfilId, ct);

        if (tieneUsuarios)
        {
            return OperacionSeguridad.Conflicto(
                $"El perfil {perfil.Descripcion} tiene usuarios asignados y no puede eliminarse.");
        }

        _db.Perfiles.Remove(perfil);
        await _db.SaveChangesAsync(ct);

        return OperacionSeguridad.Correcta;
    }
}
