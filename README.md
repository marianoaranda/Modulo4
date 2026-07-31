# Módulo de Stock y Pedidos

Sitio web para un comercio de barrio que, a partir de las compras y ventas registradas, infiere
automáticamente qué artículos hace falta pedir —por stock mínimo, punto de pedido o stock ideal— y
permite exportar el resultado a Excel.

- **Front-End**: ASP.NET MVC, .NET 8 (`src/Stock.Web`)
- **Back-End**: Web API REST, .NET 8, autenticación JWT (`src/Stock.Api`)
- **Base de datos**: SQL Server 2017, EF Core Migrations
- **Tests**: NUnit (`tests/Stock.Tests`)

La especificación funcional, el plan técnico y los escenarios de validación viven en
[`specs/001-modulo-stock-pedidos/`](specs/001-modulo-stock-pedidos/).

---

## Puesta en marcha

```powershell
# 1. Secretos locales. `.env` está ignorado por git; `.env.example` se commitea con placeholders.
#    Hay que completar SA_PASSWORD, JWT_SIGNING_KEY y SEED_ADMIN_PASSWORD.
cp .env.example .env

# 2. Levantar todo. La API migra y siembra la base sola dentro de compose.
docker compose up -d --build
```

- Web: <http://localhost:5280>
- API: <http://localhost:5279>
- Usuario inicial: `admin`, con la contraseña de `SEED_ADMIN_PASSWORD`.

El detalle de la ejecución fuera de Docker y de los comandos de migración está en
[`AGENTS.md`](AGENTS.md) y en [`quickstart.md`](specs/001-modulo-stock-pedidos/quickstart.md).

---

## Tests

```powershell
# Puerta de calidad. El .runsettings del proyecto de tests excluye la categoría Volumen.
dotnet test StockModulo.sln

# Tests de volumen (CE-002 y CE-004). Siembran 10.000 artículos y 100.000 líneas de detalle.
dotnet test StockModulo.sln --settings tests/Stock.Tests/volumen.runsettings
```

La corrida de volumen usa `--settings` y **no** `--filter TestCategory=Volumen`: el `.runsettings`
del proyecto ya trae `TestCategory!=Volumen` y el filtro de la línea de comandos se combina con él
mediante AND, de modo que no seleccionaría ninguna prueba y aun así terminaría en verde.

---

## Carga del inventario de apertura

El Stock Actual **no es un dato editable**: es siempre y exclusivamente el saldo de los movimientos
registrados —las compras suman, las ventas restan— calculado por agregación en `vw_StockActual`. No
existe, deliberadamente, ningún campo de "stock inicial".

Eso plantea una pregunta razonable al poner el sistema en marcha con mercadería ya en el depósito:
¿cómo entra ese stock preexistente? La respuesta es que **no hace falta nada especial**, y por eso
esta sección existe: sin ella el procedimiento parece faltar, cuando en realidad ya está cubierto
por el ABM de movimientos.

El procedimiento es:

1. Dar de alta el catálogo de artículos (**Artículos → Nuevo**). Un artículo sin movimientos aparece
   en ambas consultas con Stock Actual `0`, no desaparece de la grilla.
2. Contar la existencia física de cada artículo.
3. Registrar **un Movimiento de tipo Compra** (**Movimientos → Nuevo**) con la **fecha de apertura**
   y una línea por artículo, con la cantidad contada y el precio de costo con el que ingresó.
4. Verificar en **Consulta de Stock Actual** que cada artículo muestra la existencia cargada.

Un único movimiento de apertura con muchas líneas es preferible a uno por artículo: deja el
inventario inicial identificable como un solo asiento, y su baja —que arrastra el detalle en
cascada— revierte la carga entera si hubo un error de conteo.

Este procedimiento es lo que satisface RF-029, y está verificado de punta a punta por el escenario
**V-14** de [`quickstart.md`](specs/001-modulo-stock-pedidos/quickstart.md#v-14--carga-del-inventario-de-apertura-rf-029).

---

## Alcance

Fuera de alcance por decisión del PRD: manejo de múltiples proveedores por artículo. Las consultas
acotan el resultado a 10.000 filas e informan cuando lo recortaron.
