# ADR 006: QR content is a URL

Status: accepted (Phase 1)

## Context

Labels are scanned in two ways: with the app's own scanner, and with a phone's camera app by
someone who never opened StockTracker. A QR with only `ITM-7F3K9Q` is useless to the second
person.

## Decision

A label's QR encodes `{PUBLIC_BASE_URL}/s/{code}`, for example
`https://stok.example.com/s/ITM-7F3K9Q`, with the code printed under it.

- The phone camera opens the URL; `/s/:code` resolves the code and redirects to the product,
  item or location page (after login).
- The in-app scanner accepts the URL, a bare code or a manufacturer barcode. For URLs only the
  `/s/{code}` part is used; the host is not checked, so labels keep working after a domain
  change as long as the old host still serves the app.
- Codes are `PRD-`/`ITM-`/`LOC-` + 6 Crockford Base32 characters (no 0/O or 1/I/L mix-ups),
  short enough for a 21 × 38 mm label.

## Consequences

- `PUBLIC_BASE_URL` must be set before printing; label printing returns 422 when it is empty.
- Printed labels contain the host. Moving to a new domain needs a redirect from the old one or
  reprinted labels.
