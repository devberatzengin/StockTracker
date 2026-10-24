# StockTracker

QR and barcode based full-stack stock tracking application (portfolio project).

> Work in progress. See [docs/PLAN.md](docs/PLAN.md) for the roadmap.

## Stack

- **Backend:** ASP.NET Core (.NET 10), Clean Architecture, CQRS with MediatR, FluentValidation, Serilog
- **Data:** EF Core + Npgsql, external PostgreSQL
- **Frontend:** React + TypeScript (Vite), Mantine
- **Runtime:** Docker Compose

## Repository layout

```
backend/    ASP.NET Core solution (src + tests)
frontend/   React app
docker/     Docker related files
docs/       Plan and documentation
```

## Getting started

### Prerequisites

- .NET SDK 10
- Docker (integration tests use Testcontainers; also needed for Compose)
- An external PostgreSQL server

### Backend

```bash
cd backend
dotnet tool restore
dotnet build
dotnet test
ConnectionStrings__Default="Host=localhost;Database=stocktracker;Username=...;Password=..." \
  dotnet run --project src/StockTracker.Api
```

The API listens on http://localhost:5180. Swagger UI: http://localhost:5180/swagger (Development only).

`GET /health` reports `Healthy` when the database is reachable.

### Docker Compose

```bash
cp .env.example .env            # then set ConnectionStrings__Default
docker compose run --rm migrator  # apply migrations (never automatic)
docker compose up --build api
```

The `api` container listens on 8080 inside the compose network and is not published to the host.

## Configuration

Secrets are provided only through environment variables. Copy `.env.example` to `.env` and fill in the values.
