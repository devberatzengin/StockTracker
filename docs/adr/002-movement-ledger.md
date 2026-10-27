# ADR 002: The movement ledger is the single source of truth

Status: accepted (Phase 1)

## Context

Stock apps that edit a "quantity" column cannot answer "where did the 3 missing boxes go?".
Edits also race: two people taking stock at the same time overwrite each other.

## Decision

Every change of stock is a `StockMovement` row: `In`, `Out`, `Transfer`, `Adjustment`,
`Assign`, `Return`, `StatusChange`. The ledger is **append-only**:

- the EF Core interceptor rejects updates and deletes of `StockMovement`;
- a PostgreSQL trigger rejects `UPDATE` and `DELETE` on `stock_movements` as well.

`StockBalance.Quantity` and `Item.CurrentLocationId`/`Status` are results of movements. They
are changed by `StockMovementService` together with the movement and saved in the **same
transaction** (one `IUnitOfWork.SaveChangesAsync`). `StockBalance` and `Item` use PostgreSQL
`xmin` as a concurrency token; a conflict returns 409 and the user retries.

Mistakes are corrected with a new movement (e.g. an `Adjustment` after a count), never by
editing history. Movements created by one action share a `ReferenceId` (a receipt with 20
items, a count approval).

## Consequences

- Full history for every product, item, location and user, with no extra audit work.
- Balances can be verified at any time by summing the ledger (done after the load and demo
  seeds: no difference).
- Only `reset-demo` empties the ledger, with `TRUNCATE` (the trigger covers rows), and only
  with `DEMO_MODE=true`.
- The ledger grows forever; list queries are indexed (`docs/performance.md`).
