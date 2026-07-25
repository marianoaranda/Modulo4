# Investigación (Fase 0): Módulo de Stock — Generación automática de pedidos

**Funcionalidad**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Fecha**: 2026-07-25

Este documento resuelve todas las incógnitas técnicas del plan. El stack está fijado por la
constitución y `AGENTS.md`, por lo que la investigación no evalúa alternativas de stack sino
**cómo** satisfacer los requisitos dentro de ese stack.

---

## R-01: Cálculo del Stock Actual sin campo persistido (riesgo abierto del spec)

**Contexto**: El spec exige que el Stock Actual sea siempre derivado del saldo de movimientos
(Supuestos, RF-029) y a la vez que las consultas respondan en <3 s p95 con 10.000 artículos y
100.000 líneas de detalle (CE-002). El propio spec marcó la tensión como riesgo a resolver acá.

**Decisión**: Calcular el saldo **en cada consulta**, mediante una agregación SQL sobre el detalle
de movimientos, expuesta como la vista `vw_StockActual`. **No** se persiste un campo de stock ni se
crea una vista indexada.

**Justificación**: El volumen es mucho menor de lo que la redacción del requisito sugiere. Una
agregación `GROUP BY` sobre 100.000 filas con un índice de cobertura es una operación de decenas de
milisegundos en SQL Server — dos órdenes de magnitud por debajo del presupuesto de 3 s. El plan de
ejecución esperado es un *index scan* + *hash aggregate* sobre ~100k filas, más un *hash join*
contra 10k artículos. Introducir denormalización aquí sería complejidad no justificada y violaría
tanto el supuesto explícito del spec como el Principio III (fidelidad a la fuente de verdad): un
campo de stock persistido es un segundo lugar donde el stock puede estar mal.

Índice de soporte: `IX_MovimientoDetalle_ArticuloId` sobre `(ArticuloId)` con `INCLUDE (Cantidad, MovimientoNumero)`.

**Alternativas consideradas**:
- *Campo `StockActual` persistido en Artículo, actualizado por trigger o por la capa de servicio*: rechazada. Contradice el supuesto del spec, duplica la fuente de verdad y abre la puerta a desincronización silenciosa. Sólo se justificaría con volúmenes 100× mayores.
- *Vista indexada (materializada) con `SCHEMABINDING`*: rechazada. Agrega costo a cada escritura de movimiento y restricciones de esquema, para resolver un problema de rendimiento que no existe a esta escala.
- *Tabla de snapshots periódicos de saldo*: rechazada por la misma razón, y agrega la complejidad de decidir la ventana de consolidación.

**Verificación**: `quickstart.md` incluye un escenario de carga que siembra 10.000 artículos y
100.000 líneas y mide el p95 de ambas consultas, cerrando CE-002 con evidencia y no con suposición.

---

## R-02: Atomicidad y concurrencia del invariante Stock Actual ≥ 0

**Contexto**: RF-024b exige que validación y escritura sean atómicas para toda operación sobre
movimientos, que ante concurrencia una operación se aplique y la otra se rechace con el error de
*stock insuficiente*, y explícitamente que el usuario **no** reciba un error de conflicto de
concurrencia que lo obligue a reintentar.

**Decisión**: Bloqueo **pesimista** sobre las filas de `Articulo` involucradas, dentro de una única
transacción de EF Core. Protocolo obligatorio para toda ruta de escritura de movimientos:

1. Abrir transacción.
2. Tomar `UPDLOCK, HOLDLOCK` sobre las filas de `Articulo` afectadas, **ordenadas por `ArticuloId` ascendente**.
3. Calcular el Stock Actual resultante leyendo `vw_StockActual` dentro de la transacción.
4. Validar el invariante ≥ 0 para todas las líneas.
5. Insertar/actualizar/eliminar encabezado y detalle.
6. Confirmar.

**Justificación**: Con bloqueo pesimista, la segunda operación concurrente **espera** a que la
primera confirme y luego re-lee el saldo ya actualizado; si entonces no alcanza, falla con
*stock insuficiente* — que es exactamente el comportamiento que RF-024b pide. Un esquema optimista
produciría en cambio un error de concurrencia y exigiría reintento, que el requisito prohíbe.

La fila de `Articulo` funciona como **mutex por artículo**: no impide por sí sola que otra
transacción inserte detalle, por lo que la corrección depende de que *todas* las rutas de escritura
respeten el paso 2. Se documenta como invariante de arquitectura y se cubre con un test de
concurrencia. El ordenamiento ascendente por `ArticuloId` evita deadlocks entre movimientos
multilínea que compartan artículos.

**Alternativas consideradas**:
- *Nivel de aislamiento `SERIALIZABLE`*: correcto pero toma bloqueos de rango sobre el detalle, aumenta la contención y la tasa de deadlocks; los deadlocks se manifiestan como error de concurrencia, justo lo que RF-024b prohíbe exponer.
- *Concurrencia optimista con `rowversion`*: rechazada, produce el error de reintento prohibido por RF-024b.
- *`sp_getapplock` por artículo*: funcionalmente equivalente, pero introduce un mecanismo de bloqueo fuera del modelo relacional, más difícil de razonar y de testear.

---

## R-03: Almacenamiento de contraseñas (RF-007, RF-008)

**Decisión**: PBKDF2-HMAC-SHA256 vía `Rfc2898DeriveBytes`, con **salt aleatorio de 16 bytes por
usuario** generado con `RandomNumberGenerator`, 210.000 iteraciones y subclave derivada de 32 bytes.
`Hash` y `Salt` se persisten en **columnas separadas**, en Base64.

**Justificación**: El PRD (RF-04) define explícitamente los campos `Hash` y `Salt` como columnas
distintas del usuario, y RF-008 exige que el salt sea observable como propio de cada usuario. El
`PasswordHasher<TUser>` de ASP.NET Core Identity **no sirve** aquí: empaqueta salt, iteraciones y
subclave dentro de una única cadena, sin columna `Salt` separada, lo que incumpliría la forma
requerida por el PRD. Por eso se usa `Rfc2898DeriveBytes` directamente. El recuento de iteraciones
sigue la recomendación de OWASP para PBKDF2-HMAC-SHA256.

La comparación en el login usa `CryptographicOperations.FixedTimeEquals` para evitar filtrado por
tiempo. El mensaje de error es idéntico para usuario inexistente y contraseña incorrecta (RF-011),
de modo que no se revele la existencia de la cuenta.

**Alternativas consideradas**:
- *BCrypt (`BCrypt.Net-Next`)*: algoritmo sólido, pero embebe el salt en el hash resultante — misma incompatibilidad con el esquema de columnas del PRD.
- *Argon2id*: preferible criptográficamente, pero requiere paquete de terceros no incluido en el stack fijado y aporta poco frente a PBKDF2 bien parametrizado en el contexto de este proyecto.

---

## R-04: Vigencia del token JWT (hueco diferido de `/clarify`)

**Decisión**: JWT firmado con **HS256**, vigencia de **8 horas**, sin *refresh token*. La clave de
firma se inyecta por variable de entorno. `ClockSkew` se fija en cero para que la expiración sea
exacta. Claims: `sub` (UsuarioId), `name` (Usuario), `role` (Descripción del perfil), `exp`.

**Justificación**: Ocho horas cubren un turno completo de trabajo del comercio sin obligar a
reautenticar en medio de la operación, que es el patrón de uso real (un vendedor abre el sistema al
inicio del día). Un *refresh token* agregaría almacenamiento, rotación y revocación para un sistema
de 1 a 5 usuarios sin requisito que lo pida: complejidad no justificada según la constitución.
El claim `role` sostiene RF-010 (restricción de la carga de usuarios al perfil administrador)
mediante autorización basada en políticas.

**Alternativas consideradas**:
- *Vigencia corta (15–30 min) con refresh token*: postura estándar en sistemas expuestos a internet; desproporcionada para un comercio de barrio con un puñado de usuarios en red local.
- *Token sin expiración*: rechazada, deja la sesión válida indefinidamente si el token se filtra.

---

## R-05: Exportación a Excel (hueco diferido de `/clarify`)

**Decisión**: **ClosedXML** generando `.xlsx` real (OpenXML). La generación ocurre en `Stock.Api`,
que devuelve el archivo como *stream*; `Stock.Web` sólo lo retransmite al navegador.

**Justificación**: ClosedXML es MIT, sin costo ni restricción de uso comercial. Genera `.xlsx`
nativo, que es lo que RF-031 pide al hablar de "archivo Excel", a diferencia de un CSV renombrado.
Generar en la API mantiene una única implementación de la consulta y de su recorte, lo que hace
cumplir RF-031 (el Excel replica exactamente filas, orden y recorte de pantalla) por construcción y
no por coincidencia entre dos implementaciones.

**Alternativas consideradas**:
- *EPPlus*: rechazada por licenciamiento. Desde la versión 5 es comercial (licencia Polyform Noncommercial), inadecuado para un entregable que puede reutilizarse.
- *CSV con extensión `.xls`*: rechazada. Dispara advertencias de formato en Excel y no es un archivo Excel.
- *Generar el Excel en `Stock.Web`*: rechazada. Duplicaría la lógica de consulta, filtro y tope, con riesgo de divergencia frente a RF-031.

---

## R-06: Filtro por descripción insensible a mayúsculas y acentos (RF-027a)

**Decisión**: Fijar la *collation* de la columna `Articulo.Descripcion` en
`Modern_Spanish_CI_AI` (case-insensitive, accent-insensitive) y usar `EF.Functions.Like` con el
patrón `%texto%`.

**Justificación**: Delegar la insensibilidad a la collation de la columna resuelve el requisito en
el motor, sin normalizar cadenas en la aplicación ni mantener una columna espejo sin acentos. Un
`LIKE '%texto%'` provoca *scan*, pero sobre 10.000 artículos el costo es despreciable frente al
presupuesto de 3 s (ver R-01).

**Alternativas consideradas**:
- *Normalizar en C# y comparar contra una columna `DescripcionNormalizada`*: agrega una columna redundante y la obligación de mantenerla sincronizada, para un beneficio de rendimiento nulo a esta escala.
- *Full-Text Search*: desproporcionado; además cambia la semántica de "contiene" por búsqueda por palabras.

---

## R-07: Numeración global de movimientos (RF-020a)

**Decisión**: `Movimiento.Numero` es la **clave primaria `IDENTITY`** de la tabla de encabezados,
compartida por compras y ventas.

**Justificación**: Satisface los cuatro atributos de RF-020a con el mecanismo más simple
disponible: es única globalmente, compartida entre tipos, generada por el motor (no editable por el
usuario) y nunca reutilizada tras una baja, porque `IDENTITY` no reasigna valores liberados.

**Alternativas consideradas**:
- *`SEQUENCE` dedicada más `Id` sustituto*: agrega un objeto de base de datos y una columna sin aportar ninguna propiedad que `IDENTITY` no dé ya. Complejidad no justificada.
- *Numeración por tipo (`Tipo` + `Numero`)*: descartada en `/clarify`; la decisión registrada es secuencia única global.

---

## R-08: Bitácora de errores y transacciones abortadas (RF-028)

**Decisión**: Middleware global de excepciones en `Stock.Api` que escribe en la tabla `ErrorLog`
usando una **conexión y un `DbContext` independientes** de los de la petición fallida. Se registran
únicamente los errores de ejecución no controlados; las validaciones de negocio esperadas
(stock insuficiente, código duplicado, contraseña corta) **no** se registran como errores.

**Propiedad del esquema vs. conexión de escritura** — son dos cosas distintas y conviene no
confundirlas:

- El **esquema** de `dbo.ErrorLog` lo declara y lo versiona `StockDbContext`, junto con el resto de
  las tablas, en la migración inicial. Hay una sola base y un solo historial de migraciones.
- La **escritura en runtime** se hace con `ErrorLogDbContext`, que apunta a la misma base pero abre
  su propia conexión, fuera de la transacción que está fallando.

`ErrorLogDbContext` se configura por lo tanto **sin migraciones propias**: mapea una tabla que ya
existe. Separar también el esquema obligaría a un segundo historial de migraciones sobre la misma
base, sin ganancia alguna. Omitir este punto es el modo de fallo natural del diseño: si `ErrorLog`
no entra en la migración inicial, la tabla nunca se crea y el primer error no controlado falla al
registrarse con *invalid object name*, incumpliendo CE-008 justo cuando más se lo necesita.

**Justificación**: Es el punto de diseño que más fácilmente se implementa mal. Si el registro del
error se escribiera con el mismo `DbContext` de la operación fallida, el `ROLLBACK` de esa
transacción **borraría también el registro del error** — y RF-028 exige que el 100 % de los errores
queden registrados (CE-008). Una conexión separada garantiza que la bitácora sobreviva al rollback.

La distinción entre error de ejecución y validación de negocio evita que la bitácora se llene de
rechazos esperados: una venta sin stock es un resultado previsto del sistema (respuesta 422), no un
fallo. El middleware devuelve al usuario un mensaje genérico sin detalles internos, y guarda
`MachineName`, `Message` y `FullException` para diagnóstico.

**Alternativas consideradas**:
- *Registrar con el `DbContext` de la petición*: rechazada por la pérdida silenciosa de registros ante rollback descrita arriba.
- *Registrar sólo en archivo o consola*: incumple RF-028, que exige tabla con columnas específicas.

---

## R-09: Autenticación entre `Stock.Web` y `Stock.Api`

**Decisión**: `Stock.Web` autentica con cookie propia (`CookieAuthentication`), y guarda el JWT
obtenido de la API como claim protegido dentro de esa cookie. Un `DelegatingHandler` registrado en
el `HttpClient` tipado adjunta `Authorization: Bearer <jwt>` a cada llamada saliente.

**Justificación**: Mantiene el JWT fuera del alcance de JavaScript (la cookie se emite `HttpOnly` y
cifrada con Data Protection), y evita que cada controlador MVC tenga que recordar adjuntar el
encabezado. La API permanece *stateless* y valida el token en cada request, que es lo que exige
RF-012. Cuando el token expira, la API responde 401 y el `Stock.Web` cierra la sesión y redirige al
login.

**Alternativas consideradas**:
- *JWT en `localStorage` desde el navegador*: expone el token a XSS y no aplica bien a un front MVC renderizado en servidor.
- *Sesión de servidor en `Stock.Web` con el token*: requiere almacén de sesión distribuido o afinidad de servidor; la cookie cifrada logra lo mismo sin estado adicional.

---

## R-10: Estrategia de pruebas para el ciclo Test-First (Principio I)

**Decisión**: Un único proyecto `tests/Stock.Tests` (NUnit) con dos categorías separadas por
`[Category]`:

- **`Unit`** — sin base de datos. Cubre la calculadora de pedido contra el Conjunto de Datos de Referencia del spec (las 6 combinaciones), el cálculo de Precio de Venta, el hashing de contraseñas y los validadores de artículo y movimiento.
- **`Integration`** — contra el SQL Server de `docker compose`, cada corrida sobre una base propia creada y migrada al inicio. Cubre las consultas, el invariante de stock, la atomicidad todo-o-nada, la concurrencia de CE-004 y el flujo de autenticación.

**Justificación**: La lógica de pedido —el corazón del módulo— es una función pura sobre
parámetros de reposición y saldo, por lo que puede desarrollarse íntegramente en rojo→verde→refactor
sin infraestructura, que es donde el Principio I aporta más. En cambio, las decisiones de R-01, R-02
y R-06 dependen de comportamiento específico del motor (planes de agregación, bloqueos, collation) y
serían inverificables con un doble en memoria: exigen SQL Server real.

**Alternativas consideradas**:
- *SQLite o el proveedor InMemory de EF Core para las pruebas de integración*: rechazada. No reproduce `UPDLOCK`/`HOLDLOCK`, ni las collations acento-insensibles, ni los planes de agregación — es decir, no puede verificar precisamente los tres puntos de mayor riesgo del diseño. Daría una señal verde falsa.
- *Testcontainers para levantar SQL Server por corrida*: técnicamente superior en aislamiento, pero agrega una dependencia y duplica el mecanismo de arranque frente al `docker compose` que `AGENTS.md` ya define como flujo del proyecto.

---

## Resumen de decisiones

| # | Tema | Decisión |
|---|------|----------|
| R-01 | Stock Actual | Calculado por agregación en `vw_StockActual`; sin campo persistido |
| R-02 | Concurrencia | Bloqueo pesimista `UPDLOCK` sobre `Articulo`, en orden ascendente de Id |
| R-03 | Contraseñas | PBKDF2-HMAC-SHA256, 210k iteraciones, salt de 16 bytes en columna propia |
| R-04 | JWT | HS256, 8 horas, sin refresh token, clave por variable de entorno |
| R-05 | Excel | ClosedXML (MIT), `.xlsx` generado en la API |
| R-06 | Filtro | Collation `Modern_Spanish_CI_AI` sobre `Descripcion` + `LIKE '%…%'` |
| R-07 | Número de movimiento | `IDENTITY` como clave primaria del encabezado |
| R-08 | Bitácora | Middleware global con conexión independiente, sobrevive al rollback |
| R-09 | Auth Web→API | Cookie `HttpOnly` con el JWT + `DelegatingHandler` |
| R-10 | Pruebas | NUnit, `Unit` sin base y `Integration` contra SQL Server real |

**Estado**: sin `NEEDS CLARIFICATION` pendientes. Los dos huecos que `/clarify` había diferido
(vigencia del token, formato de exportación) quedan resueltos en R-04 y R-05.
