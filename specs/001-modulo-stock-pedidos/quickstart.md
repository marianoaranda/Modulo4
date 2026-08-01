# Quickstart y Validación (Fase 1): Módulo de Stock

**Funcionalidad**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Modelo de datos**: [data-model.md](./data-model.md) | **Contratos**: [contracts/](./contracts/)

Guía para levantar el módulo y **demostrar que cumple el spec**. Cada escenario cierra un criterio
de éxito concreto; no es un tutorial de uso.

---

## Prerrequisitos

- .NET SDK 8.0
- Docker (para SQL Server 2017 y el entorno completo)
- Puertos libres: `1433` (SQL Server), `5279` (API), `5280` (Web)

## Puesta en marcha

```powershell
# PRIMER PASO — secretos locales (Principio IV).
# Crear el .env a partir de la plantilla versionada y completar los tres valores:
# SA_PASSWORD, JWT_SIGNING_KEY y SEED_ADMIN_PASSWORD. El .env está ignorado por git.
cp .env.example .env

# Restaurar dependencias
dotnet restore StockModulo.sln

# Levantar todo: SQL Server + API + Web.
# La API migra y siembra la base sola (ApplyMigrationsOnStartup, sólo en compose).
docker compose up -d --build
```

Web en `http://localhost:5280`, API en `http://localhost:5279`.
Usuario inicial: `admin`. La contraseña se inyecta por la variable de entorno `SEED_ADMIN_PASSWORD`
definida en el compose — **no** está hardcodeada en el código (Principio IV).

Fuera de Docker, contra el SQL Server del compose:

```powershell
# El --context es obligatorio: hay dos DbContext y sólo StockDbContext tiene migraciones.
dotnet ef database update --project src/Stock.Api --context StockDbContext
dotnet run --project src/Stock.Api
dotnet run --project src/Stock.Web
```

## Ejecución de pruebas

```powershell
# Puerta de calidad de la constitución. El .runsettings del proyecto de tests
# excluye la categoría Volumen, así que NO dispara la siembra de 10.000 artículos
# y 100.000 líneas en cada corrida.
dotnet test StockModulo.sln

# Sólo lógica pura, sin base de datos (rápida, apta para el ciclo rojo→verde→refactor)
dotnet test StockModulo.sln --filter TestCategory=Unit

# Sólo integración: requiere el SQL Server de compose levantado
dotnet test StockModulo.sln --filter TestCategory=Integration

# Tests de volumen de CE-002 y CE-004, excluidos de la corrida por defecto.
# Va por --settings y NO por --filter: el .runsettings del csproj ya trae
# TestCategory!=Volumen y --filter se combina con ese valor mediante AND, de modo que
# `--filter TestCategory=Volumen` no selecciona ninguna prueba y termina en verde.
dotnet test StockModulo.sln --settings tests/Stock.Tests/volumen.runsettings
```

Los tests de integración hospedan las aplicaciones **in-process** con `WebApplicationFactory`; de
`docker compose` usan sólo el SQL Server (R-10).

---

## Escenarios de validación

### V-1 — Las 6 combinaciones de Generar Pedido (CE-003, RF-026)

El escenario ancla del módulo. Se siembra el **Conjunto de Datos de Referencia** definido en el
spec y se ejecuta la consulta con las 6 combinaciones.

**Preparación**: cargar los 4 artículos del conjunto de referencia y los movimientos de compra/venta
que dejan a cada uno en el Stock Actual indicado (A-001 → 5, A-002 → 15, A-003 → 60, A-004 → 0).

**Ejecución**: `GET /api/consultas/generar-pedido?soloBajoMinimo={…}&modoPedido={…}` para cada par.

**Resultado esperado**: exactamente la matriz de 6 × 4 = 24 celdas de la tabla del spec — 15
cantidades asertadas y 9 exclusiones ("—") que deben verificarse como filas ausentes. En particular:

- Con `soloBajoMinimo=false` se listan **los 4** artículos, incluidos los de cantidad 0.
- Con `soloBajoMinimo=true` se lista **sólo A-001**. A-004 queda fuera porque `0 < 0` es falso.
- Ninguna cantidad es negativa en ninguna de las 6 corridas.

Cubre también RF-026 en la UI: la misma matriz debe verse en `http://localhost:5280` en la pantalla
Generar Pedido.

### V-2 — Invariante de stock no negativo (CE-005, RF-024, RF-024a)

1. Cargar una compra de 10 unidades de un artículo.
2. Intentar una venta de 15 → se espera `422` y que **nada** quede grabado.
3. Vender 10 (queda en 0), luego intentar **dar de baja la compra original** → se espera `422`:
   la baja dejaría el stock en −10. Éste es el caso que sólo se cubre desde la corrección de RF-024a.
4. Verificar en la Consulta de Stock Actual que el saldo quedó en 0 y que la compra sigue existiendo.

### V-3 — Atomicidad todo-o-nada (RF-024c)

Enviar un movimiento de venta con 3 líneas donde la tercera excede el stock disponible.

**Resultado esperado**: `422`, y ninguna de las 3 líneas aplicada. El Stock Actual de los artículos
de las líneas 1 y 2 queda **exactamente igual** que antes del intento.

### V-4 — Concurrencia (CE-004, RF-024b)

Con un artículo en stock 10, lanzar **5 ventas simultáneas de 4 unidades** cada una.

**Resultado esperado**:
- Se graban como máximo 2 (8 unidades); las restantes fallan con `422` *stock insuficiente*.
- El Stock Actual final es ≥ 0 en todos los casos.
- **Ninguna** respuesta es un error de conflicto de concurrencia que pida reintentar — el requisito lo prohíbe explícitamente.

Es el escenario que verifica el protocolo de bloqueo pesimista descrito en
[data-model.md](./data-model.md#protocolo-de-escritura-de-movimientos-invariante-de-arquitectura).

### V-5 — Rendimiento con volumen real (CE-002)

Sembrar **10.000 artículos y 100.000 líneas de detalle** de movimiento y medir ambas consultas.

**Resultado esperado**: p95 < 3 segundos en Consulta de Stock Actual y en Generar Pedido.

Éste es el escenario que cierra el riesgo abierto que el spec dejó marcado: valida que el Stock
Actual puede calcularse por agregación **sin** persistir un campo de stock (ver
[research.md](./research.md#r-01-cálculo-del-stock-actual-sin-campo-persistido-riesgo-abierto-del-spec)).
Si este escenario fallara, la decisión R-01 debe revisarse antes de denormalizar nada.

### V-6 — Determinismo del recorte de 10.000 (RF-027b, RF-027c)

Con más de 10.000 artículos cargados, ejecutar la Consulta de Stock Actual sin filtro **dos veces**.

**Resultado esperado**: ambas corridas devuelven exactamente el mismo conjunto de filas, ordenado
por Código ascendente, y `truncado = true`. La UI informa que el resultado fue recortado.

### V-7 — Artículos sin movimientos (RF-030)

Dar de alta un artículo nuevo con `stockMinimo = 10` y **ningún** movimiento.

**Resultado esperado**: aparece en la Consulta de Stock Actual con cantidad 0, y en Generar Pedido
con `soloBajoMinimo=true` aparece con cantidad a pedir 10. Un artículo nuevo debe poder pedirse.

### V-8 — Filtro insensible a mayúsculas y acentos (RF-027a)

Con un artículo de descripción `Válvula de bronce`, filtrar por `valvula`, `VÁLVULA` y `bronce`.

**Resultado esperado**: las tres búsquedas lo encuentran. Un filtro vacío no acota nada.

### V-9 — Rango de códigos (RF-025a)

- Rango `A-001` a `A-003` → incluye ambos extremos.
- `codigoDesde` vacío → sin límite inferior; `codigoHasta` vacío → sin límite superior; ambos vacíos → todo el catálogo.
- `codigoDesde=Z-999`, `codigoHasta=A-001` (invertido) → resultado **vacío**, con mensaje informativo y **sin error**.

### V-10 — Exportación a Excel (RF-031)

Exportar ambas consultas con los mismos parámetros usados en pantalla.

**Resultado esperado**: archivo `.xlsx` que abre en Excel sin advertencias, con las mismas filas, el
mismo orden y el mismo recorte que la pantalla. Un resultado vacío exporta sólo los encabezados.

### V-11 — Credenciales y acceso (CE-006, CE-007, RF-008 a RF-012)

1. Crear dos usuarios con **la misma contraseña** → sus `Salt` y `Hash` en base deben ser distintos.
2. Consultar cualquier endpoint sin token → `401`.
3. Login con contraseña incorrecta y con usuario inexistente → **el mismo** mensaje "Usuario o contraseña incorrectos", ambos `401`.
4. Con un usuario de perfil no administrador, llamar `/api/usuarios` → `403`.
5. Intentar crear un usuario con contraseña de 7 caracteres → `400`, sin grabar.

### V-12 — Bitácora de errores sobrevive al rollback (CE-008, RF-028)

Forzar una excepción no controlada dentro de una operación transaccional de movimientos.

**Resultado esperado**: la transacción hace rollback (el movimiento no queda), **y aun así** el
error figura en `ErrorLog` con fecha, `MachineName`, mensaje y excepción completa. El usuario recibe
un mensaje genérico, sin detalle interno.

Verificar además que un rechazo de negocio (`422` por stock insuficiente) **no** genera fila en
`ErrorLog`: la bitácora es para fallos, no para rechazos esperados.

### V-13 — La identidad del administrador no depende del texto (CE-007a, RF-002b, RF-003a, RF-005a)

1. Renombrar la Descripción del perfil administrador a "operador" → sus usuarios **siguen** accediendo a `/api/usuarios` (`200`).
2. Renombrar el perfil "vendedor" a "administrador" → sus usuarios siguen recibiendo `403` en `/api/usuarios`.
3. Enviar `EsAdministrador: true` en el cuerpo de un alta o modificación de perfil → el campo se ignora y en base sigue habiendo **un solo** perfil con la marca.
4. Intentar dar de baja el perfil administrador, incluso sin usuarios asignados → `409`.
5. Con un único usuario administrador, intentar darlo de baja y luego cambiarle el perfil → `409` en ambos casos; con dos administradores, ambas operaciones se aceptan.

**Resultado esperado**: ninguna secuencia del ABM de seguridad deja al sistema sin administrador ni
transfiere el privilegio a otro perfil.

### V-14 — Carga del inventario de apertura (RF-029)

Sobre una base recién migrada y sin movimientos:

1. Verificar que los artículos del catálogo aparecen en ambas consultas con Stock Actual 0.
2. Cargar un Movimiento de tipo **Compra** con la fecha de apertura y una línea por artículo con su existencia física contada.
3. Volver a ejecutar la Consulta de Stock Actual.

**Resultado esperado**: el Stock Actual de cada artículo coincide con la existencia cargada, sin
haber usado ningún campo de "stock inicial". Confirma que RF-029 se satisface por construcción con
el CRUD de movimientos y que no hace falta código adicional — que es exactamente lo que documenta
T128.

### V-15 — La búsqueda de artículos, desde sus dos pantallas (RF-034 a RF-034c, RF-020e, RF-020f)

Con al menos dos artículos en el catálogo, uno de ellos `A-001`:

1. Abrir **Movimientos → Nuevo**. Verificar que el campo **Número** muestra el correlativo siguiente y no se puede editar; anotarlo.
2. En la primera línea del detalle, tipear `a-001` a mano y salir del campo → debajo del Código aparece la Descripción de `A-001`. La resolución es insensible a mayúsculas (RF-017a).
3. Borrar el Código → la Descripción desaparece. Tipear `Z-999` (inexistente) → la Descripción queda vacía y **no** se muestra ningún error todavía.
4. Presionar el botón de la **lupa** junto al campo. En la ventana, dejar la Descripción vacía y presionar **Buscar** → se listan artículos con las columnas Código y Descripción. Con más de 10.000, aparece el aviso de recorte.
5. Elegir `A-001` de la grilla → el Código queda en el campo y la Descripción se actualiza **igual que al tipearlo**: es la misma ruta, no una alternativa.
6. Completar Cantidad y Precio Unitario, y grabar → el Número asignado coincide con el que mostraba el paso 1 (si nadie más grabó en el medio).
7. Intentar un alta con el Código `Z-999` → la API responde **404** nombrando `Z-999` y no se graba ninguna línea del movimiento.
8. Abrir **Consulta de Stock Actual** y repetir los pasos 4 y 5 sobre **Código desde** → la ventana y su comportamiento son idénticos a los de la carga de movimientos.

**Resultado esperado**: las dos pantallas ofrecen la misma ventana —una sola definición en el
proyecto— y el Código elegido dispara exactamente las mismas operaciones que el tecleado. El
identificador interno del artículo no aparece en ningún momento.

### V-16 — Carga asistida del detalle: precio sugerido y totales (RF-020g a RF-020i)

Con el artículo `A-001` en el catálogo, Precio de Costo 100 y Margen 50 (Precio de Venta 150):

1. Abrir **Movimientos → Nuevo** con Tipo **Compra**. Cargar `A-001` en la primera línea → el Precio Unitario se completa con **100**, el Precio de Costo.
2. Cambiar el Tipo a **Venta** → el Precio Unitario **no cambia**: la sugerencia no pisa lo que ya está cargado. Volver a ingresar el Código `A-001` → ahora se sugiere **150**, el Precio de Venta.
3. Reemplazar el Precio Unitario por **123.45** a mano y poner Cantidad 2 → el Precio Total de la línea muestra **246.90** y el **Total General** lo acompaña.
4. Cargar una segunda línea con `A-001`, Cantidad 1 → el Total General pasa a **370.35**, la suma de la columna Precio Total.
5. Grabar y volver a abrir el movimiento → el Precio Unitario grabado es **123.45**, el que se tecleó, y **no** se re-sugiere el del catálogo.
6. En una línea nueva, tipear un Código inexistente → no hay sugerencia, el Precio Unitario queda como estaba y la Descripción se muestra vacía, sin error.

**Resultado esperado**: la sugerencia acelera la carga sin decidir nada — lo que se graba es siempre
lo que quedó en el campo (RF-023b)— y los dos totales son derivados: se recalculan en pantalla y no
se persisten.

---

## Mapa de cobertura

| Criterio de éxito | Escenarios |
|---|---|
| CE-001 | V-1 |
| CE-002 | V-5 |
| CE-003 | V-1 |
| CE-004 | V-4 (invariante de stock no negativo), V-5 (latencia con 5 usuarios concurrentes) |
| CE-005 | V-2, V-3 |
| CE-006 | V-11 |
| CE-007 | V-11 |
| CE-007a | V-13 |
| CE-008 | V-12 |

Los escenarios V-6 a V-10 y V-14 cubren requisitos funcionales sin criterio de éxito propio pero con
comportamiento observable definido (recorte determinista, artículos sin movimientos, filtro, rango
y exportación).
