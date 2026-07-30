using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Validation;
using Stock.Api.Security;
using Stock.Api.Services;

namespace Stock.Api.Controllers;

/// <summary>
/// Cuerpo de alta y modificación de perfil.
///
/// <b>No declara <c>EsAdministrador</c></b>, y ésa es toda la implementación de RF-003a del lado
/// del contrato: el campo enviado en el cuerpo no tiene dónde aterrizar, así que se ignora sin
/// necesidad de código que lo filtre. La marca se establece exclusivamente en la siembra.
/// </summary>
public sealed class PerfilRequest
{
    public string Descripcion { get; set; } = string.Empty;
}

public sealed record PerfilResponse(int PerfilId, string Descripcion, bool EsAdministrador);

/// <summary>
/// T115 — ABM de perfiles (RF-001 a RF-003).
///
/// Protegido con la política <c>SoloAdministrador</c> por RF-010a: el perfil determina quién
/// accede a la carga de usuarios, de modo que dejar este ABM abierto permitiría alterar
/// indirectamente el control de acceso de RF-010.
/// </summary>
[ApiController]
[Route("api/perfiles")]
[Authorize(Policy = AuthorizationPolicies.SoloAdministrador)]
public class PerfilesController : ControllerBase
{
    private readonly PerfilService _servicio;
    private readonly StockDbContext _db;

    public PerfilesController(PerfilService servicio, StockDbContext db)
    {
        _servicio = servicio;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await _db.Perfiles
            .OrderBy(p => p.PerfilId)
            .Select(p => new PerfilResponse(p.PerfilId, p.Descripcion, p.EsAdministrador))
            .ToListAsync(ct));

    [HttpGet("{perfilId:int}")]
    public async Task<IActionResult> Leer(int perfilId, CancellationToken ct)
    {
        var perfil = await _db.Perfiles
            .Where(p => p.PerfilId == perfilId)
            .Select(p => new PerfilResponse(p.PerfilId, p.Descripcion, p.EsAdministrador))
            .FirstOrDefaultAsync(ct);

        return perfil is null
            ? Traducir(OperacionSeguridad.NoEncontrado("El perfil no existe."))
            : Ok(perfil);
    }

    [HttpPost]
    public async Task<IActionResult> Alta([FromBody] PerfilRequest solicitud, CancellationToken ct)
    {
        var (resultado, perfil) = await _servicio.AltaAsync(solicitud.Descripcion, ct);

        if (!resultado.Exito)
        {
            return Traducir(resultado);
        }

        var creado = new PerfilResponse(perfil!.PerfilId, perfil.Descripcion, perfil.EsAdministrador);

        return CreatedAtAction(nameof(Leer), new { perfilId = creado.PerfilId }, creado);
    }

    [HttpPut("{perfilId:int}")]
    public async Task<IActionResult> Modificar(
        int perfilId, [FromBody] PerfilRequest solicitud, CancellationToken ct)
    {
        var resultado = await _servicio.ModificarAsync(perfilId, solicitud.Descripcion, ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    [HttpDelete("{perfilId:int}")]
    public async Task<IActionResult> Baja(int perfilId, CancellationToken ct)
    {
        var resultado = await _servicio.BajaAsync(perfilId, ct);

        return resultado.Exito ? NoContent() : Traducir(resultado);
    }

    private IActionResult Traducir(OperacionSeguridad resultado) => resultado.Fallo switch
    {
        FalloDeSeguridad.NoEncontrado => Problem(
            detail: resultado.Mensaje,
            statusCode: StatusCodes.Status404NotFound,
            title: "No encontrado"),

        FalloDeSeguridad.Conflicto => Problem(
            detail: resultado.Mensaje,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflicto"),

        _ => ProblemaDeValidacion(resultado.Errores!),
    };

    private IActionResult ProblemaDeValidacion(IReadOnlyList<ErrorDeValidacion> errores)
    {
        foreach (var error in errores)
        {
            ModelState.AddModelError(error.Campo, error.Mensaje);
        }

        return ValidationProblem(ModelState);
    }
}
