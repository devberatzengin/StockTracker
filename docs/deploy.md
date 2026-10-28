# Deploy

StockTracker runs as four containers: `api` (ASP.NET Core), `web` (nginx with the built SPA,
proxies `/api`), `migrator` (EF Core migration bundle, run on demand) and `cloudflared` (HTTPS
tunnel). PostgreSQL is **not** a container: it is an external server reached through
`ConnectionStrings__Default`.

`docker-compose.prod.yml` is an override on top of `docker-compose.yml`:

| Setting | Production value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (no Swagger, no exception details in errors) |
| Images | `ghcr.io/<GHCR_OWNER>/stocktracker-{api,web,migrator}:<IMAGE_TAG>` |
| Restart | `unless-stopped` |
| Resource limits | api 1 CPU / 512 MB, web 0.5 CPU / 128 MB, cloudflared 0.5 CPU / 128 MB, migrator 1 CPU / 512 MB |
| Logs | `json-file`, 3 × 10 MB per container |
| Web port | `127.0.0.1:8080` only; the public way in is the tunnel |
| Tunnel | named Cloudflare tunnel (`CLOUDFLARE_TUNNEL_TOKEN`), fixed host name |

## Images

Pushing a version tag builds and publishes the images (`.github/workflows/docker.yml`):

```bash
git tag v1.0.0
git push origin v1.0.0
```

This produces `stocktracker-api:1.0.0`, `:latest` and `:sha-<commit>` (same for `web` and
`migrator`) under the repository owner's GitHub Container Registry. Packages are private by
default: make them public in GitHub, or run `docker login ghcr.io` on the server.

## Settings that change for a deployment

Everything goes into `.env` on the server (copy `.env.example`). Never commit it.

| Variable | What to set |
|---|---|
| `ConnectionStrings__Default` | The production database; hosted servers usually need `;SSL Mode=Require`. |
| `Jwt__SigningKey` | A new random key, at least 32 bytes (`openssl rand -base64 48`). Changing it signs everyone out. |
| `Jwt__Issuer`, `Jwt__Audience` | Keep or set to the public host name. |
| `PUBLIC_BASE_URL` | The public HTTPS address, e.g. `https://stok.example.com`. QR labels encode `{PUBLIC_BASE_URL}/s/{code}`; printed labels keep the old address if it changes later. |
| `Storage__Provider` | `R2` for Cloudflare R2 (photos survive a server move), or `Local` with the `uploads` volume backed up. |
| `Storage__R2__AccountId`, `Storage__R2__AccessKeyId`, `Storage__R2__SecretAccessKey`, `Storage__R2__Bucket` | R2 API token with read/write access to that bucket only. |
| `Storage__SigningKey` | Random key for signed `/api/files` links (Local storage). |
| `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD` | First admin; change the password after the first login. |
| `GHCR_OWNER`, `IMAGE_TAG` | Image owner and version, e.g. `IMAGE_TAG=1.0.0`. |
| `CLOUDFLARE_TUNNEL_TOKEN` | Token of a named tunnel whose public host name points at `http://web:80`. |
| `RateLimiting__*PermitPerMinute` | Optional limits per minute: `Login` 10 and `Refresh` 30 per client IP, `Heavy` 10 per user (reports, labels, photo uploads), `User` 300 per signed-in user, `Anonymous` 100 per IP. Counters are in memory, so with several API instances each counts on its own. |
| `DEMO_MODE` | `false` (or unset) on real data. |

CORS needs no setting: the SPA and the API share one origin (nginx proxies `/api`). Only a
frontend on a different origin would need it.

The tunnel replaces a reverse proxy with certificates. With your own domain and proxy instead,
publish port 80 of `web` to that proxy only, and make sure it sends `X-Forwarded-Proto: https`
(the refresh cookie is `Secure`).

## First deployment

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml run --rm migrator
docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile tunnel up -d
```

Optional demo data (only on an empty database, with `DEMO_MODE=true`):

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml run --rm api seed-demo
```

## Update

```bash
# .env: IMAGE_TAG=1.1.0
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml run --rm migrator
docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile tunnel up -d
```

Migrations never run at startup; run the migrator before starting the new API version.

## Check

`scripts/smoke.py` goes through nginx: SPA and security headers, Swagger closed, login and the
refresh cookie, a stock receipt, scan, label PDF, dashboard and Excel report. It creates a
category, a location and a product with a unique name, so run it against a test or demo database.

```bash
SMOKE_EMAIL=admin@example.com SMOKE_PASSWORD=... python3 scripts/smoke.py https://stok.example.com
```

## Backups

- Database: the provider's backups, or `pg_dump` on a schedule. The ledger (`stock_movements`)
  is the source of truth; balances can be checked against it.
- Photos: the R2 bucket, or the `uploads` Docker volume with Local storage.
