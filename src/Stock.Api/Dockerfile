# Contexto de build: la raíz del repositorio (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Stock.Api/Stock.Api.csproj src/Stock.Api/
RUN dotnet restore src/Stock.Api/Stock.Api.csproj

COPY src/Stock.Api/ src/Stock.Api/
RUN dotnet publish src/Stock.Api/Stock.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Stock.Api.dll"]
