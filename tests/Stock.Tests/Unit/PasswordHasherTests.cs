using Stock.Api.Security;

namespace Stock.Tests.Unit;

/// <summary>
/// T093 — Derivación de contraseñas (RF-007, RF-008, R-03).
///
/// Se usa <c>Rfc2898DeriveBytes</c> directamente y no el <c>PasswordHasher&lt;TUser&gt;</c> de
/// ASP.NET Core Identity porque aquél empaqueta salt, iteraciones y subclave dentro de una única
/// cadena. El PRD (RF-04) define <c>Hash</c> y <c>Salt</c> como columnas <b>separadas</b>, y RF-008
/// exige que el salt sea observable como propio de cada usuario: con el hasher de Identity no
/// habría columna <c>Salt</c> que mirar.
/// </summary>
[TestFixture]
[Category(TestCategories.Unit)]
public class PasswordHasherTests
{
    [Test]
    public void Dos_usuarios_con_la_misma_contrasena_tienen_salt_y_hash_distintos()
    {
        // RF-008 / CE-006, en el 100% de los casos. Es lo que impide que una tabla filtrada
        // revele qué cuentas comparten contraseña, y lo que hace inútil una tabla precalculada.
        var primero = PasswordHasher.Derivar("MismaClave1");
        var segundo = PasswordHasher.Derivar("MismaClave1");

        Assert.Multiple(() =>
        {
            Assert.That(primero.Salt, Is.Not.EqualTo(segundo.Salt));
            Assert.That(primero.Hash, Is.Not.EqualTo(segundo.Hash));
        });
    }

    [Test]
    public void La_verificacion_acepta_la_contrasena_correcta()
    {
        var credencial = PasswordHasher.Derivar("Correcta123");

        Assert.That(
            PasswordHasher.Verificar("Correcta123", credencial.Hash, credencial.Salt),
            Is.True);
    }

    [TestCase("Incorrecta123")]
    [TestCase("correcta123")]
    [TestCase("Correcta12")]
    [TestCase("")]
    public void La_verificacion_rechaza_cualquier_otra_contrasena(string intento)
    {
        var credencial = PasswordHasher.Derivar("Correcta123");

        Assert.That(
            PasswordHasher.Verificar(intento, credencial.Hash, credencial.Salt),
            Is.False);
    }

    [Test]
    public void La_verificacion_con_el_salt_de_otro_usuario_falla()
    {
        // Confirma que el salt participa efectivamente de la derivación: si se ignorara, dos
        // usuarios con la misma contraseña serían intercambiables y RF-008 no se cumpliría por
        // más que las columnas existieran.
        var primero = PasswordHasher.Derivar("MismaClave1");
        var segundo = PasswordHasher.Derivar("MismaClave1");

        Assert.That(
            PasswordHasher.Verificar("MismaClave1", primero.Hash, segundo.Salt),
            Is.False);
    }

    [Test]
    public void El_salt_tiene_16_bytes_y_el_hash_32()
    {
        // R-03. Los tamaños importan porque las columnas son varbinary(16) y varbinary(32): un
        // cambio de parámetros que los alterara truncaría silenciosamente en la base.
        var credencial = PasswordHasher.Derivar("Cualquiera1");

        Assert.Multiple(() =>
        {
            Assert.That(credencial.Salt, Has.Length.EqualTo(16));
            Assert.That(credencial.Hash, Has.Length.EqualTo(32));
        });
    }

    [Test]
    public void La_contrasena_en_claro_no_aparece_en_la_representacion_protegida()
    {
        // RF-007: no recuperable ni desencriptable. Una comprobación tosca, pero atrapa el error
        // grosero de "hashear" con una transformación reversible.
        var credencial = PasswordHasher.Derivar("SecretoVisible1");
        var comoTexto = Convert.ToBase64String(credencial.Hash) + Convert.ToBase64String(credencial.Salt);

        Assert.Multiple(() =>
        {
            Assert.That(comoTexto, Does.Not.Contain("SecretoVisible1"));
            Assert.That(
                System.Text.Encoding.UTF8.GetString(credencial.Hash),
                Does.Not.Contain("SecretoVisible"));
        });
    }
}
