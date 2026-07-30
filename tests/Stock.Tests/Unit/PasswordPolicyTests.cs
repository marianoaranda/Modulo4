using Stock.Api.Domain.Validation;

namespace Stock.Tests.Unit;

/// <summary>
/// T106 — Política de contraseña (RF-009).
///
/// La política es exactamente: <b>mínimo 8 caracteres, con al menos una letra y al menos un
/// dígito</b>. Los caracteres no alfanuméricos están <b>permitidos</b> y cuentan para la longitud:
/// lo que se exige es la presencia de dos clases, no la ausencia de las demás. No hay longitud
/// máxima ni exigencia de mayúsculas o símbolos.
/// </summary>
[TestFixture]
[Category(TestCategories.Unit)]
public class PasswordPolicyTests
{
    [TestCase("Abcd123", TestName = "Siete caracteres")]
    [TestCase("a1", TestName = "Dos caracteres")]
    [TestCase("", TestName = "Vacía")]
    public void Se_rechazan_las_de_menos_de_ocho_caracteres(string password) =>
        Assert.That(PasswordPolicy.EsValida(password), Is.False);

    [TestCase("12345678")]
    [TestCase("00000000000")]
    [TestCase("1234!@#$")]
    public void Se_rechazan_las_que_no_tienen_ninguna_letra(string password) =>
        Assert.That(PasswordPolicy.EsValida(password), Is.False);

    [TestCase("abcdefgh")]
    [TestCase("ContraseñaSegura")]
    [TestCase("abcd!@#$")]
    public void Se_rechazan_las_que_no_tienen_ningun_digito(string password) =>
        Assert.That(PasswordPolicy.EsValida(password), Is.False);

    [TestCase("abcd1234", TestName = "Ocho justos, letras y dígitos")]
    [TestCase("Admin1234", TestName = "Con mayúscula")]
    [TestCase("unaClaveLarga123", TestName = "Larga")]
    public void Se_aceptan_las_de_ocho_o_mas_que_mezclan_letras_y_digitos(string password) =>
        Assert.That(PasswordPolicy.EsValida(password), Is.True);

    [TestCase("abcd123!")]
    [TestCase("P@ssw0rd")]
    [TestCase("mi clave 123")]
    [TestCase("ñandú-2026")]
    public void Se_aceptan_las_que_ademas_contienen_simbolos_o_espacios(string password) =>
        // Es la mitad del requisito que una implementación restrictiva rompería sin querer: una
        // política que prohibiera los no alfanuméricos rechazaría contraseñas mejores que las que
        // acepta.
        Assert.That(PasswordPolicy.EsValida(password), Is.True);

    [Test]
    public void No_hay_longitud_maxima() =>
        Assert.That(PasswordPolicy.EsValida(new string('a', 200) + "1"), Is.True);

    [Test]
    public void El_mensaje_de_error_describe_la_politica_completa()
    {
        // El usuario tiene que poder corregir sin adivinar cuál de las tres condiciones falló.
        Assert.Multiple(() =>
        {
            Assert.That(PasswordPolicy.Mensaje, Does.Contain("8"));
            Assert.That(PasswordPolicy.Mensaje, Does.Contain("letra"));
            Assert.That(PasswordPolicy.Mensaje, Does.Contain("dígito").IgnoreCase);
        });
    }
}
