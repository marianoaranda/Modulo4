# Plan de Implementación: Módulo de Stock — Generación automática de pedidos

**Rama**: `001-modulo-stock-pedidos` | **Fecha**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Entrada**: Especificación de `/specs/001-modulo-stock-pedidos/spec.md`

## Resumen

Sitio web para un comercio de barrio que infiere automáticamente qué artículos reponer y en qué
cantidad, a partir de las compras y ventas registradas. El núcleo es la consulta **Generar Pedido**,
que combina dos parámetros de reposición ("solo bajo mínimo" y "Modo de Pedido") para producir
`MAX(0, Nivel − Stock Actual)` por artículo.

**Enfoque técnico**: solución .NET 8 de dos aplicaciones —`Stock.Api` (Web API REST con JWT, dueña
de toda la regla de negocio) y `Stock.Web` (ASP.NET MVC, consumidor sin acceso a base **salvo una
excepción única**: la escritura de la bitácora de errores por su propia conexión, sólo diagnóstico y
sin ninguna entidad de negocio, registrada en Complexity Tracking)— sobre SQL Server 2017 vía EF
Core Migrations.

Tres decisiones sostienen el diseño:

1. El **Stock Actual nunca se persiste**: se calcula por agregación en la vista `vw_StockActual`, que es el único lugar donde existe la definición del saldo. Es lo que exige el spec y lo que hace imposible que el stock se desincronice (Principio III).
2. El invariante *stock ≥ 0* se protege con **bloqueo pesimista** sobre las filas de `Articulo`, de modo que la operación concurrente perdedora reciba "stock insuficiente" y no un error de reintento, tal como pide RF-024b.
3. La **bitácora de errores escribe por una conexión independiente**, para que el registro sobreviva al rollback de la transacción que falló — sin eso, CE-008 sería inalcanzable de forma silenciosa.

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Contexto Técnico

**Lenguaje/Versión**: C# 12 sobre .NET 8 (SDK 8.0.100 verificado en el entorno)

**Dependencias principales**: ASP.NET Core MVC, ASP.NET Core Web API, EF Core 8 (`SqlServer`, `Design`), `Microsoft.AspNetCore.Authentication.JwtBearer`, ClosedXML (exportación `.xlsx`, licencia MIT), NUnit + `Microsoft.NET.Test.Sdk`

**Almacenamiento**: SQL Server 2017, esquema gestionado por EF Core Migrations

**Testing**: NUnit vía `dotnet test StockModulo.sln`, con categorías `Unit` (sin base) e `Integration` (SQL Server real de `docker compose`)

**Plataforma destino**: contenedores Linux vía `docker compose`; desarrollo en Windows

**Tipo de proyecto**: aplicación web con front-end y back-end separados

**Objetivos de rendimiento**: ambas consultas < 3 s p95 con 10.000 artículos y 100.000 líneas de detalle (CE-002)

**Restricciones**: 1 a 5 usuarios concurrentes sin degradación (CE-004); Stock Actual siempre derivado, nunca persistido; tope de 10.000 filas por consulta con filtro opcional por descripción; contraseñas con hash + salt aleatorio por usuario en columnas separadas

**Escala/Alcance**: 10.000 artículos, ~100.000 líneas de movimiento, 5 usuarios, 6 entidades, ~10 pantallas, 2 consultas exportables

## Constitution Check

*GATE: debe pasar antes de la investigación de Fase 0. Reevaluado tras el diseño de Fase 1.*

### Evaluación inicial (pre-Fase 0)

| Principio | Estado | Fundamento |
|-----------|--------|------------|
| **I. Desarrollo Test-First** | ✅ PASA | El plan ordena el trabajo en rojo→verde→refactor. La lógica de pedido es una función pura testeable sin infraestructura, y el spec aporta el Conjunto de Datos de Referencia (matriz de 6 × 4 = 24 celdas: 15 cantidades asertadas y 9 exclusiones): los tests de CE-003 pueden escribirse antes que el código. |
| **II. Aislamiento de la Lógica de IA** | ✅ PASA (por vacuidad) | Esta funcionalidad **no contiene lógica de IA**. La "inferencia" de qué pedir es una fórmula determinista (`MAX(0, Nivel − Stock Actual)`), no una llamada a un modelo. No hay prompts, ni invocación de modelos, ni parseo de respuestas que aislar. Si en el futuro se agregara sugerencia predictiva de reposición, este principio obligaría a un módulo dedicado. |
| **III. Fidelidad a la Fuente de Verdad** | ✅ PASA | Toda cantidad a pedir es trazable a movimientos reales. El Stock Actual no se estima ni se infiere: es el saldo de compras y ventas registradas. No hay caso de "baja confianza" que derivar a un humano porque no hay estimación. |
| **IV. Gestión Segura de Secretos** | ✅ PASA | Cadena de conexión, clave de firma JWT y contraseña del administrador inicial se inyectan por variable de entorno, resueltas por Docker Compose desde un `.env` **excluido del repositorio**; lo versionado es `.env.example`, con placeholders. Los archivos de configuración quedan con los valores vacíos y la API falla al arrancar si alguno no está definido, en vez de recurrir a un valor por defecto. Las contraseñas de usuario se derivan con PBKDF2 + salt aleatorio de 16 bytes por usuario. |

**Restricciones adicionales de la constitución**:

| Restricción | Estado |
|---|---|
| Stack fijo (ASP.NET MVC, Web API .NET 8 + JWT, SQL Server 2017, EF Core, NUnit, `StockModulo.sln`) | ✅ Respetado sin desvíos |
| Sin manejo de múltiples proveedores por artículo | ✅ Fuera de alcance, no aparece en el modelo de datos |
| Consultas con tope de 10.000 y filtro opcional por descripción | ✅ RF-027, RF-027a y RF-027b; no existe ninguna consulta sin límite |

**Veredicto**: sin violaciones. Se procede a Fase 0.

### Reevaluación (post-Fase 1)

| Principio | Estado | Qué cambió en el diseño |
|-----------|--------|--------------------------|
| **I. Test-First** | ✅ PASA | R-10 fija la estrategia: `Unit` para la calculadora de pedido, validadores y hashing; `Integration` contra SQL Server real para lo que depende del motor (bloqueos, collations, agregación). Se rechazó InMemory/SQLite justamente porque daría verde falso en los tres puntos de mayor riesgo. El principio se aplica **sin excepción por capa**: (a) el esquema codifica reglas de negocio (`CHECK` de orden de stocks, columnas calculadas, índice único), por lo que `tasks.md` ordena los tests de esas restricciones **antes** que las configuraciones que las implementan; (b) la capa `Stock.Web` tiene su propia carpeta de tests con `WebApplicationFactory`, incluido el `BearerTokenHandler`, que contiene lógica real de manejo del 401; (c) la lógica de cliente de RF-016a y RF-034 se testea por su **contrato renderizado** —el test asierta que la vista incluye la partial del buscador, declara su campo de destino y referencia el script, y que el Precio de Venta sigue siendo de sólo lectura—, sin introducir un runner de JavaScript. Es lo correcto y no una concesión: RF-016a fija que el valor mostrado es informativo y que el servidor es la fuente de verdad, así que lo que el spec exige verificar es que la pantalla quede cableada y que el cliente no pueda alterar el precio grabado, y ambas cosas son asertables desde `WebApplicationFactory`. |
| **II. Aislamiento de IA** | ✅ PASA (por vacuidad) | El diseño no introdujo ninguna dependencia de IA. Sin cambios. |
| **III. Fuente de verdad** | ✅ **REFORZADO** | `vw_StockActual` es el único lugar donde se calcula el saldo, consumido tanto por las consultas como por la validación del invariante. `PrecioVenta` y `PrecioTotal` son columnas calculadas por el motor: no pueden divergir de sus insumos. Se rechazó explícitamente persistir el stock (R-01), que habría creado una segunda fuente de verdad. |
| **IV. Secretos** | ✅ PASA | R-03 y R-04 detallan la derivación de contraseñas y la firma del token. Que ningún secreto quede en el repositorio no es una afirmación declarativa: tiene mecanismo asignado (`.env` ignorado + `.env.example` con placeholders) y arranque que falla ante una variable ausente. R-04 además desacopla la autorización de la Descripción editable del perfil mediante el claim `es_admin`, para que renombrar un perfil no pueda otorgar ni quitar privilegios. |

**Veredicto post-diseño**: sin violaciones. La sección Complexity Tracking queda vacía.

## Estructura del Proyecto

### Documentación (esta funcionalidad)

```text
specs/001-modulo-stock-pedidos/
├── plan.md              # Este archivo
├── spec.md              # Especificación
├── research.md          # Fase 0: decisiones técnicas (R-01 … R-10)
├── data-model.md        # Fase 1: entidades, vista, protocolo de escritura
├── quickstart.md        # Fase 1: puesta en marcha y 14 escenarios de validación
├── contracts/           # Fase 1: contrato REST
│   ├── README.md
│   └── openapi.yaml
├── checklists/
│   ├── requirements.md  # Calidad general del spec
│   └── stock-rules.md   # Calidad de los requisitos de negocio
└── tasks.md             # Fase 2: lo genera /speckit-tasks, NO este comando
```

### Código fuente (raíz del repositorio)

```text
StockModulo.sln
docker-compose.yml

src/
├── Stock.Api/                     # Web API REST + JWT. Dueña de toda la regla de negocio.
│   ├── Domain/
│   │   ├── Entities/              # Perfil, Usuario, Articulo, Movimiento, MovimientoDetalle, ErrorLog
│   │   ├── Pedido/                # Calculadora de pedido: lógica pura, sin dependencias
│   │   └── Validation/            # Validadores de artículo, movimiento y contraseña
│   ├── Data/
│   │   ├── StockDbContext.cs      # Dueño del esquema de las SEIS tablas, incluida ErrorLog
│   │   ├── ErrorLogDbContext.cs   # Conexión independiente para escribir; sin migraciones propias (R-08)
│   │   ├── ArticuloLockRepository.cs  # Bloqueo pesimista UPDLOCK del protocolo de escritura (R-02)
│   │   ├── Configurations/        # Columnas calculadas, CHECK, collations, índices
│   │   ├── Views/                 # vw_StockActual (entidad sin clave)
│   │   ├── Migrations/
│   │   └── Seed/                  # Perfiles base y usuario admin inicial
│   ├── Services/                  # Servicios de aplicación; dueños del protocolo de escritura
│   ├── Security/                  # PBKDF2 con salt por usuario, emisión y validación de JWT
│   ├── Export/                    # Generación .xlsx con ClosedXML
│   ├── Middleware/                # Manejador global de excepciones → ErrorLog
│   └── Controllers/               # Auth, Perfiles, Usuarios, Articulos, Movimientos, Consultas
│
└── Stock.Web/                     # ASP.NET MVC. Consume la API; sin acceso a base de negocio.
    ├── Controllers/               # Incluye CuentaController (login con cookie HttpOnly)
    ├── Views/                     # ABMs + Consulta de Stock Actual + Generar Pedido + Cuenta/Login
    │   └── Shared/                # _Layout y _BuscadorArticulos.cshtml: el buscador encapsulado,
    │                              #   definido una sola vez y consumido por las pantallas que
    │                              #   piden un Código (RF-034c)
    ├── wwwroot/js/                # buscador-articulos.js (RF-034) y articulo-precio.js (RF-016a):
    │                              #   la única lógica de cliente del proyecto
    ├── Models/                    # ViewModels
    ├── Services/                  # HttpClient tipado + DelegatingHandler que adjunta el Bearer
    ├── Middleware/                # ExceptionLoggingMiddleware → ErrorLog (única excepción, ver abajo)
    └── Data/                      # ErrorLog + ErrorLogDbContext propios, sólo escritura de bitácora

tests/
└── Stock.Tests/                   # NUnit. Categorías: Unit, Integration, Volumen
    ├── Unit/                      # Sin base de datos
    │   ├── PedidoCalculatorTests      # Conjunto de Datos de Referencia (6 × 4 celdas)
    │   ├── MovimientoValidatorTests   # Cantidad, precios, fecha, tipo
    │   ├── ArticuloValidatorTests
    │   ├── PasswordHasherTests
    │   └── PasswordPolicyTests
    ├── Integration/               # WebApplicationFactory in-process + SQL Server de compose
    │   ├── IntegrationTestBase        # Base efímera por corrida + fixture autenticado
    │   ├── EsquemaArticuloTests       # CHECK, columna calculada, índice único
    │   ├── EsquemaMovimientoTests     # CHECK, cascada, NO ACTION
    │   ├── EsquemaErrorLogTests       # La tabla existe tras migrar
    │   ├── EsquemaPerfilTests         # Índice único filtrado del perfil administrador
    │   ├── VistaStockActualTests      # Saldo y artículos sin movimientos
    │   ├── GenerarPedidoTests / ConsultaStockActualTests
    │   ├── MovimientoInvarianteTests / MovimientoModificacionTests
    │   ├── MovimientoAtomicidadTests / MovimientoNumeracionTests
    │   ├── MovimientoCodigoTests      # El Código identifica la línea de detalle (RF-020e)
    │   ├── ConcurrenciaTests          # CE-004: 5 ventas simultáneas
    │   ├── ArticulosTests / UsuariosTests / PerfilesTests / SeguridadTests
    │   ├── UltimoAdministradorTests   # No se puede quedar sin administrador (RF-005a)
    │   ├── IdentidadAdministradorTests # El privilegio sigue a la marca, no a la Descripción
    │   ├── *ContractTests             # GenerarPedido, Movimientos, Articulos, Auth, Seguridad
    │   ├── ExportacionExcelTests      # Ambas consultas: Generar Pedido y Stock Actual
    │   ├── ErrorLogTests              # La bitácora sobrevive al rollback
    │   ├── RendimientoTests           # Categoría Volumen, excluida por .runsettings
    │   └── RendimientoConcurrenteTests # Categoría Volumen: p95 con 5 clientes (CE-004)
    └── Web/                       # Capa MVC con WebApplicationFactory
        ├── WebTestBase                # Con y sin sesión simulada
        ├── GenerarPedidoControllerTests / MovimientosControllerTests
        ├── ArticulosControllerTests / SeguridadControllerTests
        ├── ErrorLogWebTests           # Excepción no controlada del MVC → ErrorLog
        ├── BuscadorArticulosTests     # El buscador encapsulado, sus consumidores y el tope
        └── BearerTokenHandlerTests    # Adjunta el Bearer, maneja el 401 y exige sesión
```

**Decisión de estructura**: dos proyectos de aplicación más uno de tests, exactamente los que nombra
`AGENTS.md`. Se evaluó extraer un `Stock.Domain` separado para la lógica de negocio; se **descartó**
porque la constitución fija el stack en `Stock.Web` + `Stock.Api` y un tercer proyecto sería
complejidad no justificada a esta escala. El aislamiento que aportaría se logra igual con la carpeta
`Domain/` dentro de `Stock.Api`, cuya calculadora de pedido no tiene dependencias de EF Core ni de
ASP.NET y por lo tanto es testeable de forma pura.

## Orden de implementación sugerido

Alineado con las prioridades del spec (P1 es el valor de negocio central) y con el ciclo test-first.
El detalle por tarea lo produce `/speckit-tasks`.

1. **Andamiaje**: solución, tres proyectos, `docker-compose.yml`, `DbContext` y primera migración.
2. **Modelo y esquema**: entidades, configuraciones (columnas calculadas, `CHECK`, collations, índices), `vw_StockActual`, siembra.
3. **P1 — Generar Pedido**: primero `PedidoCalculatorTests` contra el Conjunto de Datos de Referencia (rojo), luego la calculadora (verde). Es la pieza de mayor valor y la que mejor se presta al ciclo puro.
4. **P2 — Movimientos y Stock Actual**: protocolo de escritura con bloqueo pesimista, invariante ≥ 0, todo-o-nada, y la Consulta de Stock Actual con su pipeline filtrar→ordenar→recortar.
5. **P3 — ABM de Artículos**: validaciones RF-017 a RF-019 y baja restringida.
6. **P4 — Autenticación**: PBKDF2 con salt por usuario, emisión de JWT, protección de endpoints.
7. **P5 — ABM de Usuarios y Perfiles**: política de administrador y bajas restringidas.
8. **Transversales**: bitácora de errores, exportación a Excel, front MVC.
9. **Validación**: los 14 escenarios de [quickstart.md](./quickstart.md), incluido el de volumen que cierra CE-002.

## Complexity Tracking

> Se completa sólo si el Constitution Check registra violaciones a justificar.

Ningún **principio** de la constitución se viola. Sí hay dos desviaciones respecto de reglas
operativas, ambas deliberadas y con control compensatorio:

| Desviación | Por qué es necesaria | Alternativa más simple, y por qué se descartó |
|---|---|---|
| El `.runsettings` del proyecto de tests excluye la categoría `Volumen`, de modo que `dotnet test StockModulo.sln` —la puerta de calidad literal— no ejecuta el test de CE-002 | Ese test siembra 10.000 artículos y 100.000 líneas y tarda minutos. Incluirlo en la puerta haría que cada validación de rutina pague ese costo, con el efecto predecible de que se deje de correr la puerta | Dejarlo en la corrida por defecto: se descartó porque una puerta lenta se saltea, y una puerta que se saltea no protege nada. **Control compensatorio**: T131 la ejecuta explícitamente y T122/T123 asertan el presupuesto, de modo que CE-002 y CE-004 siguen verificados antes de dar la funcionalidad por terminada |
| `Stock.Web` accede a la base de datos para escribir la bitácora de errores (T126), rompiendo la regla de que el front consume sólo la API | RF-028 y CE-008 exigen que el **100%** de los errores de ejecución quede registrado. Una excepción no controlada en la capa MVC no llega al middleware de `Stock.Api`, así que sin esto habría una clase entera de errores invisible | Exponer un endpoint `POST /api/errores` en la API: se descartó porque un sumidero de escritura anónimo es superficie de abuso, y protegerlo exigiría un secreto compartido más su rotación — más complejidad que la que evita. **Alcance acotado**: sólo diagnóstico, sólo escritura, ninguna entidad de negocio; la regla sigue valiendo para todo dato de negocio |

Las dos oportunidades de agregar complejidad al diseño se evaluaron y se rechazaron explícitamente:

- Persistir el Stock Actual o materializar una vista indexada (R-01) — innecesario al volumen real y contrario al Principio III.
- Extraer un proyecto `Stock.Domain` — el stack fijado por la constitución define dos aplicaciones, y la separación buscada se logra con una carpeta sin dependencias de framework.
