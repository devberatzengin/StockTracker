# ADR 004: Materialized path for the location tree

Status: accepted (Phase 1)

## Context

Locations form a free tree: building › room › cabinet › shelf › box, any depth. Screens ask
"everything under this room" (contents with children, counts with children, labels for a
cabinet and its shelves). Recursive CTEs work, but every such query becomes recursive.

## Decision

Each `Location` keeps `ParentId` and a `Path` with the ids from the root down to itself:
`/{rootId}/…/{id}/`. "Under X" is `Path LIKE X.Path || '%'`, a plain indexed prefix match.

- Moving a node rewrites the `Path` of its whole subtree in one statement.
- Moves and deactivations take a `pg_advisory_xact_lock`, so two concurrent moves cannot
  create a cycle; a node cannot move under its own subtree (domain check).
- Count scopes are computed from the current path; overlapping open counts are rejected.

## Consequences

- Subtree reads are simple and fast; the whole tree is returned in one call (`/locations/tree`).
- Moves are more expensive (subtree update) but rare.
- `Path` has a maximum length (400), which limits depth to roughly ten levels; enough for a
  building.
