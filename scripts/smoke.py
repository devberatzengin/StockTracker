#!/usr/bin/env python3
"""End-to-end smoke test through nginx (PLAN 4.5), for a production-mode stack.

Usage:
    SMOKE_EMAIL=admin@example.com SMOKE_PASSWORD=... python3 scripts/smoke.py [base_url]

Creates a category, a location and a product with a unique name (nothing is deleted: the
ledger is append-only), so run it against a test or demo database. Only the standard library
is used. Exits with 1 on the first failed check.
"""
import json
import os
import sys
import urllib.error
import urllib.request
import uuid

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080").rstrip("/")


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req) as res:
            return res.status, res.headers, res.read()
    except urllib.error.HTTPError as e:
        return e.code, e.headers, e.read()


def check(name, condition, detail=""):
    print(f"{'ok  ' if condition else 'FAIL'} {name}{f'  ({detail})' if detail and not condition else ''}")
    if not condition:
        sys.exit(1)


def ok_json(method, path, token, body=None, expected=(200, 201)):
    status, _, raw = call(method, path, token, body)
    check(f"{method} {path.split('?')[0]}", status in expected, f"HTTP {status}: {raw[:200]!r}")
    return json.loads(raw) if raw else None


def main():
    email, password = os.environ.get("SMOKE_EMAIL"), os.environ.get("SMOKE_PASSWORD")
    if not email or not password:
        sys.exit("Set SMOKE_EMAIL and SMOKE_PASSWORD.")

    # nginx: SPA, security headers on the page and on hashed assets.
    status, headers, html = call("GET", "/")
    check("GET / serves the SPA", status == 200 and b'<div id="root">' in html)
    for header in ["Content-Security-Policy", "X-Content-Type-Options", "Referrer-Policy", "Permissions-Policy", "X-Frame-Options"]:
        check(f"header {header}", headers.get(header) is not None)
    check("camera allowed for own origin only", "camera=(self)" in headers.get("Permissions-Policy", ""))
    check("SPA fallback for client routes", call("GET", "/products/some-client-route")[0] == 200)
    asset = html.split(b'src="', 1)[1].split(b'"', 1)[0].decode()
    status, headers, _ = call("GET", asset)
    check("hashed asset cached with headers", status == 200 and "immutable" in ", ".join(headers.get_all("Cache-Control") or [])
          and headers.get("Content-Security-Policy") is not None)

    # API through the proxy, Production behaviour.
    check("GET /api/ping", call("GET", "/api/ping")[0] == 200)
    # nginx only proxies /api, so /swagger falls back to the SPA; it must never be an OpenAPI document.
    check("Swagger not served", b'"openapi"' not in call("GET", "/swagger/v1/swagger.json")[2])
    check("anonymous request rejected", call("GET", "/api/products")[0] == 401)

    status, headers, raw = call("POST", "/api/auth/login", body={"email": email, "password": password})
    check("login", status == 200, f"HTTP {status}")
    check("refresh cookie is Secure and HttpOnly", "secure" in headers.get("Set-Cookie", "").lower()
          and "httponly" in headers.get("Set-Cookie", "").lower())
    token = json.loads(raw)["accessToken"]
    me = ok_json("GET", "/api/auth/me", token)
    check("current user", me["email"].lower() == email.lower())

    # A small flow: catalog, location, stock in, stock view, scan, label, dashboard, reports.
    suffix = uuid.uuid4().hex[:6]
    category = ok_json("POST", "/api/categories", token, {"name": f"Smoke {suffix}"})["id"]
    location = ok_json("POST", "/api/locations", token, {"name": f"Smoke rafı {suffix}", "type": "Shelf", "parentId": None})["id"]
    product = ok_json("POST", "/api/products", token, {
        "name": f"Smoke ürünü {suffix}", "description": None, "categoryId": category, "unit": "Piece",
        "trackingType": "Quantity", "isAssignable": False, "minStockLevel": 5, "barcodes": None,
    })["id"]
    ok_json("POST", "/api/movements/in", token, {"toLocationId": location, "lines": [
        {"productId": product, "quantity": 12, "serialNumbers": None}]})
    stock = ok_json("GET", f"/api/products/{product}/stock", token)
    check("stock after receipt", stock["totalQuantity"] == 12, stock)
    code = ok_json("GET", f"/api/products/{product}", token)["code"]
    check("scan resolves product code", ok_json("GET", f"/api/scan/{code}", token)["type"].lower() == "product")
    movements = ok_json("GET", f"/api/movements?productId={product}", token)
    check("movement in ledger", movements["totalCount"] == 1)

    status, headers, pdf = call("POST", "/api/labels", token, {"template": "Sheet3x8", "productIds": [product]})
    check("label PDF", status == 200 and pdf.startswith(b"%PDF"), f"HTTP {status}")
    dashboard = ok_json("GET", "/api/dashboard", token)
    check("dashboard counts products", dashboard["cards"]["activeProducts"] >= 1)
    status, headers, xlsx = call("GET", "/api/reports/stock.xlsx", token)
    check("stock report xlsx", status == 200 and xlsx.startswith(b"PK"), f"HTTP {status}")

    # Errors are ProblemDetails without internals.
    status, _, raw = call("GET", f"/api/products/{uuid.uuid4()}", token)
    check("404 problem details", status == 404 and b'"title"' in raw and b" at " not in raw)

    print("\nsmoke test passed")


if __name__ == "__main__":
    main()
