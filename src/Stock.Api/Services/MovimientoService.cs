using Microsoft.EntityFrameworkCore;
using Stock.Api.Data;
using Stock.Api.Domain.Entities;
using Stock.Api.Domain.Validation;

namespace Stock.Api.Services;

/// <summary>Motivo por el que una operación sobre movimientos no se aplicó.</summary>
public enum FalloDeMovimiento
{
    /// <summary>400 — entrada que viola una regla de validación de campo.</summary>
    Validacion,

    /// <summary>404 — el movimiento no existe.</summary>
    NoEncontrado,

    /// <summary>422 — la operación dejaría el Stock Actual de algún artículo por debajo de 0.</summary>
    StockInsuficiente,
}

public sealed record OperacionMovimiento(
    bool Exito,
    int Numero = 0,
    FalloDeMovimiento? Fallo = null,
    string? Mensaje = null,
    IReadOnlyList<ErrorDeValidacion>? Errores = null)
{
    public static OperacionMovimiento Correcta(int numero) => new(true, numero);

    public static OperacionMovimiento Invalida(IReadOnlyList<ErrorDeValidacion> errores) =>
        new(false, Fallo: FalloDeMovimiento.Validacion, Errores: errores);

    public static OperacionMovimiento Invalida(string campo, string mensaje) =>
        Invalida([new ErrorDeValidacion(campo, mensaje)]);

    public static OperacionMovimiento NoEncontrado() =>
        new(false, Fallo: FalloDeMovimiento.NoEncontrado, Mensaje: "El Movimiento no existe.");

    /// <summary>
    /// 404 con el Código ofensor (RF-020e). Comparte estado con <see cref="NoEncontrado"/> porque
    /// para el cliente son el mismo resultado —lo referenciado no existe— y lo que cambia es qué
    /// se nombra: sin el Código, quien carga diez líneas no sabe cuál rechazó el sistema.
    /// </summary>
    public static OperacionMovimiento CodigoNoEncontrado(string mensaje) =>
        new(false, Fallo: FalloDeMovimiento.NoEncontrado, Mensaje: mensaje);

    public static OperacionMovimiento SinStock(string mensaje) =>
        new(false, Fallo: FalloDeMovimiento.StockInsuficiente, Mensaje: mensaje);
}

/// <summary>
/// T074 — Dueño del <b>protocolo de escritura de movimientos</b>, el invariante de arquitectura
/// que sostiene RF-024a, RF-024b y RF-024c.
///
/// Toda ruta que cree, modifique o elimine movimientos pasa por acá y sigue esta secuencia:
/// <list type="number">
///   <item>abrir transacción;</item>
///   <item>resolver el <b>Código</b> de cada línea a su <c>ArticuloId</c> (RF-020e);</item>
///   <item>bloquear las filas de <c>Articulo</c> afectadas, en orden ascendente de Id;</item>
///   <item>leer el saldo desde <c>vw_StockActual</c>, ya dentro de la transacción;</item>
///   <item>validar que el saldo <b>resultante</b> sea ≥ 0 para TODOS los artículos afectados;</item>
///   <item>aplicar encabezado y detalle;</item>
///   <item>confirmar.</item>
/// </list>
///
/// El paso 2 traduce la identidad de negocio a la referencia física, y va <b>dentro</b> de la
/// transacción y <b>antes</b> del bloqueo: resolverlo afuera dejaría una ventana en la que el
/// artículo puede desaparecer entre la resolución y el <c>INSERT</c>, y el orden de bloqueo debe
/// seguir siendo por <c>ArticuloId</c> ascendente —nunca por el orden en que el usuario cargó los
/// Códigos—, que es lo único que evita los deadlocks entre movimientos multilínea.
///
/// El paso 4 se evalúa sobre el efecto <b>conjunto</b> de todas las líneas y antes de aplicar
/// ninguna: ahí está el todo-o-nada de RF-024c. Validar línea por línea contra el saldo inicial
/// dejaría pasar dos líneas del mismo artículo que individualmente entran y juntas no.
/// </summary>
public class MovimientoService
{
    private readonly StockDbContext _db;
    private readonly ArticuloLockRepository _bloqueo;

    public MovimientoService(StockDbContext db, ArticuloLockRepository bloqueo)
    {
        _db = db;
        _bloqueo = bloqueo;
    }

    /// <summary>Compra suma al Stock Actual, Venta resta (RF-020b).</summary>
    private static int Signo(TipoMovimiento tipo) => tipo == TipoMovimiento.Compra ? 1 : -1;

    public async Task<OperacionMovimiento> AltaAsync(MovimientoAValidar solicitud, CancellationToken ct)
    {
        var errores = MovimientoValidator.Validar(solicitud, DateOnly.FromDateTime(DateTime.Today));

        if (errores.Count > 0)
        {
            return OperacionMovimiento.Invalida(errores);
        }

        return await EnTransaccionAsync(
            resolver: async () => await ResolverAsync(solicitud, previo: null, ct),
            aplicar: async idPorCodigo =>
            {
                var movimiento = new Movimiento { Tipo = solicitud.Tipo, Fecha = solicitud.Fecha };

                foreach (var linea in solicitud.Detalle)
                {
                    movimiento.Detalle.Add(new MovimientoDetalle
                    {
                        ArticuloId = idPorCodigo[linea.Codigo],
                        Cantidad = linea.Cantidad,
                        PrecioUnitario = linea.PrecioUnitario,
                    });
                }

                _db.Movimientos.Add(movimiento);
                await _db.SaveChangesAsync(ct);

                return movimiento.Numero;
            },
            ct);
    }

    public async Task<OperacionMovimiento> ModificarAsync(
        int numero, MovimientoAValidar solicitud, CancellationToken ct)
    {
        var errores = MovimientoValidator.Validar(solicitud, DateOnly.FromDateTime(DateTime.Today));

        if (errores.Count > 0)
        {
            return OperacionMovimiento.Invalida(errores);
        }

        var existente = await _db.Movimientos
            .Include(m => m.Detalle)
            .FirstOrDefaultAsync(m => m.Numero == numero, ct);

        if (existente is null)
        {
            return OperacionMovimiento.NoEncontrado();
        }

        return await EnTransaccionAsync(
            // El efecto de una modificación es la diferencia entre lo que quedará y lo que había.
            resolver: async () => await ResolverAsync(solicitud, previo: existente, ct),
            aplicar: async idPorCodigo =>
            {
                existente.Tipo = solicitud.Tipo;
                existente.Fecha = solicitud.Fecha;

                // Se reemplaza el detalle completo: distinguir altas, bajas y cambios de línea
                // agregaría complejidad sin cambiar el resultado, y el detalle no tiene identidad
                // propia de negocio.
                _db.MovimientoDetalles.RemoveRange(existente.Detalle);

                foreach (var linea in solicitud.Detalle)
                {
                    _db.MovimientoDetalles.Add(new MovimientoDetalle
                    {
                        MovimientoNumero = numero,
                        ArticuloId = idPorCodigo[linea.Codigo],
                        Cantidad = linea.Cantidad,
                        PrecioUnitario = linea.PrecioUnitario,
                    });
                }

                await _db.SaveChangesAsync(ct);

                return numero;
            },
            ct);
    }

    public async Task<OperacionMovimiento> BajaAsync(int numero, CancellationToken ct)
    {
        var existente = await _db.Movimientos
            .Include(m => m.Detalle)
            .FirstOrDefaultAsync(m => m.Numero == numero, ct);

        if (existente is null)
        {
            return OperacionMovimiento.NoEncontrado();
        }

        return await EnTransaccionAsync(
            // RF-024a: la baja también mueve el saldo. Dar de baja una compra ya consumida por
            // ventas posteriores dejaría el stock en negativo, y se rechaza igual que una venta
            // sin stock. No hay Códigos que resolver: el detalle que se va ya está resuelto.
            resolver: () =>
            {
                var efecto = new Dictionary<int, int>();
                Restar(efecto, EfectoActualDe(existente));

                return Task.FromResult(Resolucion.Resuelta(efecto, SinCodigos));
            },
            aplicar: async _ =>
            {
                // El detalle se va en cascada por la FK (RF-021).
                _db.Movimientos.Remove(existente);
                await _db.SaveChangesAsync(ct);

                return numero;
            },
            ct);
    }

    /// <summary>Resultado del paso 2: el efecto ya traducido, o el fallo que impide seguir.</summary>
    private sealed record Resolucion(
        Dictionary<int, int>? Efecto,
        IReadOnlyDictionary<string, int>? IdPorCodigo,
        OperacionMovimiento? Fallo)
    {
        public static Resolucion Resuelta(
            Dictionary<int, int> efecto, IReadOnlyDictionary<string, int> idPorCodigo) =>
            new(efecto, idPorCodigo, null);

        public static Resolucion Abortada(OperacionMovimiento fallo) => new(null, null, fallo);
    }

    private static readonly IReadOnlyDictionary<string, int> SinCodigos =
        new Dictionary<string, int>();

    /// <summary>
    /// Los pasos 1 a 5 y 7 del protocolo. El paso 6 —aplicar— lo aporta quien llama, de modo que
    /// las tres operaciones compartan una única implementación del invariante y ninguna pueda
    /// olvidarse de un paso.
    /// </summary>
    private async Task<OperacionMovimiento> EnTransaccionAsync(
        Func<Task<Resolucion>> resolver,
        Func<IReadOnlyDictionary<string, int>, Task<int>> aplicar,
        CancellationToken ct)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

        // 2. Resolver el Código de cada línea a su ArticuloId, ya dentro de la transacción.
        var resolucion = await resolver();

        if (resolucion.Fallo is not null)
        {
            await transaccion.RollbackAsync(ct);

            return resolucion.Fallo;
        }

        var efecto = resolucion.Efecto!;

        // 3. Bloqueo pesimista, en orden ascendente de ArticuloId.
        await _bloqueo.BloquearAsync(efecto.Keys, ct);

        // 4. Saldo actual, ya dentro de la transacción y después del bloqueo: si otra operación
        //    estaba a mitad de camino, acá se espera y se lee lo que efectivamente confirmó.
        var afectados = efecto.Keys.ToList();

        var saldos = await _db.StockActual
            .Where(s => afectados.Contains(s.ArticuloId))
            .ToDictionaryAsync(s => s.ArticuloId, s => new { s.Codigo, s.StockActual }, ct);

        // 5. Validar el resultado para TODOS los artículos antes de aplicar ninguna línea.
        foreach (var (articuloId, delta) in efecto)
        {
            if (!saldos.TryGetValue(articuloId, out var saldo))
            {
                continue;
            }

            if (saldo.StockActual + delta < 0)
            {
                await transaccion.RollbackAsync(ct);

                return OperacionMovimiento.SinStock(
                    $"Stock insuficiente para el artículo {saldo.Codigo}. " +
                    $"Stock Actual: {saldo.StockActual}; la operación lo dejaría en " +
                    $"{saldo.StockActual + delta}.");
            }
        }

        // 6. Aplicar.
        var numero = await aplicar(resolucion.IdPorCodigo!);

        // 7. Confirmar.
        await transaccion.CommitAsync(ct);

        return OperacionMovimiento.Correcta(numero);
    }

    private static Dictionary<int, int> EfectoDe(
        TipoMovimiento tipo,
        IReadOnlyList<LineaAValidar> detalle,
        IReadOnlyDictionary<string, int> idPorCodigo)
    {
        var efecto = new Dictionary<int, int>();

        foreach (var linea in detalle)
        {
            var articuloId = idPorCodigo[linea.Codigo];

            // Se acumula por artículo: dos líneas del mismo artículo se evalúan por su efecto
            // conjunto, no por separado. Y como la resolución es insensible a mayúsculas, dos
            // líneas que escriben distinto el mismo Código también caen en la misma entrada.
            efecto[articuloId] = efecto.GetValueOrDefault(articuloId) + (Signo(tipo) * linea.Cantidad);
        }

        return efecto;
    }

    private static Dictionary<int, int> EfectoActualDe(Movimiento movimiento)
    {
        var efecto = new Dictionary<int, int>();

        foreach (var linea in movimiento.Detalle)
        {
            efecto[linea.ArticuloId] = efecto.GetValueOrDefault(linea.ArticuloId) +
                                       (Signo(movimiento.Tipo) * linea.Cantidad);
        }

        return efecto;
    }

    private static void Restar(Dictionary<int, int> efecto, Dictionary<int, int> aRestar)
    {
        foreach (var (articuloId, delta) in aRestar)
        {
            efecto[articuloId] = efecto.GetValueOrDefault(articuloId) - delta;
        }
    }

    /// <summary>
    /// Paso 2 del protocolo: traduce los Códigos de la solicitud a identificadores y calcula el
    /// efecto sobre el saldo. Cuando hay un movimiento previo —una modificación— el efecto es la
    /// diferencia entre lo que quedará y lo que había.
    /// </summary>
    private async Task<Resolucion> ResolverAsync(
        MovimientoAValidar solicitud, Movimiento? previo, CancellationToken ct)
    {
        var (idPorCodigo, faltantes) = await ResolverCodigosAsync(solicitud.Detalle, ct);

        if (faltantes.Count > 0)
        {
            return Resolucion.Abortada(OperacionMovimiento.CodigoNoEncontrado(
                faltantes.Count == 1
                    ? $"No existe ningún artículo con el Código {faltantes[0]}."
                    : $"No existe ningún artículo con los Códigos {string.Join(", ", faltantes)}."));
        }

        var efecto = EfectoDe(solicitud.Tipo, solicitud.Detalle, idPorCodigo);

        if (previo is not null)
        {
            Restar(efecto, EfectoActualDe(previo));
        }

        return Resolucion.Resuelta(efecto, idPorCodigo);
    }

    /// <summary>
    /// Resuelve cada Código a su identificador con la regla de RF-017a: <b>insensible a mayúsculas
    /// y sensible a acentos</b>.
    ///
    /// La insensibilidad no se programa acá: la aporta la collation <c>Modern_Spanish_CI_AS</c> de
    /// la columna, la misma que sostiene la unicidad del Código, de modo que no puedan divergir. El
    /// diccionario se arma con <c>OrdinalIgnoreCase</c> por la misma razón, del lado de C#: es lo
    /// que hace que la línea que llegó como <c>a-001</c> encuentre al artículo que el catálogo
    /// guarda como <c>A-001</c>, y que dos líneas que lo escriben distinto sean el mismo artículo.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, int> IdPorCodigo, IReadOnlyList<string> Faltantes)>
        ResolverCodigosAsync(IReadOnlyList<LineaAValidar> detalle, CancellationToken ct)
    {
        var pedidos = detalle
            .Select(l => l.Codigo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var encontrados = await _db.Articulos
            .Where(a => pedidos.Contains(a.Codigo))
            .Select(a => new { a.ArticuloId, a.Codigo })
            .ToListAsync(ct);

        var porCodigo = encontrados.ToDictionary(
            a => a.Codigo, a => a.ArticuloId, StringComparer.OrdinalIgnoreCase);

        var idPorCodigo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var faltantes = new List<string>();

        foreach (var codigo in pedidos)
        {
            if (porCodigo.TryGetValue(codigo, out var articuloId))
            {
                idPorCodigo[codigo] = articuloId;
            }
            else
            {
                faltantes.Add(codigo);
            }
        }

        return (idPorCodigo, faltantes);
    }
}
