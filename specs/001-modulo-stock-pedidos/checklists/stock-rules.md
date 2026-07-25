# Checklist de Calidad de Requisitos: Reglas de Negocio de Stock y Pedido

**Propósito**: Validar que los requisitos del núcleo de negocio (cálculo de stock, generación de pedido y consultas) estén escritos de forma completa, inequívoca, consistente y medible ANTES de avanzar a `/speckit-plan`
**Creado**: 2026-07-25
**Funcionalidad**: [spec.md](../spec.md)
**Profundidad**: Puerta formal pre-plan — un ítem sin resolver bloquea el pasaje a planificación
**Destinatario**: Autor del spec (auto-auditoría)

**Alcance**: Este checklist audita **cómo están redactados los requisitos**, no si el sistema funciona. No reemplaza a [requirements.md](./requirements.md), que valida la calidad general del spec.

## Completitud de los Requisitos

- [x] CHK001 - ¿Está especificado el tipo numérico de Stock Mínimo, Punto de Pedido y Stock Ideal (entero vs. decimal)? [Completitud, Spec §RF-013]
- [x] CHK002 - ¿Está definido cómo se carga el stock preexistente al poner el sistema en marcha (inventario de apertura), dado que el Stock Actual es 100% derivado de movimientos? [Gap, Spec §Supuestos]
- [x] CHK003 - ¿Está especificada la fórmula o el origen del campo Precio Total del detalle de movimiento, al modo en que RF-016 sí define el Precio de Venta? [Gap, Spec §RF-020]
- [x] CHK004 - ¿Está definido el orden de las filas del resultado de "Consulta de Stock Actual" y "Generar Pedido"? [Gap, Spec §RF-025, §RF-026]
- [x] CHK005 - ¿Está especificado si los artículos sin ningún movimiento aparecen en las consultas con Cantidad 0 o quedan excluidos? [Gap, Spec §RF-025, §RF-026]
- [x] CHK006 - ¿Está definido el conjunto cerrado de valores admitidos para el Tipo de Movimiento? [Completitud, Spec §RF-020]
- [x] CHK007 - ¿Están definidas las reglas de validación de la Fecha del movimiento (por ejemplo, si se admiten fechas futuras) y su efecto sobre el cálculo del Stock Actual? [Gap, Spec §RF-020]
- [x] CHK008 - ¿Está documentado el significado de negocio del Punto de Pedido y si genera alguna señal propia, o es exclusivamente un nivel de reposición seleccionable? [Completitud, Spec §RF-026]

## Claridad y Cuantificación

- [x] CHK009 - ¿Está cuantificada la semántica del "filtro opcional por descripción" (coincidencia parcial vs. exacta, sensibilidad a mayúsculas/acentos)? [Ambigüedad, Spec §RF-027]
- [x] CHK010 - ¿Es inequívoca la frase "o quedar excluido cuando corresponde" al describir los artículos con Cantidad a Pedir 0, o admite dos lecturas contradictorias? [Ambigüedad, Spec §Casos Límite]
- [x] CHK011 - ¿Está definido qué ocurre con la Cantidad a Pedir si los parámetros de reposición admiten decimales (regla de redondeo)? [Claridad, Spec §RF-026]
- [x] CHK012 - ¿Está definido si el tope de 10.000 se aplica antes o después de calcular el saldo y el filtro, y si el usuario recibe aviso cuando el resultado fue truncado? [Claridad, Spec §RF-027]
- [x] CHK013 - ¿Está explicitado el vínculo (si existe) entre el Precio Unitario cargado en un movimiento y el Precio de Costo / Precio de Venta del artículo? [Ambigüedad, Spec §RF-020, §RF-016]
- [x] CHK014 - ¿Es objetivamente interpretable "sin exigir reintento manual del usuario" como requisito de comportamiento? [Claridad, Spec §RF-024b]

## Consistencia entre Requisitos

- [x] CHK015 - ¿Es consistente que la rama "solo bajo mínimo = No" use MAX(0, …) y la rama "= Sí" no lo use, o el spec debe explicitar por qué la segunda nunca puede dar negativo? [Consistencia, Spec §RF-026]
- [x] CHK016 - ¿Está resuelto el conflicto entre CE-001 ("eligiendo dos parámetros") y RF-027, que agrega un tercer parámetro de filtro a "Generar Pedido"? [Conflicto, Spec §CE-001, §RF-027]
- [x] CHK017 - ¿Es consistente que RF-025a defina el rango de artículos sólo para "Consulta de Stock Actual" mientras RF-027 trata ambas consultas de forma conjunta? ¿Tiene "Generar Pedido" parámetro de rango? [Consistencia, Spec §RF-025a, §RF-026, §RF-027]
- [x] CHK018 - ¿Es consistente el nivel de granularidad entre los Escenarios de Aceptación de HU-1 (que consolidan las 3 variantes de "solo bajo mínimo = Sí" en uno) y CE-003, que exige las 6 combinaciones? [Consistencia, Spec §HU-1, §CE-003]
- [x] CHK019 - ¿Está alineado el alcance de la atomicidad de RF-024b (que menciona sólo el alta) con RF-024a, que extiende el invariante a bajas y modificaciones? [Consistencia, Spec §RF-024a, §RF-024b]
- [x] CHK020 - ¿Se usa un término canónico único para "Stock Actual" / "Cantidad" / "cantidad en existencia" a lo largo de requisitos, entidades y criterios de éxito? [Consistencia, Spec §RF-025, §Entidades Clave]
- [x] CHK021 - ¿El esquema de IDs con sufijos (RF-020a, RF-024a, RF-024b, RF-025a) mantiene la trazabilidad hacia los RF del PRD sin ambigüedad? [Trazabilidad, Spec §Requisitos Funcionales]

## Calidad de los Criterios de Aceptación

- [x] CHK022 - ¿Especifica CE-002 el volumen de **movimientos** (no sólo de artículos) bajo el cual debe cumplirse el umbral de 3 segundos, siendo el stock un saldo calculado? [Medibilidad, Spec §CE-002]
- [x] CHK023 - ¿Es CE-004 verificable objetivamente, es decir, define cómo se demuestra que "ninguna combinación de operaciones concurrentes" viola el invariante? [Medibilidad, Spec §CE-004]
- [x] CHK024 - ¿Existe un conjunto de datos de referencia definido contra el cual comprobar que las 6 combinaciones producen "exactamente la cantidad esperada"? [Medibilidad, Spec §CE-003]
- [x] CHK025 - ¿Tiene cada requisito nuevo del dominio (RF-020a, RF-024a, RF-024b, RF-025a) al menos un Escenario de Aceptación que lo cubra? [Trazabilidad, Spec §Escenarios de Aceptación]

## Cobertura de Escenarios

- [x] CHK026 - ¿Están definidos los requisitos del flujo de excepción cuando la consulta no devuelve ninguna fila (rango vacío, filtro sin coincidencias)? [Cobertura, Gap]
- [x] CHK027 - ¿Están especificados los requisitos de la exportación a Excel respecto de si replica el tope de 10.000, el filtro y el orden mostrados en pantalla? [Cobertura, Spec §RF-025, §RF-026]
- [x] CHK028 - ¿Están cubiertos por requisitos los efectos de una modificación de artículo que cambia sus parámetros de reposición sobre pedidos ya consultados? [Cobertura, Gap]
- [x] CHK029 - ¿Está definido el requisito de recuperación/rechazo cuando el recálculo del saldo falla a mitad de una baja o modificación de movimiento multilínea? [Cobertura, Flujo de Recuperación, Gap]

## Cobertura de Casos Límite

- [x] CHK030 - ¿Está contemplado el caso en que Stock Mínimo = Punto de Pedido = Stock Ideal (permitido por RF-019 al usar ≤), donde las 3 modalidades colapsan en el mismo resultado? [Caso Límite, Spec §RF-019, §RF-026]
- [x] CHK031 - ¿Está contemplado que un artículo con Stock Mínimo = 0 nunca puede quedar "bajo mínimo" y por tanto queda invisible al filtro "solo bajo mínimo = Sí"? [Caso Límite, Gap]
- [x] CHK032 - ¿Está definido el comportamiento cuando el Código inicial del rango es alfabéticamente mayor que el final? [Caso Límite, Spec §RF-025a]
- [x] CHK033 - ¿Están cubiertos por requisitos los límites superiores de cantidad y precio (desbordamiento en un movimiento con cantidades muy grandes)? [Caso Límite, Gap]

## Dependencias y Supuestos

- [x] CHK034 - ¿Está validado y explicitado el supuesto de que el Stock Actual jamás se almacena, frente al requisito de rendimiento de 3 segundos sobre 10.000 artículos? [Supuesto, Spec §Supuestos, §CE-002]
- [x] CHK035 - ¿Está documentado que las reglas de stock no dependen de ningún servicio externo, y por tanto no requieren requisitos de modo de fallo de terceros? [Supuesto, Spec §Riesgos]

## Estado

**Pasada de resolución completada el 2026-07-25**: los 35 ítems fueron cerrados aplicando correcciones al `spec.md`.
Cada ítem se cerró editando el requisito correspondiente, no sólo tildando la casilla. Ver el detalle de
requisitos nuevos y modificados en el spec (secciones Requisitos Funcionales, Criterios de Éxito y Supuestos).

Requisitos incorporados en esta pasada: RF-013a, RF-020b, RF-020c, RF-020d, RF-023a, RF-023b, RF-024c,
RF-026a, RF-027a, RF-027b, RF-027c, RF-029, RF-030, RF-031, RF-032, RF-033.
Requisitos modificados: RF-018, RF-024b, RF-025, RF-025a, RF-026, RF-027, CE-001, CE-002, CE-003, CE-004.

## Notas

- Este checklist audita la **redacción de los requisitos**, no la implementación. Un ítem marcado significa que el requisito está bien escrito, no que el código funcione.
- Marcar como completado con `[x]`; anotar hallazgos en línea.
- Los ítems con `[Gap]`, `[Ambigüedad]` o `[Conflicto]` señalan huecos detectados en la auditoría: requieren editar el spec, no sólo tildar.
- Al ser puerta formal pre-plan, todo ítem sin resolver debe cerrarse en el spec o justificarse explícitamente antes de `/speckit-plan`.
