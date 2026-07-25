# Plan de Implementación: Módulo de Stock — Generación automática de pedidos

**Rama**: `main` (sin rama de funcionalidad dedicada) | **Fecha**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Entrada**: Especificación de `/specs/001-modulo-stock-pedidos/spec.md`

## Resumen

Sitio web para un comercio de barrio que infiere automáticamente qué artículos reponer y en qué
cantidad, a partir de las compras y ventas registradas. El núcleo es la consulta **Generar Pedido**,
que combina dos parámetros de reposición ("solo bajo mínimo" y "Modo de Pedido") para producir
`MAX(0, Nivel − Stock Actual)` por artículo.

**Enfoque técnico**: solución .NET 8 de dos aplicaciones —`Stock.Api` (Web API REST con JWT, dueña
de toda la regla de negocio) y `Stock.Web` (ASP.NET MVC, consumidor sin acceso a base)— sobre SQL
Server 2017 vía EF Core Migrations.

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
| **I. Desarrollo Test-First** | ✅ PASA | El plan ordena el trabajo en rojo→verde→refactor. La lógica de pedido es una función pura testeable sin infraestructura, y el spec aporta el Conjunto de Datos de Referencia con las 36 cantidades esperadas: los tests de CE-003 pueden escribirse antes que el código. |
| **II. Aislamiento de la Lógica de IA** | ✅ PASA (por vacuidad) | Esta funcionalidad **no contiene lógica de IA**. La "inferencia" de qué pedir es una fórmula determinista (`MAX(0, Nivel − Stock Actual)`), no una llamada a un modelo. No hay prompts, ni invocación de modelos, ni parseo de respuestas que aislar. Si en el futuro se agregara sugerencia predictiva de reposición, este principio obligaría a un módulo dedicado. |
| **III. Fidelidad a la Fuente de Verdad** | ✅ PASA | Toda cantidad a pedir es trazable a movimientos reales. El Stock Actual no se estima ni se infiere: es el saldo de compras y ventas registradas. No hay caso de "baja confianza" que derivar a un humano porque no hay estimación. |
| **IV. Gestión Segura de Secretos** | ✅ PASA | Cadena de conexión, clave de firma JWT y contraseña del administrador inicial se inyectan por variable de entorno. Las contraseñas se derivan con PBKDF2 + salt aleatorio de 16 bytes por usuario. |

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
| **I. Test-First** | ✅ PASA | R-10 fija la estrategia: `Unit` para la calculadora de pedido, validadores y hashing; `Integration` contra SQL Server real para lo que depende del motor (bloqueos, collations, agregación). Se rechazó InMemory/SQLite justamente porque daría verde falso en los tres puntos de mayor riesgo. |
| **II. Aislamiento de IA** | ✅ PASA (por vacuidad) | El diseño no introdujo ninguna dependencia de IA. Sin cambios. |
| **III. Fuente de verdad** | ✅ **REFORZADO** | `vw_StockActual` es el único lugar donde se calcula el saldo, consumido tanto por las consultas como por la validación del invariante. `PrecioVenta` y `PrecioTotal` son columnas calculadas por el motor: no pueden divergir de sus insumos. Se rechazó explícitamente persistir el stock (R-01), que habría creado una segunda fuente de verdad. |
| **IV. Secretos** | ✅ PASA | R-03 y R-04 detallan la derivación de contraseñas y la firma del token; ningún secreto queda en el código ni en el repositorio. |

**Veredicto post-diseño**: sin violaciones. La sección Complexity Tracking queda vacía.

## Estructura del Proyecto

### Documentación (esta funcionalidad)

```text
specs/001-modulo-stock-pedidos/
├── plan.md              # Este archivo
├── spec.md              # Especificación
├── research.md          # Fase 0: decisiones técnicas (R-01 … R-10)
├── data-model.md        # Fase 1: entidades, vista, protocolo de escritura
├── quickstart.md        # Fase 1: puesta en marcha y 12 escenarios de validación
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
│   │   ├── StockDbContext.cs
│   │   ├── ErrorLogDbContext.cs   # Conexión independiente: sobrevive al rollback (R-08)
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
└── Stock.Web/                     # ASP.NET MVC. Consume la API; sin acceso a base de datos.
    ├── Controllers/
    ├── Views/                     # ABMs + Consulta de Stock Actual + Generar Pedido
    ├── Models/                    # ViewModels
    └── Services/                  # HttpClient tipado + DelegatingHandler que adjunta el Bearer

tests/
└── Stock.Tests/                   # NUnit
    ├── Unit/                      # Sin base: calculadora de pedido, validadores, hashing
    │   └── PedidoCalculatorTests   # Las 6 combinaciones vs. el Conjunto de Datos de Referencia
    └── Integration/               # Contra el SQL Server de compose
        ├── ConsultasTests          # Orden, recorte determinista, filtro, rango, artículos sin movimientos
        ├── MovimientosTests        # Invariante ≥ 0, todo-o-nada, baja restringida
        ├── ConcurrenciaTests       # CE-004: 5 ventas simultáneas
        ├── SeguridadTests          # 401/403, salts distintos, política de contraseña
        └── ErrorLogTests           # La bitácora sobrevive al rollback
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
9. **Validación**: los 12 escenarios de [quickstart.md](./quickstart.md), incluido el de volumen que cierra CE-002.

## Complexity Tracking

> Se completa sólo si el Constitution Check registra violaciones a justificar.

**Sin entradas**: el diseño no viola ningún principio ni restricción de la constitución. Las dos
oportunidades de agregar complejidad se evaluaron y se rechazaron explícitamente:

- Persistir el Stock Actual o materializar una vista indexada (R-01) — innecesario al volumen real y contrario al Principio III.
- Extraer un proyecto `Stock.Domain` — el stack fijado por la constitución define dos aplicaciones, y la separación buscada se logra con una carpeta sin dependencias de framework.
