# ADR 005: Token storage strategy

Status: accepted (Phase 1)

## Context

The SPA needs an authenticated session that survives a page reload, without exposing a
long-lived credential to JavaScript (XSS) or to cross-site requests (CSRF).

## Decision

- **Access token:** short-lived JWT (15 minutes), kept **only in memory** (React context),
  sent as `Authorization: Bearer`.
- **Refresh token:** random value stored hashed in the database, sent as an
  **`HttpOnly`, `Secure`, `SameSite=Strict` cookie** scoped to `/api/auth`, valid 7 days.
- On reload or a 401, the client calls `POST /api/auth/refresh` once and retries the request.
- **Rotation:** every refresh issues a new refresh token and revokes the old one. If a token
  that was already rotated comes back, the whole token family is revoked (likely theft).
  Tokens revoked by logout, a password change or deactivation just get 401.
- Login is rate limited per client IP (10 per minute) and locked accounts get the same
  "wrong e-mail or password" message.

## Consequences

- JavaScript never sees the refresh token; the access token is gone when the tab closes.
- `SameSite=Strict` plus the cookie path means the cookie only reaches the refresh and logout
  endpoints, from the same site. The API and SPA share an origin behind nginx.
- `Secure` cookies need HTTPS; phones use the cloudflared tunnel during development.
