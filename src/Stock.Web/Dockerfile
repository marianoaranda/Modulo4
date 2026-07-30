# Contexto de build: la raíz del repositorio (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Stock.Web/Stock.Web.csproj src/Stock.Web/
RUN dotnet restore src/Stock.Web/Stock.Web.csproj

COPY src/Stock.Web/ src/Stock.Web/
RUN dotnet publish src/Stock.Web/Stock.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Stock.Web.dll"]
