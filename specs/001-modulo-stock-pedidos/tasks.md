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
(1) escribir el test → no compila; (2) crear el andamiaje mínimo —entidades, contexto, migración
desnuda— → compila y **falla contra la base real**; (3) implementar la regla → verde. El andamiaje
del paso 2 no implementa ninguna de las reglas bajo prueba: sólo permite que el test llegue a
ejecutarse y fallar por el motivo correcto.

**Organización**: agrupadas por historia de usuario, para poder implementar y validar cada una de
forma independiente.

## Formato: `[ID] [P?] [Story] Descripción`

- **[P]**: puede correr en paralelo — **archivos distintos**, sin dependencias pendientes
- **[Story]**: historia a la que pertenece la tarea (US1…US5)
- Toda tarea incluye la ruta exacta del archivo

## Convención de rutas

Según la estructura fijada en [plan.md](./plan.md):

- API y lógica de negocio: `src/Stock.Api/`
- Front MVC: `src/Stock.Web/`
- Tests: `tests/Stock.Tests/`

---

## Fase 1: Setup (Infraestructura compartida)

**Propósito**: solución, proyectos y entorno reproducible.

- [ ] T001 Crear la solución `StockModulo.sln` en la raíz del repositorio y las carpetas `src/` y `tests/`
- [ ] T002 Crear el proyecto Web API `src/Stock.Api/Stock.Api.csproj` sobre `net8.0`
- [ ] T003 [P] Crear el proyecto ASP.NET MVC `src/Stock.Web/Stock.Web.csproj` sobre `net8.0`
- [ ] T004 [P] Crear el proyecto de tests NUnit `tests/Stock.Tests/Stock.Tests.csproj` sobre `net8.0`
- [ ] T005 Agregar los tres proyectos a `StockModulo.sln` y las referencias de `tests/Stock.Tests` hacia `src/Stock.Api` y `src/Stock.Web`
- [ ] T006 Agregar a `src/Stock.Api/Stock.Api.csproj` los paquetes `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Authentication.JwtBearer` y `ClosedXML` (modifica el archivo creado por T002)
- [ ] T007 Agregar a `tests/Stock.Tests/Stock.Tests.csproj` los paquetes `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk` y `Microsoft.AspNetCore.Mvc.Testing` (modifica el archivo creado por T004)
- [ ] T008 Agregar a `src/Stock.Web/Stock.Web.csproj` el soporte de `HttpClient` tipado (`Microsoft.Extensions.Http`) (modifica el archivo creado por T003)
- [ ] T009 Declarar `public partial class Program` en su propio namespace en `src/Stock.Api/Program.cs` y `src/Stock.Web/Program.cs`, para que `Stock.Api.Program` y `Stock.Web.Program` no colisionen en el proyecto de tests, que referencia a ambos (R-10)
- [ ] T010 Crear `docker-compose.yml` en la raíz con SQL Server 2017, `Stock.Api` en el puerto 5279 y `Stock.Web` en el 5280, según [quickstart.md](./quickstart.md)
- [ ] T011 [P] Crear `src/Stock.Api/Dockerfile` y `src/Stock.Web/Dockerfile`
- [ ] T012 [P] Configurar en `src/Stock.Api/appsettings.json` y `docker-compose.yml` la lectura por variable de entorno de la cadena de conexión, la clave de firma JWT y `SEED_ADMIN_PASSWORD`, sin ningún secreto en el código (Principio IV)
- [ ] T013 [P] Crear `.gitignore` en la raíz excluyendo `bin/`, `obj/`, `*.user` y archivos de secretos locales
- [ ] T014 [P] Definir las categorías `Unit`, `Integration` y `Volumen` en `tests/Stock.Tests/TestCategories.cs`
- [ ] T015 Crear `tests/Stock.Tests/.runsettings` que excluya la categoría `Volumen`, y referenciarlo desde `tests/Stock.Tests/Stock.Tests.csproj` con `RunSettingsFilePath`, de modo que `dotnet test StockModulo.sln` —la puerta de calidad literal de la constitución— **no** dispare la siembra masiva de CE-002

---

## Fase 2: Fundacional (Prerrequisitos bloqueantes)

**Propósito**: esquema de datos y andamiaje que TODAS las historias necesitan.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta terminar esta fase.

**Nota sobre el alcance de la migración**: las **seis** entidades entran acá, aunque
`Perfil`/`Usuario` sólo se usen en US4/US5 y `ErrorLog` en la Fase 8. Hay una sola base y un solo
historial de migraciones; fragmentarlo por historia agregaría churn sin beneficio.

**Nota sobre el ciclo rojo→verde del esquema**: el esquema **codifica reglas de negocio** (`CHECK`
de orden de stocks, columnas calculadas de precio, índice único de Código, collations). Por eso la
fase se ordena en tres bloques: los tests (T016–T019) se escriben primero y no compilan; el
andamiaje (T020–T028) los hace compilar y **fallar contra tablas reales sin restricciones**, lo que
T029 verifica explícitamente; recién entonces las configuraciones (T030–T036) los ponen en verde,
que es lo que confirma T037. La migración desnuda de T028 existe exactamente para producir ese
rojo: sin ella las tablas nacerían ya con los `CHECK` puestos y los tests estarían en verde desde
el primer momento, sin haber demostrado nada.

### Bloque 1 — Tests del esquema ⚠️ ESCRIBIR PRIMERO. No compilan: ése es el primer rojo

- [ ] T016 [P] Tests de las restricciones de `Articulo` a nivel de base (rechazo de `StockMinimo > PuntoPedido`, de valores negativos y de `Codigo` duplicado —incluida la colisión por diferencia de mayúsculas—, y cálculo de `PrecioVenta`) en `tests/Stock.Tests/Integration/EsquemaArticuloTests.cs` (RF-016, RF-017, RF-017a, RF-018, RF-019)
- [ ] T017 [P] Tests de las restricciones de `MovimientoDetalle` a nivel de base (rechazo de `Cantidad` ≤ 0 y > 1.000.000, cálculo de `PrecioTotal`, borrado en cascada desde el encabezado y `NO ACTION` hacia `Articulo`) en `tests/Stock.Tests/Integration/EsquemaMovimientoTests.cs` (RF-014a, RF-020c, RF-021, RF-023, RF-023a)
- [ ] T018 [P] Test de que `vw_StockActual` devuelve 0 para artículos sin movimientos y el saldo correcto con compras y ventas, en `tests/Stock.Tests/Integration/VistaStockActualTests.cs` (RF-030)
- [ ] T019 [P] Test de que la tabla `dbo.ErrorLog` existe y admite inserción tras aplicar las migraciones, en `tests/Stock.Tests/Integration/EsquemaErrorLogTests.cs` (RF-028)

### Bloque 2 — Andamiaje mínimo: hace que los tests compilen y fallen contra la base real

- [ ] T020 [P] Crear la entidad `Perfil` en `src/Stock.Api/Domain/Entities/Perfil.cs`
- [ ] T021 [P] Crear la entidad `Usuario` en `src/Stock.Api/Domain/Entities/Usuario.cs` con `Hash` y `Salt` como propiedades separadas
- [ ] T022 [P] Crear la entidad `Articulo` en `src/Stock.Api/Domain/Entities/Articulo.cs` con los tres parámetros de reposición como `int` (RF-013a)
- [ ] T023 [P] Crear la entidad `Movimiento` y el enum `TipoMovimiento` (Compra=1, Venta=2) en `src/Stock.Api/Domain/Entities/Movimiento.cs`
- [ ] T024 [P] Crear la entidad `MovimientoDetalle` en `src/Stock.Api/Domain/Entities/MovimientoDetalle.cs`
- [ ] T025 [P] Crear la entidad `ErrorLog` en `src/Stock.Api/Domain/Entities/ErrorLog.cs`
- [ ] T026 Crear `src/Stock.Api/Data/StockDbContext.cs` con los `DbSet` de las **seis** entidades —incluida `ErrorLog`, que es dueña de su esquema aunque en runtime se escriba por otra conexión (R-08)— **sin ninguna configuración de restricciones**
- [ ] T027 Crear la base de tests de integración en `tests/Stock.Tests/Integration/IntegrationTestBase.cs`: levanta `WebApplicationFactory<Stock.Api.Program>` in-process, crea una base efímera por corrida en el SQL Server de compose, le inyecta la cadena de conexión, aplica migraciones y la elimina al finalizar (R-10)
- [ ] T028 Generar la migración desnuda en `src/Stock.Api/Data/Migrations/`: crea las seis tablas **sin** `CHECK`, sin columnas calculadas, sin índices únicos, sin collations y sin la vista
- [ ] T029 **Verificar el rojo**: T016–T019 compilan y fallan contra tablas reales. Un test que pase en este punto está mal escrito y debe corregirse antes de seguir

### Bloque 3 — Configuraciones: ponen los tests en verde

- [ ] T030 [P] Configurar `Articulo` en `src/Stock.Api/Data/Configurations/ArticuloConfiguration.cs`: índice único de `Codigo`, collation `Modern_Spanish_CI_AS` en `Codigo`, collation `Modern_Spanish_CI_AI` en `Descripcion`, columna calculada persistida `PrecioVenta`, `CHECK` de no negatividad y `CHECK (StockMinimo <= PuntoPedido AND PuntoPedido <= StockIdeal)`
- [ ] T031 [P] Configurar `Movimiento` en `src/Stock.Api/Data/Configurations/MovimientoConfiguration.cs`: `Numero` como PK `IDENTITY` y `CHECK Tipo IN (1,2)`
- [ ] T032 [P] Configurar `MovimientoDetalle` en `src/Stock.Api/Data/Configurations/MovimientoDetalleConfiguration.cs`: columna calculada persistida `PrecioTotal`, `CHECK (Cantidad > 0 AND Cantidad <= 1000000)`, FK a `Movimiento` con `CASCADE`, FK a `Articulo` con `NO ACTION` e índice `IX_MovimientoDetalle_ArticuloId` con `INCLUDE (Cantidad, MovimientoNumero)`
- [ ] T033 [P] Configurar `Usuario` y `Perfil` en `src/Stock.Api/Data/Configurations/SeguridadConfiguration.cs`: índice único de `NombreUsuario` y FK `Usuario.PerfilId` con `NO ACTION`
- [ ] T034 [P] Configurar `ErrorLog` en `src/Stock.Api/Data/Configurations/ErrorLogConfiguration.cs` (columnas de RF-028, sin relaciones)
- [ ] T035 Crear la entidad sin clave `StockActualView` en `src/Stock.Api/Data/Views/StockActualView.cs` mapeada a `vw_StockActual`
- [ ] T036 Generar la migración de restricciones en `src/Stock.Api/Data/Migrations/`: agrega `CHECK`, columnas calculadas, índices únicos y collations, más el `CREATE VIEW dbo.vw_StockActual` con el `LEFT JOIN` e `ISNULL(...,0)` de [data-model.md](./data-model.md)
- [ ] T037 **Verificar el verde**: T016–T019 pasan contra la migración completa

### Bloque 4 — Andamiaje de aplicación

- [ ] T038 [P] Crear la siembra de perfiles base (administrador, administrativo, vendedor) en `src/Stock.Api/Data/Seed/DbSeeder.cs`
- [ ] T039 Completar `src/Stock.Api/Program.cs`: controladores, `StockDbContext`, respuestas `application/problem+json` y el flag `ApplyMigrationsOnStartup` usado sólo en compose
- [ ] T040 [P] Completar `src/Stock.Web/Program.cs`: MVC, `HttpClient` tipado apuntando a `Stock.Api` y páginas de error
- [ ] T041 [P] Crear la base de tests de la capa web en `tests/Stock.Tests/Web/WebTestBase.cs` usando `WebApplicationFactory<Stock.Web.Program>` con la API simulada (R-10)

**Punto de control**: esquema listo, migrable y con sus reglas verificadas por un ciclo rojo→verde real. Las historias pueden comenzar.

---

## Fase 3: Historia de Usuario 1 — Generar la lista de pedido (Prioridad: P1) 🎯 MVP

**Objetivo**: entregar la consulta que resuelve el problema central del negocio — qué reponer y cuánto — con sus 6 combinaciones de parámetros y exportación a Excel.

**Test independiente**: sembrar el Conjunto de Datos de Referencia del spec (4 artículos con Stock Actual 5, 15, 60 y 0), ejecutar las 6 combinaciones y verificar la matriz de 6 × 4 = 24 celdas: **15 cantidades asertadas y 9 exclusiones** que deben comprobarse como filas ausentes.

### Tests de la Historia 1 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T042 [P] [US1] Test unitario de las 6 combinaciones contra el Conjunto de Datos de Referencia — 15 cantidades asertadas y 9 exclusiones verificadas como ausencia de fila — en `tests/Stock.Tests/Unit/PedidoCalculatorTests.cs`
- [ ] T043 [US1] Agregar a `tests/Stock.Tests/Unit/PedidoCalculatorTests.cs` el caso de que la cantidad a pedir nunca es negativa cuando el stock supera el nivel (mismo archivo que T042, no paralelizable)
- [ ] T044 [P] [US1] Test de contrato de `GET /api/consultas/generar-pedido`: parámetros de reposición obligatorios, 400 ante `modoPedido` inválido, y **ausencia de parámetros de rango** en el endpoint, en `tests/Stock.Tests/Integration/GenerarPedidoContractTests.cs` (RF-026a)
- [ ] T045 [P] [US1] Test de integración de que un artículo sin movimientos aparece con stock 0 y cantidad a pedir igual a su stock mínimo, en `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` (V-7)
- [ ] T046 [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` el caso de que con `soloBajoMinimo=false` se listan todos los artículos incluidos los de cantidad 0
- [ ] T047 [US1] Agregar a `tests/Stock.Tests/Integration/GenerarPedidoTests.cs` el caso del resultado vacío con mensaje informativo y sin error
- [ ] T048 [P] [US1] Test de integración de que el `.xlsx` exportado replica filas, orden y recorte de la respuesta JSON, **y que un resultado vacío exporta sólo los encabezados**, en `tests/Stock.Tests/Integration/ExportacionExcelTests.cs` (V-10, RF-031)
- [ ] T049 [P] [US1] Test de la vista de Generar Pedido en `tests/Stock.Tests/Web/GenerarPedidoControllerTests.cs`: envío de los dos parámetros, render del aviso de recorte y retransmisión del Excel

### Implementación de la Historia 1

- [ ] T050 [P] [US1] Crear el enum `ModoPedido` en `src/Stock.Api/Domain/Pedido/ModoPedido.cs`
- [ ] T051 [US1] Implementar `PedidoCalculator` como función pura sin dependencias de EF Core ni ASP.NET en `src/Stock.Api/Domain/Pedido/PedidoCalculator.cs` (Nivel, Incluir y `MAX(0, Nivel − Stock)`)
- [ ] T052 [US1] Implementar `GenerarPedidoQueryService` en `src/Stock.Api/Services/GenerarPedidoQueryService.cs` consumiendo `vw_StockActual` y aplicando el pipeline filtrar → ordenar por Código → recortar a 10.000 → marcar `truncado`
- [ ] T053 [US1] Implementar `ExcelExporter` con ClosedXML en `src/Stock.Api/Export/ExcelExporter.cs`, generando `.xlsx` a partir de las filas ya recortadas y con sólo encabezados si no hay filas (compartido con US2)
- [ ] T054 [US1] Implementar `GET /api/consultas/generar-pedido` y `GET /api/consultas/generar-pedido/excel` en `src/Stock.Api/Controllers/ConsultasController.cs` según [contracts/openapi.yaml](./contracts/openapi.yaml)
- [ ] T055 [P] [US1] Crear el `GenerarPedidoViewModel` en `src/Stock.Web/Models/GenerarPedidoViewModel.cs`
- [ ] T056 [US1] Implementar `GenerarPedidoController` en `src/Stock.Web/Controllers/GenerarPedidoController.cs` consumiendo la API y retransmitiendo el Excel
- [ ] T057 [US1] Crear la vista de Generar Pedido en `src/Stock.Web/Views/GenerarPedido/Index.cshtml` con los dos parámetros de reposición, el filtro opcional, el botón de exportar y el aviso de recorte

**Punto de control**: US1 funciona de punta a punta sobre datos sembrados. Es el MVP demostrable.

---

## Fase 4: Historia de Usuario 2 — Movimientos y Stock Actual (Prioridad: P2)

**Objetivo**: registrar compras y ventas con el invariante de stock no negativo garantizado, y consultar el Stock Actual por rango.

**Test independiente**: registrar un conjunto de compras y ventas y verificar que la Consulta de Stock Actual devuelve, para el rango pedido, la suma de compras menos la suma de ventas de cada artículo, exportable a Excel.

### Tests de la Historia 2 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T058 [P] [US2] Tests unitarios del validador de movimiento: cantidad no entera, ≤ 0, > 1.000.000, **Precio Unitario > 9.999.999,99**, **Precio Total > 999.999.999.999,99**, fecha futura y tipo inválido, en `tests/Stock.Tests/Unit/MovimientoValidatorTests.cs` (RF-020b, RF-020d, RF-023, RF-023a)
- [ ] T059 [US2] Agregar a `tests/Stock.Tests/Unit/MovimientoValidatorTests.cs` el caso de regresión de RF-023b: un precio unitario deliberadamente distinto del Precio de Costo y del Precio de Venta del artículo **se acepta**
- [ ] T060 [P] [US2] Tests del invariante de stock en `tests/Stock.Tests/Integration/MovimientoInvarianteTests.cs`: venta que dejaría el stock por debajo de 0 rechazada con 422 sin grabar nada, y baja de una compra ya consumida por ventas posteriores rechazada con 422 (V-2, RF-024a)
- [ ] T061 [P] [US2] Tests de modificación en `tests/Stock.Tests/Integration/MovimientoModificacionTests.cs`: una modificación **exitosa** de cantidades recalcula correctamente el Stock Actual, y una modificación que dejaría el saldo negativo se rechaza con 422 (RF-022, RF-024a)
- [ ] T062 [P] [US2] Test todo-o-nada en `tests/Stock.Tests/Integration/MovimientoAtomicidadTests.cs`: un movimiento de 3 líneas donde falla la tercera no aplica ninguna (V-3, RF-024c)
- [ ] T063 [P] [US2] Tests de numeración en `tests/Stock.Tests/Integration/MovimientoNumeracionTests.cs`: `Numero` único y compartido entre compras y ventas, **y no reutilizado tras una baja** (RF-020a)
- [ ] T064 [P] [US2] Test de concurrencia en `tests/Stock.Tests/Integration/ConcurrenciaTests.cs`: 5 ventas simultáneas de 4 unidades sobre un stock de 10 graban a lo sumo 2, el resto falla con 422 de stock insuficiente y ninguna respuesta es un error de reintento (V-4, RF-024b)
- [ ] T065 [P] [US2] Tests del recorte determinista en `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs`: dos corridas sin filtro sobre más de 10.000 artículos devuelven el mismo conjunto ordenado por Código y `truncado=true` (V-6)
- [ ] T066 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` los casos del rango de códigos: extremos inclusive, extremos vacíos y rango invertido con resultado vacío sin error (V-9)
- [ ] T067 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` el caso de la collation del Código: dos códigos que difieren sólo en mayúsculas caen dentro del mismo rango, y dos que difieren en acento no (RF-025a)
- [ ] T068 [US2] Agregar a `tests/Stock.Tests/Integration/ConsultaStockActualTests.cs` el caso del filtro por descripción insensible a mayúsculas y acentos (V-8, RF-027a)
- [ ] T069 [P] [US2] Test de contrato del CRUD de `/api/movimientos` en `tests/Stock.Tests/Integration/MovimientosContractTests.cs`
- [ ] T070 [P] [US2] Tests de la capa web de movimientos y consulta de stock en `tests/Stock.Tests/Web/MovimientosControllerTests.cs`: alta con varias líneas de detalle y propagación del error 422 a la vista

### Implementación de la Historia 2

- [ ] T071 [P] [US2] Implementar `MovimientoValidator` en `src/Stock.Api/Domain/Validation/MovimientoValidator.cs` con los límites concretos de RF-023a y **sin** validación cruzada de precios (RF-023b)
- [ ] T072 [US2] Implementar el bloqueo pesimista en `src/Stock.Api/Data/ArticuloLockRepository.cs`: `SELECT ... WITH (UPDLOCK, HOLDLOCK)` sobre las filas de `Articulo` afectadas, ordenadas por `ArticuloId` ascendente
- [ ] T073 [US2] Implementar `MovimientoService` en `src/Stock.Api/Services/MovimientoService.cs` aplicando el protocolo de escritura completo de [data-model.md](./data-model.md): transacción → bloqueo → leer `vw_StockActual` → validar ≥ 0 en todas las líneas → aplicar → confirmar
- [ ] T074 [US2] Implementar el CRUD de `/api/movimientos` en `src/Stock.Api/Controllers/MovimientosController.cs` con el mapeo de códigos 400/404/422 de [contracts/README.md](./contracts/README.md)
- [ ] T075 [US2] Implementar `StockActualQueryService` en `src/Stock.Api/Services/StockActualQueryService.cs` con el mismo pipeline filtrar → ordenar → recortar → marcar, reutilizando `vw_StockActual`
- [ ] T076 [US2] Implementar `GET /api/consultas/stock-actual` y `GET /api/consultas/stock-actual/excel` en `src/Stock.Api/Controllers/ConsultasController.cs` reutilizando el `ExcelExporter` de T053
- [ ] T077 [P] [US2] Crear los ViewModels de movimiento y de consulta de stock en `src/Stock.Web/Models/MovimientoViewModel.cs` y `src/Stock.Web/Models/StockActualViewModel.cs`
- [ ] T078 [US2] Implementar `MovimientosController` en `src/Stock.Web/Controllers/MovimientosController.cs` con alta, baja y modificación de encabezado y detalle
- [ ] T079 [US2] Crear las vistas de movimientos en `src/Stock.Web/Views/Movimientos/` (Index, Create, Edit, Delete) con carga de líneas de detalle
- [ ] T080 [US2] Implementar `StockActualController` y la vista `src/Stock.Web/Views/StockActual/Index.cshtml` con rango, filtro, exportación y aviso de recorte

**Punto de control**: US1 y US2 funcionan de forma independiente. El Stock Actual ya proviene de movimientos reales.

---

## Fase 5: Historia de Usuario 3 — ABM de Artículos (Prioridad: P3)

**Objetivo**: mantener el catálogo con sus parámetros de reposición y su precio calculado.

**Test independiente**: dar de alta, modificar y eliminar artículos verificando cada validación (código único, no negatividad, orden de los tres stocks, precio de venta calculado) y la baja restringida.

### Tests de la Historia 3 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T081 [P] [US3] Tests unitarios del validador de artículo (código vacío, valores negativos, parámetro no entero, incumplimiento de Mínimo ≤ Punto de Pedido ≤ Ideal) en `tests/Stock.Tests/Unit/ArticuloValidatorTests.cs`
- [ ] T082 [P] [US3] Tests de la API de artículos en `tests/Stock.Tests/Integration/ArticulosTests.cs`: precio de venta calculado, código duplicado rechazado con 409 —incluido el duplicado que difiere sólo en mayúsculas—, y baja de artículo con movimientos rechazada con 409 conservando el histórico (RF-014a, RF-016, RF-017, RF-017a)
- [ ] T083 [US3] Agregar a `tests/Stock.Tests/Integration/ArticulosTests.cs` el caso de que modificar los parámetros de reposición se refleja en la siguiente ejecución de Generar Pedido (RF-033)
- [ ] T084 [P] [US3] Test de contrato del CRUD de `/api/articulos` en `tests/Stock.Tests/Integration/ArticulosContractTests.cs`
- [ ] T085 [P] [US3] Tests de la capa web del ABM de artículos en `tests/Stock.Tests/Web/ArticulosControllerTests.cs`: precio de venta como sólo lectura y propagación del 409 a la vista

### Implementación de la Historia 3

- [ ] T086 [P] [US3] Implementar `ArticuloValidator` en `src/Stock.Api/Domain/Validation/ArticuloValidator.cs` (RF-018, RF-019)
- [ ] T087 [US3] Implementar `ArticuloService` en `src/Stock.Api/Services/ArticuloService.cs` con la verificación previa de baja restringida para devolver un 409 legible en vez de una violación de FK
- [ ] T088 [US3] Implementar el CRUD de `/api/articulos` en `src/Stock.Api/Controllers/ArticulosController.cs`
- [ ] T089 [P] [US3] Crear el `ArticuloViewModel` en `src/Stock.Web/Models/ArticuloViewModel.cs`
- [ ] T090 [US3] Implementar `ArticulosController` en `src/Stock.Web/Controllers/ArticulosController.cs`
- [ ] T091 [US3] Crear las vistas del ABM de artículos en `src/Stock.Web/Views/Articulos/` (Index, Create, Edit, Delete) mostrando el precio de venta como campo de sólo lectura

**Punto de control**: US1, US2 y US3 funcionan de forma independiente. El valor de negocio central está completo.

---

## Fase 6: Historia de Usuario 4 — Inicio de sesión y protección del acceso (Prioridad: P4)

**Objetivo**: exigir sesión autenticada válida para toda funcionalidad salvo el login.

**Test independiente**: verificar que sin token toda llamada devuelve 401, que el login rechaza credenciales inválidas con el mensaje genérico y que con credenciales válidas autoriza el ingreso.

### Tests de la Historia 4 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T092 [P] [US4] Tests unitarios del hashing PBKDF2: dos usuarios con la misma contraseña producen salts y hashes distintos, y la verificación acepta la contraseña correcta y rechaza la incorrecta, en `tests/Stock.Tests/Unit/PasswordHasherTests.cs` (RF-008)
- [ ] T093 [P] [US4] Tests de acceso en `tests/Stock.Tests/Integration/SeguridadTests.cs`: toda llamada sin token devuelve 401, un token expirado devuelve 401, y usuario inexistente y contraseña incorrecta devuelven el **mismo** mensaje "Usuario o contraseña incorrectos" (RF-011, RF-012, V-11)
- [ ] T094 [P] [US4] Test de contrato de `POST /api/auth/login` en `tests/Stock.Tests/Integration/AuthContractTests.cs`
- [ ] T095 [P] [US4] Tests del `BearerTokenHandler` en `tests/Stock.Tests/Web/BearerTokenHandlerTests.cs`: adjunta el encabezado `Authorization` en las llamadas salientes y, ante un 401 de la API, cierra la sesión y redirige al login

### Implementación de la Historia 4

- [ ] T096 [P] [US4] Implementar `PasswordHasher` con PBKDF2-HMAC-SHA256, 210.000 iteraciones, salt aleatorio de 16 bytes y comparación en tiempo fijo, en `src/Stock.Api/Security/PasswordHasher.cs` (R-03)
- [ ] T097 [P] [US4] Implementar `JwtTokenService` en `src/Stock.Api/Security/JwtTokenService.cs`: HS256, vigencia 8 horas, `ClockSkew` en cero y claims `sub`, `name`, `role` (R-04)
- [ ] T098 [US4] Implementar `POST /api/auth/login` en `src/Stock.Api/Controllers/AuthController.cs` devolviendo el mismo mensaje ante usuario inexistente y contraseña incorrecta
- [ ] T099 [US4] Agregar el fixture de tests autenticados en `tests/Stock.Tests/Integration/IntegrationTestBase.cs`: obtiene un token del cliente de la factory y lo adjunta a las llamadas
- [ ] T100 [US4] Registrar la autenticación JWT en `src/Stock.Api/Program.cs` y aplicar `[Authorize]` a todos los controladores excepto `AuthController`
- [ ] T101 [US4] Adaptar los tests de integración de US1, US2 y US3 al fixture autenticado de T099, y confirmar que vuelven a verde tras T100
- [ ] T102 [US4] Agregar la siembra del usuario `admin` con perfil administrador en `src/Stock.Api/Data/Seed/DbSeeder.cs`, tomando la contraseña de `SEED_ADMIN_PASSWORD`
- [ ] T103 [US4] Implementar el `DelegatingHandler` que adjunta `Authorization: Bearer` y maneja el 401 cerrando sesión, en `src/Stock.Web/Services/BearerTokenHandler.cs`, y registrarlo en el `HttpClient` tipado
- [ ] T104 [US4] Implementar el login del front con cookie `HttpOnly` que guarda el JWT como claim protegido, en `src/Stock.Web/Controllers/CuentaController.cs` y `src/Stock.Web/Views/Cuenta/Login.cshtml`

**Punto de control**: el sistema exige autenticación en todas las pantallas salvo el login, y toda la suite está en verde.

---

## Fase 7: Historia de Usuario 5 — ABM de Usuarios y Perfiles (Prioridad: P5)

**Objetivo**: administrar perfiles y usuarios, con la carga de usuarios restringida al perfil administrador.

**Test independiente**: verificar el alta, modificación y baja de perfiles y usuarios, que un usuario no administrador reciba 403 en la carga de usuarios, y que las bajas restringidas se rechacen.

### Tests de la Historia 5 ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T105 [P] [US5] Test unitario de la política de contraseña de mínimo 8 caracteres alfanuméricos, en `tests/Stock.Tests/Unit/PasswordPolicyTests.cs` (RF-009)
- [ ] T106 [P] [US5] Tests de usuarios en `tests/Stock.Tests/Integration/UsuariosTests.cs`: 403 para perfil no administrador, 400 sin grabar ante contraseña corta, ninguna respuesta incluye `Hash` ni `Salt`, y **una modificación sin contraseña no re-deriva el hash** (RF-006, RF-007, RF-009, RF-010)
- [ ] T107 [P] [US5] Tests de perfiles en `tests/Stock.Tests/Integration/PerfilesTests.cs`: baja de perfil con usuarios asignados rechazada con 409, y modificación de la Descripción persistida (RF-002a, RF-003)
- [ ] T108 [P] [US5] Test de contrato del CRUD de `/api/usuarios` y `/api/perfiles` en `tests/Stock.Tests/Integration/SeguridadContractTests.cs`
- [ ] T109 [P] [US5] Tests de la capa web de los ABM de seguridad en `tests/Stock.Tests/Web/SeguridadControllerTests.cs`: la entrada de menú no se muestra a perfiles no administradores

### Implementación de la Historia 5

- [ ] T110 [P] [US5] Implementar `PasswordPolicy` en `src/Stock.Api/Domain/Validation/PasswordPolicy.cs` (RF-009)
- [ ] T111 [P] [US5] Definir la política de autorización `SoloAdministrador` en `src/Stock.Api/Security/AuthorizationPolicies.cs`, devolviendo 403 al usuario autenticado sin el perfil (RF-010)
- [ ] T112 [US5] Implementar `PerfilService` en `src/Stock.Api/Services/PerfilService.cs` con la verificación previa de baja restringida
- [ ] T113 [US5] Implementar `UsuarioService` en `src/Stock.Api/Services/UsuarioService.cs`, re-derivando el hash sólo cuando la modificación incluye contraseña
- [ ] T114 [US5] Implementar el CRUD de `/api/perfiles` en `src/Stock.Api/Controllers/PerfilesController.cs` con la política `SoloAdministrador`
- [ ] T115 [US5] Implementar el CRUD de `/api/usuarios` en `src/Stock.Api/Controllers/UsuariosController.cs` con la política `SoloAdministrador` y DTOs que nunca expongan `Hash` ni `Salt`
- [ ] T116 [P] [US5] Crear los ViewModels de usuario y perfil en `src/Stock.Web/Models/UsuarioViewModel.cs` y `src/Stock.Web/Models/PerfilViewModel.cs`
- [ ] T117 [US5] Implementar `UsuariosController` y `PerfilesController` en `src/Stock.Web/Controllers/`
- [ ] T118 [US5] Crear las vistas de los ABM en `src/Stock.Web/Views/Usuarios/` y `src/Stock.Web/Views/Perfiles/`, ocultando la entrada de menú a los perfiles no administradores

**Punto de control**: las cinco historias funcionan de forma independiente.

---

## Fase 8: Pulido y Aspectos Transversales

**Propósito**: requisitos que atraviesan todas las historias y validación final contra el spec.

### Tests ⚠️ ESCRIBIR PRIMERO, DEBEN FALLAR

- [ ] T119 [P] Test de que la bitácora sobrevive al rollback de la transacción fallida y de que un 422 de negocio **no** genera fila en `ErrorLog`, en `tests/Stock.Tests/Integration/ErrorLogTests.cs` (V-12, CE-008)
- [ ] T120 [P] Test de rendimiento con 10.000 artículos y 100.000 líneas de detalle que siembra los datos, ejecuta **30 corridas de cada consulta descartando las 3 primeras** y asierta el p95 por debajo de 3 segundos, en `tests/Stock.Tests/Integration/RendimientoTests.cs` marcado con la categoría `Volumen` (V-5, CE-002)

### Implementación

- [ ] T121 Implementar `ErrorLogDbContext` con **conexión independiente** y **sin migraciones propias** en `src/Stock.Api/Data/ErrorLogDbContext.cs`, mapeando la tabla ya creada por la migración de la Fase 2 (R-08)
- [ ] T122 Implementar el middleware global de excepciones en `src/Stock.Api/Middleware/ExceptionLoggingMiddleware.cs`: registra sólo errores de ejecución no controlados y devuelve un mensaje genérico sin detalle interno (RF-028)
- [ ] T123 [P] Agregar el layout y la navegación comunes en `src/Stock.Web/Views/Shared/_Layout.cshtml` con acceso a las dos consultas y los tres ABM
- [ ] T124 [P] Documentar en `README.md` el procedimiento de carga del inventario de apertura mediante Movimientos de Compra, que RF-029 satisface sin código adicional
- [ ] T125 [P] Verificar que `AGENTS.md` describe con exactitud los comandos reales del proyecto ya construido (compose, migraciones, tests)
- [ ] T126 Ejecutar `dotnet test StockModulo.sln` y confirmar que la suite por defecto pasa y que **no** incluyó la categoría `Volumen`, gracias al `.runsettings` de T015
- [ ] T127 Ejecutar `dotnet test StockModulo.sln --filter TestCategory=Volumen` y confirmar el presupuesto de CE-002; si falla, revisar la decisión R-01 de [research.md](./research.md) antes de denormalizar nada
- [ ] T128 Recorrer los 12 escenarios de [quickstart.md](./quickstart.md) y confirmar la cobertura de los 8 criterios de éxito

---

## Dependencias y Orden de Ejecución

### Dependencias entre fases

- **Setup (Fase 1)**: sin dependencias, arranca de inmediato
- **Fundacional (Fase 2)**: depende del Setup — **BLOQUEA todas las historias**
- **Historias (Fases 3–7)**: todas dependen de la Fase 2
- **Pulido (Fase 8)**: depende de las historias que se quieran entregar

### Dentro de la Fase 2 — el ciclo rojo→verde es estrictamente secuencial entre bloques

- Bloque 1 (T016–T019) antes que Bloque 2: los tests deben existir y no compilar
- Bloque 2 (T020–T028) antes que T029: el andamiaje los hace compilar y fallar
- **T029 es una puerta**: si algún test pasa acá, está mal escrito
- Bloque 3 (T030–T036) antes que T037: las configuraciones los ponen en verde

### Dependencias entre historias

- **US1 (P1)**: arranca apenas termina la Fase 2. Se valida sobre datos sembrados, sin necesidad del ABM de artículos ni de la carga de movimientos por pantalla.
- **US2 (P2)**: arranca apenas termina la Fase 2. Reutiliza el `ExcelExporter` de T053; si se implementara antes que US1, esa tarea se mueve a US2.
- **US3 (P3)**: independiente. Enriquece a US1 al permitir mantener los parámetros de reposición desde la UI.
- **US4 (P4)**: independiente y transversal. T100 aplica `[Authorize]` sobre los endpoints de US1–US3 y **rompe deliberadamente** sus tests de integración; T099 y T101 son las tareas que introducen el fixture autenticado y los reparan. No es un efecto colateral no planificado: es trabajo con tarea asignada.
- **US5 (P5)**: depende de US4 para la política `SoloAdministrador` y para el hasher de contraseñas (T096).

**Única dependencia real entre historias**: US5 → US4. Las demás son independientes.

### Dentro de cada historia

- Los tests se escriben primero y **deben fallar** antes de implementar (Principio I)
- Entidades antes que servicios; servicios antes que endpoints; API antes que las vistas MVC
- Las tareas que agregan casos a un archivo de test ya creado por otra tarea **no** llevan `[P]`

### Oportunidades de paralelismo

- Fase 1: T003, T004, T011, T012, T013, T014 en paralelo. T006–T008 modifican los `.csproj` creados por T002–T004, por eso no llevan `[P]`
- Fase 2: los cuatro tests de esquema (T016–T019) en paralelo; luego las seis entidades (T020–T025) en paralelo; luego las cinco configuraciones (T030–T034) en paralelo
- Dentro de cada historia, los tests marcados `[P]` escriben en archivos distintos y corren en paralelo
- Con equipo: tras la Fase 2, US1, US2, US3 y US4 pueden avanzar en paralelo; US5 espera a US4

---

## Ejemplo de paralelismo: Historia de Usuario 1

```bash
# Escribir en paralelo los tests de US1 que tocan archivos distintos (deben fallar):
Tarea: "T042 — 6 combinaciones en tests/Stock.Tests/Unit/PedidoCalculatorTests.cs"
Tarea: "T044 — contrato en tests/Stock.Tests/Integration/GenerarPedidoContractTests.cs"
Tarea: "T045 — artículo sin movimientos en tests/Stock.Tests/Integration/GenerarPedidoTests.cs"
Tarea: "T048 — exportación en tests/Stock.Tests/Integration/ExportacionExcelTests.cs"
Tarea: "T049 — capa web en tests/Stock.Tests/Web/GenerarPedidoControllerTests.cs"

# T043, T046 y T047 NO son paralelas: agregan casos a archivos que ya crean T042 y T045.

# Luego, en verde, el enum y el ViewModel sí pueden ir en paralelo:
Tarea: "T050 — enum ModoPedido en src/Stock.Api/Domain/Pedido/ModoPedido.cs"
Tarea: "T055 — GenerarPedidoViewModel en src/Stock.Web/Models/GenerarPedidoViewModel.cs"
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
7. + Pulido → bitácora, rendimiento verificado y validación integral

### Nota sobre el orden de la autenticación

US4 es P4 a propósito: el spec justifica que el valor de negocio central (US1–US3) se demuestre
antes de endurecer el acceso. La contrapartida —que aplicar `[Authorize]` rompa los tests ya
escritos— está cubierta por T099 y T101, que forman parte del alcance de US4 y no se descubren
sobre la marcha.

---

## Notas

- Las tareas `[P]` tocan archivos distintos y no tienen dependencias pendientes entre sí. Una tarea que agrega casos a un archivo creado por otra tarea nunca lleva `[P]`.
- Verificar que cada test falla antes de implementar: sin rojo previo no hay verde válido (Principio I). En la Fase 2 esa verificación tiene tarea propia (T029).
- Commitear después de cada tarea o grupo lógico
- Se puede parar en cualquier punto de control para validar la historia de forma aislada
- Los tests de integración usan `WebApplicationFactory` in-process y requieren el SQL Server de `docker compose` levantado (R-10)
- La suite por defecto excluye la categoría `Volumen` por el `.runsettings` de T015; el test de rendimiento se corre aparte con T127
