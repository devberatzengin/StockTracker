#!/usr/bin/env python3
"""Response-time measurement for list endpoints and the dashboard (PLAN 4.2).

Usage (against a database filled with `seed-load`):
    PERF_EMAIL=admin@example.com PERF_PASSWORD=... python3 scripts/perf/measure.py [base_url]

Each endpoint is called WARMUP times, then RUNS times; mean and p95 are reported in milliseconds.
Only the standard library is used.
"""
import json
import os
import statistics
import sys
import time
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5191").rstrip("/")
RUNS = int(os.environ.get("PERF_RUNS", "20"))
WARMUP = 3
TARGET_MS = 300


def request(path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method="POST" if body is not None else "GET")
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req) as res:
        return res.read()


def login():
    email, password = os.environ.get("PERF_EMAIL"), os.environ.get("PERF_PASSWORD")
    if not email or not password:
        sys.exit("Set PERF_EMAIL and PERF_PASSWORD.")
    return json.loads(request("/api/auth/login", body={"email": email, "password": password}))["accessToken"]


def first_id(token, path):
    return json.loads(request(path, token))["items"][0]["id"]


def measure(token, path):
    for _ in range(WARMUP):
        request(path, token)
    times = []
    for _ in range(RUNS):
        start = time.perf_counter()
        request(path, token)
        times.append((time.perf_counter() - start) * 1000)
    times.sort()
    p95 = times[max(0, round(0.95 * len(times)) - 1)]
    return statistics.mean(times), p95


def main():
    token = login()
    product = first_id(token, "/api/products?pageSize=1&search=Sarf")
    location = json.loads(request("/api/locations/tree", token))[0]["id"]
    endpoints = [
        "/api/products",
        "/api/products?search=Cihaz%2009&sort=name",
        f"/api/products/{product}/stock",
        "/api/items",
        "/api/items?search=SN-0042",
        "/api/locations/tree",
        f"/api/locations/{location}/contents",
        "/api/movements",
        f"/api/movements?productId={product}",
        f"/api/movements?locationId={location}",
        "/api/movements?type=Transfer&sort=occurredAt",
        "/api/assignments",
        "/api/counts",
        "/api/audit",
        "/api/notifications",
        "/api/dashboard",
    ]

    print(f"{RUNS} runs per endpoint, target p95 < {TARGET_MS} ms\n")
    print(f"{'endpoint':<62} {'mean':>8} {'p95':>8}")
    slow = 0
    for path in endpoints:
        mean, p95 = measure(token, path)
        flag = "" if p95 < TARGET_MS else "  SLOW"
        slow += bool(flag)
        print(f"{path.replace(product, '{product}').replace(location, '{location}'):<62} {mean:8.1f} {p95:8.1f}{flag}")
    sys.exit(1 if slow else 0)


if __name__ == "__main__":
    main()
