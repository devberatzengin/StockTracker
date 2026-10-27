# ADR 003: Repository + Unit of Work for writes, IAppDbContext for reads

Status: accepted (Phase 0)

## Context

With CQRS (MediatR), commands and queries have different needs. Commands load an aggregate,
run domain rules and save it atomically. Queries return shaped DTOs, often joining several
aggregates, and must be fast. A generic repository that serves both tends to leak
`IQueryable`, or grows one read method per screen.

## Decision

- **Writes:** one repository per aggregate (`IProductRepository`, `IItemRepository`,
  `ILocationRepository`, `IStockBalanceRepository`, `IMovementRepository`,
  `IAssignmentRepository`, `ICountSessionRepository`, …) plus `IUnitOfWork`. Repositories
  return aggregates, never `IQueryable`, and have no read-model methods.
- **Reads:** query handlers use `IAppDbContext` with `AsNoTracking()` and project straight
  into DTOs.
- Rules of a single entity live in the entity; coordination of several aggregates lives in a
  handler or a domain service (`StockMovementService`, `CountApprovalService`).
- Exception: users. They are ASP.NET Core Identity entities in Infrastructure, read and
  written through `IUserAccountService` / `IAuthService` (`UserManager`).

## Consequences

- Command handlers read like the use case: load, call the domain, save.
- Query handlers are free to shape SQL for one screen without touching the write model.
- Two ways to reach the database: reviewers must check that a query handler never writes and
  a command handler never builds DTOs from `IAppDbContext` for its response beyond what it needs.
