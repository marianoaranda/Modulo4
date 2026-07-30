using System.Security.Cryptography;
using System.Text;

namespace Stock.Api.Security;

/// <summary>Representación protegida de una contraseña: dos columnas separadas (RF-007, RF-008).</summary>
public sealed record CredencialProtegida(byte[] Hash, byte[] Salt);

/// <summary>
/// T097 — Derivación y verificación de contraseñas (R-03).
///
/// PBKDF2-HMAC-SHA256 con salt aleatorio de 16 bytes por usuario, 210.000 iteraciones —la
/// recomendación de OWASP para este algoritmo— y subclave de 32 bytes.
///
/// Se usa <c>Rfc2898DeriveBytes</c> directamente y no el <c>PasswordHasher&lt;TUser&gt;</c> de
/// ASP.NET Core Identity porque aquél empaqueta salt, iteraciones y subclave en una única cadena y
/// no deja columna <c>Salt</c> separada, que es la forma que exige el PRD. BCrypt tiene el mismo
/// problema: embebe el salt en el hash resultante.
/// </summary>
public static class PasswordHasher
{
    private const int Iteraciones = 210_000;
    private const int BytesDeSalt = 16;
    private const int BytesDeHash = 32;

    private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

    public static CredencialProtegida Derivar(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(BytesDeSalt);

        return new CredencialProtegida(Derivar(password, salt), salt);
    }

    public static bool Verificar(string password, byte[] hashEsperado, byte[] salt)
    {
        if (hashEsperado.Length != BytesDeHash || salt.Length != BytesDeSalt)
        {
            return false;
        }

        var calculado = Derivar(password, salt);

        // Comparación en tiempo fijo: una comparación byte a byte que corta en la primera
        // diferencia filtra por tiempo cuántos bytes iniciales coinciden.
        return CryptographicOperations.FixedTimeEquals(calculado, hashEsperado);
    }

    private static byte[] Derivar(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? string.Empty),
            salt,
            Iteraciones,
            Algoritmo,
            BytesDeHash);
}
