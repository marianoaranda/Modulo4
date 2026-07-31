# AGENTS.md

## Propósito
Sitio web para un comercio de barrio que, a partir de las compras y ventas registradas,
infiere automáticamente qué artículos hace falta pedir (por stock mínimo, punto de pedido o stock ideal).

## Stack
- Front-End: ASP.NET MVC, .NET 8 (`Stock.Web`)
- Back-End: Web API REST, .NET 8, autenticación JWT (`Stock.Api`)
- Base de datos: SQL Server 2017, acceso vía EF Core Migrations
- Tests: NUnit (`dotnet test`)
- Solución: `StockModulo.sln`

## Cómo correr
```
# PRIMER PASO: crear el archivo de secretos locales a partir de la plantilla y completar
# los tres valores (SA_PASSWORD, JWT_SIGNING_KEY, SEED_ADMIN_PASSWORD).
# `.env` está ignorado por git y NO se commitea; `.env.example` sí, con placeholders.
cp .env.example .env

# Instalar dependencias
dotnet restore StockModulo.sln

# Levantar todo (SQL Server + API + Web) con Docker.
# La API migra y siembra la base sola (flag ApplyMigrationsOnStartup, sólo en compose).
# Web en http://localhost:5280, API en http://localhost:5279.
# Usuario inicial: admin, con la contraseña definida en SEED_ADMIN_PASSWORD.
docker compose up -d --build

# Aplicar migraciones de base de datos (necesario sólo fuera de Docker).
# El --context es obligatorio: hay dos DbContext y sólo StockDbContext tiene migraciones.
# ErrorLogDbContext mapea la tabla que crea la migración inicial y no genera esquema propio.
dotnet ef database update --project src/Stock.Api --context StockDbContext

# Correr Front y Back en local sin Docker (requiere el SQL Server de compose)
dotnet run --project src/Stock.Api
dotnet run --project src/Stock.Web

# Correr tests (puerta de calidad; el .runsettings excluye la categoría Volumen)
dotnet test StockModulo.sln

# Tests de volumen (CE-002, CE-004): siembran 10.000 artículos y 100.000 líneas.
# Va por --settings y NO por --filter: el .runsettings del csproj ya trae
# TestCategory!=Volumen y --filter se combina con AND, dejando cero pruebas... en verde.
dotnet test StockModulo.sln --settings tests/Stock.Tests/volumen.runsettings
```

## Qué NO hacer
- No guardar ni loguear la contraseña en texto plano ni en un hash reversible: siempre hash + salt aleatorio por usuario (RF-03, RF-04).
- No implementar manejo de múltiples proveedores por artículo: está explícitamente fuera de alcance del PRD.
- No armar consultas de stock/pedido sin límite ni filtro: usar TOP 10000 y filtro opcional por descripción (`LIKE '%%'`) para mitigar el riesgo de volumen de artículos.
