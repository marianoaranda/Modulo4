# Especificación de Funcionalidad: Módulo de Stock — Generación automática de pedidos

**Rama de la funcionalidad**: `001-modulo-stock-pedidos`

**Fecha de creación**: 2026-07-24

**Estado**: Implementado, con brecha conocida de interfaz.

Los requisitos RF-001 a RF-033 están implementados y verificados. Los requisitos
**RF-016a, RF-020e, RF-020f y RF-034 a RF-034c** —todos de interfaz de usuario, incorporados el
2026-07-31 al integrar clarificaciones de la sesión 2026-07-25 que habían quedado sin traducir a
requisitos— están **especificados pero no implementados**, y por lo tanto no tienen tareas ni
cobertura de tests todavía. Se los identifica en la lista con la marca *pendiente de
implementación*.

**Entrada**: Descripción del usuario: "Generá el spec a partir del PRD que está en /PRD.md"

## Clarificaciones

### Sesión 2026-07-25

- Q: ¿Cómo se determina el Número del encabezado de un Movimiento y qué unicidad tiene? → A: Autogenerado por el sistema, con una secuencia única global compartida entre compras y ventas.
- Q: ¿Qué pasa si la baja o modificación de una compra dejaría el stock de un artículo en negativo? → A: El invariante "stock ≥ 0" aplica a toda operación; esas bajas/modificaciones se rechazan.
- Q: ¿Qué pasa al dar de baja un artículo con movimientos o un perfil con usuarios asignados? → A: Baja restringida: la operación se rechaza con un error; no hay baja lógica ni cascada.
- Q: ¿Cómo se garantiza el stock ≥ 0 si dos usuarios graban ventas del mismo artículo a la vez? → A: Validación y grabación atómicas: una se graba y la otra se rechaza con el error de stock insuficiente.
- Q: ¿Sobre qué campo se define el "rango de artículos" de la Consulta de Stock Actual y son obligatorios sus extremos? → A: Rango inclusivo sobre el Código con orden alfabético (texto); ambos extremos opcionales, vacío = sin límite por ese lado.
- Q: ¿Cómo se muestra el precio de venta en la carga de articulos? → A: cuando se esten editando por pantalla los campos de precio de costo o margen, debe verse por pantalla el resultado del precio de venta de manera interactiva. Agregale algo de javascript a la pantalla de articulos para que calcule el precio de venta cuando cambia el precio de costo o el margen.
- Q: ¿Cómo se obtiene el numero de movimiento? → A: en la carga de movimientos, Sugerime automaticamente el numero de movimmiento correlativo, sin importar si el tipo de movimiento es compra o venta, la numeracion es la misma para todos los tipos de movimiento.
- Q: ¿Cómo buscan los codigos de articulo? → A: Tanto en la carga de movimientos, como en la consulta de sock actual, y en la generacion de pedidos, al lado de cada textbox que pida un código de articulo, de haber un boton chico que tenga solamente un icono de una lupa, que al presionarlo
abra un pop up de buequeda, y cuando se elija un registro de la grilla de busqueda se debe trasladar el codigo al texbox. Dicho pop up de busqueda debe pedir un campo descripcion y un boton "buscar", al presionar buscar llena una grilla con scroll vertical, mostrando 2 columnas, el codigo y la descripcion del articulo filtrando los articulos que cumplen con la descripcion ingresada, buscando por contenido usando LIKE, si la descripcion ingresada esta vacia listar todos los articulos en la grilla de la busqueda. cuando el usuario elija un registro de la busqueda y lo acepte, se deben realizar todas las operaciones asociadas al codigo, tal cual, como si el codigo lo hibiese ingresado a mano el usuario. El pop up de busqueda no deberia tener un alto mayor a 600 pixels.
En cada pantalla que llame a la busqueda se debe mostrar en algun lado la descripcion asociada al codigo de articulo. y obviamente las descripciones se deben mantener actualizadas para que coincidan con lo que representa cada codigo ingresado, cuando el usuario lo cambia manualmente, debe cambiar la descripción
y cuando el usuario utiliza la busqueda, tambien debe cambiar la descripcion.
- Q: ¿Cómo se implementa la consulta de articulos? → A: El pop up de busqueda de articulos, se debe implementar aparte, de manera encapsulada, de modo tal que las pantallas que la necesiten, solo tengan que hacer lo minimo para lograr invocar el pop up de busqueda.

### Sesión 2026-07-31

- Q: Las cuatro clarificaciones de la sesión anterior sobre la interfaz (cálculo interactivo del Precio de Venta, sugerencia del Número de Movimiento, popup de búsqueda de artículos y su encapsulamiento) quedaron registradas pero nunca se integraron a Requisitos Funcionales, por lo que no llegaron a la implementación. ¿Qué se hace con ellas? → A: Integrarlas al spec como requisitos formales (RF-016a, RF-020e, RF-020f y RF-034 a RF-034c) sin implementarlas todavía, de modo que la brecha quede documentada y trazable.

## Escenarios de Usuario y Pruebas *(obligatorio)*

### Historia de Usuario 1 - Generar la lista de pedido automáticamente (Prioridad: P1)

El administrativo/vendedor de un comercio de barrio necesita saber, sin cálculos manuales,
qué artículos reponer y en qué cantidad. Abre la consulta "Generar Pedido", elige si quiere
ver todos los artículos o solo los que están por debajo del mínimo, selecciona hasta qué nivel
quiere reponer (stock mínimo, punto de pedido o stock ideal) y obtiene la lista de artículos
con la cantidad a pedir de cada uno, que puede exportar a Excel.

**Por qué esta prioridad**: Es la razón de ser del módulo. Elimina el problema central del negocio
(quedarse sin lo que más rota o comprar de más lo que no se vende) y entrega valor por sí sola
sobre un catálogo y movimientos ya cargados.

**Prueba independiente**: Sobre el Conjunto de Datos de Referencia definido en Criterios de Éxito,
ejecutar la consulta con cada una de las 6 combinaciones de parámetros y verificar que la cantidad
a pedir de cada artículo coincide exactamente con la esperada, y que el resultado se exporta a Excel.

**Escenarios de Aceptación**:

1. **Dado** "solo bajo mínimo" = No y modo "Hasta Stock Mínimo", **Cuando** se ejecuta Generar Pedido, **Entonces** se listan todos los artículos y para cada uno la cantidad a pedir = MAX(0, Stock Mínimo − Stock Actual).
2. **Dado** "solo bajo mínimo" = No y modo "Hasta Punto Pedido", **Cuando** se ejecuta Generar Pedido, **Entonces** se listan todos los artículos y para cada uno la cantidad a pedir = MAX(0, Punto de Pedido − Stock Actual).
3. **Dado** "solo bajo mínimo" = No y modo "Hasta Stock Ideal", **Cuando** se ejecuta Generar Pedido, **Entonces** se listan todos los artículos y para cada uno la cantidad a pedir = MAX(0, Stock Ideal − Stock Actual).
4. **Dado** "solo bajo mínimo" = Sí y modo "Hasta Stock Mínimo", **Cuando** se ejecuta Generar Pedido, **Entonces** solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la cantidad a pedir = Stock Mínimo − Stock Actual.
5. **Dado** "solo bajo mínimo" = Sí y modo "Hasta Punto Pedido", **Cuando** se ejecuta Generar Pedido, **Entonces** solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la cantidad a pedir = Punto de Pedido − Stock Actual.
6. **Dado** "solo bajo mínimo" = Sí y modo "Hasta Stock Ideal", **Cuando** se ejecuta Generar Pedido, **Entonces** solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la cantidad a pedir = Stock Ideal − Stock Actual.
7. **Dado** un resultado en pantalla, **Cuando** se presiona "Exportar a Excel", **Entonces** se descarga un archivo Excel con las columnas Código, Descripción y Cantidad a Pedir, con las mismas filas, el mismo orden y el mismo recorte que la pantalla.
8. **Dado** un conjunto de parámetros que no arroja ninguna fila, **Cuando** se ejecuta Generar Pedido, **Entonces** se muestra una grilla vacía con un mensaje informativo, sin error.

---

### Historia de Usuario 2 - Registrar compras/ventas y consultar el stock actual (Prioridad: P2)

El administrativo/vendedor registra los movimientos del día (compras que suman y ventas que
restan) y consulta la cantidad en existencia de cada artículo por rango, exportable a Excel.
El stock actual de cada artículo es el saldo de sus movimientos.

**Por qué esta prioridad**: Sin movimientos registrados no hay stock real y la generación de pedido
(P1) carece de datos. Es la fuente de verdad sobre la que se calcula todo lo demás.

**Prueba independiente**: Registrar un conjunto de compras y ventas y verificar que la Consulta de
Stock Actual devuelve, para el rango solicitado, el Stock Actual = suma de compras − suma de ventas
de cada artículo, y que el resultado se exporta a Excel.

**Escenarios de Aceptación**:

1. **Dado** un movimiento nuevo válido, **Cuando** se agrega, **Entonces** queda persistido con encabezado y detalle, y el sistema le asigna un Número autogenerado.
2. **Dado** que se graban consecutivamente una compra y una venta, **Cuando** se comparan sus Números, **Entonces** son distintos entre sí (secuencia global compartida) y ningún Número se repite en todo el sistema.
3. **Dado** una línea de detalle con cantidad 0, negativa o no entera, **Cuando** se intenta grabar, **Entonces** el sistema rechaza la operación y no graba el movimiento.
4. **Dado** una línea de detalle, **Cuando** se graba el movimiento, **Entonces** su Precio Total = Cantidad × Precio Unitario, calculado por el sistema.
5. **Dado** un movimiento de venta que dejaría el stock de algún artículo por debajo de 0, **Cuando** se intenta grabar, **Entonces** el sistema muestra un error y no graba el movimiento.
6. **Dado** una compra ya consumida por ventas posteriores, **Cuando** se intenta darla de baja o reducir su cantidad de modo que el stock de algún artículo quede por debajo de 0, **Entonces** el sistema muestra un error y no aplica ningún cambio.
7. **Dado** un movimiento multilínea, **Cuando** falla la validación de cualquiera de sus líneas durante un alta, baja o modificación, **Entonces** ninguna línea queda aplicada y el Stock Actual de todos los artículos permanece como estaba.
8. **Dado** dos ventas concurrentes del mismo artículo cuya suma excede el stock disponible, **Cuando** ambas se intentan grabar, **Entonces** exactamente una se graba y la otra se rechaza con el error de stock insuficiente, evaluado contra el stock ya actualizado.
9. **Dado** un rango de artículos, **Cuando** se ejecuta la Consulta de Stock Actual, **Entonces** devuelve Código, Descripción y Cantidad (el Stock Actual calculado por saldo de movimientos), ordenada por Código ascendente y exportable a Excel.
10. **Dado** un rango con Código inicial y final informados, **Cuando** se ejecuta la Consulta de Stock Actual, **Entonces** se incluyen los artículos cuyo Código está entre ambos extremos inclusive según orden alfabético; y si uno o ambos extremos se dejan vacíos, no se aplica límite por ese lado.
11. **Dado** un artículo del catálogo sin ningún movimiento registrado, **Cuando** se ejecuta cualquiera de las dos consultas, **Entonces** el artículo aparece con Stock Actual 0 y se le aplican las reglas de pedido como a cualquier otro.
12. **Dado** un resultado que alcanza el tope de 10.000 filas, **Cuando** se muestra, **Entonces** el sistema informa explícitamente que el resultado fue recortado y que conviene acotar con el filtro.

---

### Historia de Usuario 3 - Administrar el catálogo de artículos (Prioridad: P3)

El administrador mantiene el catálogo: da de alta, modifica y da de baja artículos con sus
parámetros de reposición (stock mínimo, punto de pedido, stock ideal) y su precio, que se
calcula a partir del costo y el margen.

**Por qué esta prioridad**: Los parámetros de reposición de cada artículo son insumo de P1 y el
catálogo es referencia de P2, pero puede validarse de forma aislada como ABM.

**Escenarios de Aceptación**:

1. **Dado** un artículo nuevo, **Cuando** se agrega/modifica/elimina, **Entonces** el cambio queda persistido y recuperable (o deja de existir) por su Código.
2. **Dado** un precio de costo y un margen, **Cuando** se graba el artículo, **Entonces** el precio de venta = Precio de Costo × (1 + Margen / 100).
3. **Dado** un Código repetido, un valor negativo en costo/margen/stocks, un parámetro de reposición no entero, o el incumplimiento de Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal, **Cuando** se intenta grabar, **Entonces** el sistema rechaza la operación y no graba el registro.
4. **Dado** un artículo con al menos un movimiento asociado, **Cuando** se intenta darlo de baja, **Entonces** el sistema muestra un error y el artículo sigue existiendo con su histórico intacto.
5. **Dado** un artículo cuyos parámetros de reposición se modifican, **Cuando** se vuelve a ejecutar Generar Pedido, **Entonces** el resultado refleja los parámetros vigentes al momento de la ejecución (la consulta no conserva resultados previos).

---

### Historia de Usuario 4 - Iniciar sesión y proteger el acceso (Prioridad: P4)

Todo usuario debe autenticarse para usar el sistema. Solo la pantalla de inicio de sesión es
pública; cualquier otra funcionalidad exige una sesión autenticada válida.

**Por qué esta prioridad**: Es transversal a todas las funcionalidades, pero el valor de negocio
central (P1–P3) puede demostrarse antes de endurecer el acceso. Aun así es requisito para uso real.

**Escenarios de Aceptación**:

1. **Dado** un usuario inexistente o con contraseña incorrecta, **Cuando** intenta iniciar sesión, **Entonces** el sistema muestra "Usuario o contraseña incorrectos" y no autoriza el ingreso.
2. **Dado** un usuario existente con contraseña correcta, **Cuando** inicia sesión, **Entonces** el sistema autoriza el ingreso.
3. **Dado** una solicitud sin sesión autenticada válida, **Cuando** se invoca una funcionalidad protegida, **Entonces** el sistema responde no autorizado (401) y deniega el acceso.

---

### Historia de Usuario 5 - Administrar usuarios y perfiles de seguridad (Prioridad: P5)

El administrador da de alta, modifica y da de baja perfiles de seguridad y usuarios. Las
contraseñas se almacenan siempre de forma no reversible. Solo el perfil administrador puede
acceder a la carga de usuarios.

**Por qué esta prioridad**: Necesario para operación multiusuario y para que exista el control de
acceso de P4, pero no aporta valor de negocio directo por sí mismo.

**Escenarios de Aceptación**:

1. **Dado** un perfil sin usuarios asignados o un usuario, **Cuando** se da de alta/modifica/baja, **Entonces** el cambio queda persistido y recuperable (o deja de existir) por su identificador.
2. **Dado** el alta de un usuario, **Cuando** se graba, **Entonces** la contraseña se almacena como hash con salt aleatorio propio (dos usuarios con la misma contraseña tienen hashes distintos) y nunca en texto plano ni en formato reversible.
3. **Dado** una contraseña que incumple la política de RF-009 —menos de 8 caracteres, o sin ninguna letra, o sin ningún dígito—, **Cuando** se intenta grabar, **Entonces** el sistema muestra un error y no graba el registro; y **Dado** una contraseña de 8 o más caracteres que mezcla letras, dígitos y símbolos, **Entonces** se acepta.
4. **Dado** un usuario cuyo perfil no es administrador, **Cuando** intenta acceder a la carga de usuarios, **Entonces** el sistema deniega el acceso.
5. **Dado** un perfil con al menos un usuario asignado, **Cuando** se intenta darlo de baja, **Entonces** el sistema muestra un error y el perfil sigue existiendo.
6. **Dado** el perfil administrador, **Cuando** se modifica su Descripción a cualquier otro texto, **Entonces** sus usuarios conservan el acceso a la carga de usuarios; y **Cuando** se cambia la Descripción de otro perfil a "administrador", **Entonces** sus usuarios siguen recibiendo acceso denegado.
7. **Dado** el único usuario con perfil administrador, **Cuando** se intenta darlo de baja o cambiarle el perfil, **Entonces** el sistema muestra un error y el usuario conserva su perfil administrador.

---

### Casos Límite

- **Catálogo grande**: con más de 10.000 artículos, las consultas recortan el resultado a las primeras 10.000 filas según el orden por Código, informan que hubo recorte y ofrecen un filtro opcional por descripción para acotar el volumen.
- **Stock ya suficiente**: un artículo cuyo Stock Actual ya alcanza o supera el nivel elegido arroja Cantidad a Pedir 0 y se lista igual cuando "solo bajo mínimo" = No; nunca arroja un valor negativo.
- **Artículo sin movimientos**: su Stock Actual es 0 y participa de ambas consultas como cualquier otro; si su Stock Mínimo es mayor que 0, queda por debajo del mínimo.
- **Stock Mínimo igual a 0**: el artículo nunca cumple Stock Actual < Stock Mínimo, por lo que queda siempre fuera del resultado cuando "solo bajo mínimo" = Sí. Es comportamiento esperado, no un defecto.
- **Parámetros de reposición iguales**: si Stock Mínimo = Punto de Pedido = Stock Ideal (permitido por RF-019), las tres modalidades de pedido arrojan el mismo resultado.
- **Rango invertido**: si el Código inicial es alfabéticamente mayor que el final, la consulta devuelve un resultado vacío con mensaje informativo, no un error.
- **Resultado vacío**: cualquier combinación de rango y filtro que no arroje filas muestra una grilla vacía con mensaje informativo y permite exportar un Excel con solo los encabezados.
- **Venta sin stock**: una venta que dejaría el stock de algún artículo por debajo de 0 se rechaza por completo (no se graba parcialmente).
- **Ventas concurrentes del mismo artículo**: si dos usuarios graban a la vez ventas que juntas superan el stock disponible, solo una se graba; la otra se rechaza con el error de stock insuficiente y el saldo nunca queda negativo.
- **Baja/modificación de movimientos**: modificar o eliminar un movimiento recalcula el Stock Actual derivado; la validación de stock no negativo aplica a toda operación, incluida la baja o modificación de una compra ya consumida por ventas posteriores, que se rechaza en lugar de dejar el stock en negativo.
- **Fallo parcial en movimiento multilínea**: si cualquier línea falla su validación, no se aplica ninguna; el movimiento es todo-o-nada.
- **Cantidad no entera o ≤ 0**: cualquier línea con cantidad que no sea entero positivo invalida todo el movimiento. Los dos casos se rechazan en capas distintas y con códigos distintos: el **no entero** en el borde de la solicitud con 400 (RF-018a), el **entero ≤ 0** como regla de negocio (RF-023).
- **Cantidad o precio fuera de rango**: una línea cuya Cantidad supere 1.000.000 de unidades, cuyo Precio Unitario sea negativo o supere 9.999.999,99, o cuyo Precio Total supere 999.999.999.999,99, invalida todo el movimiento.
- **Código duplicado**: no se permite dar de alta ni modificar un artículo hacia un Código ya usado.
- **Baja de entidad referenciada**: no se permite eliminar un artículo con movimientos asociados ni un perfil con usuarios asignados; la operación se rechaza con un error y el registro permanece intacto.
- **Renombre del perfil administrador**: cambiar la Descripción del perfil administrador es una operación válida y no altera los privilegios de sus usuarios; tampoco los otorga a un perfil renombrado a "administrador". El privilegio sigue a la marca interna, no al texto.
- **Sistema sin administrador**: no existe secuencia de operaciones del ABM de seguridad que deje al sistema sin un usuario administrador. Se rechazan la baja del perfil administrador, la baja del último usuario administrador y el cambio de perfil de ese último usuario.
- **Error de ejecución**: cualquier error en tiempo de ejecución queda registrado en la bitácora de errores sin exponer detalles internos al usuario.

## Requisitos *(obligatorio)*

### Convención de identificadores

Los requisitos se numeran **RF-0XX** y referencian entre paréntesis el requisito de origen del PRD.
Un sufijo alfabético (por ejemplo **RF-024a**) identifica un requisito derivado que refina o
completa al RF base con el mismo número, incorporado a partir de una clarificación o de una
auditoría de calidad. El sufijo preserva la trazabilidad hacia el PRD del requisito padre.

Los requisitos se agrupan por **tema**, no por número: dentro de la lista, RF-029 aparece junto a los
movimientos que lo satisfacen y RF-028 al final con el registro de errores. El identificador es una
etiqueta estable de trazabilidad, **no una posición**; buscar un RF por su número no debe hacerse por
orden de aparición.

La marca *pendiente de implementación* señala un requisito acordado y especificado que todavía no se
construyó. No es una nota de estado transitoria del documento: mientras esté, ese requisito no tiene
tarea ni test asociado, y el sistema no lo cumple.

### Requisitos Funcionales

**Perfiles de seguridad**
- **RF-001** (RF-01): El sistema DEBE permitir dar de alta un perfil de seguridad con Identificador (autonumérico) y Descripción.
- **RF-002** (RF-02): El sistema DEBE permitir dar de baja un perfil de seguridad existente.
- **RF-002a** (RF-02): El sistema DEBE rechazar la baja de un perfil de seguridad que tenga usuarios asignados, mostrando un error y sin eliminar el registro (baja restringida; no hay baja lógica ni eliminación en cascada).
- **RF-002b** (RF-02): El sistema DEBE rechazar la baja del perfil administrador, aunque no tenga usuarios asignados y cualquiera sea su Descripción vigente, para que nunca deje de existir el perfil que habilita RF-004 a RF-006.
- **RF-003** (RF-03): El sistema DEBE permitir modificar la Descripción de un perfil existente.
- **RF-003a** (RF-03/RF-10): El sistema DEBE identificar al perfil administrador por una **marca interna inmutable**, independiente de su Descripción. La Descripción es un rótulo editable por RF-003 y NO DEBE ser base de ninguna decisión de autorización: renombrar un perfil no otorga ni quita privilegios, y renombrar el perfil administrador no deja al sistema sin administrador. La marca se establece exclusivamente en la siembra inicial y no es editable desde el ABM de perfiles, por lo que existe siempre exactamente un perfil administrador.

**Usuarios**
- **RF-004** (RF-04): El sistema DEBE permitir dar de alta un usuario con identificador, nombre de usuario, nombre completo y credenciales almacenadas de forma no reversible.
- **RF-005** (RF-05): El sistema DEBE permitir dar de baja un usuario existente.
- **RF-005a** (RF-05/RF-06): El sistema DEBE rechazar la baja de un usuario, y la modificación que le cambie el perfil, cuando sea el **último usuario con perfil administrador**, mostrando un error y sin grabar. El sistema nunca puede quedar sin al menos un usuario capaz de operar RF-004 a RF-006.
- **RF-006** (RF-06): El sistema DEBE permitir modificar los datos de un usuario existente.

**Seguridad de credenciales**
- **RF-007** (RF-07): El sistema DEBE almacenar la contraseña de cada usuario de forma no recuperable ni desencriptable en texto plano.
- **RF-008** (RF-08): El sistema DEBE generar la representación protegida de la contraseña con un valor aleatorio (salt) propio de cada usuario, de modo que dos usuarios con la misma contraseña tengan representaciones distintas.
- **RF-009** (RF-09): El sistema DEBE rechazar el alta o modificación de un usuario cuya contraseña no cumpla la política mínima, mostrando un error y sin grabar. La política es: **longitud mínima de 8 caracteres, con al menos una letra y al menos un dígito**. Los caracteres no alfanuméricos están **permitidos** y cuentan para la longitud; lo que se exige es la presencia de ambas clases, no la ausencia de las demás. No hay longitud máxima ni exigencia de mayúsculas o símbolos.

**Acceso**
- **RF-010** (RF-10): El sistema DEBE restringir la carga de usuarios (RF-004 a RF-006) exclusivamente al perfil administrador, respondiendo **prohibido (403)** a un usuario autenticado cuyo perfil no sea administrador. La condición de administrador se evalúa contra la marca inmutable de RF-003a, nunca contra la Descripción del perfil. Se distingue del no autorizado (401) de RF-012, que corresponde a la ausencia de sesión válida.
- **RF-010a** (RF-10): El sistema DEBE extender la restricción de RF-010 al ABM de perfiles de seguridad (RF-001 a RF-003), respondiendo **prohibido (403)** al usuario autenticado que no sea administrador. Fundamento: el perfil determina quién accede a la carga de usuarios, de modo que dejar el ABM de perfiles abierto permitiría a cualquier usuario alterar indirectamente el control de acceso de RF-010. Ambos ABM de seguridad quedan restringidos; el resto de las funcionalidades sigue disponible para todo usuario autenticado.
- **RF-011** (RF-11): El sistema DEBE ofrecer una pantalla de inicio de sesión que valide usuario y contraseña contra la representación protegida (usando el salt del usuario), mostrando "Usuario o contraseña incorrectos" ante credenciales inválidas.
- **RF-012** (RF-12): El sistema DEBE exigir una sesión autenticada válida para toda funcionalidad salvo el inicio de sesión, y rechazar (no autorizado) toda solicitud a una funcionalidad protegida sin sesión válida.

**Artículos**
- **RF-013** (RF-13): El sistema DEBE permitir dar de alta un artículo con Código, Descripción, Precio de Costo, Margen (%), Precio de Venta (calculado), Stock Mínimo, Punto de Pedido y Stock Ideal.
- **RF-013a** (RF-13): El sistema DEBE tratar el Código como texto y los tres parámetros de reposición (Stock Mínimo, Punto de Pedido, Stock Ideal) como números **enteros** no negativos, en coherencia con RF-023, que restringe las cantidades de movimiento a enteros. En consecuencia, el Stock Actual y la Cantidad a Pedir son siempre enteros y no requieren regla de redondeo.
- **RF-014** (RF-14): El sistema DEBE permitir dar de baja un artículo existente.
- **RF-014a** (RF-14): El sistema DEBE rechazar la baja de un artículo que tenga movimientos asociados, mostrando un error y sin eliminar el registro, de modo que el histórico de movimientos y el Stock Actual derivado se preserven íntegros (baja restringida; no hay baja lógica ni eliminación en cascada).
- **RF-015** (RF-15): El sistema DEBE permitir modificar los datos de un artículo existente.
- **RF-016** (RF-16): El sistema DEBE calcular el Precio de Venta como Precio de Costo × (1 + Margen / 100).
- **RF-016a** (RF-16) — *pendiente de implementación*: El sistema DEBE mostrar el Precio de Venta recalculado **de forma interactiva en la pantalla de artículos**, actualizándolo a medida que el usuario edita el Precio de Costo o el Margen, sin necesidad de grabar ni recargar. El valor mostrado es informativo y NO editable: la fuente de verdad sigue siendo el cálculo de RF-016 en el servidor, de modo que un cliente que no ejecute el recálculo no puede alterar el precio grabado.
- **RF-017** (RF-17): El sistema DEBE rechazar el alta o modificación de un artículo con Código duplicado (el Código es único).
- **RF-017a** (RF-17): El sistema DEBE evaluar la unicidad del Código con la misma regla de comparación que su ordenamiento (RF-025a): **insensible a mayúsculas y sensible a acentos**. En consecuencia, `A-001` y `a-001` son el mismo Código y el segundo se rechaza como duplicado, mientras que dos códigos que difieren en un acento son distintos.
- **RF-018** (RF-18): El sistema DEBE rechazar el alta o modificación si Precio de Costo, Margen, Stock Mínimo, Punto de Pedido o Stock Ideal es negativo, o si alguno de los tres parámetros de reposición incumple el tipo entero que fija RF-013a.
- **RF-018a** (RF-18/RF-23): El sistema DEBE rechazar todo valor **no entero** enviado a un campo entero —los tres parámetros de reposición de RF-018 y la Cantidad de línea de RF-023— en el **borde de la solicitud**, al deserializar el pedido, respondiendo un error de validación (400) que identifique el campo ofensor, sin llegar a las reglas de negocio ni grabar. El rechazo del no entero es, por lo tanto, un requisito del contrato de la API y no una regla que los validadores de dominio puedan observar: éstos reciben valores ya tipados como enteros. Un valor entero fuera de rango sí es un rechazo de negocio y sigue las reglas de RF-018, RF-019 y RF-023a.
- **RF-019** (RF-19): El sistema DEBE rechazar el alta o modificación que no cumpla Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal.

**Movimientos**
- **RF-020** (RF-20): El sistema DEBE permitir dar de alta un Movimiento (venta o compra) con encabezado (Tipo, Número, Fecha) y detalle (Código, Cantidad, Precio Unitario, Precio Total).
- **RF-020a** (RF-20): El sistema DEBE generar automáticamente el Número del Movimiento a partir de una secuencia única global compartida por compras y ventas: el Número identifica al Movimiento por sí solo (no se repite entre tipos), no es editable por el usuario y no se reutiliza tras una baja.
- **RF-020b** (RF-20): El sistema DEBE admitir para el Tipo de Movimiento exclusivamente los valores **Compra** y **Venta**; una compra suma al Stock Actual y una venta resta.
- **RF-020c** (RF-20): El sistema DEBE calcular el Precio Total de cada línea de detalle como Cantidad × Precio Unitario; no es un valor cargado por el usuario.
- **RF-020d** (RF-20): El sistema DEBE rechazar un Movimiento cuya Fecha sea posterior a la fecha actual. El Stock Actual considera todos los movimientos registrados, sin corte ni proyección por fecha.
- **RF-020e** (RF-20) — *pendiente de implementación*: El sistema DEBE identificar el artículo de cada línea de detalle por su **Código** en toda la interfaz de usuario —carga, edición y visualización—, y NO DEBE exigir al usuario el identificador interno del artículo. Alinea la interfaz con la definición de la entidad "Detalle de movimiento", donde el Código es la identidad de negocio y el identificador es sólo la referencia física.
- **RF-020f** (RF-20) — *pendiente de implementación*: El sistema DEBE mostrar en la pantalla de carga de un Movimiento nuevo el Número correlativo que le correspondería, en modo **sólo lectura**. Es una sugerencia informativa y no altera RF-020a: el Número definitivo lo asigna la secuencia al grabar, de modo que dos cargas simultáneas no puedan quedarse con el mismo valor por haberlo visto en pantalla.
- **RF-021** (RF-21): El sistema DEBE permitir dar de baja un Movimiento existente (encabezado y detalle).
- **RF-022** (RF-22): El sistema DEBE permitir modificar un Movimiento existente (encabezado y detalle).
- **RF-023** (RF-23): El sistema DEBE rechazar el alta o modificación de un Movimiento con alguna línea cuya Cantidad no sea un número entero mayor que 0.
- **RF-023a** (RF-23): El sistema DEBE rechazar el alta o modificación de un Movimiento con alguna línea que exceda alguno de estos límites, mostrando un error y sin grabar: Cantidad mayor a 1.000.000 de unidades; Precio Unitario mayor a 9.999.999,99; Precio Total (Cantidad × Precio Unitario) mayor a 999.999.999.999,99.
- **RF-023c** (RF-20/RF-23): El sistema DEBE rechazar el alta o modificación de un Movimiento con alguna línea cuyo Precio Unitario sea **negativo**, mostrando un error y sin grabar. Fija el extremo inferior que RF-023a dejaba abierto: el precio de una operación real nunca es menor que cero, aunque sí puede ser cero (por ejemplo, una bonificación). La regla es independiente de RF-023b: acota el signo, no vincula el precio al catálogo.
- **RF-023b** (RF-20): El sistema NO DEBE validar el Precio Unitario de una línea contra el Precio de Costo ni el Precio de Venta del artículo: el precio se informa por movimiento y refleja la operación real, sin vínculo con el catálogo.
- **RF-024** (RF-24): El sistema DEBE rechazar el alta o modificación de un Movimiento de venta que dejaría el Stock Actual de algún artículo por debajo de 0, mostrando un error y sin grabar. *(Refinado por RF-024a, que generaliza el invariante a toda operación; se conserva por trazabilidad al RF-24 del PRD y no requiere implementación ni test propios además de los de RF-024a.)*
- **RF-024a** (RF-21/RF-22/RF-24): El sistema DEBE mantener el invariante Stock Actual ≥ 0 en TODA operación sobre Movimientos, incluidas la baja y la modificación de una compra: si el resultado dejaría el Stock Actual de algún artículo por debajo de 0, la operación se rechaza por completo mostrando un error y sin grabar ningún cambio.
- **RF-024b** (RF-21/RF-22/RF-24): El sistema DEBE evaluar la validación de stock y aplicar el cambio como una única operación atómica en TODA operación sobre Movimientos (alta, baja y modificación), de modo que dos operaciones concurrentes sobre el mismo artículo no puedan validar ambas contra el mismo Stock Actual. Ante concurrencia, una operación se aplica y la otra se rechaza con el error de stock insuficiente evaluado contra el Stock Actual ya actualizado; el usuario nunca recibe un error de conflicto de concurrencia que lo obligue a reintentar.
- **RF-024c** (RF-20/RF-21/RF-22): El sistema DEBE tratar cada Movimiento como una unidad todo-o-nada: si cualquier línea de detalle falla una validación durante un alta, baja o modificación, ninguna línea queda aplicada y el Stock Actual de todos los artículos involucrados permanece inalterado.

**Stock inicial**
- **RF-029** (RF-20/RF-25): El sistema DEBE permitir cargar el stock preexistente al poner en marcha el sistema mediante Movimientos de tipo Compra con la fecha de apertura correspondiente. No existe un campo de stock inicial editable: el Stock Actual es siempre y exclusivamente el saldo de los movimientos registrados.

**Consultas**
- **RF-025** (RF-25): El sistema DEBE ofrecer la consulta "Consulta de Stock Actual", con parámetro rango de artículos (inicial y final), columnas Código, Descripción y Cantidad —donde Cantidad es el Stock Actual, saldo de movimientos: ventas restan, compras suman—, exportable a Excel.
- **RF-025a** (RF-25): El sistema DEBE interpretar el rango de artículos como un rango inclusivo sobre el Código, comparado y ordenado alfabéticamente como texto. Ambos extremos son opcionales: si el inicial está vacío no se aplica límite inferior, si el final está vacío no se aplica límite superior, y si ambos están vacíos se consideran todos los artículos; en todos los casos rige el tope de RF-027. Si el Código inicial es alfabéticamente mayor que el final, el resultado es vacío y no un error. La comparación y el orden del Código son **insensibles a mayúsculas y sensibles a acentos**, según la regla de ordenamiento alfabético del español; no es un orden ordinal por punto de código. Esta distinción es observable: determina qué filas entran y en qué posición quedan frente al tope de RF-027.
- **RF-026** (RF-26): El sistema DEBE ofrecer la consulta "Generar Pedido" con parámetros de reposición "solo bajo mínimo" (booleano) y "Modo de Pedido" (Hasta Stock Mínimo / Hasta Punto Pedido / Hasta Stock Ideal), columnas Código, Descripción y Cantidad a Pedir, exportable a Excel, calculada según:
  - "solo bajo mínimo" = No: se listan TODOS los artículos del catálogo, con Cantidad a Pedir = MAX(0, Nivel − Stock Actual); las filas con Cantidad a Pedir 0 se muestran igual, no se omiten.
  - "solo bajo mínimo" = Sí: se listan solo los artículos con Stock Actual < Stock Mínimo, con Cantidad a Pedir = MAX(0, Nivel − Stock Actual). En esta rama el MAX(0, …) es redundante pero se aplica por uniformidad: dado que RF-019 garantiza Stock Mínimo ≤ Nivel y el filtro garantiza Stock Actual < Stock Mínimo, la diferencia es siempre mayor que 0.
  - Donde Nivel es Stock Mínimo, Punto de Pedido o Stock Ideal según el Modo de Pedido.
- **RF-026a** (RF-26): El sistema NO DEBE ofrecer parámetro de rango de artículos en "Generar Pedido"; sus únicos parámetros de reposición son los dos de RF-026, más el filtro opcional de acotación de RF-027a.
- **RF-026b** (RF-26): El sistema DEBE exigir **ambos** parámetros de reposición de RF-026 en cada ejecución de "Generar Pedido", sin valores por defecto implícitos: una solicitud que omita "solo bajo mínimo" o "Modo de Pedido" se rechaza con un error de validación. Fundamento: los dos parámetros determinan por completo el resultado (RF-026) y un valor por defecto silencioso produciría una lista de pedido que el usuario no pidió y no puede distinguir de la que sí. El filtro por descripción de RF-027a, en cambio, es opcional por definición.
- **RF-027** (RF-25/RF-26): El sistema DEBE acotar el volumen de ambas consultas a un máximo de 10.000 filas y ofrecer un filtro opcional por descripción.
- **RF-027a** (RF-25/RF-26): El sistema DEBE aplicar el filtro opcional por descripción como coincidencia parcial (el texto buscado aparece en cualquier posición de la Descripción), insensible a mayúsculas/minúsculas y a acentos. Un filtro vacío no acota el resultado.
- **RF-027b** (RF-25/RF-26): El sistema DEBE ordenar el resultado de ambas consultas por Código ascendente, **con la regla de comparación que fija RF-025a** (insensible a mayúsculas, sensible a acentos; RF-025a es la fuente autoritativa y esta regla no la redefine), aplicar el rango y el filtro primero, y recién sobre el conjunto ya filtrado y ordenado aplicar el tope de 10.000 filas, de modo que el resultado sea determinista y reproducible.
- **RF-027c** (RF-25/RF-26): El sistema DEBE informar explícitamente al usuario cuando el resultado fue recortado por alcanzar el tope de 10.000 filas, indicando que debe acotar la consulta con el filtro por descripción.
- **RF-030** (RF-25/RF-26): El sistema DEBE incluir en el resultado de ambas consultas a los artículos del catálogo sin movimientos registrados, con Stock Actual 0, aplicándoles las mismas reglas de pedido que al resto.
- **RF-031** (RF-25/RF-26): El sistema DEBE producir una exportación a Excel que replique exactamente las filas, el orden y el recorte mostrados en pantalla al momento de exportar. Un resultado vacío exporta un archivo con solo los encabezados de columna.
- **RF-032** (RF-25/RF-26): El sistema DEBE mostrar una grilla vacía con un mensaje informativo, sin error, cuando la combinación de parámetros no arroja ninguna fila. El texto es exactamente **"No hay artículos que cumplan los criterios de la consulta."**, se muestra en el lugar de la grilla y no se acompaña de ningún indicador de error. Fijarlo acá lo hace verificable: el test asierta esa cadena, no la mera ausencia de filas.
- **RF-032a** (RF-25/RF-26): El sistema DEBE mostrar, cuando el resultado se recortó por RF-027c, el texto exacto **"Se muestran las primeras 10.000 filas. Acote la búsqueda con el filtro por descripción."**, visible junto a la grilla y distinguible del mensaje de resultado vacío de RF-032. Ambos mensajes son informativos, no errores.
- **RF-033** (RF-26): El sistema DEBE calcular "Generar Pedido" siempre contra el estado vigente del catálogo y de los movimientos al momento de ejecutar la consulta. El resultado no se persiste ni se versiona: modificar los parámetros de reposición de un artículo se refleja en la siguiente ejecución.

**Búsqueda de artículos por pantalla** *(todo el grupo, pendiente de implementación)*
- **RF-034** (RF-20/RF-25/RF-26): El sistema DEBE ofrecer, junto a cada campo de texto que pida un Código de artículo —carga de movimientos, Consulta de Stock Actual y Generar Pedido—, un botón pequeño identificado sólo con un ícono de lupa que abra una ventana de búsqueda de artículos.
- **RF-034a** (RF-20/RF-25/RF-26): La ventana de búsqueda DEBE pedir un campo Descripción y un botón "Buscar" que llene una grilla con desplazamiento vertical de dos columnas —Código y Descripción— con los artículos cuya Descripción contenga el texto ingresado (coincidencia parcial, con la misma regla insensible a mayúsculas y acentos de RF-027a). Una Descripción vacía lista todos los artículos. La altura de la ventana NO DEBE superar los 600 píxeles.
- **RF-034b** (RF-20/RF-25/RF-26): Al elegir y aceptar un registro de la grilla, el sistema DEBE trasladar su Código al campo de origen y disparar **exactamente las mismas operaciones asociadas** que si el usuario lo hubiera tecleado a mano, sin ninguna ruta de código alternativa. Además, cada pantalla que use el buscador DEBE mostrar la Descripción del artículo correspondiente al Código vigente, y mantenerla sincronizada tanto cuando el Código se elige desde la búsqueda como cuando el usuario lo edita manualmente.
- **RF-034c** (RF-20/RF-25/RF-26): El buscador DEBE implementarse como un **componente encapsulado e independiente**, de modo que una pantalla que lo necesite sólo deba hacer lo mínimo para invocarlo. Fundamento: son tres pantallas las que lo consumen y RF-034b exige que todas se comporten igual; tres copias del mismo diálogo divergirían y la equivalencia con la carga manual dejaría de sostenerse.

**Registro de errores**
- **RF-028** (RF-27): El sistema DEBE registrar todo error de ejecución en una bitácora con Identificador (autonumérico), Fecha/Hora, Nombre de la máquina, Mensaje y Detalle de la excepción.

### Entidades Clave *(incluir si la funcionalidad involucra datos)*

- **Perfil de seguridad**: rol de acceso; identificador, descripción (ej.: administrador, administrativo, vendedor) y una **marca de administrador** interna, establecida en la siembra inicial, no editable y no expuesta al ABM, que es la única base de las decisiones de autorización (RF-003a). La descripción es un rótulo editable. No puede eliminarse mientras tenga usuarios asignados, y el perfil marcado como administrador no puede eliminarse nunca.
- **Usuario**: persona que opera el sistema; identificador, nombre de usuario, nombre completo, credenciales protegidas (representación no reversible + salt propio) y perfil asociado. El último usuario cuyo perfil tenga la marca de administrador no puede darse de baja ni cambiar de perfil.
- **Artículo**: ítem del catálogo; Código único (texto; base del orden alfabético y del rango de las consultas), descripción, precio de costo, margen, precio de venta calculado, y los tres parámetros de reposición enteros no negativos (stock mínimo, punto de pedido, stock ideal). Su **Stock Actual** es derivado (saldo de movimientos), no un campo propio, siempre entero y nunca negativo. No puede eliminarse mientras tenga movimientos asociados.
- **Movimiento (encabezado)**: compra o venta; tipo (conjunto cerrado: Compra | Venta), número y fecha (no futura). El Número es autogenerado por el sistema desde una única secuencia global (compras y ventas comparten numeración), es único en todo el sistema, no editable y no reutilizable.
- **Detalle de movimiento**: líneas del movimiento; artículo referenciado —el **Código** es su identidad de negocio, la que ve y carga el usuario; la referencia física interna es el identificador del artículo—, cantidad (entero > 0), precio unitario informado por operación (no negativo) y precio total calculado como cantidad × precio unitario.
- **Registro de error**: bitácora de fallos; identificador, fecha/hora, nombre de máquina, mensaje y detalle de la excepción.

**Terminología canónica**: se usa **Stock Actual** en todo el documento para designar el saldo de
movimientos de un artículo. "Cantidad" se emplea únicamente como rótulo de la columna de la
"Consulta de Stock Actual", que expone ese mismo valor, y como campo del detalle de movimiento.

## Criterios de Éxito *(obligatorio)*

### Resultados Medibles

- **CE-001**: El usuario obtiene la lista de artículos a pedir eligiendo los dos parámetros de reposición ("solo bajo mínimo" y "Modo de Pedido") y ejecutando una única consulta, sin ningún cálculo manual. El filtro por descripción de RF-027a es un acotador opcional del volumen, no un parámetro de reposición, y su omisión no altera el cálculo.
- **CE-002**: Las consultas "Consulta de Stock Actual" y "Generar Pedido" responden en menos de 3 segundos (p95) sobre un volumen de referencia de 10.000 artículos y 100.000 líneas de detalle de movimiento.
- **CE-003**: Las 6 combinaciones de parámetros de "Generar Pedido" producen exactamente las cantidades del Conjunto de Datos de Referencia definido abajo, y esa cantidad nunca es negativa.
- **CE-004**: Con hasta 5 usuarios concurrentes, las consultas siguen cumpliendo el presupuesto de latencia de CE-002 y ninguna operación concurrente puede violar el invariante de stock no negativo. Se verifica lanzando 5 ventas simultáneas del mismo artículo cuya suma excede el stock disponible y comprobando que la cantidad total efectivamente grabada nunca supera el stock disponible previo y que el Stock Actual resultante es ≥ 0.
- **CE-005**: El Stock Actual de un artículo nunca queda por debajo de 0: ninguna alta, baja ni modificación de movimiento (venta o compra) puede grabarse si el resultado violara ese invariante.
- **CE-006**: Ninguna contraseña puede recuperarse en texto plano; dos usuarios con la misma contraseña presentan representaciones protegidas distintas en el 100% de los casos.
- **CE-007**: Ninguna funcionalidad distinta del inicio de sesión es accesible sin una sesión autenticada válida.
- **CE-007a**: Ninguna operación disponible en el ABM de seguridad puede dejar al sistema sin un usuario administrador ni transferir el privilegio de administrador a otro perfil. Se verifica intentando renombrar perfiles en ambos sentidos, eliminar el perfil administrador y eliminar o reasignar al último usuario administrador.
- **CE-008**: El 100% de los errores de ejecución quedan registrados en la bitácora de errores.

### Conjunto de Datos de Referencia (para CE-003)

Catálogo de prueba con el Stock Actual resultante de sus movimientos:

| Código | Stock Mínimo | Punto de Pedido | Stock Ideal | Stock Actual | Caso que cubre |
|--------|--------------|-----------------|-------------|--------------|----------------|
| A-001  | 10 | 20 | 50 | 5  | Por debajo del mínimo |
| A-002  | 10 | 20 | 50 | 15 | Sobre el mínimo, bajo el punto de pedido |
| A-003  | 10 | 20 | 50 | 60 | Por encima del stock ideal |
| A-004  | 0  | 0  | 0  | 0  | Parámetros en cero, sin movimientos |

Cantidad a Pedir esperada para cada una de las 6 combinaciones:

| solo bajo mínimo | Modo de Pedido | A-001 | A-002 | A-003 | A-004 |
|------------------|----------------|-------|-------|-------|-------|
| No  | Hasta Stock Mínimo | 5  | 0  | 0 | 0 |
| No  | Hasta Punto Pedido | 15 | 5  | 0 | 0 |
| No  | Hasta Stock Ideal  | 45 | 35 | 0 | 0 |
| Sí  | Hasta Stock Mínimo | 5  | — | — | — |
| Sí  | Hasta Punto Pedido | 15 | — | — | — |
| Sí  | Hasta Stock Ideal  | 45 | — | — | — |

"—" indica que el artículo no se incluye en el resultado por no cumplir Stock Actual < Stock Mínimo.
Nótese que A-004 queda excluido con "solo bajo mínimo" = Sí porque 0 < 0 es falso.

## Supuestos

- El proyecto es un curso/entrega con stack y restricciones ya fijados por el PRD, `AGENTS.md` y la constitución (sitio web con separación Front-End/Back-End, autenticación por token, base de datos relacional, límite de 10.000 artículos y filtro por descripción); estos detalles de implementación se detallan en el plan, no en este spec.
- El "Stock Actual" de un artículo es siempre un valor derivado del saldo de sus movimientos (compras suman, ventas restan) y no se almacena como dato editable. **Riesgo abierto para el plan**: sostener CE-002 (3 segundos p95) sobre un saldo calculado con 100.000 líneas de movimiento exige una estrategia de cálculo eficiente; el plan debe justificar cómo se logra sin introducir un campo de stock persistido que contradiga este supuesto.
- El "Nivel" de reposición en "Generar Pedido" corresponde a Stock Mínimo, Punto de Pedido o Stock Ideal según el Modo de Pedido seleccionado.
- El Punto de Pedido es exclusivamente un nivel de reposición seleccionable en "Generar Pedido". No dispara alertas, avisos ni notificaciones propias: cualquier señalización automática al cruzarlo está fuera de alcance.
- Las reglas de stock y pedido se calculan íntegramente con datos propios del sistema y no dependen de ningún servicio externo, por lo que no se requieren requisitos de modo de fallo, reintento ni degradación frente a terceros.
- Fuera de alcance (según PRD): carga de proveedores, manejo de múltiples proveedores por artículo, generación de órdenes de compra y permisos por perfil para las pantallas de negocio. Todo usuario autenticado accede a artículos, movimientos y ambas consultas; la restricción por perfil alcanza **exclusivamente a los dos ABM de seguridad** —usuarios (RF-010) y perfiles (RF-010a)—, porque son los que gobiernan el propio control de acceso.
- Existe un usuario administrador inicial, sembrado junto con el perfil administrador y su marca interna, para poder operar el alta de usuarios y perfiles. RF-002b y RF-005a garantizan que ese punto de entrada no pueda perderse por operación del propio ABM.
