namespace Stock.Tests.Web;

/// <summary>
/// T110 — Capa web de los ABM de seguridad (RF-010, RF-010a).
///
/// Ocultar la entrada de menú no es control de acceso: la API responde 403 igual. Es que un menú
/// que ofrece pantallas que van a fallar es una interfaz que miente.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
public class SeguridadControllerTests : WebTestBase
{
    [Test]
    public async Task Un_administrador_ve_las_entradas_de_usuarios_y_perfiles()
    {
        Api.ResponderJson("[]");

        var cliente = ClienteConSesion(esAdmin: true);
        var html = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("/Usuarios"));
            Assert.That(html, Does.Contain("/Perfiles"));
        });
    }

    [Test]
    public async Task Un_perfil_sin_el_claim_es_admin_no_ve_ninguna_de_las_dos_entradas()
    {
        // RF-010 y RF-010a: las dos entradas se ocultan, no sólo la de usuarios.
        Api.ResponderJson("[]");

        var cliente = ClienteConSesion(esAdmin: false);
        var html = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("/Usuarios"));
            Assert.That(html, Does.Not.Contain("/Perfiles"));
        });
    }

    [Test]
    public async Task Un_perfil_cuya_descripcion_es_administrador_pero_sin_la_marca_tampoco_las_ve()
    {
        // RF-003a en la capa de presentación: el menú se decide por el claim `es_admin`, nunca
        // comparando la Descripción del perfil contra la cadena "administrador".
        //
        // La sesión que emite el fixture con esAdmin: false lleva `role` = "administrador" y
        // `es_admin` = "false", que es exactamente el caso peligroso.
        Api.ResponderJson("[]");

        var cliente = ClienteConSesion(esAdmin: false);
        var html = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("/Usuarios"));
            Assert.That(html, Does.Not.Contain("/Perfiles"));
        });
    }

    [Test]
    public async Task Las_pantallas_de_negocio_se_ofrecen_a_todo_usuario_autenticado()
    {
        // El alcance cerrado del PRD: la restricción alcanza sólo a los dos ABM de seguridad.
        Api.ResponderJson("[]");

        var cliente = ClienteConSesion(esAdmin: false);
        var html = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("/Articulos"));
            Assert.That(html, Does.Contain("/Movimientos"));
            Assert.That(html, Does.Contain("/StockActual"));
            Assert.That(html, Does.Contain("/GenerarPedido"));
        });
    }

    [Test]
    public async Task El_listado_de_usuarios_no_muestra_ninguna_credencial()
    {
        Api.ResponderJson("""
            [{"usuarioId":1,"nombreUsuario":"admin","nombreCompleto":"Administrador","perfilId":1}]
            """);

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/Usuarios")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("admin"));
            Assert.That(html, Does.Not.Contain("hash").IgnoreCase);
            Assert.That(html, Does.Not.Contain("salt").IgnoreCase);
        });
    }

    [Test]
    public async Task El_formulario_de_perfil_no_expone_ningun_control_para_la_marca()
    {
        // RF-003a: la marca no es editable desde el ABM. Un control en pantalla —aunque la API lo
        // ignorara— sugeriría que se puede fabricar un administrador desde acá.
        Api.ResponderJson("""{"perfilId":2,"descripcion":"vendedor","esAdministrador":false}""");

        var cliente = ClienteConSesion();
        var html = await (await cliente.GetAsync("/Perfiles/Edit/2")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("vendedor"));
            Assert.That(html, Does.Not.Match(@"<input[^>]*EsAdministrador"),
                "No hay control editable para la marca.");
        });
    }
}
