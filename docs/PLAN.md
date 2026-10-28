# Stok Takip Uygulaması — Proje Planı

Oct 23, 2026 · @Berat

## Özet ve kararlar

Proje beş fazda ilerler (Faz 0–4). Her faz, çalışan ve demo edilebilir bir sürümle biter; bir faz bitmeden sonrakine geçilmez. Projenin merkezi fikri şudur: **değiştirilemez hareket defteri (StockMovement) tek doğruluk kaynağıdır**, bakiyeler ve konumlar bu defterin sonucudur.

| Konu | Karar |
| --- | --- |
| Amaç | Portfolyo projesi, gerçekçi domain |
| Backend | ASP.NET Core, Clean Architecture, CQRS (MediatR) |
| Veri erişimi | Yazma: Repository + UoW · Okuma: IAppDbContext |
| Frontend | React + TypeScript (Vite) |
| Veritabanı | PostgreSQL (harici sunucu), EF Core + Npgsql |
| Ürün modeli | Karma: tekil takip (Serialized) + miktar bazlı (Quantity) |
| QR | Her ürün tanımı, tekil adet ve lokasyonun QR'ı var; ürünün varsa üretici barkodu da var |
| Tarama | Web + telefon kamerası, ayrıca kodu elle girme |
| Lokasyon | Serbest ağaç (Bina > Oda > Dolap > Raf > Kutu …) |
| Hareketler | Giriş, çıkış, transfer, sayım düzeltmesi, zimmet, iade |
| Zimmet | Ürün tanımında seçilir; zimmetlenebilir ürün tekil takipli olmak zorunda; yalnızca sistem kullanıcılarına verilir |
| Yetki | Rol bazlı: Admin, Depo Sorumlusu, Personel · JWT |
| Ekstralar | Toplu etiket basımı, kritik stok uyarısı, dashboard/rapor, fotoğraf, audit log |

**Varsayılan kararlar** (cevaplanmadı, önerilen seçenekle ilerleniyor; değiştirmek istersen bu tabloyu güncelle):

| Konu | Varsayılan |
| --- | --- |
| Silme | Her yerde soft delete (IsActive). Hareket kaydı olan hiçbir şey fiziksel olarak silinmez. |
| Kategori | Faz 1'de düz liste. Ağaç yapısı gerekirse Faz 4'te. |
| Hesapsız personel | Admin hesap açar; giriş yetkisi kapalı olabilir, yine de zimmet alabilir. |
| QR içeriği | Uygulama URL'si + kod (örn. `…/s/ITM-7F3K9Q`). Uygulama içi tarayıcı kodu URL'den ayıklar. |

**Uygulama kararları** (23 Ekim'de netleşti):

| Konu | Karar |
| --- | --- |
| API stili | Controller + MediatR |
| UI | Mantine, grafikler için Mantine Charts |
| Token saklama | Access token bellekte; refresh token httpOnly + Secure + SameSite=Strict cookie |
| Refresh token tekrar kullanımı (24 Ekim) | Toplu iptal yalnızca rotation ile değiştirilmiş bir token tekrar gelince; logout, şifre değişikliği veya pasifleştirme ile iptal edilen token sadece 401 alır |
| Telefondan HTTPS test | cloudflared tüneli + Vite proxy (tek tünel) |
| Fotoğraf depolama | Geliştirmede yerel disk, deploy'da Cloudflare R2 |
| Fotoğraf sınırları | En fazla 5 MB; JPEG, PNG, WebP; ürün başına en fazla 10; 400 px thumbnail |
| Etiket şablonları | A4 3x8 (70x37 mm), A4 2x4 (105x74 mm), A4 5x13 (38x21 mm), termal rulo 50x30 mm |
| Seri no | Opsiyonel; girilirse aynı ürün içinde benzersiz |
| Çalışma ortamı | Docker Compose: api, web, migrator, cloudflared konteynerleri; PostgreSQL harici. Deploy sonraya bırakıldı. |

**Uygulama sırasında alınan kararlar** (Faz 0–1.2, 24 Ekim):

| Konu | Karar |
| --- | --- |
| Paket lisansları | MediatR 12.5 ve FluentAssertions 7.2 kullanılır (Apache 2.0). MediatR 13+ ve FluentAssertions 8+ ticari lisans ister. |
| Tünel hedefi | cloudflared `web:80`'e (nginx) bağlanır; nginx `/api`'yi `api:8080`'e proxy'ler. Vite dev sunucusu tünellenmez. |
| CI testleri | Backend workflow'u integration testleri dahil hepsini çalıştırır (Testcontainers, GitHub runner'da Docker var). Üç Dockerfile (api, migrator, web) push edilmeden build edilir. |
| AppUser'ın yeri | Infrastructure'da (Identity'ye bağlı; Domain paketsiz kalır). Diğer aggregate'ler kullanıcıya sadece `Guid` ile referans verir. |
| Kullanıcı veri erişimi | Kural 2 ve 3'ün tek istisnası: kullanıcı okuma ve yazmaları `IUserAccountService` / `IAuthService` üzerinden ASP.NET Core Identity (`UserManager`) ile yapılır. |
| Kullanıcı rolü | Her kullanıcının tek rolü vardır; `CanLogin = false` kullanıcıya da rol verilir (varsayılan Personel). |
| E-posta benzersizliği | Identity'nin `RequireUniqueEmail` ayarı kapalı; `normalized_email` üzerinde partial unique index (boş e-posta serbest). |
| Kilitli hesap | Kilitli hesap da "E-posta veya şifre hatalı" mesajını alır (hesabın varlığı açığa çıkmaz). |
| Audit alanları | `IAuditable` (`CreatedAt/By`, `UpdatedAt/By`); `BaseEntity` ve `AppUser` uygular, interceptor doldurur. RefreshToken sadece kendi `CreatedAt`'ını tutar. |
| Hata modeli | `UnauthorizedException` → 401 eklendi. ProblemDetails `title` ve `detail` Türkçe; framework'ün ürettiği yanıtlar da (404 route, 401/403) Türkçe başlık alır. Token'sız istekte eşleşmeyen route 401 döner (fallback policy). |
| Unique index ihlali | Eşzamanlı isteklerde unique index ihlali (barkod, aktif kategori adı) 422 domain hatasına çevrilir. |
| Enum saklama | API'de ve veritabanında string (`"Serialized"`, `"Piece"`). |
| Barkod düzenleme | Yeni üründe barkodlar oluşturma isteğiyle birlikte kaydedilir; var olan üründe ekleme/silme ayrı uçlarla anında kaydedilir. |
| Stok kontrolleri | `IProductStockChecker` ve `ILocationContentChecker` 1.5'ten itibaren bakiye, Item ve hareket tablolarına bakar (1.2–1.4'te geçici "stok yok / boş" implementasyonları vardı). |
| Lokasyon ad benzersizliği | Aktif kardeşler arasında, büyük/küçük harf duyarsız; kök lokasyonlar da kendi aralarında benzersiz (`NULLS NOT DISTINCT` partial unique index). |
| Lokasyon ağacı ucu | `GET /locations/tree` iç içe ağacı tek seferde döner; liste ucu sayfalama konvansiyonunun istisnası. |
| Lokasyon taşıma | Taşıma ve pasifleştirme `pg_advisory_xact_lock` ile sıraya girer (eşzamanlı taşımalar döngü oluşturamaz). Pasif lokasyona ekleme/taşıma ve pasif lokasyonu taşıma reddedilir; yeniden aktifleştirme yok. |
| Hareket defteri koruması | `StockMovement` güncelleme/silmesi hem interceptor hem veritabanı trigger'ı ile reddedilir. `StockMovement` audit alanı taşımaz (`OccurredAt`, `UserId` yeterli). |
| Hareket hataları | Satır alan hataları 400, satırdaki iş kuralı ihlalleri 422 + `code`; ikisinde de `errors["lines[2].quantity"]`. Tüm hata anahtarları camelCase. |
| Stok eşzamanlılığı | `StockBalance` ve `Item` `xmin` ile korunur; çakışmada 409 "Stok bu arada değişti, tekrar deneyin." Aynı (ürün, lokasyon) bakiyesinin eşzamanlı oluşturulması da 409. |
| Çıkış sebebi | Sebep ve not çıkış komutunda tektir, tüm satırların hareketlerine kopyalanır. |
| Item durumları | State pattern tüm geçişleriyle (zimmet, tamir dahil) 1.5'te yazıldı; zimmet ve tamir komutları Faz 2'de kullanır. |
| Toplu kod üretimi | `ICodeGenerator.GenerateManyAsync` kodları tek sorguyla kontrol eder (girişte 100 satır × 100 adete kadar). |
| Kod üretimi | `ICodeGenerator` her denemede kodun varlığını (unique index üzerinden) sorgular, en fazla 3 deneme; kayıt anındaki yarışta index ihlali olursa hata döner. |
| Tarama girdisi | URL ise host kontrol edilmez, sadece `/s/{kod}` alınır. Elle giriş alanı tarama penceresinde her zaman görünür. |
| Lokasyon detayı (1.3) | Breadcrumb ve alt lokasyon listesi; içerik tablosu ve "alt lokasyonlar dahil" anahtarı `/contents` ile 1.5'te. |

**Uygulama sırasında alınan kararlar** (Faz 2.1–2.2, 26 Ekim):

| Konu | Karar |
| --- | --- |
| Arızalı iade | `Return` hareketi (Assigned → InStock) ve hemen ardından `StatusChange` (InStock → InRepair) yazılır; ikisi aynı `ReferenceId` ile tek transaction'da. İade notu iki hareketin de notudur. `FromStatus`/`ToStatus` sadece `StatusChange`'te dolu kalır. |
| Aynı işlemdeki hareketlerin sırası | Hareket listesinde aynı `OccurredAt`'taki `StatusChange`, onu doğuran hareketten sonra gelir (v7 id'ler aynı milisaniye içinde sıralı değildir). |
| Teslim tarihi | `date` kolonu (`DateOnly`); "bugün" Europe/Istanbul günüdür (sabit UTC+3). Gecikme: iade edilmemiş ve `DueDate < bugün`; teslim günü gecikme sayılmaz. |
| Zimmet alıcısı seçimi | `GET /users/lookup?search=` (`CanMoveStock`): sadece aktif kullanıcılar, ad/departman araması. `/users` uçları Admin'e kalır. |
| Kullanıcı detayı | Admin için `GET /users/{id}` ve `/users/:id` sayfası (bilgiler + zimmet geçmişi). |
| Zimmet uçları yetkisi | Zimmet ver/iade al/liste ve durum işlemleri `CanMoveStock`; `GET /assignments/mine` herkes; `GET /assignments/{id}` `CanMoveStock` ya da zimmetin sahibi. |
| Item'ın geçerli işlemleri | `ItemDto.availableActions` `ItemState`'ten okunur (`Assign` ayrıca ürünün zimmetlenebilir olmasını ister); frontend düğmeleri sadece buna göre gösterir. `ItemDto.openAssignment` açık zimmetin sahibini ve teslim tarihini taşır. |
| Zimmet hareketlerinde kişi | Assignment, `AssignReferenceId` ve `ReturnReferenceId` tutar; hareket listesi `Assign`/`Return` satırlarına `assigneeName` ekler. |
| Eşzamanlı zimmet | Kısmi benzersiz index (`item_id WHERE returned_at IS NULL`) ihlali 409 "Bu adet bu arada zimmetlendi, tekrar deneyin." |
| Açık zimmet kısıtları | Açık zimmeti olan kullanıcıyı pasifleştirme ve açık zimmeti olan üründe `IsAssignable`'ı kapatma 422 ile reddedilir (`Assignment.UserHasOpenAssignments`, `Product.HasOpenAssignments`). |

**Uygulama sırasında alınan kararlar** (Faz 2.3, 26 Ekim):

| Konu | Karar |
| --- | --- |
| Okutma kaydı | Her okutma `count_scans` tablosunda (satır, miktar, okutan, zaman, oturum içi sıra no) tutulur; satırın sayılan miktarı okutmaların toplamıdır. "Son okutmayı geri al" satırın son okutmasını siler; okutması kalmayan beklenmeyen satır da silinir. |
| Kapsam | Sayımın kapsamı lokasyonun güncel yolundan hesaplanır (ayrı bir `ScopePath` saklanmaz). Kapsam dışı bağlam lokasyonuyla okutma 422. Çakışma kontrolü advisory lock altında yapılır. |
| Okutma ucu | `POST /counts/{id}/scan` lokasyon etiketine `Location` (bağlam değişir, kayıt yok), miktarsız Quantity ürüne `QuantityRequired` döner; tekil ürün barkodu 422 ("adetin kendi etiketini okutun"). Aynı adet aynı yerde tekrar okutulursa `AlreadyCounted`. |
| Başka rafta bulunan adet | Kapsam içinde başka rafta okutulan beklenen adet, okutulduğu yerde beklenmeyen satır açar ("Buraya taşı · Dokunma"); beklenen satır "başka rafta bulundu" olur ve kayıp kararına düşmez. |
| Fark hesabı | Onay anındaki güncel duruma göre: miktar farkı = sayılan − güncel bakiye; okutulmamış beklenen adet sadece hâlâ o lokasyonda `InStock`/`InRepair` ise "Bulunamadı"; beklenmeyen adet için "Buraya taşı" sadece `InStock` iken (tamirdeki adet transfer edilemez, sadece not). |
| Onay isteği | `{ decisions: [{ lineId, resolution }] }`; karar gereken satırda eksik karar 422 `Count.UndecidedLines`, satıra uymayan karar 422 `Count.InvalidResolution` (`errors["decisions[lineId]"]`). |
| Kayıp adet | `Out` hareketi, sebep `Lost`, not "Sayımda bulunamadı." |
| Sayım sırasındaki hareketler | `GET /movements?countId=` kapsamda, sayım başladıktan sonra olan hareketleri (onayın kendi hareketleri hariç) listeler; inceleme cevabı bunların sayısını taşır. |
| Eşzamanlı okutma | `CountSession` `xmin` ile korunur; her okutma oturumun sıra numarasını artırır, çakışmada 409 "Sayım bu arada değişti, tekrar okutun." |
| Yetkiler | Okutma, geri alma, incelemeye gönderme, liste ve detay: herkes. Başlatma, inceleme (`/review`), onay ve iptal: `CanApproveCount`. |

**Uygulama sırasında alınan kararlar** (Faz 3.1–3.2, 27 Ekim):

| Konu | Karar |
| --- | --- |
| Görsel kütüphanesi | SkiaSharp (MIT) + `SkiaSharp.NativeAssets.Linux.NoDependencies`. ImageSharp 3.1.12'de bilinen yüksek önemli açıklar var; açıkları kapatan 3.2.0 ve 4.1.x Release derlemede Six Labors lisans anahtarı istiyor. |
| Audit kapsamı | PLAN listesine ek olarak `StockBalance`, `CountLine`, `CountScan` ve Identity claim/token/rol tabloları yazılmaz; `Normalized*`, `UserName`, `AccessFailedCount`, `LockoutEnd`, `LockoutEnabled`, `EmailConfirmed`, `PhoneNumber*`, `TwoFactorEnabled`, `xmin`, `ScanSequence`, `CreatedAt/By` alanları yazılmaz. |
| Audit işlemleri | `Deleted` eklendi (fotoğraf ve barkod fiziksel silinir). `Created` satırı tüm alanların yeni değerini taşır. Kullanıcısız işlemlerde (seed) `UserId` boştur. |
| Alt kayıtlar | `ParentEntityId` kolonu: barkod ve fotoğraf satırları ürünün id'sini taşır; ürünün "Değişiklik geçmişi" bunları da gösterir. |
| Rol değişikliği | `user_roles` satırları kullanıcının geçmişine `role: { old, new }` olarak yazılır; Identity'nin aynı istekteki "rol sil" ve "rol ekle" kayıtları tek satırda birleştirilir. |
| Audit yetkisi | `GET /audit` ve tüm "Değişiklik geçmişi" bölümleri `CanViewAudit` (Admin). AuditLog da defter gibi append-only (interceptor). |
| Fotoğraf depolama | `Storage__Provider` (`Local` varsayılan, `R2`), `Storage__SigningKey` (yerel imza, ≥ 32 karakter), `Storage__R2__*`; seçili sağlayıcının ayarı eksikse API açılmaz. Docker'da `Storage__LocalPath=/app/uploads` compose'ta verilir. |
| Fotoğraf işleme | Dosya imzası (JPEG/PNG/WebP) + Skia ile çözme; 40 megapiksel üstü reddedilir. EXIF yönü piksele uygulanır, yeniden kodlama metadata taşımaz. Ana 1600 px (q82), thumbnail 400 px (q75), WebP; küçük görsel büyütülmez. Geçersiz dosya 400 (`errors.file`), 10 sınırı 422. |
| Fotoğraf tutarlılığı | Yükleme ürün satırını `FOR UPDATE` ile kilitler; dosyalar yazılır, veritabanı kaydı başarısızsa silinir. Silmede önce satır, sonra dosya (kalan dosya sadece loglanır). |
| İmzalı URL | `/api/files/{key}?exp=&sig=` göreli döner, anonim; HMAC-SHA256 (`"{key}:{exp}"`), süresi geçmiş ya da hatalı imza 403, `Cache-Control: private, max-age=<kalan süre>`. |
| Galeri | Ürün detayının "Genel" sekmesinde (yeni ürün kaydedildikten sonra). Sürükle-bırak + telefon için ok düğmeleri; `accept="image/*" capture="environment"`. Thumbnail ürün listesinde, ürün seçicide ve tara-taşı listesinde. |

**Uygulama sırasında alınan kararlar** (Faz 3.3–3.4, 27 Ekim):

| Konu | Karar |
| --- | --- |
| Etiket kütüphaneleri | QRCoder 1.8.1 (MIT), QuestPDF 2026.9.1 (Community lisansı; `Settings.License = Community`). Lato fontu ve Linux native kütüphaneleri paketle gelir. |
| `PUBLIC_BASE_URL` | Boşsa `POST /labels` 422 `Labels.BaseUrlMissing`; API açılışını engellemez. |
| Başlangıç pozisyonu | API'de 1'den başlayan hücre numarası (`startPosition`); önceki `startPosition − 1` hücre boş kalır. Termal ruloda her zaman 1. |
| Etiket hedefleri | Tam olarak biri: `productIds`, `itemIds`, `referenceId` (o girişte oluşan adetler, seri no sırasıyla), `locationId` (lokasyon + aktif alt lokasyonlar, ağaç sırasıyla). Her şablon her hedefle çalışır; arayüz kaynağa göre 3x8 ya da 2x4 önerir. Aynı id iki kez gelirse bir kez basılır. |
| Kâğıt geometrisi | 3x8 ve 2x4: yan 0, üst 0,5 mm; 5x13: yan 10, üst 12 mm; termal 50x30: her etiket ayrı sayfa. QR PNG, 10 px/modül, M seviyesi, 4 modül sessiz alan. |
| Etiket yetkisi | `POST /labels` `CanMoveStock`; en fazla 500 etiket (genişletme sonrası sayılır, 422 `Labels.TooMany`). PDF yeni sekmede açılır; "%100 ölçek" notu modalda. |
| Kritik stok tetikleri | `In`, `Out`, `Adjustment`'a ek olarak `Assign`, `Return`, `StatusChange` (tekil ürünlerde toplam = `InStock` adet). `Transfer` tetiklemez. Kontrol kayıttan sonra MediatR notification ile çalışır; hata isteği bozmaz, loglanır. |
| Bildirim alıcıları | Her alıcıya ayrı satır. Kritik stok: aktif ve giriş yapabilen Admin + Depo Sorumluları. Teslim yaklaşıyor: zimmet alan (giriş yapabiliyorsa). Gecikmiş: zimmet alan (giriş yapabiliyorsa) + Depo Sorumluları. |
| Tekrar önleme | `(user_id, dedup_key) WHERE resolved_at IS NULL` kısmi benzersiz index; yarışta 409, günlük kontrol telafi eder. |
| Kapanış | Kritik stok toplam ≥ minimum olunca (ya da ürün pasif / minimum kaldırılmışsa bir sonraki kontrolde). Zimmet bildirimleri iadede (`AssignmentReturned` olayı); günlük kontrol iade edilmiş zimmetlerin açık bildirimlerini de kapatır. |
| Günlük kontrol | `BackgroundService`: açılıştan 1 dk sonra bir kez, sonra her gün 08:00 (UTC+3) `PeriodicTimer(1 gün)`. `Notifications__DailyCheckEnabled=false` ile kapatılır (testler). |
| Okunmamış sayısı | Zil sadece okunmamış ve kapanmamış bildirimleri sayar; kapanmışlar listede "Çözüldü" rozetiyle kalır. 60 sn'de bir sorgulama. |

**Uygulama sırasında alınan kararlar** (Faz 3.5, 27 Ekim):

| Konu | Karar |
| --- | --- |
| Kritik stok tanımı | Bildirimlerle aynı ortak sorgu (`StockTotals`): Quantity'de bakiye toplamı, Serialized'da `InStock` adet; aktif ve minimumu olan ürün, toplam < minimum. |
| Günlük hareket grafiği | İstanbul gününe göre bugün dahil 30 gün; hareketsiz günler 0. Gruplama 30 günlük (zaman, tip) çiftleri yüklenerek bellekte yapılır (sabit UTC+3). Tüm tipler döner, grafik sadece olanları çizer. |
| Dashboard listeleri | En çok hareket gören 10 ürün hareket sayısına göre; durum dağılımı `InStock`/`Assigned`/`InRepair`; kritik stok eksik miktara göre ilk 20; gecikmiş zimmetlerde teslim tarihi en eski 10 (gecikme günüyle). "Toplam adet" hurdaya ayrılanları saymaz. |
| Excel | ClosedXML 0.105.1 (MIT; bağımlılığı SixLabors.Fonts 1.0.0, lisans anahtarı istemez). Kolon genişlikleri sabit (font ölçümü yok); başlık kalın + dondurulmuş, otomatik filtre; tarihler İstanbul saatiyle gerçek Excel tarihi. |
| Rapor içerikleri | Stok durumu: aktif ürünler + "Lokasyon dağılımı" sayfası (Quantity bakiyeleri, Serialized'da lokasyondaki `InStock`/`InRepair` adet). Hareket dökümü: `from`/`to` İstanbul günü (iki gün dahil), kaynak/hedef tam lokasyon yolu. Zimmet listesi: isteğe bağlı `status`. |
| Satır sınırı | Satırlar önce sayılır; 50.000'i aşan rapor üretilmeden 422 `Reports.TooManyRows` (hareket dökümünde "Tarih aralığını daraltın"). |
| Yetki ve ekranlar | `GET /dashboard` ve `GET /reports/*.xlsx` `CanViewReports`. Menüde "Panel" (`/dashboard`) ve "Raporlar" (`/reports`). |

**Uygulama sırasında alınan kararlar** (güvenilirlik, 28 Ekim):

| Konu | Karar |
| --- | --- |
| Ters kayıt | `POST /movements/{id}/reverse` (`CanMoveStock`, not zorunlu). Orijinal hareket değişmez; tersi yönde yeni hareket `reverses_movement_id` ile orijinali gösterir (yeni hareket tipi yok). Miktar `In` → aynı raftan `Out` (sebep `Other`), `Out` → aynı rafa `In`, `Transfer` → geri transfer; tekil `In` → adet hurdaya (`Out`), tekil `Transfer` → geri transfer. Tekil adet hâlâ `InStock` ve hareketin bıraktığı yerde olmalı (`Movement.ItemChangedSinceMovement`). Bir hareket bir kez ters çevrilir (kısmi unique index, `Movement.AlreadyReversed`); ters kayıt ve zimmet/iade/durum/sayım hareketleri ters çevrilemez (`Movement.NotReversible`). Süre sınırı yok. |
| Idempotency | Stok değiştiren komutlar (`IIdempotentCommand`: giriş, çıkış, transfer, ters kayıt, zimmet ver/iade, tamire gönder/dön, sayım onayı) `Idempotency-Key` başlığı kabul eder. Anahtar `idempotency_requests` tablosuna komutun **kendi transaction'ında** yazılır (MediatR behavior); hata alan komut anahtarı tüketmez. Aynı anahtar + aynı gövde ilk cevabı döner, farklı gövde 422 `Idempotency.KeyReused`; eşzamanlı kopya ilk cevabı 3 sn bekler, yoksa 409. Frontend gönderim başına bir anahtar üretir, başarıdan sonra yeniler. Kayıtlar 7 gün sonra günlük job ile silinir. |
| Defter mutabakatı | Günlük job her bakiyeyi miktar hareketlerinin toplamıyla, her adedin durum ve lokasyonunu son hareketleriyle karşılaştırır (`LedgerReconciliation`). Fark varsa aktif Admin'lere tek "Defter tutarsızlığı" bildirimi (`LedgerMismatch`, dedup), ayrıntılar loga; veri düzelince bildirim kapanır. Otomatik düzeltme yapılmaz. |
| Günlük job kilidi | Job, oturum seviyesinde `pg_try_advisory_lock` alabilen tek API kopyasında çalışır; kilit alınamazsa o çalışma atlanır. |
| Rate limit | Kayan pencere (1 dk, 6 segment), aşımda 429 + `Retry-After`. Genel tavan: giriş yapmış kullanıcı başına 300/dk, anonim IP başına 100/dk. Politikalar: login IP başına 10/dk, refresh IP başına 30/dk, ağır uçlar (Excel raporları, etiket PDF'i, fotoğraf yükleme) kullanıcı başına 10/dk. Hepsi `RateLimiting__*PermitPerMinute` ile değişir; sayaçlar bellekte (tek API kopyası varsayımı). Hangi ucun hangi politikayı kullandığı testle sabit. |

## Mimari

Bağımlılıklar her zaman içe doğru akar: API → Application → Domain. Infrastructure, Application'daki arayüzleri uygular. Domain hiçbir pakete bağımlı değildir.

### Backend katmanları

| Proje | Sorumluluk | İçermez |
| --- | --- | --- |
| `StockTracker.Domain` | Entity'ler, value object'ler, enum'lar, domain kuralları, domain exception'ları, State pattern (Item durumları) | EF, MediatR, HTTP |
| `StockTracker.Application` | Command/Query'ler (MediatR), handler'lar, DTO'lar, FluentValidation, arayüzler (`IAppDbContext`, repository'ler, `IUnitOfWork`, `IFileStorage`, `ICodeGenerator`, `ICurrentUser`, `IDateTime`) | EF Core implementasyonu |
| `StockTracker.Infrastructure` | `AppDbContext`, EF konfigürasyonları, migration'lar, repository ve UoW implementasyonları, Identity + JWT, QR/PDF/Excel üretimi, dosya depolama, background job'lar | İş kuralı |
| `StockTracker.Api` | Controller'lar (MediatR'a ince delege), auth middleware, global exception handling (ProblemDetails), Swagger | İş kuralı, sorgu |
| `tests/*` | Domain unit, Application unit, API integration testleri | — |

### Veri erişim stratejisi

- **Command (yazma):** Aggregate'e özel repository ile yüklenir, domain metodu çağrılır, `IUnitOfWork.SaveChangesAsync` ile tek transaction'da kaydedilir. Hareket + bakiye/Item güncellemesi her zaman aynı UoW içindedir.
- **Query (okuma):** `IAppDbContext` üzerinden `AsNoTracking` + projection ile doğrudan DTO'ya okunur. Repository'ye okuma metodu eklenmez.
- Repository'ler `IQueryable` dışarı açmaz. Generic repository tek başına kullanılmaz; her aggregate'in kendi repository'si vardır (`IProductRepository`, `IItemRepository`, `ILocationRepository`, `IStockBalanceRepository`, `IMovementRepository`, Faz 2'de `IAssignmentRepository`, `ICountSessionRepository`).
- MediatR pipeline behavior'ları: validation, logging, (Faz 3'te) performans uyarısı.

### Frontend yapısı

- Vite + React + TypeScript, React Router, TanStack Query (sunucu durumu), React Hook Form + Zod (formlar), Mantine (UI), Mantine Charts (grafikler).
- Kimlik: access token yalnızca bellekte (React context); refresh token httpOnly cookie'de. Sayfa yenilenince ya da 401 gelince `/auth/refresh` çağrılır, istek bir kez tekrarlanır.
- Klasörleme özellik bazlı: `features/auth`, `features/products`, `features/locations`, `features/scan`, `features/movements`, `features/assignments`, `features/counts`, `features/dashboard`.
- Tarama: `@zxing/browser` (QR + EAN/UPC aynı kütüphaneyle). Tek bir `ScanDialog` bileşeni: kamera + elle giriş alanı, sonucu `/api/scan/{code}`'a gönderir.
- Vite dev server `/api` isteklerini backend'e proxy'ler; frontend ve API aynı origin'de görünür (cookie ve tünel için).
- API tipleri backend'in OpenAPI şemasından `openapi-typescript` ile üretilir; elle tip yazılmaz.

### Temel kütüphaneler

| İş | Kütüphane |
| --- | --- |
| CQRS | MediatR |
| Doğrulama | FluentValidation |
| ORM | EF Core + Npgsql |
| Kimlik | ASP.NET Core Identity + JWT Bearer + refresh token |
| QR üretimi | QRCoder |
| PDF etiket | QuestPDF |
| Excel | ClosedXML |
| Log | Serilog |
| Test | xUnit, FluentAssertions, Testcontainers (PostgreSQL) |

Ek kütüphaneler: Mantine + Mantine Charts (UI ve grafik), SkiaSharp (thumbnail), AWSSDK.S3 (Cloudflare R2 erişimi).

## Domain modeli

Model baştan tüm fazlar için tasarlanır, ama her entity yalnızca ait olduğu fazda koda girer. Tüm entity'lerde `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` ve (silinebilir olanlarda) `IsActive` bulunur.

### Entity'ler

| Entity | Faz | Tür | Temel alanlar | Kurallar |
| --- | --- | --- | --- | --- |
| User | 1 | Identity | Ad, e-posta, rol, `CanLogin`, aktiflik | Pasif kullanıcıya zimmet verilemez |
| Category | 1 | Entity | Ad | Ad benzersiz |
| Product | 1 | Aggregate root | Kod `PRD-`, ad, açıklama, kategori, birim, `TrackingType`, `IsAssignable`, `MinStockLevel` | `IsAssignable` ise `TrackingType = Serialized` · stok/hareket varsa takip tipi değişmez |
| ProductBarcode | 1 | Product'a ait | Değer, format (EAN-13, UPC, Code128…) | Değer sistem genelinde benzersiz |
| Item | 1 | Aggregate root | Kod `ITM-`, ürün, seri no, `Status`, mevcut lokasyon | Sadece Serialized ürünler için · durum geçişleri State pattern ile · seri no opsiyonel, girilirse aynı ürün içinde benzersiz |
| Location | 1 | Aggregate root | Kod `LOC-`, ad, tip, üst lokasyon, `Path` | Döngü yok · taşınınca alt ağacın Path'i güncellenir · içi doluysa pasifleştirilemez |
| StockBalance | 1 | Aggregate root | Ürün, lokasyon, miktar, concurrency token | Sadece Quantity ürünler · (ürün, lokasyon) benzersiz · miktar ≥ 0 |
| StockMovement | 1 | Append-only | Tip, ürün, item?, kaynak?, hedef?, miktar, kullanıcı, not, `OccurredAt`, `ReferenceId?` | Hiç güncellenmez/silinmez · tipe göre alan kuralları (aşağıda) |
| Assignment | 2 | Aggregate root | Item, alan kullanıcı, veren kullanıcı, veriliş, beklenen iade, iade tarihi, iade notu | Item'ın aynı anda tek açık zimmeti olur |
| CountSession | 2 | Aggregate root | Lokasyon, alt lokasyonlar dahil mi, durum, başlatan, onaylayan | Bir lokasyonda aynı anda tek açık sayım |
| CountLine | 2 | CountSession'a ait | Ürün, item?, beklenen, sayılan | Onayda fark → Adjustment hareketi |
| ProductPhoto | 3 | Product'a ait | Dosya anahtarı, sıra, ana fotoğraf mı | Ürün başına bir ana fotoğraf |
| AuditLog | 3 | Append-only | Entity tipi, entity id, işlem, değişen alanlar (JSON), kullanıcı, zaman | Interceptor yazar, el ile yazılmaz |
| Notification | 3 | Entity | Alıcı, tip, mesaj, ilgili kayıt, okundu mu | — |

### Hareket tipleri

| Tip | Kaynak | Hedef | Etki |
| --- | --- | --- | --- |
| `In` (giriş) | — | zorunlu | Bakiye artar ya da yeni Item lokasyona girer |
| `Out` (çıkış) | zorunlu | — | Bakiye azalır ya da Item `Disposed olur` |
| `Transfer` | zorunlu | zorunlu, farklı | Bakiye kaynaktan hedefe geçer ya da Item'ın lokasyonu değişir |
| `Adjustment` (sayım) | biri dolu | biri dolu | Sayım farkı kadar artış/azalış |
| `Assign` (zimmet) | zorunlu | — | Item `Assigned` olur, lokasyonu boşalır |
| `Return` (iade) | — | zorunlu | Item `InStock` olur, iade edilen lokasyona girer |
| StatusChange (Faz 2) | — | — | Item durumu değişir (tamire gönder / tamirden döndü); lokasyon değişmez |

Tekil (Serialized) hareketlerde `ItemId` zorunlu ve miktar her zaman 1'dir. Miktar bazlı hareketlerde `ItemId` boş, miktar > 0'dır.

### Item durumları

`InStock` → `Assigned` → `InStock` (iade) · `InStock` → `InRepair` → `InStock` · `InStock`/`InRepair` → `Disposed` (son durum). Zimmetli ya da hurdaya ayrılmış bir Item transfer edilemez; `Disposed` hiçbir harekete giremez.

### Kod formatı ve tarama çözümleme

- Format: `PRD-`, `ITM-`, `LOC-` + 6 karakter Crockford Base32 (0/O, 1/I/L karışıklığı yok). Kod üretimi `ICodeGenerator` ile yapılır; her tabloda benzersiz index vardır.
- `GET /api/scan/{input}` sırası: (1) giriş URL ise kodu ayıkla, (2) büyük harfe çevir, boşlukları sil, (3) ön ek varsa ilgili tablodan bul, (4) ön ek yoksa `ProductBarcode` tablosunda ara, (5) bulunamazsa 404.
- Dönüş: `{ type: "product" | "item" | "location", id, code, title, subtitle, isActive, status }` (ayrıntı 1.4'te). Frontend bu tipe göre detay sayfasına yönlendirir.

## Faz 0 — Kurulum ve altyapı

Amaç: hiçbir iş özelliği yokken uçtan uca çalışan bir iskelet. Faz sonunda `docker compose up` ile backend ve frontend ayağa kalkar, harici PostgreSQL'e bağlanır ve telefon kamerası HTTPS tüneli üzerinden açılır.

**Günlük geliştirme döngüsü:** Kod yazarken backend Rider'da, frontend `npm run dev` ile yerelde çalışır (hot reload, debugger). Docker Compose; entegre çalıştırma, telefondan test ve faz sonu demoları için kullanılır. Mac'te kaynak kodu konteynere bağlayıp hot reload yapmak yavaş olduğu için bu ayrım bilinçli.

### 0.1 Repo ve solution

- [ ] Repo yapısı: `backend/`, `frontend/`, `docker/`, `docs/`, kökte `docker-compose.yml`, `.env.example`, `.gitignore` (`.env`, `bin/`, `obj/`, `node_modules/`, `uploads/`), `README.md`
- [ ] `backend/StockTracker.sln` içinde: `src/StockTracker.Domain`, `src/StockTracker.Application`, `src/StockTracker.Infrastructure`, `src/StockTracker.Api`, `tests/StockTracker.Domain.Tests`, `tests/StockTracker.Application.Tests`, `tests/StockTracker.Api.IntegrationTests`
- [ ] Referanslar: Application → Domain · Infrastructure → Application · Api → Application + Infrastructure · Domain hiçbir projeye referans vermez
- [ ] `Directory.Build.props`: hedef .NET sürümü, `Nullable` ve `TreatWarningsAsErrors` açık; `Directory.Packages.props` ile merkezi paket sürümleri
- [ ] `.editorconfig` (C# stil kuralları) ve frontend için ESLint + Prettier

### 0.2 Backend iskeleti

- [ ] Domain: `BaseEntity` (`Guid Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), `ISoftDeletable` (`IsActive`), `DomainException` ve kural ihlali için anlamlı hata kodları (örn. `Product.TrackingTypeLocked`)
- [ ] Application: `ICurrentUser`, `IDateTime`, `IAppDbContext`, `IUnitOfWork` arayüzleri; MediatR kaydı; `ValidationBehavior` ve `LoggingBehavior`
- [ ] Hata modeli: handler'lar exception fırlatır, API'deki global handler bunları ProblemDetails'e çevirir — `ValidationException` → 400 (alan hataları ile), `NotFoundException` → 404, `DomainException` → 422 (hata koduyla), `ConcurrencyException` → 409, `ForbiddenException` → 403
- [ ] Serilog: konsola JSON log; istek başına `TraceId` ProblemDetails'e eklenir
- [ ] Ayarlar Options pattern ile: `JwtOptions`, `StorageOptions`, `CorsOptions`; tüm gizli değerler ortam değişkeninden
- [ ] Swagger (sadece Development), `/health` (DB bağlantısını da kontrol eder)

### 0.3 Veritabanı

- [ ] `AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IAppDbContext`
- [ ] Kurallar: tablo ve kolon adları snake\_case (`EFCore.NamingConventions`), tüm zamanlar `timestamptz` ve UTC, `decimal` miktarlar `numeric(18,3)`, string'lerde maksimum uzunluk zorunlu
- [ ] Konfigürasyonlar her entity için ayrı `IEntityTypeConfiguration` sınıfında
- [ ] `SaveChanges` sırasında audit alanlarını dolduran interceptor (Faz 3'teki AuditLog'dan ayrı, sadece `CreatedAt/By`, `UpdatedAt/By`)
- [ ] Soft delete için global query filter (`IsActive`); gerektiğinde `IgnoreQueryFilters` ile pasifler de görülür
- [ ] İlk migration: Identity tabloları; harici DB'ye uygulanır ve bağlantı doğrulanır

### 0.4 Docker Compose

| Servis | İmaj | Görev |
| --- | --- | --- |
| `api` | `backend/Dockerfile` (çok aşamalı: SDK ile build → ASP.NET runtime) | Backend, konteyner içinde 8080 portu; dışarı açılmaz |
| `web` | `frontend/Dockerfile` (Node ile build → Nginx) | React build'ini sunar, `/api` isteklerini `api:8080`'e proxy'ler; host'ta 8080 portu |
| `migrator` | `backend/Dockerfile.migrator` (EF migration bundle) | `docker compose run --rm migrator` ile elle çalışır, migration'ları uygular ve kapanır |
| `cloudflared` | `cloudflare/cloudflared` | `web:80`'i HTTPS tüneliyle dışarı açar; `tunnel` profili ile sadece istenince başlar |

- [ ] PostgreSQL harici: bağlantı cümlesi `.env` içindeki `ConnectionStrings__Default` ile verilir; compose'da veritabanı servisi yok
- [ ] Volume: `uploads` (fotoğraflar, Faz 3'te kullanılır)
- [ ] `api` sağlık kontrolü `/health`; `web` ona bağımlı başlar
- [ ] Migration'lar uygulama açılırken otomatik çalışmaz; her zaman `migrator` ile bilinçli uygulanır
- [ ] `.env.example` tüm değişkenleri açıklamalı listeler; gerçek `.env` repoya girmez
- [ ] Apple Silicon: kullanılan tüm imajlar arm64 destekliyor; ek ayar gerekmez

### 0.5 Frontend iskeleti

- [ ] Vite + React + TypeScript, mutlak import yolu (`@/`)
- [ ] Mantine + Mantine Charts + Mantine Notifications; açık tema varsayılan
- [ ] React Router: `AppLayout` (sol menü + üst bar + içerik), `/login`, korumalı rotalar için boş bir sarmalayıcı
- [ ] TanStack Query client ayarı: varsayılan `staleTime` 30 sn, hata bildirimi Mantine Notifications ile
- [ ] `apiClient`: tek fetch sarmalayıcısı; ProblemDetails'i ayrıştırır, 401'de refresh denemesi için kanca bırakır (Faz 1'de doldurulur)
- [ ] `npm run gen:api`: çalışan backend'in OpenAPI şemasından `src/api/schema.d.ts` üretir

### 0.6 Tarama denemesi

- [ ] `/dev/scan` sayfası: `@zxing/browser` ile arka kamera, sürekli okuma; okunan değer ve format ekrana yazılır
- [ ] Test: ekrandan ya da kâğıttan bir QR, bir ürün üzerindeki EAN-13 barkodu
- [ ] `docker compose --profile tunnel up` ile verilen `https://…trycloudflare.com` adresi telefonda açılır, kamera izni istenir ve okuma çalışır

### 0.7 CI

- [ ] GitHub Actions `backend.yml`: restore, build, test (Domain ve Application testleri)
- [ ] GitHub Actions `frontend.yml`: install, lint, type-check, build
- [ ] İki Docker imajı CI'da build edilir (push edilmez), Dockerfile'ların bozulmadığı garanti altına alınır

### Kabul kriteri

`docker compose run --rm migrator` ve `docker compose --profile tunnel up` sonrası telefondan tünel adresi açılıyor, `/health` yeşil, `/dev/scan` bir QR ve bir EAN barkodu okuyor, CI yeşil.

## Faz 1 — Çekirdek: kimlik, ürün, lokasyon, tarama, hareket

Amaç: bir ürünü tanımlayıp rafa koymak, taşımak, çıkarmak ve QR okutarak geçmişini görmek. Adımlar bu sırayla yapılır; her adım backend + frontend + testleriyle birlikte biter.

### 1.1 Kimlik ve kullanıcılar

**Veri**

| Entity | Alanlar |
| --- | --- |
| AppUser (Identity) | `FullName` (zorunlu, ≤ 100), `Department` (opsiyonel, ≤ 100), `IsActive`, `CanLogin`, e-posta (giriş yapacaksa zorunlu ve benzersiz) |
| RefreshToken | `UserId`, `TokenHash` (SHA-256), `ExpiresAt` (7 gün), `CreatedAt`, `RevokedAt`, `ReplacedByHash` |

**Kurallar**

- Access token 15 dakika; içinde `sub`, `name`, `role` claim'leri
- Refresh token her kullanımda yenilenir (rotation). Rotation ile yenisiyle değiştirilmiş bir token tekrar gelirse çalıntı sayılır ve kullanıcının tüm token'ları iptal edilir. Logout, şifre değişikliği veya pasifleştirme ile iptal edilmiş bir token gelirse sadece 401 döner; diğer oturumlar etkilenmez
- Cookie: `refresh_token`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`
- Şifre: en az 8 karakter, büyük harf, küçük harf ve rakam içerir; 5 hatalı denemede 15 dakika kilit (Identity lockout)
- `IsActive = false` ya da `CanLogin = false` olan kullanıcı giriş yapamaz; hata mesajı her durumda aynıdır ("E-posta veya şifre hatalı")
- `CanLogin = false` kullanıcı için şifre ve e-posta gerekmez; sadece zimmet alıcısı olarak vardır
- İlk Admin, uygulama açılışında `SEED_ADMIN_EMAIL` ve `SEED_ADMIN_PASSWORD` ortam değişkenlerinden oluşturulur (yoksa)
- Admin kendi rolünü düşüremez ve kendini pasifleştiremez; sistemde en az bir aktif Admin kalır

**Rol matrisi**

| İşlem | Admin | Depo Sorumlusu | Personel |
| --- | --- | --- | --- |
| Kullanıcı yönetimi | ✓ | — | — |
| Kategori, ürün, lokasyon tanımlama | ✓ | ✓ | — |
| Giriş, çıkış, transfer | ✓ | ✓ | — |
| Zimmet verme ve iade alma (Faz 2) | ✓ | ✓ | — |
| Sayım başlatma ve sayma (Faz 2) | ✓ | ✓ | ✓ (sadece sayma) |
| Sayım onayı (Faz 2) | ✓ | ✓ | — |
| Tarama, ürün/lokasyon görüntüleme | ✓ | ✓ | ✓ |
| Kendi zimmetlerini görme | ✓ | ✓ | ✓ |
| Dashboard ve raporlar (Faz 3) | ✓ | ✓ | — |
| Audit log (Faz 3) | ✓ | — | — |

Politikalar: `CanManageUsers`, `CanManageCatalog`, `CanMoveStock`, `CanApproveCount`, `CanViewReports`, `CanViewAudit`. Controller'larda rol adı değil politika adı kullanılır.

**Ekranlar**

- [ ] Giriş sayfası; başarılı girişte son ziyaret edilen sayfaya dönüş
- [ ] `AuthProvider`: access token bellekte; uygulama açılışında sessiz `/auth/refresh` denemesi; 401'de bir kez refresh + isteği tekrar et, başarısızsa girişe yönlendir
- [ ] Rol bazlı menü ve route koruması (yetkisiz sayfa → 403 ekranı)
- [ ] Kullanıcılar sayfası (Admin): tablo (ad, departman, rol, giriş yetkisi, durum), oluştur/düzenle modalı, pasifleştir, şifre sıfırla
- [ ] Profil menüsü: şifre değiştir, çıkış

### 1.2 Kategori ve ürün

**Veri**

| Alan | Kural |
| --- | --- |
| Category.`Name` | Zorunlu, ≤ 100, aktifler arasında büyük/küçük harf duyarsız benzersiz |
| Product.`Code` | `PRD-XXXXXX`, sistem üretir, değişmez |
| Product.`Name` | Zorunlu, ≤ 150 |
| Product.`Description` | Opsiyonel, ≤ 1000 |
| Product.`CategoryId` | Zorunlu, aktif kategori |
| Product.`Unit` | `Piece`, `Kg`, `Meter`, `Liter`, `Package`, `Box`; Serialized ürünlerde her zaman `Piece` |
| Product.`TrackingType` | `Serialized` ya da `Quantity`; stok veya hareket varsa değişmez |
| Product.`IsAssignable` | Sadece `Serialized` ürünlerde `true` olabilir |
| Product.`MinStockLevel` | Opsiyonel, ≥ 0; Serialized'da tam sayı |
| ProductBarcode.`Value` | Zorunlu, ≤ 64, boşluklar silinir, sistem genelinde benzersiz |
| ProductBarcode.`Format` | `Ean13`, `Ean8`, `UpcA`, `Code128`, `Other`; EAN/UPC'de kontrol hanesi doğrulanır |

**Kurallar**

- Stoğu (bakiye > 0 ya da `InStock`/`Assigned`/`InRepair` Item) olan ürün pasifleştirilemez
- İçinde aktif ürün olan kategori pasifleştirilemez
- Pasif ürün listelerde varsayılan olarak gizlenir, taramada "pasif" etiketiyle görünür, yeni harekete giremez

**Ekranlar**

- [ ] Kategoriler: basit tablo + modal
- [ ] Ürün listesi: ad/kod/barkod ile arama, kategori ve takip tipi filtresi, "pasifleri göster", sayfa başı 25
- [ ] Ürün formu: takip tipi seçimi (radio); `IsAssignable` sadece Serialized seçiliyken aktif; stok varsa takip tipi kilitli ve açıklamalı
- [ ] Barkod yönetimi ürün formunun içinde: ekle (elle ya da kamerayla okutarak), sil
- [ ] Ürün detayı sekmeleri: Genel · Stok · Item'lar (Serialized) · Hareketler

### 1.3 Lokasyon ağacı

**Veri**

| Alan | Kural |
| --- | --- |
| `Code` | `LOC-XXXXXX`, değişmez |
| `Name` | Zorunlu, ≤ 100; aynı üst lokasyon altında benzersiz |
| `Type` | `Building`, `Room`, `Cabinet`, `Shelf`, `Box`, `Other`; tipler arası sıralama zorunluluğu yok |
| `ParentId` | Boşsa kök lokasyon |
| `Path` | `/{id}/{id}/…/`, sistem tutar |
| `Depth` | Kök = 0; en fazla 8 |

**Kurallar**

- Taşıma: hedef, lokasyonun kendisi ya da alt ağacındaki bir düğüm olamaz; derinlik sınırı aşılamaz
- Taşımada alt ağacın `Path` ve `Depth` değerleri tek SQL `UPDATE` ile, aynı transaction'da güncellenir
- Pasifleştirme: alt ağaçta stok ya da Item varsa reddedilir; boşsa alt lokasyonlarıyla birlikte pasifleşir
- `Path` üzerinde `text_pattern_ops` indeksi; alt ağaç sorgusu `Path LIKE '/…/{id}/%'`

**Ekranlar**

- [ ] Lokasyonlar sayfası: Mantine Tree ile ağaç (aç/kapa, arama), seçili düğüm için sağ panel
- [ ] Ekle (seçili düğümün altına), yeniden adlandır, taşı (hedef seçici modal), pasifleştir
- [ ] Lokasyon detayı: breadcrumb (Bina › Oda › Dolap › Raf), içerik tablosu, "alt lokasyonlar dahil" anahtarı, alt lokasyon listesi

### 1.4 Tarama

**Kod üretimi**

- [ ] `ICodeGenerator`: ön ek + `-` + 6 karakter Crockford Base32, `RandomNumberGenerator` ile (yaklaşık 1 milyar olasılık); benzersiz index ihlalinde en fazla 3 kez yeniden dener

**Çözümleme kuralları (`GET /api/scan/{input}`)**

1. Girdi bir URL ise `/s/{kod}` kısmı alınır
2. Baştaki ve sondaki boşluklar silinir, büyük harfe çevrilir
3. `PRD`, `ITM` ya da `LOC` ile başlıyorsa: tire yoksa eklenir (`ITM7F3K9Q` → `ITM-7F3K9Q`), kod kısmında `O` → `0`, `I`/`L` → `1` düzeltmesi yapılır, ilgili tabloda aranır
4. Ön ek yoksa `ProductBarcode` tablosunda aranır
5. Bulunamazsa 404; girdi geçerli bir EAN/UPC ise cevapta `suggestCreateProduct: true` döner

Cevap: `type` (`product` / `item` / `location`), `id`, `code`, `title`, `subtitle` (örn. Item için ürün adı + konum), `isActive`, `status` (Item için).

**QR içeriği ve yönlendirme**

- [ ] QR'a `{PUBLIC_BASE_URL}/s/{kod}` yazılır; `PUBLIC_BASE_URL` ortam değişkenidir (şimdilik tünel adresi, deploy'da gerçek alan adı)
- [ ] Frontend'de `/s/:code` rotası: giriş yapılmamışsa önce login, sonra scan çağrısı ve ilgili detay sayfasına yönlendirme. Böylece telefonun normal kamerasıyla okutmak da çalışır

**Tarayıcı bileşeni**

- [ ] `ScanDialog` iki modda: tekli (bir okuma sonrası kapanır) ve sürekli (art arda okuma; transfer ve sayımda kullanılır)
- [ ] Arka kamera varsayılan; kamera değiştirme; destekleniyorsa fener (torch) düğmesi
- [ ] Başarılı okumada titreşim (`navigator.vibrate`) ve kısa ses; aynı kod 2 saniye içinde tekrar okunursa yok sayılır
- [ ] Kamera izni reddedilirse elle giriş alanı açık kalır ve izin talimatı gösterilir
- [ ] Üst barda her sayfadan erişilen "Tara" düğmesi; 404'te "Kayıt bulunamadı" ve (EAN ise) "Bu barkodla yeni ürün oluştur" kısayolu

### 1.5 Stok hareketleri

**Veri**

| Alan | Kural |
| --- | --- |
| `Type` | `In`, `Out`, `Transfer`, `Adjustment`, `Assign`, `Return` |
| `ProductId` | Her zaman dolu |
| `ItemId` | Serialized hareketlerde zorunlu, Quantity'de boş |
| `FromLocationId`, `ToLocationId` | Tip tablosundaki kurallara göre |
| `Quantity` | `numeric(18,3)`, > 0; Serialized'da 1 |
| `Reason` | `Out` için zorunlu: `Consumed`, `Damaged`, `Lost`, `Other`; diğerlerinde opsiyonel |
| `Note` | Opsiyonel, ≤ 500; `Reason = Other` ise zorunlu |
| `ReferenceId` | Aynı işlemde oluşan hareketleri gruplar (Guid) |
| `UserId`, `OccurredAt` | Sistem doldurur |

**Komutlar**

| Komut | Girdi | Etki |
| --- | --- | --- |
| `StockIn` | Hedef lokasyon + satırlar. Quantity satırı: ürün + miktar. Serialized satırı: ürün + adet + opsiyonel seri no listesi | Quantity: bakiye artar (yoksa oluşur). Serialized: her adet için yeni Item (`InStock`) + bir `In` hareketi |
| `StockOut` | Satırlar: Item ya da (ürün + kaynak lokasyon + miktar) + sebep | Quantity: bakiye azalır. Serialized: Item `Disposed` olur, lokasyonu boşalır |
| `StockTransfer` | Hedef lokasyon + satırlar: Item ya da (ürün + kaynak lokasyon + miktar) | Bakiye kaynaktan hedefe geçer ya da Item'ın lokasyonu değişir |

**Kurallar**

- Bir komuttaki tüm satırlar tek transaction'dır; bir satır geçersizse hiçbiri yazılmaz ve hata hangi satırda olduğunu (`lines[2].quantity`) söyler
- Tek komutta en fazla 100 satır; Serialized girişte satır başına en fazla 100 adet
- Hedef ve kaynak lokasyon aktif olmalı; ürün aktif olmalı
- Transfer'de kaynak ve hedef aynı olamaz; Item sadece `InStock` ise transfer edilir
- Quantity bakiyesi eksiye düşemez; bakiye 0'a inerse satır silinmez, 0 olarak kalır
- Eşzamanlılık: StockBalance `xmin` ile korunur; çakışmada 409 ve "Stok bu arada değişti, tekrar deneyin" mesajı. Otomatik yeniden deneme yok
- Koordinasyon `StockMovementService`'te; handler sadece yükler, servisi çağırır, UoW ile kaydeder

**Ekranlar**

- [ ] Giriş formu (masaüstü): hedef lokasyon seçici (ağaçtan ya da okutarak), satır tablosu (ürün seç ya da okut, miktar/adet, seri no alanları), özet ve onay
- [ ] Çıkış formu: satırlar + sebep + not
- [ ] Transfer formu: hedef lokasyon + satırlar
- [ ] "Tara-taşı" mobil akışı: (1) hedef rafı okut, (2) sürekli modda ürünleri okut — Item anında listeye eklenir; Quantity üründe kaynak lokasyon (bakiyesi olanlar listelenir) ve miktar sorulur, (3) özet ve onay
- [ ] Başarılı işlem sonrası özet ekranı: oluşan Item kodları (Faz 3'te buradan etiket basılacak)

### 1.6 Detay ve geçmiş sayfaları

- [ ] Ürün detayı · Stok sekmesi: lokasyon yolu + miktar (Quantity) ya da durum bazında Item sayıları (Serialized)
- [ ] Ürün detayı · Item'lar sekmesi: kod, seri no, durum, konum; durum filtresi
- [ ] Item detayı: ürün, seri no (düzenlenebilir), durum rozeti, konum yolu, hareket zaman çizelgesi
- [ ] Lokasyon detayı: içerik (alt lokasyonlar dahil anahtarı), hareket geçmişi (giren + çıkan)
- [ ] Hareketler sayfası: tarih aralığı, tip, ürün, lokasyon, kullanıcı filtreleri; `ReferenceId`'ye göre gruplu görünüm; sayfa başı 25, en yeni üstte
- [ ] Geçmiş satırları insan diliyle: "Berat, 200 adet Vida M4'ü Raf A1'den Raf B2'ye taşıdı"

### API uçları

| Alan | Uçlar |
| --- | --- |
| Auth | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`, `POST /auth/change-password` |
| Kullanıcı | `GET /users`, `POST /users`, `PUT /users/{id}`, `POST /users/{id}/deactivate`, `POST /users/{id}/reset-password` |
| Kategori | `GET /categories`, `POST /categories`, `PUT /categories/{id}`, `POST /categories/{id}/deactivate` |
| Ürün | `GET /products`, `POST /products`, `GET /products/{id}`, `PUT /products/{id}`, `POST /products/{id}/deactivate`, `POST /products/{id}/barcodes`, `DELETE /products/{id}/barcodes/{barcodeId}`, `GET /products/{id}/stock` |
| Item | `GET /items`, `GET /items/{id}`, `PUT /items/{id}` (seri no) |
| Lokasyon | `GET /locations/tree`, `POST /locations`, `GET /locations/{id}`, `PUT /locations/{id}`, `POST /locations/{id}/move`, `POST /locations/{id}/deactivate`, `GET /locations/{id}/contents?includeChildren=` |
| Tarama | `GET /scan/{input}` |
| Hareket | `POST /movements/in`, `POST /movements/out`, `POST /movements/transfer`, `GET /movements` |

Liste uçlarının hepsi `page`, `pageSize` (≤ 100), `search`, `sort` parametrelerini ve aynı sayfalama cevabını (`items`, `totalCount`, `page`, `pageSize`) kullanır.

### Testler

- Domain: takip tipi kilidi, `IsAssignable ⇒ Serialized`, barkod kontrol hanesi, Item durum geçişleri, lokasyon döngü ve derinlik kontrolü, eksi bakiye
- Application: komut validator'ları, `StockMovementService` senaryoları (mock'suz, in-memory aggregate'lerle)
- Integration (Testcontainers PostgreSQL): login → refresh rotation → çalıntı token tespiti; giriş → transfer → çıkış sonrası bakiye ve geçmiş; eşzamanlı iki çıkışta biri 409; lokasyon taşımada alt ağaç path'leri; tarama çözümleme (URL, tiresiz kod, barkod, 404)

### Kabul kriteri (demo senaryosu)

1. Admin giriş yapar, bir Depo Sorumlusu ve bir `CanLogin = false` personel oluşturur
2. Depo Sorumlusu "Laptop" (Serialized, zimmetlenebilir, EAN barkodlu) ve "Vida M4" (Quantity, min 100) ürünlerini tanımlar
3. Bina › Oda › Dolap › Raf A1 ve Raf B2 ağacını kurar
4. 3 laptop (seri numaralı) ve 500 vidayı Raf A1'e girer
5. Telefondan tara-taşı akışıyla Raf B2'yi okutup 200 vidayı ve 1 laptopu taşır
6. Laptopun kutusundaki EAN'ı okutunca ürün, laptopun QR'ını okutunca o adet açılır ve geçmişi görünür
7. Raf A1'in QR'ı telefonun normal kamerasıyla okutulunca uygulamada lokasyon detayı açılır

## Faz 2 — Zimmet, Item durumları ve sayım

Amaç: kimin elinde ne olduğunu bilmek, arızalı ürünleri izlemek ve rafın gerçek durumunu sistemle karşılaştırmak. Üç süreç de Faz 1'in hareket defterine yazar; stoğu değiştiren yeni bir yol açılmaz.

### 2.1 Zimmet

**Veri (Assignment)**

| Alan | Kural |
| --- | --- |
| `ItemId` | Zorunlu; ürünü `IsAssignable` olmalı |
| `AssigneeUserId` | Zorunlu; aktif kullanıcı (`CanLogin` şart değil) |
| `AssignedByUserId`, `AssignedAt` | Sistem doldurur |
| `DueDate` | Opsiyonel; bugünden sonra olmalı |
| `Note` | Opsiyonel, ≤ 500 |
| `ReturnedAt`, `ReturnedByUserId`, `ReturnLocationId` | İadede dolar |
| `ReturnCondition` | `Good` ya da `Faulty`; iadede zorunlu |
| `ReturnNote` | Opsiyonel, ≤ 500; `Faulty` ise zorunlu |

**Kurallar**

- Zimmet sadece `InStock` Item'a verilir; Item'ın aynı anda tek açık zimmeti olur (veritabanında `ReturnedAt IS NULL` için kısmi benzersiz index)
- Zimmet verilince Item `Assigned` olur, lokasyonu boşalır; `Assign` hareketinin kaynağı Item'ın son lokasyonudur
- İadede Item seçilen lokasyona girer; `Good` → `InStock`, `Faulty` → `InRepair`; `Return` hareketi yazılır
- Zimmet devri yoktur: başka birine geçecekse önce iade, sonra yeni zimmet
- Gecikme durumu saklanmaz, hesaplanır: `ReturnedAt` boş ve `DueDate` < bugün
- Açık zimmeti olan kullanıcı pasifleştirilemez; açık zimmeti olan üründe `IsAssignable` kapatılamaz

**Ekranlar**

- [ ] Zimmet ver (mobil uyumlu): Item'ı okut → kullanıcı seç (ad/departman araması) → teslim tarihi ve not → onay
- [ ] İade al: Item'ı okut → iade lokasyonu (ağaçtan seç ya da rafı okut) → durum (Sağlam/Arızalı) ve not → onay
- [ ] Zimmetler sayfası (Depo Sorumlusu): açık, gecikmiş, iade edilmiş filtreleri; kullanıcı ve ürün araması
- [ ] Zimmetlerim sayfası (herkes): kendi açık zimmetleri, teslim tarihleri, gecikme rozeti
- [ ] Item ve kullanıcı detayında zimmet geçmişi sekmesi

### 2.2 Item durum işlemleri

Faz 1'de `InRepair` durumu tanımlandı ama ona girip çıkmanın yolu yoktu; bu adım onu tamamlar.

- [ ] Yeni hareket tipi `StatusChange`: lokasyon değişmez, miktar 1, `Note` zorunlu; hareket satırında eski ve yeni durum saklanır (`FromStatus`, `ToStatus` alanları — sadece bu tipte dolu)
- [ ] İşlemler: "Tamire gönder" (`InStock` → `InRepair`), "Tamirden döndü" (`InRepair` → `InStock`). Hurdaya ayırma Faz 1'deki `Out` (sebep `Damaged`) ile yapılmaya devam eder
- [ ] Item detayında durum işlem düğmeleri; sadece geçerli geçişler görünür (State pattern'den okunur)
- [ ] Domain modeli bölümündeki hareket tipleri tablosuna `StatusChange` eklenir

### 2.3 Sayım

**Veri**

| Entity | Alanlar |
| --- | --- |
| CountSession | `LocationId`, `IncludeChildren`, `Status` (`Counting`, `Reviewing`, `Approved`, `Cancelled`), `StartedBy`, `StartedAt`, `ApprovedBy`, `ApprovedAt`, `Note` |
| CountLine | `LocationId` (sayıldığı lokasyon), `ProductId`, `ItemId?`, `ExpectedQuantity` (başlangıç snapshot'ı), `CountedQuantity?`, `LineType` (`Expected`, `Unexpected`), `Resolution?` |

**Akış**

1. Başlat: lokasyonu okut, alt lokasyonlar dahil mi seç. O kapsamdaki tüm bakiyeler ve `InStock`/`InRepair` Item'lar `Expected` satırı olarak yazılır
2. Say: sayan kişi önce bulunduğu lokasyonu okutur (bağlam), sonra ürünleri sürekli modda okutur. Item okutulunca o satır sayıldı (1) işaretlenir. Quantity ürün okutulunca miktar girilir; aynı ürün tekrar okutulursa miktar mevcut sayıma eklenir
3. Kapsamda beklenmeyen bir Item okutulursa `Unexpected` satırı açılır ve nerede görünmesi gerektiği gösterilir; zimmetli ya da hurdaya ayrılmış bir Item ise ayrıca uyarı verilir
4. İncelemeye gönder: durum `Reviewing` olur, sayım kilitlenir
5. Onay ekranı: her fark satırı için karar verilir (aşağıdaki tablo). Kararı verilmemiş satır varken onaylanamaz
6. Onay: kararlar hareketlere dönüşür, hepsi aynı `ReferenceId` ile tek transaction'da yazılır

**Fark kararları**

| Durum | Seçenekler | Oluşan hareket |
| --- | --- | --- |
| Quantity, sayılan ≠ güncel bakiye | Farkı uygula · Dokunma | `Adjustment` (artış ya da azalış) |
| Quantity, hiç sayılmadı | 0 kabul et · Kapsam dışı | 0 kabul edilirse `Adjustment` |
| Beklenen Item okutulmadı | Kayıp olarak çıkar · Dokunma | `Out` (sebep `Lost`) |
| Beklenmeyen Item okutuldu (`InStock`) | Buraya taşı · Dokunma | `Transfer` |
| Beklenmeyen Item zimmetli ya da `Disposed` | Sadece not düşülür | — |

**Kurallar**

- Quantity farkı onay anındaki güncel bakiyeye göre hesaplanır (snapshot'a göre değil); sayım başladıktan sonra kapsamda hareket olduysa onay ekranında uyarı ve hareket listesi gösterilir
- Sayım sırasında hareketler engellenmez
- Kapsamı çakışan (aynı ya da üst/alt lokasyon) iki açık sayım olamaz
- `Counting` ya da `Reviewing` durumundaki sayım iptal edilebilir; iptal hiçbir hareket üretmez
- Personel sayabilir; başlatma, iptal ve onay `CanApproveCount` ister

**Ekranlar**

- [ ] Sayımlar listesi: durum filtresi, lokasyon, başlatan, tarih
- [ ] Sayım ekranı (mobil öncelikli): üstte bağlam lokasyonu, sürekli tarayıcı, sayılan / beklenen ilerleme çubuğu, son okutulanlar listesi (geri al düğmesiyle)
- [ ] Onay ekranı (masaüstü): fark tablosu, satır başına karar seçimi, "tümüne uygula" kısayolu, hareket uyarıları
- [ ] Sayım sonuç raporu: sayılan, eşleşen, fark sayısı, üretilen hareketler

### API uçları

| Alan | Uçlar |
| --- | --- |
| Zimmet | `POST /assignments`, `POST /assignments/{id}/return`, `GET /assignments?status=open\|overdue\|returned&userId=&productId=`, `GET /assignments/mine`, `GET /assignments/{id}` |
| Item durumu | `POST /items/{id}/send-to-repair`, `POST /items/{id}/return-from-repair` |
| Sayım | `POST /counts`, `GET /counts`, `GET /counts/{id}`, `POST /counts/{id}/scan` (bağlam lokasyonu + kod + miktar), `DELETE /counts/{id}/lines/{lineId}/last-scan`, `POST /counts/{id}/submit`, `GET /counts/{id}/review`, `POST /counts/{id}/approve` (satır kararlarıyla), `POST /counts/{id}/cancel` |

### Testler

- Domain: zimmetlenemez ürüne zimmet reddi, çift açık zimmet reddi, zimmetli Item transfer reddi, durum geçişleri, sayım durum makinesi, kararsız satırla onay reddi
- Integration: zimmet → arızalı iade → `InRepair` → tamirden döndü → `InStock`; sayım sırasında araya giren transfer sonrası farkın doğru hesaplanması; kayıp Item onayında `Out` hareketi; çakışan sayım reddi

### Kabul kriteri (demo senaryosu)

1. Depo Sorumlusu bir laptopu `CanLogin = false` personele, 1 hafta teslim tarihiyle zimmetler
2. Giriş yapabilen bir Personel'e başka bir laptop zimmetlenir; Personel "Zimmetlerim"de görür
3. Laptop arızalı olarak iade edilir (`InRepair`), sonra "Tamirden döndü" ile `InStock` olur; tüm adımlar Item geçmişinde görünür
4. Raf B2 sayılır: 200 yerine 185 vida sayılır, rafta olmaması gereken bir laptop okutulur
5. Onayda vida farkı uygulanır, laptop "Buraya taşı" ile transfer edilir; geçmişte `Adjustment` ve `Transfer` aynı referansla görünür

## Faz 3 — Audit, fotoğraf, etiket, uyarı, dashboard

Amaç: sistemi "veri tutan" halden "kullanılan ürün" haline getirmek. Adımlar birbirinden bağımsızdır ama bu sırayla yapılır, çünkü audit her sonraki adımın değişikliklerini de kaydeder.

### 3.1 Audit log

**Veri:** `EntityType`, `EntityId`, `Action` (`Created`, `Updated`, `Deactivated`, `Reactivated`), `Changes` (jsonb: `{ alan: { old, new } }`), `UserId`, `CreatedAt`. İndeksler: (`EntityType`, `EntityId`), `CreatedAt`.

- [ ] `AuditInterceptor` (`SaveChangesInterceptor`): `ChangeTracker`'daki eklenen ve değişen entity'leri okur, AuditLog satırlarını aynı `SaveChanges` içinde ekler (aynı transaction)
- [ ] Kapsam dışı entity'ler: StockMovement (zaten defter), AuditLog, RefreshToken, Notification
- [ ] Kapsam dışı alanlar: `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `TokenHash`, `UpdatedAt`, `UpdatedBy`
- [ ] `IsActive` true → false değişimi `Deactivated`, tersi `Reactivated` olarak yazılır
- [ ] Değişen alan yoksa (sadece audit alanları değiştiyse) satır yazılmaz
- [ ] Ekranlar: ürün, Item, lokasyon, kullanıcı detaylarında "Değişiklik geçmişi" sekmesi (eski → yeni, kim, ne zaman); Admin için tüm kayıtlar sayfası (entity tipi, kullanıcı, tarih filtresi)

### 3.2 Fotoğraf

**Veri (ProductPhoto):** `ProductId`, `StorageKey`, `ThumbnailKey`, `ContentType`, `SizeBytes`, `SortOrder`, `IsPrimary`.

- [ ] `IFileStorage`: `SaveAsync`, `DeleteAsync`, `GetReadUrl(key, süre)`. İki uygulama: `LocalFileStorage` (Docker `uploads` volume'ü) ve `R2FileStorage` (deploy'da); `StorageOptions.Provider` ile seçilir
- [ ] Yükleme akışı: multipart → boyut (≤ 5 MB) ve dosya imzası (magic bytes) kontrolü, uzantıya güvenilmez → SkiaSharp ile EXIF yönü düzeltilir ve EXIF silinir → uzun kenarı 1600 px'e küçültülmüş ana görsel + 400 px thumbnail, ikisi de WebP
- [ ] Okuma: access token bellekte olduğu için `<img>` etiketi kimlik gönderemez. Bu yüzden API, 1 saat geçerli imzalı URL döner: yerel depolamada HMAC imzalı `/api/files/{key}?exp=&sig=`, R2'de presigned URL
- [ ] Kurallar: ürün başına en fazla 10 fotoğraf; ilk yüklenen otomatik ana fotoğraf; ana fotoğraf silinirse sıradaki ana olur
- [ ] Fotoğraflar soft delete kuralının istisnasıdır: hiçbir hareket onlara referans vermediği için dosya ve satır fiziksel olarak silinir
- [ ] Ekranlar: ürün formunda galeri (yükle, sırala, ana yap, sil); telefonda `accept="image/*" capture="environment"` ile doğrudan kamera; tarama sonucunda ve listelerde thumbnail

### 3.3 Toplu etiket basımı

| Şablon | Kullanım | İçerik |
| --- | --- | --- |
| A4 3x8 (70x37 mm) | Ürün ve Item | QR (28 mm) + kod + ad (2 satır, kısaltılır) + Item'da seri no |
| A4 2x4 (105x74 mm) | Raf, dolap, oda | QR (50 mm) + kod + ad (büyük) + üst lokasyon yolunun son iki seviyesi |
| A4 5x13 (38x21 mm) | Küçük ürünler | QR (17 mm) + kod |
| Termal rulo 50x30 mm | Etiket yazıcısı | QR (24 mm) + kod + ad (1 satır) |

- [ ] QR: QRCoder, hata düzeltme seviyesi M, en az 4 modüllük sessiz alan; içerik `{PUBLIC_BASE_URL}/s/{kod}`
- [ ] PDF: QuestPDF; ölçüler milimetre olarak sabit; yazdırma notu: "Gerçek boyut / %100 ölçek"
- [ ] "Başlangıç pozisyonu" seçeneği: A4 şablonlarda yarısı kullanılmış etiket kâğıdı için baştan kaç hücrenin boş bırakılacağı
- [ ] Seçim kaynakları: ürün listesinden çoklu seçim, bir girişin `ReferenceId`'si (o girişte oluşan tüm Item'lar), bir lokasyon alt ağacı, tek kayıt detay sayfası
- [ ] Bir istekte en fazla 500 etiket
- [ ] Faz 1'deki giriş özet ekranına "Etiketleri yazdır" düğmesi eklenir

### 3.4 Kritik stok ve gecikme uyarıları

**Veri (Notification):** `UserId`, `Type` (`LowStock`, `AssignmentDueSoon`, `AssignmentOverdue`), `Title`, `Message`, `EntityType`, `EntityId`, `DedupKey`, `IsRead`, `CreatedAt`, `ResolvedAt`.

| Uyarı | Tetik | Alıcı | Kapanış |
| --- | --- | --- | --- |
| Kritik stok | Toplam stok < `MinStockLevel`. Toplam = Quantity'de tüm bakiyelerin toplamı, Serialized'da `InStock` Item sayısı | Admin ve Depo Sorumluları | Stok minimumun üstüne çıkınca otomatik |
| Teslim yaklaşıyor | Teslim tarihine 1 gün kala | Zimmet alan (`CanLogin` ise) | İade edilince |
| Gecikmiş zimmet | Teslim tarihi geçti, iade yok | Zimmet alan (`CanLogin` ise) + Depo Sorumluları | İade edilince |

- [ ] Anlık kontrol: toplam stoğu değiştiren hareketler (`Out`, `Adjustment`, `In`) commit edildikten sonra MediatR notification ile ilgili ürün kontrol edilir; `Transfer` toplamı değiştirmediği için tetiklemez
- [ ] Günlük kontrol: `BackgroundService` + `PeriodicTimer`, her gün 08:00 (Europe/Istanbul); tüm ürünler ve açık zimmetler taranır
- [ ] Tekrar önleme: aynı `DedupKey` (örn. `LowStock:{productId}`) ile açık bildirim varsa yenisi üretilmez
- [ ] Frontend: üst barda okunmamış sayısıyla zil, açılır liste, "tümünü okundu yap", bildirimler sayfası; yenileme 60 saniyede bir sorgulama ile (gerçek zamanlı bağlantı yok)

### 3.5 Dashboard ve raporlar

**Dashboard**

- [ ] Kartlar: aktif ürün sayısı, toplam Item, kritik stoktaki ürün sayısı, açık zimmet, gecikmiş zimmet, tamirdeki Item
- [ ] Grafik 1: son 30 günün günlük hareket sayısı, tipe göre yığılmış sütun
- [ ] Grafik 2: son 30 günde en çok hareket gören 10 ürün (yatay sütun)
- [ ] Grafik 3: Item durum dağılımı (`InStock`, `Assigned`, `InRepair`)
- [ ] Liste: kritik stoktaki ürünler (eksik miktarla), en eski gecikmiş zimmetler
- [ ] Tüm sorgular `IAppDbContext` + projection; dashboard tek uçtan (`GET /dashboard`) döner

**Excel raporları (ClosedXML)**

| Rapor | Kolonlar |
| --- | --- |
| Stok durumu | Ürün kodu, ad, kategori, takip tipi, birim, toplam, minimum, kritik mi · ikinci sayfada lokasyon bazında dağılım |
| Hareket dökümü (tarih aralığı) | Tarih, tip, ürün, Item kodu, seri no, kaynak, hedef, miktar, kullanıcı, sebep, not, referans |
| Zimmet listesi | Item kodu, ürün, seri no, alan kişi, departman, veriliş, teslim, iade, durum (açık/gecikmiş/iade edildi) |

- [ ] Tarihler veritabanında UTC, ekranda ve raporlarda Europe/Istanbul saatiyle gösterilir
- [ ] Rapor başına en fazla 50.000 satır; aşılırsa tarih aralığını daraltma uyarısı

### API uçları

| Alan | Uçlar |
| --- | --- |
| Audit | `GET /audit?entityType=&entityId=&userId=&from=&to=` |
| Fotoğraf | `POST /products/{id}/photos`, `PUT /products/{id}/photos/order`, `POST /products/{id}/photos/{photoId}/primary`, `DELETE /products/{id}/photos/{photoId}`, `GET /files/{key}` (imzalı) |
| Etiket | `POST /labels` (şablon, hedefler, başlangıç pozisyonu) → PDF |
| Bildirim | `GET /notifications`, `GET /notifications/unread-count`, `POST /notifications/{id}/read`, `POST /notifications/read-all` |
| Dashboard ve rapor | `GET /dashboard`, `GET /reports/stock.xlsx`, `GET /reports/movements.xlsx?from=&to=`, `GET /reports/assignments.xlsx` |

### Testler

- Audit: ürün adı değişince tek satır ve doğru eski/yeni değer; şifre alanı asla yazılmıyor
- Fotoğraf: uzantısı `.jpg` olup içeriği farklı dosya reddediliyor; süresi geçmiş imzalı URL 403
- Etiket: her şablon için PDF üretiliyor ve sayfa sayısı doğru (örn. 30 etiket 3x8'de 2 sayfa)
- Uyarı: minimum altına inen ürün tek bildirim üretiyor; tekrar çıkışta ikinci bildirim yok; stok geri gelince kapanıyor

### Kabul kriteri (demo senaryosu)

1. 3 yeni laptop girilir, özet ekranından 3x8 etiket PDF'i basılır ve telefonla okunur
2. Raf B2 için 2x4 etiket basılır; yarı kullanılmış kâğıda başlangıç pozisyonu 5 ile yazdırılır
3. Vida çıkışıyla stok minimumun altına iner; Depo Sorumlusunun zilinde bildirim görünür
4. Laptopa telefondan fotoğraf eklenir, taramada thumbnail görünür
5. Ürün adı değiştirilir; audit sekmesinde eski ve yeni değer görünür
6. Dashboard bu hareketleri gösterir; hareket dökümü Excel olarak indirilir

## Faz 4 — Sağlamlaştırma, demo ortamı, portfolyo

Amaç: projeyi bir işe alımcının 5 dakikada anlayıp deneyebileceği hale getirmek ve deploy'a hazır bırakmak. Yeni özellik eklenmez. Deploy bu planın dışındadır; bu faz sadece deploy'u tek adımlık bir iş haline getirir.

### 4.1 Test boşlukları

- [ ] Tüm domain kurallarının unit testi var (Domain modeli bölümündeki "Kurallar" kolonu kontrol listesi olarak kullanılır)
- [ ] Her API ucunun en az bir integration testi var: başarılı senaryo + yetkisiz rol (403)
- [ ] Frontend: Vitest + React Testing Library ile kritik bileşenler (`ScanDialog` elle giriş akışı, `AuthProvider` refresh akışı, hareket formu doğrulamaları)
- [ ] CI'da integration testleri de çalışır (Testcontainers GitHub Actions'ta Docker ile)

### 4.2 Performans

- [ ] Yük verisi scripti: 1.000 ürün, 5.000 Item, 200 lokasyon, 50.000 hareket
- [ ] Hedef: bu veriyle liste uçları ve `/dashboard` yerelde 300 ms altında cevap verir
- [ ] İndeks kontrolü: `stock_movements` (`product_id`, `occurred_at`), (`item_id`), (`from_location_id`), (`to_location_id`), (`reference_id`); `items` (`product_id`, `status`); `audit_logs` (`entity_type`, `entity_id`)
- [ ] Yavaş sorgular `EXPLAIN ANALYZE` ile incelenir; N+1 olan yerler projection'a çevrilir

### 4.3 Güvenlik gözden geçirme

- [ ] Her controller action'ında politika var; politikasız uç kalmadığını doğrulayan bir test (reflection ile)
- [ ] Login için rate limit: IP başına dakikada 10 deneme (ASP.NET Core rate limiter)
- [ ] Güvenlik başlıkları Nginx'te: `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` (kamera sadece kendi origin'i)
- [ ] Production modunda Swagger kapalı, ProblemDetails'te stack trace yok
- [ ] Repoda secret olmadığı kontrol edilir (`gitleaks` CI adımı)

### 4.4 Seed ve demo verisi

- [ ] `docker compose run --rm api seed-demo` komutu: "Demo Atölye" senaryosu
- [ ] İçerik: Bina › 3 oda › dolaplar › raflar ağacı; 8 kategori; 40 ürün (yaklaşık yarısı Serialized); 150 Item; 6 ay geriye yayılmış hareketler; 10 zimmet (2'si gecikmiş); 1 onaylanmış sayım; 2 kritik stok ürünü
- [ ] Demo hesapları: `admin@demo`, `depo@demo`, `personel@demo` (şifreler README'de)
- [ ] `reset-demo` komutu: veriyi silip seed'i yeniden kurar (sadece `DEMO_MODE=true` iken çalışır)

### 4.5 Production benzeri Docker ve deploy hazırlığı

- [ ] `docker-compose.prod.yml` override: `ASPNETCORE_ENVIRONMENT=Production`, kaynak limitleri, `restart: unless-stopped`, log rotasyonu
- [ ] İmaj etiketleri git tag'i ile (`v1.0.0`); CI tag'de imajları GitHub Container Registry'ye push eder
- [ ] Deploy'da değişecek ayarların listesi `docs/deploy.md`'de: `PUBLIC_BASE_URL`, CORS origin'i, `StorageOptions.Provider=R2` + R2 anahtarları, JWT anahtarı, gerçek alan adı ya da kalıcı cloudflared tüneli
- [ ] Yerelde production modunda uçtan uca deneme: `docker compose -f docker-compose.yml -f docker-compose.prod.yml up` ile Faz 1–3 kabul senaryoları tekrar çalıştırılır

### 4.6 Portfolyo

- [ ] README: tek cümlelik özet, ekran görüntüleri, telefonda tarama GIF'i, özellik listesi, mimari şeması, "hareket defteri" tasarım kararının kısa anlatımı, yerelde çalıştırma adımları (3 komut), demo hesapları
- [ ] `docs/adr/` altında ADR'ler: 001 karma ürün modeli, 002 hareket defteri tek doğruluk kaynağı, 003 Repository + UoW yazma / IAppDbContext okuma ayrımı, 004 materialized path, 005 token saklama stratejisi, 006 QR içeriği olarak URL
- [ ] `docs/api.md`: uç listesi ve örnek istek/cevaplar (Swagger'dan)
- [ ] GitHub repo açıklaması, konu etiketleri, sabitlenmiş repo; CV'ye 2–3 satırlık proje özeti

### 4.7 Opsiyonel (sadece 4.1–4.6 bittiyse)

- [ ] Kategori ağacı
- [ ] PWA: ana ekrana ekleme, uygulama ikonu
- [ ] Karanlık tema
- [ ] İngilizce dil desteği

### Kabul kriteri

Production modundaki Docker kurulumunda seed verisiyle tüm faz senaryoları geçiyor; tüm testler CI'da yeşil; performans hedefi tutuyor; README'yi okuyan biri 3 komutla projeyi kendi makinesinde ayağa kaldırıp telefonundan bir etiketi okutabiliyor; ADR'ler yazılı.

## Çalışma kuralları

Planın işe yaraması bu kurallara uymana bağlı. En sık bozulan kural, bir faz bitmeden sonrakinden "küçük bir şey" eklemektir.

### Bir görev ne zaman "bitti" sayılır

1. Backend ucu + frontend ekranı birlikte çalışıyor (yalnızca backend bitmiş görev bitmemiştir)
2. Domain kuralı varsa unit testi var; akış varsa en az bir integration testi var
3. Yetki politikası uygulanmış
4. Migration varsa adı anlamlı ve harici DB'ye uygulanmış
5. PR açılmış, CI yeşil, `main`'e merge edilmiş

### Git akışı

- Her alt başlık (örn. 1.3 Lokasyon ağacı) bir feature branch: `feature/1.3-location-tree`
- Commit mesajları Conventional Commits: `feat:`, `fix:`, `test:`, `refactor:`, `docs:`
- Her faz sonunda tag: `v0.1` (Faz 1), `v0.2` (Faz 2), `v0.3` (Faz 3), `v1.0` (Faz 4)

### Kapsam disiplini

- Aklına gelen yeni fikirler doğrudan koda değil, bu dokümanın sonuna "Sonraki fikirler" başlığına yazılır
- Bir karar değişirse önce "Özet ve kararlar" tablosu güncellenir, sonra kod
- Bir faz içindeki adımların sırası değiştirilmez; adımlar birbirine bağımlı sıralandı (örn. tarama, lokasyon ve ürün kodları olmadan test edilemez)
- Takıldığın yerde bir sonraki adıma geçme; o adımı daraltarak bitir

### Sonraki fikirler

- (boş)
