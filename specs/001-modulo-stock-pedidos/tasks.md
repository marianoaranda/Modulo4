---
description: "Lista de tareas para la implementación del Módulo de Stock"
---

# Tareas: Módulo de Stock — Generación automática de pedidos

**Entrada**: Documentos de diseño de `/specs/001-modulo-stock-pedidos/`

**Prerrequisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **OBLIGATORIOS**. El template los trata como opcionales, pero el Principio I de la
constitución ("Desarrollo Test-First") es **NO NEGOCIABLE**: ningún código de producción se
introduce sin un test que haya fallado antes. Esto aplica **a todas las capas, incluida
`Stock.Web`**, y también al esquema de base de datos cuando codifica reglas de negocio.

**Qué cuenta como rojo en un lenguaje tipado**: un test que **no compila** porque el tipo bajo
prueba todavía no existe ya es un estado rojo válido. El ciclo completo es entonces de tres pasos:
(1) escribir el test → no compila; (2) crear el andamiaje mínimo —entidades, contexto, registro del
contexto, migración desnuda— → compila y **falla contra la base real**; (3) implementar la regla →
verde. El andamiaje del paso 2 no implementa ninguna de las reglas bajo prueba: sólo permite que el
test llegue a ejecutarse y fallar por el motivo correcto.

**Organización**: agrupadas por historia de usuario, para poder implementar y validar cada una de
forma independiente.

## Formato: `[ID] [P?] [Story] Descripción`

- **[P]**: puede correr en paralelo — **archivos distintos**, sin dependencias pendientes
- **[Story]**: historia a la que pertenece la tarea (US1…US5)
- Toda tarea incluye la ruta exacta del archivo
- Un **sufijo alfabético** (por ejemplo `T105a`) identifica una tarea incorporada después de la
  numeración inicial, que se ejecuta inmediatamente después de la tarea base con el mismo número.
  Es la misma convención que usa `spec.md` para los requisitos derivados y evita renumerar la lista
  completa cada vez que una auditoría agrega trabajo.

## Convención de rutas

Según la estructura fijada en [plan.md](./plan.md):

- API y lógica de negocio: `src/Stock.Api/`
- Front MVC: `src/Stock.Web/`
- Tests: `tests/Stock.Tests/`

---

## Fase 1: Setup (Infraestructura compartida)

**Propósito**: solución, proyectos y entorno reproducible.

- [X] T001 Crear la solución `StockModulo.sln` en la raíz del repositorio y las carpetas `src/` y `tests/`
- [X] T002 Crear el proyecto Web API `src/Stock.Api/Stock.Api.csproj` sobre `net8.0`
- [X] T003 [P] Crear el proyecto ASP.NET MVC `src/Stock.Web/Stock.Web.csproj` sobre `net8.0`
- [X] T004 [P] Crear el proyecto de tests NUnit `tests/Stock.Tests/Stock.Tests.csproj` sobre `net8.0`
- [X] T005 Agregar los tres proyectos a `StockModulo.sln` y las referencias de `tests/Stock.Tests` hacia `src/Stock.Api` y `src/Stock.Web`
- [X] T006 Agregar a `src/Stock.Api/Stock.Api.csproj` los paquetes `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Authentication.JwtBearer` y `ClosedXML` (modifica el archivo creado por T002)
- [X] T007 Agregar a `tests/Stock.Tests/Stock.Tests.csproj` los paquetes `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk` y `Microsoft.AspNetCore.Mvc.Testing` (modifica el archivo creado por T004)
- [X] T008 Agregar a `src/Stock.Web/Stock.Web.csproj` el soporte de `HttpClient` tipado (`Microsoft.Extensions.Http`) y `Microsoft.EntityFrameworkCore.SqlServer`, este último **sólo** para escribir la bitácora de errores (modifica el archivo creado por T003)
- [X] T009 Declarar `public partial class Program` en su propio namespace en `src/Stock.Api/Program.cs` y `src/Stock.Web/Program.cs`, para que `Stock.Api.Program` y `Stock.Web.Program` no colisionen en el proyecto de tests, que referencia a ambos (R-10)
- [X] T010 Crear `docker-compose.yml` en la raíz con SQL Server 2017, `Stock.Api` en el puerto 5279 y `Stock.Web` en el 5280, según [quickstart.md](./quickstart.md). **Ningún valor secreto literal**: la contraseña `SA` de SQL Server, la clave de firma JWT y `SEED_ADMIN_PASSWORD` se referencian como `${SA_PASSWORD}`, `${JWT_SIGNING_KEY}` y `${SEED_ADMIN_PASSWORD}`, que Docker Compose resuelve desde el `.env` de T013a
- [X] T011 [P] Crear `src/Stock.Api/Dockerfile` y `src/Stock.Web/Dockerfile`
- [X] T012 Configurar la lectura por variable de entorno de la cadena de conexión, la clave de firma JWT y `SEED_ADMIN_PASSWORD` en `src/Stock.Api/appsettings.json` y `src/Stock.Web/appsettings.json`, dejando los valores **vacíos** en el archivo versionado y haciendo que la API falle al arrancar con un mensaje explícito si alguno no está definido, en vez de caer en un valor por defecto (Principio IV)
- [X] T013 [P] Crear `.gitignore` en la raíz excluyendo `bin/`, `obj/`, `*.user`, `.env` y `*.env.local`, de modo que ningún archivo de secretos locales pueda commitearse por descuido
- [X] T013a Crear `.env.example` en la raíz con las tres claves (`SA_PASSWORD`, `JWT_SIGNING_KEY`, `SEED_ADMIN_PASSWORD`) y **placeholders, no valores reales**, y crear el `.env` local a partir de él con valores generados. `.env.example` se commitea; `.env` queda ignorado por T013 (Principio IV; depende de T010 y T013)
- [X] T013b [P] Documentar en [quickstart.md](./quickstart.md) y en `AGENTS.md` que el primer paso de la puesta en marcha es `cp .env.example .env` y completar los tres valores, reemplazando la mención de la credencial fija `admin / Admin1234` de `AGENTS.md` por una referencia a `SEED_ADMIN_PASSWORD`
- [X] T014 [P] Definir las categorías `Unit`, `Integration` y `Volumen` en `tests/Stock.Tests/TestCategories.cs`
- [X] T015 Crear `tests/Stock.Tests/.runsettings` que excluya la categoría `Volumen`, y referenciarlo desde `tests/Stock.Tests/Stock.Tests.csproj` con `RunSettingsFilePath`, de modo que `dotnet test StockModulo.sln` —la puerta de calidad literal de la constitución— **no** dispare la siembra masiva de CE-002 (desviación registrada en Complexity Tracking de [plan.md](./plan.md))

---

## Fase 2: Fundacional (Prerrequisitos bloqueantes)

**Propósito**: esquema de datos y andamiaje que TODAS las historias necesitan.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta terminar esta fase.

**Nota sobre el alcance de la migración**: las **seis** entidades entran acá, aunque
`Perfil`/`Usuario` sólo se usen en US4/US5 y `ErrorLog` en la Fase 8. Hay una sola base y un solo
historial de migraciones; fragmentarlo por historia agregaría churn sin beneficio.

**Nota sobre el ciclo rojo→verde del esquema**: el esquema **codifica reglas de negocio** (`CHECK`
de orden de stocks, columnas calculadas de precio, índice único de Código, collations). Por eso la
fase se ordena en tres bloques: los tests (T016–T019a) se escriben primero y no compilan; el
andamiaje (T020–T029) los hace compilar y **fallar contra tablas reales sin restricciones**, lo que
T030 verifica explícitamente; recién entonces las configuraciones (T031–T037) los ponen en verde,
que es lo que confirma T038. La migración desnuda de T029 existe exactamente para producir ese
rojo: sin ella las tablas nacerían ya con los `CHECK` puestos y los tests estarían en verde desde
el primer momento, sin haber demostrado nada.

### Bloque 1 — Tests del esquema ⚠️ ESCRIBIR PRIMERO. No compilan: ése es el primer rojo

- [X] T016 [P] Tests de las restricciones de `Articulo` a nivel de base (rechazo de `StockMinimo > PuntoPedido`, de valores negativos y de `Codigo` duplicado —incluida la colisión por diferencia de mayúsculas—, y cálculo de `PrecioVenta`) en `tests/Stock.Tests/Integration/EsquemaArticuloTests.cs` (RF-016, RF-017, RF-017a, RF-018, RF-019)
- [X] T017 [P] Tests de las restricciones de `MovimientoDetalle` a nivel de base (rechazo de `Cantidad` ≤ 0 y > 1.000.000, rechazo de `PrecioUnitario` negativo y aceptación de `PrecioUnitario` = 0, cálculo de `PrecioTotal`, borrado en cascada desde el encabezado y `NO ACTION` hacia `Articulo`) en `tests/Stock.Tests/Integration/EsquemaMovimientoTests.cs` (RF-014a, RF-020c, RF-021, RF-023, RF-023a, RF-023c)
- [X] T018 [P] Test de que `vw_StockActual` devuelve 0 para artículos sin movimientos y el saldo correcto con compras y ventas, en `tests/Stock.Tests/Integration/VistaStockActualTests.cs` (RF-030)
- [X] T019 [P] Test de que la tabla `dbo.ErrorLog` existe y admite inserción tras aplicar las migraciones, en `tests/Stock.Tests/Integration/EsquemaErrorLogTests.cs` (RF-028)
- [X] T019a [P] Test de la restricción de unicidad del perfil administrador a nivel de base en `tests/Stock.Tests/Integration/EsquemaPerfilTests.cs`: insertar un segundo `Perfil` con `EsAdministrador = 1` se rechaza por el índice único filtrado, mientras que varios perfiles con `EsAdministrador = 0` conviven sin problema (RF-003a)

### Bloque 2 — Andamiaje mínimo: hace que los tests compilen y fallen contra la base real

- [X] T020 [P] Crear la entidad `Perfil` en `src/Stock.Api/Domain/Entities/Perfil.cs` con `Descripcion` y la marca `EsAdministrador` (`bool`), que es la identidad de autorización estable e independiente de la Descripción (RF-003a)
- [X] T021 [P] Crear la entidad `Usuario` en `src/Stock.Api/Domain/Entities/Usuario.cs` con `Hash` y `Salt` como propiedades separadas
- [X] T022 [P] Crear la entidad `Articulo` en `src/Stock.Api/Domain/Entities/Articulo.cs` con los tres parámetros de reposición como `int` (RF-013a)
- [X] T023 [P] Crear la entidad `Movimiento` y el enum `TipoMovimiento` (Compra=1, Venta=2) en `src/Stock.Api/Domain/Entities/Movimiento.cs`
- [X] T024 [P] Crear la entidad `MovimientoDetalle` en `src/Stock.Api/Domain/Entities/MovimientoDetalle.cs`
- [X] T025 [P] Crear la entidad `ErrorLog` en `src/Stock.Api/Domain/Entities/ErrorLog.cs`
- [X] T026 Crear `src/Stock.Api/Data/StockDbContext.cs` con los `DbSet` de las **seis** entidades —incluida `ErrorLog`, que es dueña de su esquema aunque en runtime se escriba por otra conexión (R-08)— **sin ninguna configuración de restricciones**
- [X] T027 Registrar `StockDbContext` en `src/Stock.Api/Program.cs` con la cadena de conexión tomada de configuración. Es el mínimo necesario para que la `WebApplicationFactory` de T028 arranque: sin este registro, los tests del Bloque 1 fallarían por error de arranque y la puerta T030 verificaría un rojo por el motivo equivocado
- [X] T028 Crear la base de tests de integración en `tests/Stock.Tests/Integration/IntegrationTestBase.cs`: levanta `WebApplicationFactory<Stock.Api.Program>` in-process, crea una base efímera por corrida en el SQL Server de compose, le inyecta la cadena de conexión, aplica migraciones y la elimina al finalizar (R-10)
- [X] T029 Generar la migración desnuda en `src/Stock.Api/Data/Migrations/`: crea las seis tablas **sin** `CHECK`, sin columnas calculadas, sin índices únicos, sin collations y sin la vista
- [X] T030 **Verificar el rojo**: T016–T019a compilan, arrancan y fallan **por la restricción ausente**, no por error de configuración. Un test que pase en este punto está mal escrito y debe corregirse antes de seguir

### Bloque 3 — Configuraciones: ponen los tests en verde

- [X] T031 [P] Configurar `Articulo` en `src/Stock.Api/Data/Configurations/ArticuloConfiguration.cs`: índice único de `Codigo`, collation `Modern_Spanish_CI_AS` en `Codigo`, collation `Modern_Spanish_CI_AI` en `Descripcion`, columna calculada persistida `PrecioVenta`, `CHECK` de no negatividad y `CHECK (StockMinimo <= PuntoPedido AND PuntoPedido <= StockIdeal)`
- [X] T032 [P] Configurar `Movimiento` en `src/Stock.Api/Data/Configurations/MovimientoConfiguration.cs`: `Numero` como PK `IDENTITY` y `CHECK Tipo IN (1,2)`
- [X] T033 [P] Configurar `MovimientoDetalle` en `src/Stock.Api/Data/Configurations/MovimientoDetalleConfiguration.cs`: columna calculada persistida `PrecioTotal`, `CHECK (Cantidad > 0 AND Cantidad <= 1000000)`, `CHECK (PrecioUnitario >= 0)` (RF-023c), FK a `Movimiento` con `CASCADE`, FK a `Articulo` con `NO ACTION` e índice `IX_MovimientoDetalle_ArticuloId` con `INCLUDE (Cantidad, MovimientoNumero)`
- [X] T034 [P] Configurar `Usuario` y `Perfil` en `src/Stock.Api/Data/Configurations/SeguridadConfiguration.cs`: índice único de `NombreUsuario`, FK `Usuario.PerfilId` con `NO ACTION`, `Perfil.EsAdministrador` con `DEFAULT 0` e **índice único filtrado** `WHERE EsAdministrador = 1`, que garantiza en el esquema que exista a lo sumo un perfil administrador (RF-003a)
- [X] T035 [P] Configurar `ErrorLog` en `src/Stock.Api/Data/Configurations/ErrorLogConfiguration.cs` (columnas de RF-028, sin relaciones)
- [X] T036 Crear la entidad sin clave `StockActualView` en `src/Stock.Api/Data/Views/StockActualView.cs` mapeada a `vw_StockActual`
- [X] T037 Generar la migración de restricciones en `src/Stock.Api/Data/Migrations/`: agrega `CHECK`, columnas calculadas, índices únicos —incluido el filtrado de `Perfil.EsAdministrador`— y collations, más el `CREATE VIEW dbo.vw_StockActual` con el `LEFT JOIN` e `ISNULL(...,0)` de [data-model.md](./data-model.md)
- [X] T038 **Verificar el verde**: T016–T019a pasan contra la migración completa

### Bloque 4 — Andamiaje de aplicación

- [X] T039 [P] Crear la siembra de perfiles base en `src/Stock.Api/Data/Seed/DbSeeder.cs`: `administrador` con `EsAdministrador = true`, `administrativo` y `vendedor` con `false`. Es el **único** lugar del sistema que establece la marca (RF-003a)
- [X] T040 Completar `src/Stock.Api/Program.cs`: controladores, respuestas `application/problem+json` y el flag `ApplyMigrationsOnStartup` usado sólo en compose (el registro del `DbContext` ya lo hizo T027)
- [X] T041 [P] Completar `src/Stock.Web/Program.cs`: MVC, `HttpClient` tipado apuntando a `Stock.Api` y páginas de error. **Sin el filtro de autorización global**: ese filtro es código de producción de RF-012 y su test es T096, en la Fase 6, así que introducirlo acá sería implementar antes del rojo y violaría el Principio I. Lo agrega T105b, después de T096
- [X] T042 [P] Crear la base de tests de la capa web en `tests/Stock.Tests/Web/WebTestBase.cs` usando `WebApplicationFactory<Stock.Web.Program>` con la API simulada (R-10). Todavía **sin** sesión simulada: en esta fase la app web no exige autenticación. El fixture de sesión lo agrega T105a, en paralelo exacto con lo que T100 hace del lado de la API

**Punto de control**: esquema listo, migrable y con sus reglas verificadas por un ciclo rojo→verde real. Las historias pueden comenzar.

---

## Fase 3: Historia de Usuario 1 — Generar la lista de pedido (Prioridad: P1) 🎯 MVP

**Objetivo**: entregar la consulta que resuelve el problema central del negocio — qué reponer y cuánto — con sus 6 combinaciones de parámetros y exportación a Excel.

**Test independiente**: sembrar el Conjunto de Datos de Referencia del spec (4 artículos con Stock Actual 5, 15, 60 y 0), ejecutar las 6 combinaciones y verificar la matriz de 6 × 4 = 24 celdas: **15 cantidades asertadas y 9 exclusiones** que deben comprobarse como filas ausentes.

### Tests de la Historia 1 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T043 [P] [US1] Test unitario de las 6 combinaciones contra el Conjunto de Datos de Referencia — 15 cantidades asertadas y 9 exclusiones verificadas como ausencia de fila — en `tests/Stock.Tests/Unit/PedidoCalculatorTests.cs`
- [X] T044 [US1] Agregar a `tests/Stock.Tests/Unit/PedidoCalculatorTests.cs` el caso de que la cantidad a pedir nunca es negativa cuando el stock supera el nivel (mismo archivo que T043, no paralelizable)
- [X] T045 [P] [US1] Test de contrato de `GET /api/consultas/generar-pedido` en `tests/Stock.Tests/Integration/GenerarPedidoContractTests.cs`: omitir `soloBajoMinimo` o `modoPedido` devuelve 400 sin aplicar ningún valor por defecto, un `modoPedido` inválido devuelve 400, y el endpoint **no acepta parámetros de rango** (RF-026a, RF-026b)
- [X] T046 [P] [US1] Test de integración de que un artículo sin movimientos aparece con stock 0 y cantidad a pedir igual a su stock mínimo, en `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` (V-7)
- [X] T047 [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` el caso de que con `soloBajoMinimo=false` se listan todos los artículos incluidos los de cantidad 0
- [X] T048 [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` el caso del resultado vacío: la respuesta trae cero filas, sin error, y la vista muestra el texto exacto de RF-032
- [X] T048a [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` los casos del tope y el orden que RF-027 exige a **ambas** consultas y que hasta ahora sólo se verificaban en Stock Actual: sobre más de 10.000 artículos, dos corridas devuelven el mismo conjunto ordenado por Código con `truncado=true`, y el recorte se aplica **después** de filtrar y ordenar (RF-027, RF-027b, RF-027c)
- [X] T048b [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` el caso del filtro por descripción insensible a mayúsculas y acentos en Generar Pedido, y que un filtro vacío no acota el resultado (RF-027a)
- [X] T049 [P] [US1] Test de integración de que el `.xlsx` exportado replica filas, orden y recorte de la respuesta JSON, **y que un resultado vacío exporta sólo los encabezados**, en `tests/Stock.Tests/Integration/ExportacionExcelTests.cs` (V-10, RF-031)
- [X] T050 [P] [US1] Test de la vista de Generar Pedido en `tests/Stock.Tests/Web/GenerarPedidoControllerTests.cs`: envío de los dos parámetros, render del aviso de recorte con el texto exacto de RF-032a, mensaje de resultado vacío con el texto exacto de RF-032 y retransmisión del Excel

### Implementación de la Historia 1

- [X] T051 [P] [US1] Crear el enum `ModoPedido` en `src/Stock.Api/Domain/Pedido/ModoPedido.cs`
- [X] T052 [US1] Implementar `PedidoCalculator` como función pura sin dependencias de EF Core ni ASP.NET en `src/Stock.Api/Domain/Pedido/PedidoCalculator.cs` (Nivel, Incluir y `MAX(0, Nivel − Stock)`)
- [X] T053 [US1] Implementar `GenerarPedidoQueryService` en `src/Stock.Api/Services/GenerarPedidoQueryService.cs` consumiendo `vw_StockActual` y aplicando el pipeline filtrar → ordenar por Código → recortar a 10.000 → marcar `truncado`
- [X] T054 [US1] Implementar `ExcelExporter` con ClosedXML en `src/Stock.Api/Export/ExcelExporter.cs`, generando `.xlsx` a partir de las filas ya recortadas y con sólo encabezados si no hay filas (compartido con US2)
- [X] T055 [US1] Implementar `GET /api/consultas/generar-pedido` y `GET /api/consultas/generar-pedido/excel` en `src/Stock.Api/Controllers/ConsultasController.cs` según [contracts/openapi.yaml](./contracts/openapi.yaml)
- [X] T056 [P] [US1] Crear el `GenerarPedidoViewModel` en `src/Stock.Web/Models/GenerarPedidoViewModel.cs`
- [X] T057 [US1] Implementar `GenerarPedidoController` en `src/Stock.Web/Controllers/GenerarPedidoController.cs` consumiendo la API y retransmitiendo el Excel
- [X] T058 [US1] Crear la vista de Generar Pedido en `src/Stock.Web/Views/GenerarPedido/Index.cshtml` con los dos parámetros de reposición, el filtro opcional, el botón de exportar, y los dos mensajes informativos con el **texto literal** que fijan RF-032 y RF-032a, tomados de un archivo de recursos compartido para que vista y test no puedan divergir

**Punto de control**: US1 funciona de punta a punta sobre datos sembrados. Es el MVP demostrable.

---

## Fase 4: Historia de Usuario 2 — Movimientos y Stock Actual (Prioridad: P2)

**Objetivo**: registrar compras y ventas con el invariante de stock no negativo garantizado, y consultar el Stock Actual por rango.

**Test independiente**: registrar un conjunto de compras y ventas y verificar que la Consulta de Stock Actual devuelve, para el rango pedido, la suma de compras menos la suma de ventas de cada artículo, exportable a Excel.

### Tests de la Historia 2 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T059 [P] [US2] Tests unitarios del validador de movimiento: cantidad ≤ 0, cantidad > 1.000.000, **Precio Unitario negativo o > 9.999.999,99**, **Precio Total > 999.999.999.999,99**, fecha futura y tipo inválido, en `tests/Stock.Tests/Unit/MovimientoValidatorTests.cs`. **Sin caso de "cantidad no entera"**: el validador recibe la cantidad ya tipada como `int`, así que un no entero no puede alcanzarlo y el caso sería vacuo. Ese rechazo se verifica en T070a, a nivel de contrato (RF-020b, RF-020d, RF-023, RF-023a, RF-023c)
- [X] T060 [US2] Agregar a `tests/Stock.Tests/Unit/MovimientoValidatorTests.cs` el caso de regresión de RF-023b: un precio unitario deliberadamente distinto del Precio de Costo y del Precio de Venta del artículo **se acepta**
- [X] T061 [P] [US2] Tests del invariante de stock en `tests/Stock.Tests/Integration/MovimientoInvarianteTests.cs`: venta que dejaría el stock por debajo de 0 rechazada con 422 sin grabar nada, y baja de una compra ya consumida por ventas posteriores rechazada con 422 (V-2, RF-024a)
- [X] T062 [P] [US2] Tests de modificación en `tests/Stock.Tests/Integration/MovimientoModificacionTests.cs`: una modificación **exitosa** de cantidades recalcula correctamente el Stock Actual, y una modificación que dejaría el saldo negativo se rechaza con 422 (RF-022, RF-024a)
- [X] T063 [P] [US2] Test todo-o-nada en `tests/Stock.Tests/Integration/MovimientoAtomicidadTests.cs`: un movimiento de 3 líneas donde falla la tercera no aplica ninguna (V-3, RF-024c)
- [X] T064 [P] [US2] Tests de numeración en `tests/Stock.Tests/Integration/MovimientoNumeracionTests.cs`: `Numero` único y compartido entre compras y ventas, **y no reutilizado tras una baja** (RF-020a)
- [X] T065 [P] [US2] Test de concurrencia en `tests/Stock.Tests/Integration/ConcurrenciaTests.cs`: 5 ventas simultáneas de 4 unidades sobre un stock de 10 graban a lo sumo 2, el resto falla con 422 de stock insuficiente y ninguna respuesta es un error de reintento (V-4, RF-024b)
- [X] T066 [P] [US2] Tests del recorte determinista en `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs`: dos corridas sin filtro sobre más de 10.000 artículos devuelven el mismo conjunto ordenado por Código y `truncado=true` (V-6)
- [X] T067 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` los casos del rango de códigos: extremos inclusive, extremos vacíos y rango invertido con resultado vacío sin error (V-9)
- [X] T068 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` el caso de la collation del Código: dos códigos que difieren sólo en mayúsculas caen dentro del mismo rango, y dos que difieren en acento no (RF-025a)
- [X] T069 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` el caso del filtro por descripción insensible a mayúsculas y acentos (V-8, RF-027a)
- [X] T069a [P] [US2] Test de la exportación de la **Consulta de Stock Actual** en `tests/Stock.Tests/Integration/ExportacionStockActualExcelTests.cs`: el `.xlsx` de `GET /api/consultas/stock-actual/excel` replica filas, orden y recorte de la respuesta JSON con el mismo rango y filtro, y un resultado vacío exporta sólo los encabezados. RF-031 exige la réplica exacta en **ambas** exportaciones y hasta ahora sólo se verificaba la de Generar Pedido (RF-031)
- [X] T070 [P] [US2] Test de contrato del CRUD de `/api/movimientos` en `tests/Stock.Tests/Integration/MovimientosContractTests.cs`, incluido el recorrido completo alta → lectura → modificación → baja
  - **Nota (auditoría 2026-07-31)**: se completó enviando `articuloId` en la línea de detalle. `contracts/openapi.yaml` y RF-020e especifican `codigo`, así que hoy este test **no verifica el contrato vigente**. T133 lo corrige y T139/T140 lo ponen en verde.
- [X] T070a [US2] Agregar a `tests/Stock.Tests/Integration/MovimientosContractTests.cs` el rechazo del no entero en el borde: un cuerpo JSON con `"cantidad": 1.5` devuelve **400 `application/problem+json`** identificando el campo, sin grabar nada y sin llegar al validador de dominio. Se asierta el cuerpo del problema, no sólo el código, para distinguirlo del 400 genérico del framework (RF-018a)
- [X] T071 [P] [US2] Tests de la capa web de movimientos y consulta de stock en `tests/Stock.Tests/Web/MovimientosControllerTests.cs`: alta con varias líneas de detalle y propagación del error 422 a la vista

### Implementación de la Historia 2

- [X] T072 [P] [US2] Implementar `MovimientoValidator` en `src/Stock.Api/Domain/Validation/MovimientoValidator.cs` con los límites concretos de RF-023a y **sin** validación cruzada de precios (RF-023b)
- [X] T073 [US2] Implementar el bloqueo pesimista en `src/Stock.Api/Data/ArticuloLockRepository.cs`: `SELECT ... WITH (UPDLOCK, HOLDLOCK)` sobre las filas de `Articulo` afectadas, ordenadas por `ArticuloId` ascendente
- [X] T074 [US2] Implementar `MovimientoService` en `src/Stock.Api/Services/MovimientoService.cs` aplicando el protocolo de escritura completo de [data-model.md](./data-model.md): transacción → bloqueo → leer `vw_StockActual` → validar ≥ 0 en todas las líneas → aplicar → confirmar
  - **Nota (auditoría 2026-07-31)**: quedó **incompleto** respecto del protocolo que referencia. El paso 2 de [data-model.md](./data-model.md) —resolver el Código de cada línea a su `ArticuloId` dentro de la transacción, abortando con 404 si no existe— no está implementado, porque el servicio recibe el `ArticuloId` ya resuelto. Lo cierra T140.
- [X] T075 [US2] Implementar el CRUD de `/api/movimientos` en `src/Stock.Api/Controllers/MovimientosController.cs` con el mapeo de códigos 400/404/422 de [contracts/README.md](./contracts/README.md)
- [X] T076 [US2] Implementar `StockActualQueryService` en `src/Stock.Api/Services/StockActualQueryService.cs` con el mismo pipeline filtrar → ordenar → recortar → marcar, reutilizando `vw_StockActual`
- [X] T077 [US2] Implementar `GET /api/consultas/stock-actual` y `GET /api/consultas/stock-actual/excel` en `src/Stock.Api/Controllers/ConsultasController.cs` reutilizando el `ExcelExporter` de T054
- [X] T078 [P] [US2] Crear los ViewModels de movimiento y de consulta de stock en `src/Stock.Web/Models/MovimientoViewModel.cs` y `src/Stock.Web/Models/StockActualViewModel.cs`
- [X] T079 [US2] Implementar `MovimientosController` en `src/Stock.Web/Controllers/MovimientosController.cs` con alta, baja y modificación de encabezado y detalle
- [X] T080 [US2] Crear las vistas de movimientos en `src/Stock.Web/Views/Movimientos/` (Index, Create, Edit, Delete) con carga de líneas de detalle
- [X] T081 [US2] Implementar `StockActualController` y la vista `src/Stock.Web/Views/StockActual/Index.cshtml` con rango, filtro, exportación y los dos mensajes informativos —recorte y resultado vacío— con el **texto literal** de RF-032a y RF-032, reutilizando el mismo archivo de recursos que T058 para que ambas pantallas digan exactamente lo mismo

**Punto de control**: US1 y US2 funcionan de forma independiente. El Stock Actual ya proviene de movimientos reales.

---

## Fase 5: Historia de Usuario 3 — ABM de Artículos (Prioridad: P3)

**Objetivo**: mantener el catálogo con sus parámetros de reposición y su precio calculado.

**Test independiente**: dar de alta, modificar y eliminar artículos verificando cada validación (código único, no negatividad, orden de los tres stocks, precio de venta calculado) y la baja restringida.

### Tests de la Historia 3 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T082 [P] [US3] Tests unitarios del validador de artículo (código vacío, valores negativos, incumplimiento de Mínimo ≤ Punto de Pedido ≤ Ideal) en `tests/Stock.Tests/Unit/ArticuloValidatorTests.cs`. **Sin caso de "parámetro no entero"**, por el mismo motivo que T059: los tres parámetros llegan tipados como `int`. Ese rechazo se verifica en T085a (RF-018, RF-019)
- [X] T083 [P] [US3] Tests de la API de artículos en `tests/Stock.Tests/Integration/ArticulosTests.cs`: precio de venta calculado, código duplicado rechazado con 409 —incluido el duplicado que difiere sólo en mayúsculas—, y baja de artículo con movimientos rechazada con 409 conservando el histórico (RF-014a, RF-016, RF-017, RF-017a)
- [X] T084 [US3] Agregar a `tests/Stock.Tests/Integration/ArticulosTests.cs` el caso de que modificar los parámetros de reposición se refleja en la siguiente ejecución de Generar Pedido (RF-033)
- [X] T085 [P] [US3] Test de contrato del CRUD de `/api/articulos` en `tests/Stock.Tests/Integration/ArticulosContractTests.cs`, incluido el recorrido completo alta → lectura → modificación → baja (RF-013, RF-014, RF-015)
- [X] T085a [US3] Agregar a `tests/Stock.Tests/Integration/ArticulosContractTests.cs` el rechazo del no entero en el borde: un cuerpo con `"stockMinimo": 2.5` devuelve **400 `application/problem+json`** identificando el campo, sin grabar (RF-018a)
- [X] T086 [P] [US3] Tests de la capa web del ABM de artículos en `tests/Stock.Tests/Web/ArticulosControllerTests.cs`: precio de venta como sólo lectura y propagación del 409 a la vista

### Implementación de la Historia 3

- [X] T087 [P] [US3] Implementar `ArticuloValidator` en `src/Stock.Api/Domain/Validation/ArticuloValidator.cs` (RF-018, RF-019)
- [X] T088 [US3] Implementar `ArticuloService` en `src/Stock.Api/Services/ArticuloService.cs` con la verificación previa de baja restringida para devolver un 409 legible en vez de una violación de FK
- [X] T089 [US3] Implementar el CRUD de `/api/articulos` en `src/Stock.Api/Controllers/ArticulosController.cs`
- [X] T090 [P] [US3] Crear el `ArticuloViewModel` en `src/Stock.Web/Models/ArticuloViewModel.cs`
- [X] T091 [US3] Implementar `ArticulosController` en `src/Stock.Web/Controllers/ArticulosController.cs`
- [X] T092 [US3] Crear las vistas del ABM de artículos en `src/Stock.Web/Views/Articulos/` (Index, Create, Edit, Delete) mostrando el precio de venta como campo de sólo lectura

**Punto de control**: US1, US2 y US3 funcionan de forma independiente. El valor de negocio central está completo.

---

## Fase 6: Historia de Usuario 4 — Inicio de sesión y protección del acceso (Prioridad: P4)

**Objetivo**: exigir sesión autenticada válida para toda funcionalidad salvo el login.

**Test independiente**: verificar que sin token toda llamada devuelve 401, que el login rechaza credenciales inválidas con el mensaje genérico y que con credenciales válidas autoriza el ingreso.

### Tests de la Historia 4 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T093 [P] [US4] Tests unitarios del hashing PBKDF2: dos usuarios con la misma contraseña producen salts y hashes distintos, y la verificación acepta la contraseña correcta y rechaza la incorrecta, en `tests/Stock.Tests/Unit/PasswordHasherTests.cs` (RF-008)
- [X] T094 [P] [US4] Tests de acceso en `tests/Stock.Tests/Integration/SeguridadTests.cs`: toda llamada sin token devuelve 401, un token expirado devuelve 401, y usuario inexistente y contraseña incorrecta devuelven el **mismo** mensaje "Usuario o contraseña incorrectos" (RF-011, RF-012, V-11)
- [X] T095 [P] [US4] Test de contrato de `POST /api/auth/login` en `tests/Stock.Tests/Integration/AuthContractTests.cs`
- [X] T096 [P] [US4] Tests del `BearerTokenHandler` y del filtro de autorización global en `tests/Stock.Tests/Web/BearerTokenHandlerTests.cs`: adjunta el encabezado `Authorization` en las llamadas salientes, una vista protegida no se renderiza sin sesión, y ante un 401 de la API cierra la sesión y redirige al login

### Implementación de la Historia 4

- [X] T097 [P] [US4] Implementar `PasswordHasher` con PBKDF2-HMAC-SHA256, 210.000 iteraciones, salt aleatorio de 16 bytes y comparación en tiempo fijo, en `src/Stock.Api/Security/PasswordHasher.cs` (R-03)
- [X] T098 [P] [US4] Implementar `JwtTokenService` en `src/Stock.Api/Security/JwtTokenService.cs`: HS256, vigencia 8 horas, `ClockSkew` en cero y claims `sub`, `name`, `role` (Descripción del perfil, **sólo para mostrar**) y `es_admin` (derivado de `Perfil.EsAdministrador`, única base de la autorización) (R-04, RF-003a)
- [X] T099 [US4] Implementar `POST /api/auth/login` en `src/Stock.Api/Controllers/AuthController.cs` devolviendo el mismo mensaje ante usuario inexistente y contraseña incorrecta
- [X] T100 [US4] Agregar el fixture de tests autenticados en `tests/Stock.Tests/Integration/IntegrationTestBase.cs`: obtiene un token del cliente de la factory y lo adjunta a las llamadas
- [X] T101 [US4] Registrar la autenticación JWT en `src/Stock.Api/Program.cs` y aplicar `[Authorize]` a todos los controladores excepto `AuthController`
- [X] T102 [US4] Adaptar los tests de integración de US1, US2 y US3 al fixture autenticado de T100, y confirmar que vuelven a verde tras T101
- [X] T103 [US4] Agregar la siembra del usuario `admin` en `src/Stock.Api/Data/Seed/DbSeeder.cs`, asignándole el perfil que tiene `EsAdministrador = true` —localizado por la marca, no por la Descripción— y tomando la contraseña de `SEED_ADMIN_PASSWORD`, sin valor por defecto embebido
- [X] T104 [US4] Implementar el `DelegatingHandler` que adjunta `Authorization: Bearer` y maneja el 401 cerrando sesión, en `src/Stock.Web/Services/BearerTokenHandler.cs`, y registrarlo en el `HttpClient` tipado
- [X] T105 [US4] Implementar el login del front con cookie `HttpOnly` que guarda el JWT como claim protegido, en `src/Stock.Web/Controllers/CuentaController.cs` y `src/Stock.Web/Views/Cuenta/Login.cshtml`
- [X] T105a [US4] Agregar el fixture de sesión simulada a `tests/Stock.Tests/Web/WebTestBase.cs`: emite la cookie de autenticación con el JWT de prueba ya adentro, para que un test de capa web pueda elegir explícitamente correr con sesión o sin ella (contraparte web de T100)
- [X] T105b [US4] Registrar el **filtro de autorización global** en `src/Stock.Web/Program.cs`, de modo que ninguna vista distinta del login se renderice sin sesión (RF-012). Es la implementación que T096 dejó en rojo, y **rompe deliberadamente** los tests de capa web de US1–US3, igual que T101 rompe los de integración
- [X] T105c [US4] Adaptar los tests de capa web de US1, US2 y US3 (`GenerarPedidoControllerTests`, `MovimientosControllerTests`, `ArticulosControllerTests`) al fixture con sesión de T105a, y confirmar que vuelven a verde tras T105b (contraparte web de T102)

**Punto de control**: el sistema exige autenticación en todas las pantallas salvo el login, y toda la suite está en verde.

---

## Fase 7: Historia de Usuario 5 — ABM de Usuarios y Perfiles (Prioridad: P5)

**Objetivo**: administrar perfiles y usuarios, con la carga de usuarios restringida al perfil administrador.

**Test independiente**: verificar el alta, modificación y baja de perfiles y usuarios, que un usuario no administrador reciba 403 en la carga de usuarios, y que las bajas restringidas se rechacen.

### Tests de la Historia 5 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T106 [P] [US5] Tests unitarios de la política de contraseña en `tests/Stock.Tests/Unit/PasswordPolicyTests.cs`: se rechazan las de menos de 8 caracteres, las que no tienen ninguna letra y las que no tienen ningún dígito; se **aceptan** las de 8 o más que mezclan letras y dígitos, incluidas las que además contienen símbolos (RF-009)
- [X] T107 [P] [US5] Tests de usuarios en `tests/Stock.Tests/Integration/UsuariosTests.cs`: 403 para perfil no administrador, 400 sin grabar ante contraseña corta, ninguna respuesta incluye `Hash` ni `Salt`, y **una modificación sin contraseña no re-deriva el hash** (RF-006, RF-007, RF-009, RF-010)
- [X] T107a [P] [US5] Test del último administrador en `tests/Stock.Tests/Integration/UltimoAdministradorTests.cs`: con un solo usuario administrador, su baja se rechaza con 409 sin grabar, y la modificación que le cambia el perfil se rechaza con 409; con dos administradores, ambas operaciones sobre uno de ellos se aceptan (RF-005a, CE-007a)
- [X] T108 [P] [US5] Tests de perfiles en `tests/Stock.Tests/Integration/PerfilesTests.cs`: baja de perfil con usuarios asignados rechazada con 409, modificación de la Descripción persistida, y baja del perfil administrador rechazada con 409 aun sin usuarios asignados (RF-002a, RF-002b, RF-003)
- [X] T108a [P] [US5] Test de que el privilegio sigue a la marca y no al texto, en `tests/Stock.Tests/Integration/IdentidadAdministradorTests.cs`: renombrar el perfil administrador a "operador" **conserva** el acceso de sus usuarios a `/api/usuarios`, renombrar el perfil "vendedor" a "administrador" **no** se lo concede, y ningún DTO de alta o modificación de perfil permite fijar `EsAdministrador` —el campo enviado en el cuerpo se ignora y no se persiste— (RF-003a, RF-010, CE-007a)
- [X] T109 [P] [US5] Test de contrato del CRUD de `/api/usuarios` y `/api/perfiles` en `tests/Stock.Tests/Integration/SeguridadContractTests.cs`, incluido el recorrido completo alta → lectura → modificación → baja de ambos recursos, y el **403 para perfil no administrador en los dos recursos**, no sólo en usuarios (RF-001, RF-002, RF-004, RF-005, RF-010, RF-010a)
- [X] T110 [P] [US5] Tests de la capa web de los ABM de seguridad en `tests/Stock.Tests/Web/SeguridadControllerTests.cs`: las entradas de menú de **usuarios y perfiles** no se muestran a perfiles sin el claim `es_admin`, y un perfil cuya Descripción es "administrador" pero sin el claim tampoco las ve (RF-010, RF-010a)

### Implementación de la Historia 5

- [X] T111 [P] [US5] Implementar `PasswordPolicy` en `src/Stock.Api/Domain/Validation/PasswordPolicy.cs`: longitud mínima 8, al menos una letra y al menos un dígito, sin prohibir caracteres no alfanuméricos (RF-009)
- [X] T112 [P] [US5] Definir la política de autorización `SoloAdministrador` en `src/Stock.Api/Security/AuthorizationPolicies.cs` exigiendo el claim **`es_admin = "true"`**, devolviendo 403 al usuario autenticado que no lo tenga. La política **no** debe mirar el claim `role` ni ninguna cadena de Descripción (RF-010, RF-003a)
- [X] T113 [US5] Implementar `PerfilService` en `src/Stock.Api/Services/PerfilService.cs` con la verificación previa de baja restringida (RF-002a), el rechazo de la baja del perfil administrador (RF-002b) y DTOs de alta/modificación que **no incluyan** `EsAdministrador`, de modo que la marca sea inalcanzable desde la API (RF-003a)
- [X] T114 [US5] Implementar `UsuarioService` en `src/Stock.Api/Services/UsuarioService.cs`, re-derivando el hash sólo cuando la modificación incluye contraseña, y rechazando la baja o el cambio de perfil del último usuario administrador. La verificación del conteo de administradores restantes va **dentro de la misma transacción** que la escritura, para que dos bajas concurrentes no puedan eliminar a los dos últimos (RF-005a)
- [X] T115 [US5] Implementar el CRUD de `/api/perfiles` en `src/Stock.Api/Controllers/PerfilesController.cs` con la política `SoloAdministrador` (RF-010a, que extiende la restricción al ABM de perfiles porque es el que gobierna quién es administrador)
- [X] T116 [US5] Implementar el CRUD de `/api/usuarios` en `src/Stock.Api/Controllers/UsuariosController.cs` con la política `SoloAdministrador` y DTOs que nunca expongan `Hash` ni `Salt`
- [X] T117 [P] [US5] Crear los ViewModels de usuario y perfil en `src/Stock.Web/Models/UsuarioViewModel.cs` y `src/Stock.Web/Models/PerfilViewModel.cs`
- [X] T118 [US5] Implementar `UsuariosController` y `PerfilesController` en `src/Stock.Web/Controllers/`
- [X] T119 [US5] Crear las vistas de los ABM en `src/Stock.Web/Views/Usuarios/` y `src/Stock.Web/Views/Perfiles/`, ocultando la entrada de menú según el claim `es_admin` de la cookie de sesión —nunca comparando la Descripción del perfil contra la cadena "administrador"—, y sin exponer ningún control que permita editar la marca de administrador

**Punto de control**: las cinco historias funcionan de forma independiente.

---

## Fase 8: Pulido y Aspectos Transversales

**Propósito**: requisitos que atraviesan todas las historias y validación final contra el spec.

### Tests ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T120 [P] Test de que la bitácora sobrevive al rollback de la transacción fallida y de que un 422 de negocio **no** genera fila en `ErrorLog`, en `tests/Stock.Tests/Integration/ErrorLogTests.cs` (V-12, CE-008)
- [X] T121 [P] Test de que una excepción no controlada en la capa MVC también queda registrada en `ErrorLog`, en `tests/Stock.Tests/Web/ErrorLogWebTests.cs` (RF-028, CE-008)
- [X] T122 [P] Test de rendimiento secuencial con 10.000 artículos y 100.000 líneas de detalle que siembra los datos, ejecuta **50 corridas medidas de cada consulta descartando 5 de calentamiento** y asierta el p95 por debajo de 3 segundos, en `tests/Stock.Tests/Integration/RendimientoTests.cs` con la categoría `Volumen` (V-5, CE-002)
- [X] T123 [P] Test de rendimiento **bajo concurrencia** en `tests/Stock.Tests/Integration/RendimientoConcurrenteTests.cs` con la categoría `Volumen`: sobre el mismo volumen, 5 clientes concurrentes ejecutan ambas consultas y el p95 agregado se mantiene por debajo de 3 segundos (CE-004, segunda cláusula)

### Implementación

- [X] T124 Implementar `ErrorLogDbContext` con **conexión independiente** y **sin migraciones propias** en `src/Stock.Api/Data/ErrorLogDbContext.cs`, mapeando la tabla ya creada por la migración de la Fase 2 (R-08)
- [X] T125 Implementar el middleware global de excepciones en `src/Stock.Api/Middleware/ExceptionLoggingMiddleware.cs`: registra sólo errores de ejecución no controlados y devuelve un mensaje genérico sin detalle interno (RF-028)
- [X] T125a Crear en `src/Stock.Web/Data/` la entidad `ErrorLog` y el `ErrorLogDbContext` **propios de `Stock.Web`**, mapeados a la tabla `dbo.ErrorLog` ya creada por la migración de la Fase 2, con conexión independiente y **sin migraciones propias**. Es imprescindible y no era deducible de ninguna tarea previa: `Stock.Web` no referencia a `Stock.Api` (T005 sólo agrega esa referencia desde el proyecto de tests) y el `ErrorLogDbContext` de T124 vive en `src/Stock.Api/Data/`, de modo que sin esta tarea T126 no compila. La duplicación de la clase es deliberada: es preferible a que la capa web tome una dependencia sobre el ensamblado de la API (R-08)
- [X] T126 Implementar el manejador global de excepciones de la capa MVC en `src/Stock.Web/Middleware/ExceptionLoggingMiddleware.cs`, escribiendo en `ErrorLog` con el contexto propio de T125a. Es la **única** excepción a la regla de que `Stock.Web` no accede a la base: sólo diagnóstico, sólo escritura, ninguna entidad de negocio (RF-028, CE-008; desviación registrada en Complexity Tracking de [plan.md](./plan.md))
- [X] T127 [P] Agregar el layout y la navegación comunes en `src/Stock.Web/Views/Shared/_Layout.cshtml` con acceso a las dos consultas y los tres ABM
- [X] T128 [P] Documentar en `README.md` el procedimiento de carga del inventario de apertura mediante Movimientos de Compra, que RF-029 satisface sin código adicional, y remitir al escenario V-14 de [quickstart.md](./quickstart.md), que lo verifica de punta a punta. Con eso RF-029 deja de ser el único requisito sostenido sólo por documentación
- [X] T129 [P] Verificar que `AGENTS.md` describe con exactitud los comandos reales del proyecto ya construido (compose, migraciones, tests)
- [X] T130 Ejecutar `dotnet test StockModulo.sln` y confirmar que la suite por defecto pasa y que **no** incluyó la categoría `Volumen`, gracias al `.runsettings` de T015
- [X] T131 Ejecutar `dotnet test StockModulo.sln --settings tests/Stock.Tests/volumen.runsettings` y confirmar el presupuesto de CE-002 y CE-004; si falla, revisar la decisión R-01 de [research.md](./research.md) antes de denormalizar nada
- [X] T132 Recorrer los 14 escenarios de [quickstart.md](./quickstart.md) y confirmar la cobertura de los 9 criterios de éxito

---

## Fase 9: Brecha de interfaz (RF-016a, RF-020e, RF-020f, RF-034 a RF-034c)

**Propósito**: construir los siete requisitos de interfaz que la auditoría del 2026-07-31 incorporó
al spec y que quedaron marcados *pendiente de implementación*. Ninguno estaba tareado: las Fases 1
a 8 se cerraron sin ellos, de modo que hasta acá `tasks.md` mostraba 146 tareas en `[X]` mientras
el spec declaraba una brecha conocida. Esta fase la cierra.

**Dependencias**: toda la fase depende de las Fases 3 a 6 (movimientos, consultas, artículos y
autenticación ya construidos). No bloquea a nadie: es la última.

**Ciclo rojo→verde con ruptura deliberada**: T133 cambia los casos de `MovimientosContractTests`
que hoy envían `articuloId` y los deja en rojo hasta T139/T140. Es el mismo patrón de T101/T102 y
T105b/T105c —romper a propósito tests ya escritos, con tarea asignada para repararlos— y no un
efecto colateral no planificado.

**Nota sobre la lógica de cliente**: RF-016a y RF-034 son los primeros requisitos con lógica de
JavaScript. No se introduce un runner de JS: el rojo se produce sobre el **contrato renderizado**
de la vista, según fija la reevaluación post-Fase 1 de [plan.md](./plan.md).

**Nota sobre el borde del navegador (auditoría 2026-08-01)**: el buscador es el primer componente
que necesita **datos** desde el cliente, y no puede pedírselos a `Stock.Api` directamente. El JWT
vive en un claim de la cookie de sesión y lo adjunta `BearerTokenHandler` a las llamadas salientes
**del servidor**; el navegador no lo tiene, y la API no expone CORS. Una llamada del script a
`http://localhost:5279/api/articulos` fallaría en el navegador, y los tests de esta fase —que
asertan marcado renderizado— no lo detectarían. Por eso T136a/T141a agregan una acción JSON en
`Stock.Web`, del mismo origen que la página, que proxea la consulta con la sesión ya establecida.
Mover el token al navegador sería la alternativa, y está descartada: expondría al cliente una
credencial que hoy nunca sale del servidor.

### Tests de la Fase 9 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T133 Cambiar en `tests/Stock.Tests/Integration/MovimientosContractTests.cs` las líneas de detalle para que envíen `codigo` en vez de `articuloId`, y asertar que ninguna respuesta expone el identificador interno del artículo, según `contracts/openapi.yaml` (RF-020e). Modifica un archivo existente: no lleva `[P]`
- [X] T134 [P] Tests de resolución del Código en `tests/Stock.Tests/Integration/MovimientoCodigoTests.cs`: un Código inexistente devuelve **404 `application/problem+json`** identificando el Código ofensor, sin grabar ninguna línea, y `a-001` resuelve al artículo `A-001` por la regla insensible a mayúsculas y sensible a acentos de RF-017a (RF-020e)
- [X] T135 Agregar a `tests/Stock.Tests/Integration/MovimientoNumeracionTests.cs` el test de `GET /api/movimientos/proximo-numero`: devuelve el correlativo siguiente, **no consume la secuencia** —dos llamadas seguidas devuelven el mismo valor— y el valor avanza recién después de un alta. Sin este caso, RF-020f podría implementarse con un `IDENTITY` consumido, que contradice RF-020a (RF-020f)
- [X] T136 [P] Tests del buscador en `tests/Stock.Tests/Web/BuscadorArticulosTests.cs`: la ventana pide Descripción y "Buscar", la grilla trae las dos columnas Código y Descripción, y una Descripción vacía **no lista sin límite** sino que aplica el tope de 10.000 con el aviso de recorte de RF-032a. La altura declarada no supera los 600 píxeles (RF-034, RF-034a)
- [X] T136a [P] Tests de la puerta JSON del buscador en `tests/Stock.Tests/Web/BuscadorArticulosEndpointTests.cs`: bajo sesión válida, `GET /Articulos/Buscar?descripcion=` responde `application/json` con Código y Descripción de cada fila y **ningún token** en el cuerpo ni en los encabezados; sin sesión, el filtro global de T105b la manda al login y no devuelve datos; y el tope de 10.000 con su aviso de recorte viaja en la respuesta, no lo inventa el script (RF-034a)
- [X] T137 Agregar a `tests/Stock.Tests/Web/BuscadorArticulosTests.cs` los casos de RF-034b y RF-034c: cada pantalla con un campo de Código —detalle de movimientos y los dos extremos del rango de Stock Actual— incluye **la misma partial**, con una sola definición del diálogo en todo el proyecto; y la Descripción mostrada se mantiene sincronizada tanto al elegir desde la búsqueda como al editar el Código a mano, por la misma ruta de código
- [X] T138 Agregar a `tests/Stock.Tests/Web/ArticulosControllerTests.cs` el caso de RF-016a: las vistas de alta y edición emiten el hook de recálculo sobre Precio de Costo y Margen. El caso de sólo lectura del Precio de Venta ya existe desde T086 y **debe seguir en verde**: es lo que garantiza que el cliente no pueda alterar el precio grabado

### Implementación de la Fase 9

- [X] T139 Cambiar `LineaRequest.ArticuloId` por `Codigo` (string) y quitar `ArticuloId` de `DetalleResponse` en `src/Stock.Api/Controllers/MovimientosController.cs`. El comentario de RF-018a sigue valiendo para `Cantidad`, que continúa tipada como `int` (RF-020e)
- [X] T140 Resolver el Código a `ArticuloId` **dentro de la transacción**, completando el paso 2 del protocolo de escritura de [data-model.md](./data-model.md), y abortar con 404 indicando el Código ofensor si no existe, en `src/Stock.Api/Services/MovimientoService.cs` (RF-020e)
- [X] T141 Implementar `GET /api/movimientos/proximo-numero` en `src/Stock.Api/Controllers/MovimientosController.cs` según `contracts/openapi.yaml`, sin consumir la secuencia (RF-020f)
- [X] T141a Agregar la acción JSON `Buscar(string descripcion)` a `src/Stock.Web/Controllers/ArticulosController.cs`, que proxea `GET /api/articulos?descripcion=` con `StockApiClient` —el token lo sigue adjuntando `BearerTokenHandler` del lado del servidor— y devuelve sólo Código y Descripción, con el tope de 10.000 y el indicador de recorte. Es el único origen de datos del buscador; el script nunca llama a `Stock.Api` (RF-034a)
- [X] T142 [P] Crear el componente encapsulado del buscador en `src/Stock.Web/Views/Shared/_BuscadorArticulos.cshtml` y `src/Stock.Web/wwwroot/js/buscador-articulos.js`, consumiendo la acción **del mismo origen** de T141a —`GET /Articulos/Buscar?descripcion=`, nunca `Stock.Api` directamente— con su tope de 10.000 y altura máxima de 600 píxeles (RF-034, RF-034a, RF-034c)
- [X] T143 Consumir el buscador desde `src/Stock.Web/Views/Movimientos/` y `src/Stock.Web/Views/StockActual/Index.cshtml`, trasladando el Código elegido por **la misma ruta de código** que el tipeo manual y mostrando la Descripción del Código vigente (RF-034b)
- [X] T144 Mostrar el Número sugerido en modo sólo lectura en `src/Stock.Web/Views/Movimientos/Create.cshtml`, consumiendo T141 (RF-020f)
- [X] T145 [P] Crear `src/Stock.Web/wwwroot/js/articulo-precio.js` y engancharlo en `src/Stock.Web/Views/Articulos/Create.cshtml` y `Edit.cshtml`, recalculando el Precio de Venta al editar Precio de Costo o Margen, sin grabar ni recargar y sin habilitar el campo (RF-016a)
- [X] T146 Quitar de `spec.md` las siete marcas *pendiente de implementación* de RF-016a, RF-020e, RF-020f y RF-034 a RF-034c, y actualizar el encabezado de Estado dejando declarada la brecha que sigue abierta (las tres marcas de RF-020g a RF-020i las quita T155, en la Fase 10), y agregar a [quickstart.md](./quickstart.md) el escenario **V-15**, que recorre el buscador desde sus dos pantallas y verifica la equivalencia con la carga manual
- [X] T147 Ejecutar `dotnet test StockModulo.sln` y confirmar que toda la suite vuelve a verde, incluidos los casos que T133 rompió a propósito

**Punto de control**: la brecha del 2026-07-31 queda cerrada y `tasks.md` sin tareas silenciosas para ella.

---

## Fase 10: Carga asistida del detalle de movimientos (RF-020g, RF-020h, RF-020i)

**Propósito**: construir los tres requisitos de interfaz que la clarificación del 2026-08-01 incorporó
al spec —sugerencia del Precio Unitario según el Tipo, grilla de detalle de cuatro columnas con la
Descripción debajo del Código, y Total General—, todos marcados *pendiente de implementación*.

**Dependencias**: depende por completo de la **Fase 9**, y no es una preferencia de orden sino una
condición de corrección:

- RF-020g sugiere el precio al establecerse el **Código** de la línea. Hasta T139/T143 el detalle se
  carga por `ArticuloId`, así que no hay un Código sobre el cual disparar la sugerencia.
- RF-020g exige que la sugerencia salga por **la misma ruta de código** que usa el buscador de
  RF-034b (T143). Si se construyera antes, esa ruta única todavía no existe y habría que escribir
  una segunda, que es exactamente lo que RF-034c prohíbe.
- RF-020h ubica la Descripción **debajo del Código**; la Descripción sincronizada la introduce T143.

**Nota sobre la lógica de cliente**: vale la misma regla de la Fase 9. No se introduce un runner de
JS: el rojo se produce sobre el **contrato renderizado** de la vista —qué columnas emite, qué datos
del artículo expone para la sugerencia, qué rótulo lleva el total— y sobre el contrato de la API que
alimenta la sugerencia, que sí se testea de punta a punta.

**Una consulta por Código, no dos**: RF-020g pide resolver Descripción, Precio de Costo y Precio de
Venta con una única consulta. T151/T152 la agregan como filtro exacto `codigo` sobre el
`GET /api/articulos` que ya existe, en vez de un endpoint nuevo: el Código es texto y puede traer
caracteres que no viajan bien en un segmento de ruta, y el tope de 10.000 de RF-027 ya rige ahí.
Un Código inexistente devuelve **arreglo vacío con 200**, no 404: para la pantalla es "no hay
sugerencia", no un error (RF-020g); el 404 sigue siendo el de RF-020e, al grabar.

**Orden real de ejecución (2026-08-01)**: T151, T152 y T152a —el filtro `codigo` en la API y en la
puerta JSON— **se construyeron dentro de la Fase 9**, no acá. No fue una desprolijidad sino una
dependencia que la planificación no había visto: RF-034b (Fase 9) exige que la Descripción se
sincronice también cuando el usuario **tipea** el Código, y eso ya requiere resolver un Código
puntual contra el catálogo. Sin ese filtro, la Fase 9 sólo podía sincronizar la Descripción al
elegir desde la ventana, que es media implementación de RF-034b. Sus tests (T148, T150a) se
escribieron acá igualmente y pasaron en verde desde el primer intento; queda anotado para que nadie
lea ese verde inmediato como un test que no probó nada.

**El script no habla con la API**: vale la nota del borde del navegador de la Fase 9. La sugerencia
sale del mismo origen que la página, extendiendo con `codigo` la acción de T141a (T150a/T152a), y no
de una segunda llamada a `Stock.Api` que el navegador no puede autenticar. Es además lo que sostiene
la "única consulta por Código" de RF-020g: una sola puerta, la misma que ya usa el buscador.

### Tests de la Fase 10 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T148 Agregar a `tests/Stock.Tests/Integration/ArticulosContractTests.cs` los casos del filtro exacto `GET /api/articulos?codigo=`: devuelve el único artículo de ese Código con su Descripción, Precio de Costo y Precio de Venta; `a-001` resuelve `A-001` por la regla insensible a mayúsculas y sensible a acentos de RF-017a; un Código inexistente devuelve `200` con arreglo vacío; y `codigo` y `descripcion` combinados no se contradicen. Modifica un archivo existente: no lleva `[P]` (RF-020g)
- [X] T149 [P] Tests del contrato renderizado de la sugerencia en `tests/Stock.Tests/Web/MovimientoDetalleAsistidoTests.cs`: la vista del detalle expone, por línea, el Tipo vigente del movimiento y el punto de enganche del script de sugerencia; declara **un solo** origen de datos del artículo (el mismo que consume el buscador de T143, sin una segunda ruta); y al abrir un movimiento existente para editarlo **no** emite ninguna marca de re-sugerencia sobre las líneas ya grabadas (RF-020g)
- [X] T150 Agregar a `tests/Stock.Tests/Web/MovimientoDetalleAsistidoTests.cs` los casos de la grilla y el total: el encabezado del detalle trae exactamente cuatro columnas en el orden **Código, Cantidad, Precio Unitario, Precio Total**; la Descripción se emite **dentro de la celda del Código**, debajo de él y no como quinta columna; el Precio Total de la línea se renderiza como no editable; y existe un total rotulado exactamente **"Total General"**, que vale 0 con el detalle vacío. Agrega casos al archivo que crea T149: no lleva `[P]` (RF-020h, RF-020i)

- [X] T150a Agregar a `tests/Stock.Tests/Web/BuscadorArticulosEndpointTests.cs` los casos de `GET /Articulos/Buscar?codigo=`: bajo sesión válida devuelve la Descripción, el Precio de Costo y el Precio de Venta del artículo de ese Código en una sola respuesta; un Código inexistente responde **200 con cuerpo vacío**, no 404 ni error; y `codigo` y `descripcion` no se pisan entre sí. Agrega casos al archivo que crea T136a: no lleva `[P]` (RF-020g)

### Implementación de la Fase 10

- [X] T151 Documentar en `specs/001-modulo-stock-pedidos/contracts/openapi.yaml` el parámetro de consulta `codigo` de `GET /api/articulos` —coincidencia **exacta** con la regla de RF-017a, arreglo de 0 ó 1 elementos, sin 404— y dejar asentado que es la única resolución de Código que consume la pantalla de movimientos (RF-020g)
- [X] T152 Implementar el filtro exacto por `codigo` en `src/Stock.Api/Controllers/ArticulosController.cs` y su servicio, reusando la comparación de RF-017a que ya sostiene la unicidad del Código, sin duplicar la regla de comparación (RF-020g)
- [X] T152a Extender la acción `Buscar` de T141a en `src/Stock.Web/Controllers/ArticulosController.cs` con el parámetro `codigo`, que pasa a T152 y devuelve además el Precio de Costo y el Precio de Venta del artículo resuelto. Un Código inexistente devuelve cuerpo vacío con 200, sin error (RF-020g)
- [X] T153 [P] Crear `src/Stock.Web/wwwroot/js/movimiento-detalle.js`: al establecerse o cambiar el Código de una línea, consultar **la acción del mismo origen de T152a** —`GET /Articulos/Buscar?codigo=`, nunca `Stock.Api` directamente— y completar el Precio Unitario con el Precio de Costo si el Tipo es Compra y con el Precio de Venta si es Venta, dejándolo **editable**; no re-sugerir al cambiar el Tipo con líneas ya cargadas; no sugerir nada si el Código no existe (y vaciar la Descripción); y recalcular el Precio Total de la línea y el Total General ante cualquier cambio de Cantidad, Precio Unitario, Código, alta o baja de línea (RF-020g, RF-020h, RF-020i)
- [X] T154 Rehacer la grilla de `src/Stock.Web/Views/Movimientos/_Formulario.cshtml` con las cuatro columnas de RF-020h, la Descripción debajo del Código dentro de su celda, el Precio Total no editable y la fila de **"Total General"**, y enganchar `movimiento-detalle.js` en `Create.cshtml` y `Edit.cshtml`. El evento de cambio de Código que dispara la sugerencia es **el mismo** que T143 usa para sincronizar la Descripción: se agrega un suscriptor, no una segunda ruta (RF-020g, RF-020h, RF-020i, RF-034b)
- [X] T155 Quitar de `spec.md` las tres marcas *pendiente de implementación* de RF-020g a RF-020i y dejar el encabezado de Estado sin brecha declarada, y agregar a [quickstart.md](./quickstart.md) el escenario **V-16**: cargar una compra y una venta del mismo artículo verificando que el precio sugerido es el de costo y el de venta respectivamente, que reemplazarlo a mano graba el valor tecleado (RF-023b) y que el Total General coincide con la suma de los Precios Totales
- [X] T156 Ejecutar `dotnet test StockModulo.sln` y confirmar que toda la suite queda en verde

**Punto de control**: el spec queda sin requisitos pendientes y `tasks.md` sin brecha silenciosa.

---

## Fase 11: Comodidades de carga y de consulta (RF-020j, RF-025b, RF-026c)

**Propósito**: construir los tres requisitos que la segunda tanda de clarificaciones del 2026-08-01
incorporó al spec: el detalle de movimientos con líneas a demanda, y las dos consultas abriendo con
sus parámetros ya sugeridos.

**Dependencias**: la Fase 11 depende de las Fases 9 y 10, que son las que construyeron la grilla de
detalle y el buscador que las líneas nuevas tienen que heredar. Las tres tareas de implementación
son entre sí independientes: tocan tres pantallas distintas.

**Ninguno cambia una regla de negocio**: los tres son comodidades de pantalla y ninguno altera qué
devuelve una consulta ni qué se graba. Eso fija qué hay que verificar —que la pantalla abra con lo
sugerido y que lo sugerido sea editable— y, sobre todo, qué **no** debe cambiar:

- RF-026c es una **preselección visible**, no un valor por defecto del servidor. RF-026b sigue
  rechazando la solicitud que omita un parámetro de reposición, y abrir la pantalla **no** ejecuta
  la consulta. T159 cubre las dos cosas: sin ese par de casos, la preselección se implementaría con
  un default en el servidor, que es exactamente lo que RF-026b prohíbe.
- RF-025b no altera el resultado: el rango completo y el rango vacío devuelven las mismas filas.
- RF-020j no relaja RF-023: una línea **en blanco** no se envía, pero una con Código y Cantidad 0
  se sigue rechazando.

**Los extremos del catálogo no salen del listado**: `GET /api/articulos` recorta en 10.000 filas
(RF-027), así que el último de esa página no es el último del catálogo. Por eso T161/T162 agregan un
recurso propio que los calcula en el motor con la collation de la columna, que es la que define el
orden de RF-025a.

### Tests de la Fase 11 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [X] T157 Agregar a `tests/Stock.Tests/Integration/ArticulosContractTests.cs` los casos de `GET /api/articulos/extremos`: devuelve el primer y el último Código según el orden de RF-025a —insensible a mayúsculas, sensible a acentos, no ordinal por punto de código—, con el catálogo vacío devuelve ambos en nulo y **no** 404, y con más de 10.000 artículos devuelve el último real del catálogo y no el de la primera página. Modifica un archivo existente: no lleva `[P]` (RF-025b)
- [X] T158 [P] Tests de la preselección de "Generar Pedido" en `tests/Stock.Tests/Web/GenerarPedidoPreseleccionTests.cs`: al abrir la pantalla, "solo bajo mínimo" viene en **No** y "Modo de Pedido" en **Hasta Stock Ideal**, ambos editables; **la pantalla no llama a la API** —abrir no consulta— y los valores elegidos por el usuario ganan sobre los sugeridos (RF-026c)
- [X] T158a Agregar a `tests/Stock.Tests/Web/GenerarPedidoPreseleccionTests.cs` el caso que protege a RF-026b: una solicitud a la **API** que omita un parámetro de reposición se sigue rechazando, de modo que la preselección viva sólo en la pantalla. Es el caso que distingue "preseleccionar" de "poner un default en el servidor" (RF-026b, RF-026c)
- [X] T159 [P] Tests del rango sugerido en `tests/Stock.Tests/Web/StockActualRangoTests.cs`: al abrir la Consulta de Stock Actual, los campos traen el primer y el último Código que devuelve T162; con el catálogo vacío quedan en blanco y sin error; **abrir la pantalla no ejecuta la consulta** (sigue valiendo la distinción entre primer ingreso y consulta sin filtros); y un rango tecleado por el usuario no se pisa con el sugerido (RF-025b)
- [X] T160 Agregar a `tests/Stock.Tests/Web/MovimientoDetalleAsistidoTests.cs` los casos de RF-020j: la pantalla de alta abre con **una sola** línea vacía y no con cinco; existe un botón rotulado exactamente **"Agregar Línea"**; la plantilla de la línea nueva declara los mismos ganchos que las existentes —búsqueda, Descripción, Cantidad, Precio Unitario, Precio Total—, de modo que no pueda nacer una línea de segunda clase; y la pantalla de edición sigue abriendo con las líneas grabadas. Agrega casos a un archivo existente: no lleva `[P]` (RF-020j)
- [X] T160a [P] Test de contrato del alta con líneas en blanco en `tests/Stock.Tests/Web/MovimientoLineasEnBlancoTests.cs`: un formulario con tres líneas donde la del medio quedó vacía envía a la API **sólo las dos completas**, sin desplazar ni renumerar mal las demás; y una línea con Código cargado y Cantidad 0 **sí** viaja, para que la API la rechace por RF-023 (RF-020j)

### Implementación de la Fase 11

- [X] T161 Documentar en `specs/001-modulo-stock-pedidos/contracts/openapi.yaml` el recurso `GET /api/articulos/extremos`, que devuelve `{ codigoDesde, codigoHasta }` con los extremos del catálogo según RF-025a y ambos en `null` con el catálogo vacío (RF-025b)
- [X] T162 Implementar `GET /api/articulos/extremos` en `src/Stock.Api/Controllers/ArticulosController.cs` con `MIN`/`MAX` sobre el Código, **sin traer filas a memoria** y sin recorrer el listado con su tope: el orden lo define la collation de la columna, la misma de RF-025a (RF-025b)
- [X] T163 Consumir T162 desde `src/Stock.Web/Controllers/StockActualController.cs` y precargar el rango **sólo en el primer ingreso a la pantalla**, sin pisar lo que el usuario haya tecleado ni convertir el ingreso en una consulta ejecutada (RF-025b)
- [X] T164 [P] Preseleccionar los dos parámetros de reposición en `src/Stock.Web/Controllers/GenerarPedidoController.cs` y su vista: la vista recibe los valores sugeridos y la rama que hoy detecta "todavía no se eligieron" **se conserva**, porque es la que impide que abrir la pantalla dispare la consulta (RF-026c, RF-026b)
- [X] T165 [P] Reemplazar en `src/Stock.Web/Views/Movimientos/_Formulario.cshtml` las cuatro filas en blanco por una plantilla de línea y un botón **"Agregar Línea"**, y agregar a `src/Stock.Web/wwwroot/js/movimiento-detalle.js` el clonado con renumeración secuencial de los índices del modelo. La línea clonada no necesita cableado propio: la búsqueda, la Descripción y la sugerencia ya trabajan por delegación desde T142/T153 (RF-020j)
- [X] T166 Quitar de `spec.md` las tres marcas *pendiente de implementación* de RF-020j, RF-025b y RF-026c y actualizar el encabezado de Estado, y agregar a [quickstart.md](./quickstart.md) el escenario **V-17**, que recorre las dos pantallas de consulta recién abiertas y la carga de un movimiento de siete líneas
- [X] T167 Ejecutar `dotnet test StockModulo.sln` y confirmar que toda la suite queda en verde

**Punto de control**: las tres pantallas abren listas para operar y el spec vuelve a quedar sin
requisitos pendientes.

---

## Dependencias y Orden de Ejecución

### Dependencias entre fases

- **Setup (Fase 1)**: sin dependencias, arranca de inmediato
- **Fundacional (Fase 2)**: depende del Setup — **BLOQUEA todas las historias**
- **Historias (Fases 3–7)**: todas dependen de la Fase 2
- **Pulido (Fase 8)**: depende de las historias que se quieran entregar
- **Brecha de interfaz (Fase 9)**: depende de las Fases 3 a 6; bloquea a la Fase 10
- **Comodidades de carga y consulta (Fase 11)**: depende de las Fases 9 y 10 —hereda la grilla de detalle y el buscador—; no bloquea a ninguna otra
- **Carga asistida del detalle (Fase 10)**: depende de la **Fase 9 completa** —sin el detalle por Código (T139/T140), sin la puerta JSON del mismo origen (T141a) ni el buscador con su Descripción sincronizada (T142/T143) no hay dónde enganchar la sugerencia sin abrir una segunda ruta de resolución—; no bloquea a ninguna otra

### Dentro de la Fase 2 — el ciclo rojo→verde es estrictamente secuencial entre bloques

- Bloque 1 (T016–T019a) antes que Bloque 2: los tests deben existir y no compilar
- Bloque 2 (T020–T029) antes que T030: el andamiaje los hace compilar y fallar. **T027 es imprescindible acá**: sin el registro del `DbContext`, la factory de T028 no arranca y el rojo sería por error de configuración, no por la restricción ausente
- **T030 es una puerta**: si algún test pasa acá, está mal escrito
- Bloque 3 (T031–T037) antes que T038: las configuraciones los ponen en verde

### Dependencias entre historias

- **US1 (P1)**: arranca apenas termina la Fase 2. Se valida sobre datos sembrados, sin necesidad del ABM de artículos ni de la carga de movimientos por pantalla.
- **US2 (P2)**: arranca apenas termina la Fase 2. Reutiliza el `ExcelExporter` de T054; si se implementara antes que US1, esa tarea se mueve a US2.
- **US3 (P3)**: independiente. Enriquece a US1 al permitir mantener los parámetros de reposición desde la UI.
- **US4 (P4)**: independiente y transversal. El endurecimiento del acceso rompe deliberadamente tests ya escritos, **en las dos capas y con tarea asignada en ambas**:
  - *API*: T101 aplica `[Authorize]` sobre los endpoints de US1–US3 y rompe sus tests de integración; T100 introduce el fixture autenticado y T102 los repara.
  - *Web*: T105b registra el filtro de autorización global y rompe los tests de capa web de US1–US3; T105a introduce el fixture de sesión y T105c los repara.
  Ninguno de los dos es un efecto colateral no planificado.
- **US5 (P5)**: depende de US4 para la política `SoloAdministrador`, para el claim `es_admin` que la sostiene (T098) y para el hasher de contraseñas (T097).

**Única dependencia real entre historias**: US5 → US4. Las demás son independientes.

### Dentro de cada historia

- Los tests se escriben primero y **deben fallar** antes de implementar (Principio I)
- Entidades antes que servicios; servicios antes que endpoints; API antes que las vistas MVC
- Las tareas que agregan casos a un archivo de test ya creado por otra tarea **no** llevan `[P]`

### Oportunidades de paralelismo

- Fase 1: T003, T004, T011, T013, T013b, T014 en paralelo. T006–T008 y T012 modifican archivos creados por tareas anteriores, y T013a depende de T010 y T013, por eso no llevan `[P]`
- Fase 2: los cinco tests de esquema (T016–T019a) en paralelo; luego las seis entidades (T020–T025) en paralelo; luego las cinco configuraciones (T031–T035) en paralelo
- Dentro de cada historia, los tests marcados `[P]` escriben en archivos distintos y corren en paralelo
- Con equipo: tras la Fase 2, US1, US2, US3 y US4 pueden avanzar en paralelo; US5 espera a US4
- Fase 9: T136a puede escribirse en paralelo con T134 y T136 (archivos distintos). T141a no lleva `[P]`: modifica `ArticulosController.cs`, que ya existe, y T142 depende de él
- Fase 11: T158, T159, T160a, T164 y T165 llevan `[P]` —tocan archivos distintos, de tres pantallas independientes—. T157, T158a y T160 agregan casos a archivos existentes, y T163 depende de T162
- Fase 10: sólo T149 y T153 llevan `[P]`. T150 y T150a agregan casos a archivos que crean otras tareas (T149 y T136a); T148 modifica un archivo existente; y T152, T152a y T154 dependen en cadena de lo que documenta T151, de lo que expone T152 y de lo que expone T152a

---

## Ejemplo de paralelismo: Historia de Usuario 1

```bash
# Escribir en paralelo los tests de US1 que tocan archivos distintos (deben fallar):
Tarea: "T043 — 6 combinaciones en tests/Stock.Tests/Unit/PedidoCalculatorTests.cs"
Tarea: "T045 — contrato en tests/Stock.Tests/Integration/GenerarPedidoContractTests.cs"
Tarea: "T046 — artículo sin movimientos en tests/Stock.Tests/Integration/GenerarPedidoTests.cs"
Tarea: "T049 — exportación en tests/Stock.Tests/Integration/ExportacionExcelTests.cs"
Tarea: "T050 — capa web en tests/Stock.Tests/Web/GenerarPedidoControllerTests.cs"

# T044, T047 y T048 NO son paralelas: agregan casos a archivos que ya crean T043 y T046.

# Luego, en verde, el enum y el ViewModel sí pueden ir en paralelo:
Tarea: "T051 — enum ModoPedido en src/Stock.Api/Domain/Pedido/ModoPedido.cs"
Tarea: "T056 — GenerarPedidoViewModel en src/Stock.Web/Models/GenerarPedidoViewModel.cs"
```

---

## Estrategia de Implementación

### MVP primero (sólo US1)

1. Completar la Fase 1 (Setup)
2. Completar la Fase 2 (Fundacional) — **crítica, bloquea todo**
3. Completar la Fase 3 (US1)
4. **PARAR Y VALIDAR**: correr el escenario V-1 del quickstart — 15 cantidades asertadas y 9 exclusiones verificadas
5. Demostrable: la consulta que resuelve el problema central del negocio, sobre datos sembrados

### Entrega incremental

1. Setup + Fundacional → base lista y con reglas de esquema verificadas por un ciclo rojo→verde real
2. + US1 → **MVP**, la lista de pedido funciona
3. + US2 → el stock ya viene de movimientos reales cargados por el usuario
4. + US3 → el catálogo se mantiene desde la UI
5. + US4 → el sistema queda protegido por autenticación
6. + US5 → operación multiusuario completa
7. + Pulido → bitácora completa, rendimiento verificado y validación integral

### Nota sobre el orden de la autenticación

US4 es P4 a propósito: el spec justifica que el valor de negocio central (US1–US3) se demuestre
antes de endurecer el acceso. La contrapartida —que endurecer el acceso rompa los tests ya
escritos— está cubierta en las dos capas por T100/T102 (API) y T105a/T105c (web), que forman parte
del alcance de US4 y no se descubren sobre la marcha.

Corolario del Principio I: **ni el `[Authorize]` de la API ni el filtro global de la web pueden
adelantarse a la Fase 6**, porque sus tests (T094 y T096) viven ahí. Por eso T041 deja
`src/Stock.Web/Program.cs` sin filtro de autorización, aunque el archivo se cree en la Fase 2.

---

## Notas

- Las tareas `[P]` tocan archivos distintos y no tienen dependencias pendientes entre sí. Una tarea que modifica un archivo creado por otra tarea nunca lleva `[P]`.
- Verificar que cada test falla antes de implementar: sin rojo previo no hay verde válido (Principio I). En la Fase 2 esa verificación tiene tarea propia (T030).
- Commitear después de cada tarea o grupo lógico
- Se puede parar en cualquier punto de control para validar la historia de forma aislada
- Los tests de integración usan `WebApplicationFactory` in-process y requieren el SQL Server de `docker compose` levantado (R-10)
- La suite por defecto excluye la categoría `Volumen` por el `.runsettings` de T015; los tests de rendimiento se corren aparte con T131
