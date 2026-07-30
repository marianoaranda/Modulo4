namespace Stock.Api.Domain.Validation;

/// <summary>
/// T111 — Política mínima de contraseña (RF-009).
///
/// Longitud mínima de 8, al menos una letra y al menos un dígito. Los caracteres no alfanuméricos
/// están <b>permitidos</b> y cuentan para la longitud: lo que se exige es la presencia de dos
/// clases, no la ausencia de las demás. No hay longitud máxima ni exigencia de mayúsculas.
///
/// Prohibir símbolos sería el error fácil acá, y rechazaría contraseñas mejores que las que
/// acepta.
/// </summary>
public static class PasswordPolicy
{
    public const int LongitudMinima = 8;

    public const string Mensaje =
        "La contraseña debe tener al menos 8 caracteres, con al menos una letra y al menos un dígito.";

    public static bool EsValida(string? password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= LongitudMinima
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
