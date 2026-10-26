# StockTracker — Claude Code Talimatları

QR ve barkod tabanlı, full-stack bir stok takip uygulaması (portfolyo projesi).
Ayrıntılı plan: `docs/PLAN.md`. Bir kararı burada bulamazsan önce oraya bak; orada da yoksa **tahmin etme, sor**.

## Stack

- Backend: ASP.NET Core (Controller + MediatR), Clean Architecture, CQRS
- Veri: EF Core + Npgsql, **harici PostgreSQL** (bağlantı `.env` → `ConnectionStrings__Default`)
- Frontend: React + TypeScript (Vite), Mantine, Mantine Charts, TanStack Query, React Router, React Hook Form + Zod
- Tarama: `@zxing/browser` · QR: QRCoder · PDF: QuestPDF · Excel: ClosedXML · Görsel: SkiaSharp (ImageSharp'ın yamalı sürümleri Release derlemede lisans anahtarı istiyor)
- Test: xUnit, FluentAssertions, Testcontainers (PostgreSQL), Vitest + React Testing Library
- Çalışma ortamı: Docker Compose (`api`, `web`, `migrator`, `cloudflared`); veritabanı konteynerde DEĞİL
- Geliştirici makinesi: macOS (Apple Silicon). Windows komutu yazma.

## Solution yapısı

```
backend/
  src/StockTracker.Domain          -> hiçbir şeye bağımlı değil
  src/StockTracker.Application     -> Domain
  src/StockTracker.Infrastructure  -> Application
  src/StockTracker.Api             -> Application + Infrastructure
  tests/StockTracker.Domain.Tests
  tests/StockTracker.Application.Tests
  tests/StockTracker.Api.IntegrationTests
frontend/
docker/
docs/
```

## Mimari kurallar (pazarlıksız)

1. **Hareket defteri tek doğruluk kaynağıdır.** `StockMovement` sadece eklenir; asla güncellenmez, silinmez. `StockBalance` ve `Item.CurrentLocationId` hareketin sonucudur ve hareketle **aynı transaction'da** güncellenir.
2. **Yazma tarafı:** aggregate'e özel repository + `IUnitOfWork`. Repository'ler `IQueryable` döndürmez. Generic repository tek başına kullanılmaz.
3. **Okuma tarafı:** query handler'lar `IAppDbContext` üzerinden `AsNoTracking` + projection ile doğrudan DTO okur. Repository'ye okuma metodu ekleme.
4. Tek entity kuralları entity içinde; birden çok aggregate'i ilgilendiren koordinasyon handler ya da domain service'te (`StockMovementService`).
5. Item durum geçişleri State pattern ile.
6. Controller'lar incedir: isteği MediatR'a iletir, başka iş yapmaz. Yetki için **politika adı** kullanılır, rol adı değil.
7. Hatalar: handler exception fırlatır, global handler ProblemDetails'e çevirir (Validation 400, NotFound 404, Domain 422 + hata kodu, Concurrency 409, Forbidden 403).

## Konvansiyonlar

- Kod, isimler, commit mesajları ve kod yorumları İngilizce; kullanıcıya görünen arayüz metinleri Türkçe.
- Id'ler `Guid`. Tüm zamanlar UTC (`timestamptz`), arayüzde Europe/Istanbul gösterilir.
- Tablo/kolon adları snake_case (`EFCore.NamingConventions`). Miktarlar `numeric(18,3)`. Her string'in maksimum uzunluğu tanımlı.
- Soft delete: `IsActive` + global query filter. Fotoğraflar tek istisna.
- Kod formatı: `PRD-`, `ITM-`, `LOC-` + 6 karakter Crockford Base32.
- Liste uçları: `page`, `pageSize` (≤ 100), `search`, `sort` → `{ items, totalCount, page, pageSize }`.
- Migration'lar uygulama açılışında otomatik çalışmaz; `docker compose run --rm migrator` ile uygulanır.
- Gizli değerler sadece ortam değişkeninde. `.env` repoya girmez; yeni değişken eklersen `.env.example`'ı güncelle.
- Commit: Conventional Commits (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`).

## Her aşamada çalışma şekli

1. Önce `docs/PLAN.md`'nin ilgili bölümünü ve mevcut kodu oku.
2. **Kod yazmadan önce planını göster** (dosya listesi + kısa yaklaşım) ve onayımı bekle.
3. `feature/<aşama-no>-<kısa-ad>` branch'inde çalış.
4. Sadece istenen aşamayı yap. Sonraki aşamalardan bir şey ekleme; gerekli görürsen sadece öner.
5. Minimal ve hedefli değişiklik: çalışan kodu gereksiz yere yeniden düzenleme.
6. Domain kuralı yazdıysan unit testini, akış yazdıysan integration testini yaz.
7. Bitirmeden önce: `dotnet build`, `dotnet test`, frontend'de `npm run lint`, `npm run build` yeşil olmalı.
8. Sonunda: ne yaptığının kısa özeti, elle test adımları, varsa açık kalan noktalar. Sonra commit at; `main`'e merge etme.

## Komutlar

```bash
# backend
cd backend && dotnet build && dotnet test
# migration ekleme (yerelde)
dotnet ef migrations add <Name> -p src/StockTracker.Infrastructure -s src/StockTracker.Api
# frontend
cd frontend && npm run dev | npm run lint | npm run test | npm run build | npm run gen:api
# docker
docker compose run --rm migrator
docker compose up --build
docker compose --profile tunnel up
```
