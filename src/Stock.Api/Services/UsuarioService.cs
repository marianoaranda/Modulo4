using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;
using Stock.Api.Security;

namespace Stock.Api.Services;

/// <summary>Datos de alta o modificación de un usuario. La contraseña es opcional al modificar.</summary>
public sealed record UsuarioAGrabar(
    string NombreUsuario, string NombreCompleto, int PerfilId, string? Password);

/// <summary>
/// T114 — ABM de usuarios (RF-004 a RF-009, RF-005a).
/// </summary>
public class UsuarioService
{
    private readonly StockDbContext _db;

    public UsuarioService(StockDbContext db) => _db = db;

    public async Task<(OperacionSeguridad Resultado, Usuario? Usuario)> AltaAsync(
        UsuarioAGrabar solicitud, CancellationToken ct)
    {
        var error = ValidarDatos(solicitud, passwordObligatoria: true);

        if (error is not null)
        {
            return (error, null);
        }

        if (!await _db.Perfiles.AnyAsync(p => p.PerfilId == solicitud.PerfilId, ct))
        {
            return (OperacionSeguridad.Invalida("perfilId", "El perfil indicado no existe."), null);
        }

        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == solicitud.NombreUsuario, ct))
        {
            return (OperacionSeguridad.Conflicto(
                $"Ya existe un usuario con el nombre {solicitud.NombreUsuario}."), null);
        }

        // La política se verifica ANTES de derivar: no tiene sentido gastar 210.000 iteraciones en
        // una contraseña que se va a rechazar, y la contraseña en claro no debe recorrer más capas
        // de las necesarias.
        var credencial = PasswordHasher.Derivar(solicitud.Password!);

        var usuario = new Usuario
        {
            NombreUsuario = solicitud.NombreUsuario.Trim(),
            NombreCompleto = solicitud.NombreCompleto.Trim(),
            PerfilId = solicitud.PerfilId,
            Hash = credencial.Hash,
            Salt = credencial.Salt,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);

        return (OperacionSeguridad.Correcta, usuario);
    }

    public async Task<OperacionSeguridad> ModificarAsync(
        int usuarioId, UsuarioAGrabar solicitud, CancellationToken ct)
    {
        var error = ValidarDatos(solicitud, passwordObligatoria: false);

        if (error is not null)
        {
            return error;
        }

        // RF-005a: la verificación del conteo de administradores va DENTRO de la misma transacción
        // que la escritura. Contarlos antes de abrirla dejaría que dos operaciones simultáneas
        // vieran ambas dos administradores, se creyeran seguras y dejaran cero.
        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

        var usuario = await _db.Usuarios
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId, ct);

        if (usuario is null)
        {
            return OperacionSeguridad.NoEncontrado("El usuario no existe.");
        }

        if (!await _db.Perfiles.AnyAsync(p => p.PerfilId == solicitud.PerfilId, ct))
        {
            return OperacionSeguridad.Invalida("perfilId", "El perfil indicado no existe.");
        }

        var nombreEnUso = await _db.Usuarios.AnyAsync(
            u => u.NombreUsuario == solicitud.NombreUsuario && u.UsuarioId != usuarioId, ct);

        if (nombreEnUso)
        {
            return OperacionSeguridad.Conflicto(
                $"Ya existe un usuario con el nombre {solicitud.NombreUsuario}.");
        }

        var perdiaElPrivilegio = usuario.Perfil?.EsAdministrador == true
            && !await EsPerfilAdministradorAsync(solicitud.PerfilId, ct);

        if (perdiaElPrivilegio && await EsElUltimoAdministradorAsync(usuarioId, ct))
        {
            return OperacionSeguridad.Conflicto(
                "No se puede cambiar el perfil del último usuario administrador: " +
                "el sistema quedaría sin quien administre usuarios y perfiles.");
        }

        usuario.NombreUsuario = solicitud.NombreUsuario.Trim();
        usuario.NombreCompleto = solicitud.NombreCompleto.Trim();
        usuario.PerfilId = solicitud.PerfilId;

        // RF-006: la contraseña es opcional. Re-derivar el hash con una cadena vacía dejaría
        // afuera del sistema a quien sólo quiso corregirse el nombre.
        if (!string.IsNullOrEmpty(solicitud.Password))
        {
            var credencial = PasswordHasher.Derivar(solicitud.Password);
            usuario.Hash = credencial.Hash;
            usuario.Salt = credencial.Salt;
        }

        await _db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return OperacionSeguridad.Correcta;
    }

    public async Task<OperacionSeguridad> BajaAsync(int usuarioId, CancellationToken ct)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

        var usuario = await _db.Usuarios
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId, ct);

        if (usuario is null)
        {
            return OperacionSeguridad.NoEncontrado("El usuario no existe.");
        }

        // RF-005a, dentro de la transacción por el mismo motivo que en la modificación.
        if (usuario.Perfil?.EsAdministrador == true && await EsElUltimoAdministradorAsync(usuarioId, ct))
        {
            return OperacionSeguridad.Conflicto(
                "No se puede dar de baja al último usuario administrador: " +
                "el sistema quedaría sin quien administre usuarios y perfiles.");
        }

        _db.Usuarios.Remove(usuario);
        await _db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return OperacionSeguridad.Correcta;
    }

    private Task<bool> EsPerfilAdministradorAsync(int perfilId, CancellationToken ct) =>
        _db.Perfiles.AnyAsync(p => p.PerfilId == perfilId && p.EsAdministrador, ct);

    /// <summary>
    /// Cuenta los administradores <b>que no son éste</b>. La consulta se hace con el bloqueo de la
    /// transacción en curso, de modo que una operación concurrente que también quiera quitar un
    /// administrador espere y vea el conteo ya actualizado.
    /// </summary>
    private async Task<bool> EsElUltimoAdministradorAsync(int usuarioId, CancellationToken ct)
    {
        var otros = await _db.Usuarios
            .Where(u => u.UsuarioId != usuarioId && u.Perfil!.EsAdministrador)
            .CountAsync(ct);

        return otros == 0;
    }

    private static OperacionSeguridad? ValidarDatos(UsuarioAGrabar solicitud, bool passwordObligatoria)
    {
        if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario))
        {
            return OperacionSeguridad.Invalida("nombreUsuario", "El Nombre de Usuario es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.NombreCompleto))
        {
            return OperacionSeguridad.Invalida("nombreCompleto", "El Nombre Completo es obligatorio.");
        }

        // En el alta la contraseña es obligatoria: un usuario sin credencial no podría entrar
        // nunca y dejaría una fila inútil. En la modificación, omitirla significa conservarla.
        if (passwordObligatoria && string.IsNullOrEmpty(solicitud.Password))
        {
            return OperacionSeguridad.Invalida("password", "La contraseña es obligatoria en el alta.");
        }

        // RF-009.
        if (!string.IsNullOrEmpty(solicitud.Password) && !PasswordPolicy.EsValida(solicitud.Password))
        {
            return OperacionSeguridad.Invalida("password", PasswordPolicy.Mensaje);
        }

        return null;
    }
}
