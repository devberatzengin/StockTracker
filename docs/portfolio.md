# Portfolio notes

Texts for the GitHub repository and a CV. Repository settings are changed by hand on GitHub.

## GitHub repository

**Description** (max 350 characters):

> QR and barcode stock tracking: phone scanning, an append-only movement ledger, device
> assignments, counts with approval, label PDFs and Excel reports. ASP.NET Core (.NET 10, Clean
> Architecture, CQRS), React + TypeScript, PostgreSQL, Docker.

**Topics:**

`aspnet-core` `dotnet` `csharp` `clean-architecture` `cqrs` `mediatr` `ef-core` `postgresql`
`react` `typescript` `mantine` `tanstack-query` `qr-code` `barcode-scanner` `inventory-management`
`docker-compose` `testcontainers`

**Website:** the demo URL, if one is deployed (`docs/deploy.md`).

Pin the repository on the profile.

## CV summary

**StockTracker**: full-stack inventory system (ASP.NET Core, React, PostgreSQL)

- Built a QR/barcode stock tracker with phone scanning, device assignments, counts and label
  printing on .NET 10 Clean Architecture with CQRS; stock changes go through an append-only
  movement ledger enforced by a database trigger and optimistic concurrency.
- Covered all 71 endpoints with access-policy tests and 500+ backend tests against real
  PostgreSQL (Testcontainers); list endpoints stay under 10 ms p95 on 50,000 movements.
- Shipped with Docker Compose, GHCR images per release tag, nginx security headers, login rate
  limiting and secret scanning in CI.

## Still to add

- Screenshots of the dashboard, scanning, item page and label PDF in `docs/images/`, referenced
  at the top of the README.
- A short GIF of scanning a label with a phone.
