# Performance

PLAN 4.2 target: list endpoints and `/api/dashboard` respond in **under 300 ms (p95)** with a
realistic data volume.

## Data set

`seed-load` fills an empty database (only when `DEMO_MODE=true`):

| What | Count |
|---|---|
| Products | 1,000 (500 serialized, 500 quantity; 20 categories) |
| Items | 5,000 (10 per serialized product) |
| Locations | 200 (1 building › 9 rooms › 30 cabinets › 160 shelves) |
| Movements | 50,000 over 180 days (receipts, transfers, consumption, item transfers) |

Movements are created with `StockMovementService`, so balances and item locations match the ledger
(checked after seeding: no balance differs from the sum of its movements, no negative balance).
Seeding takes about 15 seconds.

```bash
# fresh database, migrations applied, DEMO_MODE=true in .env
docker compose run --rm api seed-load
# locally
cd backend/src/StockTracker.Api && DEMO_MODE=true dotnet run -- seed-load
```

## Measurement

`scripts/perf/measure.py` logs in, calls each endpoint 3 times to warm up, then 20 times, and prints
the mean and p95 in milliseconds. It exits with 1 if any p95 is over the target.

```bash
PERF_EMAIL=admin@example.com PERF_PASSWORD=... python3 scripts/perf/measure.py http://localhost:5191
```

Environment: MacBook (Apple Silicon), API in Release with `ASPNETCORE_ENVIRONMENT=Production`,
PostgreSQL 16 in a local Docker container, default page size. Network latency to an external
database is not included; add the round trip time per query on a hosted database.

## Results (ms)

| Endpoint | Before mean | Before p95 | After mean | After p95 |
|---|---:|---:|---:|---:|
| `GET /api/products` | 13.9 | 28.6 | 3.8 | 5.5 |
| `GET /api/products?search=…&sort=name` | 6.4 | 7.9 | 4.4 | 6.3 |
| `GET /api/products/{id}/stock` | 5.4 | 7.6 | 3.7 | 5.0 |
| `GET /api/items` | 3.5 | 3.9 | 2.4 | 2.6 |
| `GET /api/items?search=SN-0042` | 12.3 | 18.9 | 5.9 | 6.7 |
| `GET /api/locations/tree` | 5.5 | 9.9 | 1.8 | 4.4 |
| `GET /api/locations/{id}/contents` | 4.9 | 8.0 | 1.7 | 1.9 |
| `GET /api/movements` | 23.4 | 31.5 | 6.1 | 6.7 |
| `GET /api/movements?productId=…` | 5.5 | 9.0 | 2.9 | 3.4 |
| `GET /api/movements?locationId=…` | 4.6 | 7.0 | 6.7 | 7.2 |
| `GET /api/movements?type=Transfer&sort=occurredAt` | 21.3 | 27.7 | 7.5 | 8.1 |
| `GET /api/assignments` | 2.9 | 6.0 | 1.4 | 1.6 |
| `GET /api/counts` | 1.8 | 2.1 | 1.3 | 1.4 |
| `GET /api/audit` | 5.4 | 6.3 | 3.8 | 4.2 |
| `GET /api/notifications` | 1.7 | 2.0 | 1.3 | 2.6 |
| `GET /api/dashboard` | 46.1 | 59.5 | 46.0 | 47.8 |

Every endpoint was already under the target before the change. Part of the drop in the other
endpoints is the second run being warmer; the movement list is the real improvement.

## Findings (EXPLAIN ANALYZE)

Indexes listed in PLAN were already in place: `stock_movements (product_id, occurred_at)`,
`item_id`, `from_location_id`, `to_location_id`, `reference_id`; `items (product_id, status)`;
`audit_logs (entity_type, entity_id)`.

1. **Unfiltered movement list sorted the whole ledger.** `ORDER BY occurred_at DESC LIMIT 25`
   without a product filter ran as a sequential scan plus top-N sort over all 50,000 rows
   (6.8 ms, growing linearly with the ledger). The `(product_id, occurred_at)` index cannot serve
   it. Added `ix_stock_movements_occurred_at` (migration `AddMovementOccurredAtIndex`): the plan is
   now a backward index scan with an incremental sort for the tie-breakers, 0.2 ms. The dashboard's
   last-30-days window uses the same index.
2. **Dashboard: PostgreSQL JIT.** The low-stock query (correlated sums per product) has a high
   cost estimate, so PostgreSQL compiles it with JIT: 25–33 ms with JIT, 14 ms with `jit = off`.
   The dashboard runs it twice (card count and list). Not changed in code: JIT is a server setting,
   and pooled managed databases (PgBouncer) may reject the `options` connection parameter. If the
   dashboard matters on a hosted database, the database owner can run
   `ALTER DATABASE <db> SET jit = off;`.

Nothing else showed a sequential scan over a large table on a hot path.
