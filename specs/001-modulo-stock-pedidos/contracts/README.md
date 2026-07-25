# Contratos (Fase 1)

**Funcionalidad**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

`Stock.Api` es la única interfaz externa del sistema. `Stock.Web` es un consumidor más de esta API,
sin acceso directo a la base de datos: toda regla de negocio vive detrás de estos endpoints.

- [`openapi.yaml`](./openapi.yaml) — contrato REST completo.

## Convenciones transversales

**Autenticación** — Todos los endpoints exigen `Authorization: Bearer <jwt>` salvo
`POST /api/auth/login`, que es el único público. Sin token válido, la respuesta es `401`. (RF-012)

**Autorización** — Sólo `/api/usuarios` y `/api/perfiles` requieren el perfil administrador; el
resto es accesible a cualquier usuario autenticado, según el alcance cerrado del PRD. (RF-010)

**Errores** — Todas las respuestas de error usan `application/problem+json` (RFC 7807). El campo
`detail` lleva un mensaje apto para mostrar al usuario final; nunca expone detalle interno de una
excepción. (RF-028)

## Mapeo de códigos de estado

La distinción entre `400`, `409` y `422` es deliberada y hace testeable el comportamiento del spec:

| Código | Significado | Casos | Requisito |
|--------|-------------|-------|-----------|
| `400` | Entrada mal formada o que viola una regla de validación de campo | Cantidad no entera o ≤ 0, cantidad > 1.000.000, contraseña < 8 alfanuméricos, valores negativos, `StockMinimo ≤ PuntoPedido ≤ StockIdeal` incumplido, fecha futura | RF-009, RF-018, RF-019, RF-020d, RF-023, RF-023a |
| `401` | Sin sesión válida, o credenciales incorrectas en el login | Token ausente, inválido o expirado; usuario o contraseña incorrectos | RF-011, RF-012 |
| `403` | Autenticado pero sin el perfil requerido | Usuario no administrador contra `/api/usuarios` | RF-010 |
| `404` | El recurso no existe | Id inexistente en cualquier ABM | — |
| `409` | Conflicto con el estado actual de los datos | Código de artículo duplicado, baja de artículo con movimientos, baja de perfil con usuarios | RF-002a, RF-014a, RF-017 |
| `422` | Sintácticamente válido pero viola un invariante de negocio | La operación dejaría el Stock Actual por debajo de 0 | RF-024, RF-024a |
| `500` | Error de ejecución no controlado | Cualquier excepción no prevista; queda en `ErrorLog` | RF-028 |

Los códigos `400`, `409` y `422` representan **rechazos esperados** y por diseño **no** se escriben
en la bitácora de errores: sólo `500` lo hace. (R-08)

## Nota sobre el `422` de stock

`422` es la respuesta ante concurrencia perdida. Por RF-024b, cuando dos operaciones compiten por el
mismo artículo, la que pierde recibe `422` con el mensaje de stock insuficiente evaluado contra el
saldo ya actualizado — **nunca** un `409` de conflicto de concurrencia que obligue al usuario a
reintentar. El contrato no expone ningún código de reintento porque el diseño no lo produce.
