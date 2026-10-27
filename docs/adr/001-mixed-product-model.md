# ADR 001: Mixed product model (Serialized + Quantity)

Status: accepted (Phase 1)

## Context

A workshop stores two kinds of things. A laptop or an oscilloscope is tracked one by one: it has
a serial number, an owner, a repair history. Cable ties or gloves are counted: nobody labels
each box, only how many are on which shelf matters. One model for both either loses the
history of single devices or forces a label on every screw.

## Decision

A `Product` is the definition and has a `TrackingType`:

- **Serialized**: every physical unit is an `Item` (`ITM-` code, optional serial number,
  status, current location). Movements carry the `ItemId` and always have quantity 1.
- **Quantity**: stock is a `StockBalance` per (product, location). Movements carry a
  quantity > 0 and no item.

Only Serialized products can be assignable (`IsAssignable` requires `Serialized`): a person
is responsible for a specific unit, not for "3 of something". Item status transitions
(`InStock`, `Assigned`, `InRepair`, `Disposed`) use the State pattern, so each status
decides which actions are allowed.

The tracking type cannot change once the product has stock or movements.

## Consequences

- Every stock operation has two branches (item or balance). `StockMovementService` keeps
  them in one place; the UI asks for serial numbers or a quantity based on the product.
- "Total stock" means the balance sum for Quantity products and the `InStock` item count
  for Serialized ones (`StockTotals`, shared by low-stock alerts, the dashboard and reports).
- Labels exist for products, items and locations; scanning resolves any of them.
