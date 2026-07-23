# PRD-001: Módulo de Stock — Genera pedidos de mercadería automáticamente.

## Contexto y Problema
En un pequeño comercio de barrio que compra y vende artículos (por ejemplo, un negocio de repuestos de artículos del hogar), resulta difícil saber si se tiene en existencia la cantidad necesaria de cada artículo. Sin un control ordenado de compras y ventas, el dueño no sabe qué reponer ni cuánto, y termina quedándose sin stock de lo que más rota o comprando de más lo que no se vende.

Personas:
- **Administrador**: dueño/encargado del comercio. Da de alta usuarios y perfiles, mantiene el catálogo de artículos y necesita saber qué pedir.
- **Administrativo / Vendedor**: registra las compras y ventas del día a día y consulta el stock y los pedidos a generar.

## Objetivos
Este módulo va a ser un SITIO WEB que, con solo registrar las ventas y las compras de mercadería, permita inferir automáticamente la lista de artículos que hace falta pedir según los siguientes criterios:

- Pedir todos los artículos hasta alcanzar el stock mínimo de cada uno.
- Pedir todos los artículos hasta alcanzar el punto de pedido de cada uno.
- Pedir todos los artículos hasta alcanzar el stock ideal de cada uno.
- Pedir solo los artículos que estén por debajo del mínimo, hasta el stock mínimo de cada uno.
- Pedir solo los artículos que estén por debajo del mínimo, hasta el punto de pedido de cada uno.
- Pedir solo los artículos que estén por debajo del mínimo, hasta el stock ideal de cada uno.

## Requerimientos Funcionales

### Perfiles de seguridad
- RF-01: El sistema debe permitir dar de alta un perfil de seguridad con los campos ID (autonumérico) y Descripción (ejemplo: administrador, administrativo, vendedor).
- RF-02: El sistema debe permitir dar de baja un perfil de seguridad existente.
- RF-03: El sistema debe permitir modificar la Descripción de un perfil de seguridad existente.

### Usuarios
- RF-04: El sistema debe permitir dar de alta un usuario con los siguientes campos: UsuarioId, Usuario, NombreCompleto, Hash, Salt.
- RF-05: El sistema debe permitir dar de baja un usuario existente.
- RF-06: El sistema debe permitir modificar los datos de un usuario existente.

### Seguridad de credenciales
- RF-07: La contraseña de cada usuario debe almacenarse como un hash generado a partir de dicha contraseña, de forma que no sea posible desencriptarla ni recuperarla en texto plano.
- RF-08: El hash de la contraseña de cada usuario debe generarse utilizando un salt aleatorio propio de ese usuario, de forma que dos usuarios con la misma contraseña tengan hashes distintos entre sí.
- RF-09: El sistema debe rechazar el alta o la modificación de un usuario cuya contraseña tenga menos de 8 caracteres alfanuméricos, mostrando un mensaje de error y sin grabar el registro.

### Acceso
- RF-10: La carga de usuarios (RF-04, RF-05, RF-06) solo debe estar accesible para usuarios del perfil administrador.
- RF-11: El sistema debe tener una pantalla de inicio de sesión, donde se pida usuario y contraseña, y dicha contraseña debe validarse contra el hash asociado al usuario, teniendo en cuenta el salt del mismo usuario.
- RF-12: El sistema debe exigir una sesión autenticada válida (JWT) para acceder a cualquier funcionalidad, con excepción de la pantalla de inicio de sesión (RF-11). Toda solicitud a un endpoint protegido de la API sin un token JWT válido debe ser rechazada.

### Artículos
- RF-13: El sistema debe permitir dar de alta un artículo con los siguientes campos: Código, Descripción, Precio de Costo, Margen (%), Precio de Venta (calculado automáticamente según RF-16), Stock Mínimo, Punto de Pedido, Stock Ideal.
- RF-14: El sistema debe permitir dar de baja un artículo existente.
- RF-15: El sistema debe permitir modificar los datos de un artículo existente.
- RF-16: El sistema debe calcular automáticamente el Precio de Venta de cada artículo a partir del Precio de Costo y el Margen (%), aplicando la siguiente fórmula:

  Precio de Venta = Precio de Costo × (1 + Margen / 100)

- RF-17: El sistema debe rechazar el alta o la modificación de un artículo cuyo Código coincida con el de otro artículo ya existente, de forma que el Código sea único.
- RF-18: El sistema debe rechazar el alta o la modificación de un artículo si alguno de los campos Precio de Costo, Margen, Stock Mínimo, Punto de Pedido o Stock Ideal es un valor negativo.
- RF-19: El sistema debe rechazar el alta o la modificación de un artículo que no cumpla la condición Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal.

### Movimientos
- RF-20: El sistema debe permitir dar de alta un Movimiento (venta o compra), informando los siguientes campos en el encabezado: Tipo de Movimiento, Número y Fecha; y los siguientes campos en el detalle: Código, Cantidad, Precio Unitario y Precio Total.
- RF-21: El sistema debe permitir dar de baja un Movimiento existente (encabezado y detalle).
- RF-22: El sistema debe permitir modificar un Movimiento existente (encabezado y detalle).
- RF-23: El sistema debe rechazar el alta o la modificación de un Movimiento en el que alguna línea de detalle tenga una Cantidad que no sea un número entero mayor que 0.
- RF-24: El sistema debe rechazar el alta o la modificación de un Movimiento de tipo venta que dejaría el Stock Actual de alguno de sus artículos por debajo de 0, mostrando un mensaje de error y sin grabar el movimiento.

### Consultas
- RF-25: El sistema debe tener una consulta por pantalla, exportable a Excel, llamada "Consulta de Stock Actual" que permita ver la cantidad en existencia actual de cada artículo. Los parámetros de esta consulta deben ser el rango de artículos (artículo inicial y artículo final) y las columnas de la grilla deben ser: Código, Descripción y Cantidad. La cantidad de cada artículo se calcula del saldo de cada uno, según los movimientos registrados (las ventas restan y las compras suman).
- RF-26: El sistema debe tener una consulta por pantalla, exportable a Excel, llamada "Generar Pedido" que permita ver la cantidad a pedir de cada artículo. Los parámetros de esta consulta deben ser "solo bajo mínimo" (boolean) y "Modo de Pedido" (lista desplegable con las opciones: Hasta Stock Mínimo, Hasta Punto Pedido, Hasta Stock Ideal); las columnas de la grilla deben ser: Código, Descripción y Cantidad a Pedir. El cálculo de la Cantidad a Pedir para cada una de las 6 combinaciones posibles de estos dos parámetros está especificado en AC-31 a AC-36.

### Registro de errores
- RF-27: Ante cualquier error de ejecución, el mensaje debe guardarse en una tabla de errores, con las siguientes columnas: ErrorId (autonumérico), ErrorDateTime, MachineName, Message, FullException.

## Requerimientos No Funcionales

- RNF-01: El Front-End debe ser un sitio Web ASP.NET MVC con .NET 8.
- RNF-02: El Back-End debe estar implementado completamente en una Web API REST con .NET 8, en un proyecto aparte, y con autenticación JWT (JSON Web Token), que es invocada desde el Front-End (ver RF-12).
- RNF-03: La base de datos debe ser SQL Server 2017.
- RNF-04: Las consultas "Consulta de Stock Actual" y "Generar Pedido" deben responder en menos de 3 segundos (p95), incluso con hasta 10000 artículos.
- RNF-05: El sistema debe soportar entre 1 y 5 usuarios concurrentes.
- RNF-06: La contraseña de cada usuario debe tener un mínimo de 8 caracteres alfanuméricos.

## Criterios de Aceptación

### Perfiles
- AC-01 (RF-01): Dado un registro nuevo de perfil, Cuando se agrega, Entonces queda persistido y puede recuperarse por su ID.
- AC-02 (RF-02): Dado un perfil existente, Cuando se elimina, Entonces deja de existir y no puede recuperarse por su ID.
- AC-03 (RF-03): Dado un perfil existente, Cuando se modifica su Descripción, Entonces el cambio queda persistido.

### Usuarios
- AC-04 (RF-04): Dado un registro nuevo de usuario, Cuando se agrega, Entonces queda persistido y puede recuperarse por su UsuarioId.
- AC-05 (RF-05): Dado un usuario existente, Cuando se elimina, Entonces deja de existir y no puede recuperarse por su UsuarioId.
- AC-06 (RF-06): Dado un usuario existente, Cuando se modifican sus datos, Entonces el cambio queda persistido.

### Seguridad de credenciales
- AC-07 (RF-07): Dado el alta de un usuario con una contraseña, Cuando se graba el registro, Entonces la contraseña se almacena como hash y no en texto plano ni en un formato reversible.
- AC-08 (RF-08): Dadas dos altas de usuario con la misma contraseña, Cuando se generan sus registros, Entonces los salts grabados para cada usuario son distintos entre sí.
- AC-09 (RF-09): Dado el alta o modificación de un usuario con una contraseña de menos de 8 caracteres alfanuméricos, Cuando se intenta grabar, Entonces el sistema rechaza la operación, muestra un mensaje de error y no graba el registro.
- AC-10 (RF-09): Dado el alta o modificación de un usuario con una contraseña de 8 o más caracteres alfanuméricos, Cuando se graba, Entonces la operación se acepta y el registro queda persistido.

### Acceso
- AC-11 (RF-10): Dado un usuario cuyo perfil no es administrador, Cuando intenta acceder a la carga de usuarios, Entonces el sistema deniega el acceso.
- AC-12 (RF-11): Dado un usuario que no existe, Cuando intenta iniciar sesión, Entonces el sistema muestra el mensaje "Usuario o contraseña incorrectos" y no autoriza el ingreso.
- AC-13 (RF-11): Dado un usuario existente con contraseña incorrecta, Cuando intenta iniciar sesión, Entonces el sistema muestra el mensaje "Usuario o contraseña incorrectos" y no autoriza el ingreso.
- AC-14 (RF-11): Dado un usuario existente con contraseña correcta, Cuando inicia sesión, Entonces el sistema autoriza el ingreso.
- AC-15 (RF-12): Dado un request sin un token JWT válido, Cuando se invoca un endpoint protegido de la API, Entonces el sistema responde con error 401 (No autorizado) y deniega el acceso.

### Artículos
- AC-16 (RF-13): Dado un registro nuevo de artículo, Cuando se agrega, Entonces queda persistido y puede recuperarse por su Código.
- AC-17 (RF-14): Dado un artículo existente, Cuando se elimina, Entonces deja de existir y no puede recuperarse por su Código.
- AC-18 (RF-15): Dado un artículo existente, Cuando se modifican sus datos, Entonces el cambio queda persistido.
- AC-19 (RF-16): Dado un Precio de Costo y un Margen (%) cargados, Cuando se graba el artículo, Entonces el Precio de Venta se calcula como Precio de Costo × (1 + Margen / 100).
- AC-20 (RF-17): Dado un artículo cuyo Código coincide con el de otro artículo existente, Cuando se intenta grabar, Entonces el sistema rechaza la operación y no graba el registro.
- AC-21 (RF-18): Dado un artículo con Precio de Costo, Margen, Stock Mínimo, Punto de Pedido o Stock Ideal negativo, Cuando se intenta grabar, Entonces el sistema rechaza la operación y no graba el registro.
- AC-22 (RF-19): Dado un artículo que no cumple Stock Mínimo ≤ Punto de Pedido ≤ Stock Ideal, Cuando se intenta grabar, Entonces el sistema rechaza la operación y no graba el registro.

### Movimientos
- AC-23 (RF-20): Dado un movimiento nuevo, Cuando se agrega, Entonces queda persistido en las tablas de encabezado y detalle.
- AC-24 (RF-21): Dado un movimiento existente, Cuando se elimina, Entonces deja de existir en las tablas de encabezado y detalle.
- AC-25 (RF-22): Dado un movimiento existente, Cuando se modifica, Entonces el cambio queda persistido en las tablas de encabezado y detalle.
- AC-26 (RF-23): Dado un movimiento con una línea de detalle cuya Cantidad es 0, negativa o no entera, Cuando se intenta grabar, Entonces el sistema rechaza la operación y no graba el movimiento.
- AC-27 (RF-24): Dado un movimiento de venta que dejaría el Stock Actual de alguno de sus artículos por debajo de 0, Cuando se intenta grabar, Entonces el sistema rechaza la operación, muestra un mensaje de error y no graba el movimiento.
- AC-28 (RF-24): Dado un movimiento de venta que deja el Stock Actual de todos sus artículos en 0 o más, Cuando se graba, Entonces la operación se acepta y el movimiento queda persistido.

### Consulta de Stock Actual
- AC-29 (RF-25): Dado un rango de artículos, Cuando se ejecuta la Consulta de Stock Actual, Entonces devuelve las columnas Código, Descripción y Cantidad, calculada por saldo de movimientos (las ventas restan y las compras suman).
- AC-30 (RF-25): Dado un resultado de la Consulta de Stock Actual, Cuando se presiona el botón "Exportar a Excel", Entonces se descarga un archivo Excel con el contenido de la grilla.

### Generar Pedido
- AC-31 (RF-26): Dado "solo bajo mínimo" = No y Modo de Pedido = "Hasta Stock Mínimo", Cuando se ejecuta Generar Pedido, Entonces para cada artículo la Cantidad a Pedir = MAX(0, Stock Mínimo − Stock Actual).
- AC-32 (RF-26): Dado "solo bajo mínimo" = No y Modo de Pedido = "Hasta Punto Pedido", Cuando se ejecuta Generar Pedido, Entonces para cada artículo la Cantidad a Pedir = MAX(0, Punto de Pedido − Stock Actual).
- AC-33 (RF-26): Dado "solo bajo mínimo" = No y Modo de Pedido = "Hasta Stock Ideal", Cuando se ejecuta Generar Pedido, Entonces para cada artículo la Cantidad a Pedir = MAX(0, Stock Ideal − Stock Actual).
- AC-34 (RF-26): Dado "solo bajo mínimo" = Sí y Modo de Pedido = "Hasta Stock Mínimo", Cuando se ejecuta Generar Pedido, Entonces solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la Cantidad a Pedir = Stock Mínimo − Stock Actual.
- AC-35 (RF-26): Dado "solo bajo mínimo" = Sí y Modo de Pedido = "Hasta Punto Pedido", Cuando se ejecuta Generar Pedido, Entonces solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la Cantidad a Pedir = Punto de Pedido − Stock Actual.
- AC-36 (RF-26): Dado "solo bajo mínimo" = Sí y Modo de Pedido = "Hasta Stock Ideal", Cuando se ejecuta Generar Pedido, Entonces solo se incluyen los artículos con Stock Actual < Stock Mínimo, y para cada uno la Cantidad a Pedir = Stock Ideal − Stock Actual.
- AC-37 (RF-26): Dado un resultado de la consulta Generar Pedido, Cuando se presiona el botón "Exportar a Excel", Entonces se descarga un archivo Excel con el contenido de la grilla.

### Registro de errores
- AC-38 (RF-27): Dado un error de ejecución en el sistema, Cuando este ocurre, Entonces sus datos quedan grabados en la tabla de errores.

### Rendimiento (verificación de RNF)
- AC-39 (RNF-04): Dado un catálogo de 10000 artículos, Cuando se ejecutan las consultas "Consulta de Stock Actual" y "Generar Pedido", Entonces cada una responde en menos de 3 segundos (p95).

## Fuera de Alcance
- Queda fuera de alcance la carga de proveedores (Alta, Baja y Modificación).
- Queda fuera de alcance el manejo de múltiples proveedores por artículo.
- Queda fuera de alcance la generación de órdenes de compra.
- Queda fuera de alcance definir permisos de acceso por perfil para las pantallas distintas de la carga de usuarios: todo usuario autenticado (RF-12) puede acceder a las demás funcionalidades; la única restricción por perfil es la de RF-10.

## Riesgos y Dependencias
- Riesgo: Que haya más de 10000 artículos. Mitigación: limitar las consultas con TOP 10000 y agregar filtro opcional por descripción con LIKE '%%'.
- Dependencia: ninguna.
