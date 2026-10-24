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
- Node.js 24+
- Docker (integration tests use Testcontainers; also needed for Compose)
- An external PostgreSQL server

### Backend

```bash
cd backend
dotnet tool restore
dotnet build
dotnet test
export ConnectionStrings__Default="Host=localhost;Database=stocktracker;Username=...;Password=..."
export Jwt__Issuer=stocktracker Jwt__Audience=stocktracker Jwt__SigningKey="$(openssl rand -base64 48)"
export SEED_ADMIN_EMAIL=admin@example.com SEED_ADMIN_PASSWORD=Admin12345
dotnet ef database update -p src/StockTracker.Infrastructure -s src/StockTracker.Api --connection "$ConnectionStrings__Default"
dotnet run --project src/StockTracker.Api
```

The API listens on http://localhost:5180. Swagger UI: http://localhost:5180/swagger (Development only).

`GET /health` reports `Healthy` when the database is reachable.

Sign in from Swagger with `POST /api/auth/login`, then paste the `accessToken` into **Authorize**.
The refresh token is set as an `HttpOnly` cookie scoped to `/api/auth`.

### Frontend

```bash
cd frontend
npm install
npm run dev        # http://localhost:5273, proxies /api to http://localhost:5180
npm run lint
npm run test
npm run build
npm run gen:api    # regenerates src/api/schema.d.ts; the backend must be running in Development
```

Set `API_PROXY_TARGET` to proxy `/api` somewhere other than `http://localhost:5180`.

### Docker Compose

```bash
cp .env.example .env            # then set ConnectionStrings__Default
docker compose run --rm migrator  # apply migrations (never automatic)
docker compose up --build         # web on http://localhost:8080
```

The `api` container is not published to the host; `web` (nginx) serves the app and proxies `/api` to it.

### Testing on a phone

The camera only works over HTTPS. Start the Cloudflare quick tunnel:

```bash
docker compose --profile tunnel up --build
```

Open the `https://….trycloudflare.com` URL printed in the `cloudflared` logs on the phone, sign in and use **Tara** in the top bar.
Set `PUBLIC_BASE_URL` to that address so printed QR labels (`{PUBLIC_BASE_URL}/s/{code}`) open the app from the phone's own camera.
The tunnel URL is public while it runs.

## Configuration

Secrets are provided only through environment variables. Copy `.env.example` to `.env` and fill in the values.
