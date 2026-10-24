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

### Backend

```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/StockTracker.Api
```

The API listens on http://localhost:5180. Swagger UI: http://localhost:5180/swagger (Development only).

## Configuration

Secrets are provided only through environment variables. Copy `.env.example` to `.env` and fill in the values.
