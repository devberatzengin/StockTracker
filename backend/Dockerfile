# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer so it is cached until a project file changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/StockTracker.Domain/StockTracker.Domain.csproj src/StockTracker.Domain/
COPY src/StockTracker.Application/StockTracker.Application.csproj src/StockTracker.Application/
COPY src/StockTracker.Infrastructure/StockTracker.Infrastructure.csproj src/StockTracker.Infrastructure/
COPY src/StockTracker.Api/StockTracker.Api.csproj src/StockTracker.Api/
RUN dotnet restore src/StockTracker.Api/StockTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/StockTracker.Api/StockTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is only used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
RUN mkdir -p /app/uploads && chown "$APP_UID" /app/uploads
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "StockTracker.Api.dll"]
