<!--
Reporte de Impacto de Sincronización
====================================
Cambio de versión: (plantilla / sin ratificar) → 1.0.0
Justificación: Ratificación inicial de la constitución del proyecto. Bump MAYOR a 1.0.0
               porque establece el primer conjunto gobernado de principios.

Principios definidos (4; el layout de 5 slots de la plantilla se redujo a 4 según el input):
  - I. Desarrollo Test-First (NO NEGOCIABLE)
  - II. Aislamiento de la Lógica de IA
  - III. Fidelidad a la Fuente de Verdad (No Alucinar)
  - IV. Gestión Segura de Secretos

Secciones añadidas:
  - Restricciones Adicionales (stack, alcance, límites de consulta)
  - Flujo de Trabajo y Puertas de Calidad
  - Gobernanza

Secciones eliminadas: ninguna (creación inicial).

Plantillas que requieren actualización:
  - ✅ .specify/templates/plan-template.md  (los gates de Constitution Check alinean; sin edición)
  - ✅ .specify/templates/spec-template.md  (sin conflicto de principios; sin edición)
  - ✅ .specify/templates/tasks-template.md (el orden test-first ya está reflejado; sin edición)

TODOs pendientes: ninguno. Fecha de ratificación fijada a la fecha de adopción inicial (2026-07-23).
-->

# Constitución de StockModulo

## Principios Fundamentales

### I. Desarrollo Test-First (NO NEGOCIABLE)

Los tests se escriben ANTES que la implementación. El ciclo obligatorio es
Rojo → Verde → Refactor: primero un test que falla (Rojo), luego el código mínimo
que lo hace pasar (Verde), y por último la limpieza sin cambiar el comportamiento
(Refactor). Ningún código de producción se introduce sin un test que lo justifique
y que haya fallado previamente. Los tests corren con NUnit vía `dotnet test`.

**Justificación**: Escribir el test primero fuerza a definir el comportamiento esperado
antes de la solución, previene regresiones y produce un diseño verificable en lugar
de uno racionalizado a posteriori.

### II. Aislamiento de la Lógica de IA

Toda la lógica de llamadas a modelos de IA (construcción de prompts, invocación del
modelo, parseo de respuestas, manejo de errores del proveedor) vive en un módulo
dedicado y aislado. NUNCA se mezcla con la lógica de negocio ni con las capas de
acceso a datos o presentación. La lógica de negocio consume la IA únicamente a través
de una interfaz explícita definida por ese módulo.

**Justificación**: El aislamiento permite testear la lógica de negocio con dobles de
prueba, cambiar de proveedor o modelo sin tocar reglas de dominio, y contener el
carácter no determinista de la IA detrás de un límite claro y auditable.

### III. Fidelidad a la Fuente de Verdad (No Alucinar)

El sistema NUNCA inventa ni infiere datos que no estén respaldados por su fuente de
verdad (compras, ventas y stock registrados). Toda sugerencia de pedido debe ser
trazable a datos reales. Ante ambigüedad, datos insuficientes o baja confianza, el
sistema NO adivina: deriva el caso a revisión humana de forma explícita.

**Justificación**: Un comercio toma decisiones de compra con dinero real a partir de
estas sugerencias. Un dato inventado produce pedidos erróneos y pérdida de confianza;
derivar a un humano ante la duda es preferible a una respuesta falsa presentada como
cierta.

### IV. Gestión Segura de Secretos

Ningún secreto (contraseñas, cadenas de conexión, claves de API, tokens JWT) se
hardcodea en el código fuente ni se commitea al repositorio. Los secretos se inyectan
por configuración externa o variables de entorno. Las contraseñas de usuario se
almacenan siempre con hash + salt aleatorio por usuario; nunca en texto plano ni con
un hash reversible.

**Justificación**: Los secretos hardcodeados se filtran a través del historial de Git y
de los builds, y son imposibles de rotar sin recompilar. Externalizarlos y hashear
credenciales protege a los usuarios y cumple RF-03 y RF-04.

## Restricciones Adicionales

- **Stack fijo**: Front-End ASP.NET MVC (.NET 8, `Stock.Web`); Back-End Web API REST
  (.NET 8, JWT, `Stock.Api`); base de datos SQL Server 2017 vía EF Core Migrations;
  tests con NUnit. La solución es `StockModulo.sln`.
- **Alcance cerrado**: NO se implementa manejo de múltiples proveedores por artículo;
  está explícitamente fuera del alcance del PRD.
- **Límite de consultas**: Las consultas de stock/pedido usan `TOP 10000` y un filtro
  opcional por descripción (`LIKE '%%'`) para mitigar el riesgo de volumen de artículos.
  No se permiten consultas sin límite ni filtro.

## Flujo de Trabajo y Puertas de Calidad

- Cada cambio de código va acompañado de sus tests, escritos primero (Principio I).
- `dotnet test StockModulo.sln` debe pasar antes de integrar cualquier cambio.
- Toda revisión (PR o equivalente) verifica cumplimiento de los cuatro principios:
  test-first, aislamiento de IA, fidelidad a la fuente de verdad y gestión de secretos.
- Cualquier desviación de un principio debe justificarse explícitamente en la sección
  de Complexity Tracking del plan; una desviación no justificada bloquea la integración.

## Gobernanza

Esta constitución tiene precedencia sobre cualquier otra práctica o convención del
proyecto. En caso de conflicto entre una decisión de implementación y un principio
aquí definido, prevalece el principio.

- **Enmiendas**: Toda modificación de esta constitución requiere documentación del
  cambio, justificación y actualización de las plantillas dependientes en `.specify/`.
- **Versionado**: Se aplica versionado semántico. MAYOR para remociones o redefiniciones
  incompatibles de gobernanza o principios; MENOR para nuevos principios o secciones o
  ampliaciones materiales de la guía; PARCHE para aclaraciones y correcciones no semánticas.
- **Cumplimiento**: Cada revisión de código y cada plan de implementación deben verificar
  la conformidad con esta constitución. La complejidad no justificada debe eliminarse o
  documentarse en Complexity Tracking.
- **Guía operativa**: `AGENTS.md` (referenciado desde `CLAUDE.md`) provee la guía de
  ejecución en tiempo de desarrollo y debe mantenerse consistente con estos principios.

**Versión**: 1.0.0 | **Ratificada**: 2026-07-23 | **Última enmienda**: 2026-07-23
