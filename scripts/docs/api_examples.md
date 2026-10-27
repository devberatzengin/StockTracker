## Examples

### Login

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "depo@demo", "password": "Demo12345" }
```

```json
{ "accessToken": "eyJhbGciOi…", "expiresAt": "2026-10-27T10:15:00Z" }
```

The response also sets `Set-Cookie: refresh_token=…; Path=/api/auth; Secure; HttpOnly; SameSite=Strict`.

### Stock in (quantity and serialized lines in one receipt)

```http
POST /api/movements/in
Authorization: Bearer …

{
  "toLocationId": "0199…",
  "lines": [
    { "productId": "0199…a1", "quantity": 50, "serialNumbers": null },
    { "productId": "0199…b2", "quantity": 2, "serialNumbers": ["SN-1001", "SN-1002"] }
  ]
}
```

The response carries the shared `referenceId` and the movements; it can be passed to
`POST /api/labels` (`{ "template": "Sheet3x8", "referenceId": "…" }`) to print the new items' labels.

### Scan

```http
GET /api/scan/https%3A%2F%2Fstok.example.com%2Fs%2FITM-7F3K9Q
```

```json
{ "type": "item", "id": "0199…", "code": "ITM-7F3K9Q", "title": "Dijital Multimetre", "subtitle": "SN48213377", "isActive": true, "status": "InStock" }
```

The input may be a label URL, a bare `PRD-`/`ITM-`/`LOC-` code or a manufacturer barcode.

### Domain error

```json
{
  "title": "İşlem kurallara aykırı.",
  "status": 422,
  "detail": "Durumu zimmetli olan adet transfer edilemez.",
  "code": "Item.InvalidTransition",
  "instance": "/api/movements/transfer",
  "traceId": "00-…"
}
```
