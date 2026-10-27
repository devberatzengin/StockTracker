using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common.Codes;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Identity;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Seeding;

/// <summary>
/// "Demo Atölye" scenario (PLAN 4.4): a workshop building with 3 rooms, 8 categories, 40 products,
/// 150 items, six months of movements, 10 open assignments (2 overdue), 1 approved count and
/// 2 products below their minimum. Every movement goes through the domain services.
/// </summary>
public sealed class DemoSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager,
    StockMovementService movements,
    CountApprovalService countApproval,
    LowStockMonitor lowStock,
    AssignmentReminderMonitor reminders,
    IDateTime dateTime,
    IServiceProvider services,
    ILogger<DemoSeeder> logger)
{
    /// <summary>Shared by the demo accounts and printed in the README; demo data only.</summary>
    public const string Password = "Demo12345";
    public const string AdminEmail = "admin@demo";
    public const string WarehouseEmail = "depo@demo";
    public const string StaffEmail = "personel@demo";

    private const int HistoryDays = 180;
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    private static readonly string[] CategoryNames =
    [
        "Bilgisayar ve Çevre Birimleri", "Ölçüm Cihazları", "Elektrikli Aletler", "El Aletleri",
        "Elektronik Komponent", "Kablo ve Bağlantı", "Sarf Malzeme", "İş Güvenliği",
    ];

    private static readonly ProductSpec[] Specs =
    [
        // Serialized (150 items).
        new("Dizüstü Bilgisayar", 0, Unit.Piece, true, 4, Items: 12, Secure: true),
        new("Masaüstü Bilgisayar", 0, Unit.Piece, false, null, Items: 6),
        new("Monitör 24\"", 0, Unit.Piece, true, null, Items: 20),
        new("Tablet", 0, Unit.Piece, true, null, Items: 8, Secure: true),
        new("El Tipi Barkod Okuyucu", 0, Unit.Piece, true, null, Items: 6),
        new("Dijital Multimetre", 1, Unit.Piece, true, 3, Items: 12),
        new("Osiloskop", 1, Unit.Piece, true, null, Items: 4, Secure: true),
        new("Pens Ampermetre", 1, Unit.Piece, true, null, Items: 6),
        new("Lazer Mesafe Ölçer", 1, Unit.Piece, true, null, Items: 5),
        new("Termal Kamera", 1, Unit.Piece, true, 2, Items: 2, Secure: true),
        new("Akülü Matkap", 2, Unit.Piece, true, 3, Items: 14),
        new("Avuç Taşlama", 2, Unit.Piece, true, null, Items: 6),
        new("Dekupaj Testere", 2, Unit.Piece, true, null, Items: 4),
        new("Lehim İstasyonu", 2, Unit.Piece, true, 2, Items: 8),
        new("Sıcak Hava Tabancası", 2, Unit.Piece, true, null, Items: 5),
        new("Tork Anahtarı", 3, Unit.Piece, true, null, Items: 6),
        new("Takım Çantası", 3, Unit.Piece, true, null, Items: 16, Counted: true),
        new("3D Yazıcı", 0, Unit.Piece, false, null, Items: 3),
        new("Projeksiyon Cihazı", 0, Unit.Piece, true, null, Items: 3),
        new("Kaynak Makinesi", 2, Unit.Piece, false, null, Items: 4),

        // Quantity.
        new("Direnç Seti 1/4 W", 4, Unit.Box, false, 5, Stock: 30),
        new("Seramik Kondansatör Seti", 4, Unit.Box, false, 5, Stock: 25),
        new("Arduino Uno R3", 4, Unit.Piece, false, 10, Stock: 60),
        new("Breadboard 830 Delik", 4, Unit.Piece, false, 10, Stock: 50),
        new("Jumper Kablo Seti", 5, Unit.Package, false, 10, Stock: 60),
        new("NYA Kablo 1,5 mm²", 5, Unit.Meter, false, 100, Stock: 600),
        new("Cat6 Ethernet Kablosu", 5, Unit.Meter, false, 50, Stock: 400),
        new("Kablo Bağı 200 mm", 5, Unit.Package, false, 10, Stock: 80, Barcode: "869100000001"),
        new("Lehim Teli 0,8 mm", 6, Unit.Kg, false, 2, Stock: 12),
        new("İzopropil Alkol", 6, Unit.Liter, false, 5, Stock: 30, Barcode: "869100000002"),
        new("A4 Fotokopi Kağıdı", 6, Unit.Package, false, 10, Stock: 80, Barcode: "869100000003"),
        new("AA Alkalin Pil", 6, Unit.Package, false, 10, Stock: 70, Barcode: "869100000004"),
        new("Matkap Ucu Seti", 3, Unit.Box, false, 3, Stock: 15),
        new("Zımpara Kağıdı P120", 6, Unit.Package, false, 10, Stock: 60),
        new("Nitril Eldiven", 7, Unit.Box, false, 20, Stock: 120, Barcode: "869100000005"),
        new("Koruyucu Gözlük", 7, Unit.Piece, false, 10, Stock: 50),
        new("FFP2 Toz Maskesi", 7, Unit.Box, false, 10, Stock: 60),
        new("Kulak Tıkacı", 7, Unit.Box, false, 5, Stock: 30),
        new("Elektrik Bandı", 5, Unit.Piece, false, 20, Stock: 120, Barcode: "869100000006"),
        new("Isıyla Daralan Makaron Seti", 5, Unit.Box, false, 5, Stock: 25),
    ];

    /// <summary>The quantity product that ends below its minimum (the other is Termal Kamera).</summary>
    private const string CriticalQuantityProduct = "Nitril Eldiven";
    private const decimal CriticalQuantityTotal = 12;

    private readonly StockMovementService _movementService = movements;
    private readonly CountApprovalService _countApproval = countApproval;
    private readonly Random _random = new(2026);
    private readonly SeedCodes _codes = new(start: 7_919, scramble: true);
    private readonly List<StockMovement> _ledger = [];
    private readonly Dictionary<(Guid, Guid), StockBalance> _balances = [];
    private readonly List<Item> _items = [];
    private readonly List<Assignment> _assignments = [];
    private readonly Dictionary<Guid, Product> _products = [];
    private readonly Dictionary<Guid, Location> _locations = [];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await SeedGuard.EnsureEmptyAsync(db, cancellationToken);
        var users = await CreateUsersAsync(cancellationToken);
        var today = dateTime.Today();
        var start = today.AddDays(-HistoryDays);

        var categories = CategoryNames.Select(Category.Create).ToList();
        var tree = BuildTree();
        var products = Specs.Select(spec => (Spec: spec, Product: CreateProduct(spec, categories[spec.Category]))).ToList();
        foreach (var (_, product) in products)
        {
            _products[product.Id] = product;
        }

        var history = new History(this, tree, products, users, today);
        history.Receive(start);
        for (var day = start.AddDays(14); day < today; day = day.AddDays(1))
        {
            history.Day(day);
        }

        history.Finish();

        db.Categories.AddRange(categories);
        db.Locations.AddRange(_locations.Values);
        db.Products.AddRange(_products.Values);
        db.Items.AddRange(_items);
        db.StockBalances.AddRange(_balances.Values);
        db.Assignments.AddRange(_assignments);
        db.CountSessions.Add(history.Count!);
        db.StockMovements.AddRange(_ledger);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        await reminders.CheckAsync(cancellationToken);
        await lowStock.CheckAsync(null, cancellationToken);

        logger.LogInformation(
            "Demo data: {Products} products, {Items} items, {Locations} locations, {Movements} movements, {Open} open assignments",
            _products.Count, _items.Count, _locations.Count, _ledger.Count, _assignments.Count(a => a.ReturnedAt is null));
    }

    /// <summary>Deletes all data (users included, roles kept) and seeds the demo again. DEMO_MODE only.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        string[] keep = ["roles", "role_claims", "__EFMigrationsHistory"];
        var tables = db.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .OfType<string>()
            .Distinct()
            .Where(t => !keep.Contains(t))
            .Select(t => $"\"{t}\"");

        // TRUNCATE is the one way the append-only ledger is emptied: its trigger rejects UPDATE and DELETE only.
        var sql = "TRUNCATE TABLE " + string.Join(", ", tables) + " CASCADE";
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        db.ChangeTracker.Clear();
        logger.LogWarning("Demo reset: all data deleted");

        await services.SeedIdentityAsync();
        await RunAsync(cancellationToken);
    }

    private async Task<DemoUsers> CreateUsersAsync(CancellationToken cancellationToken)
    {
        var admin = await CreateUserAsync(AdminEmail, "Ayşe Demir", "Yönetim", Roles.Admin, canLogin: true);
        var warehouse = await CreateUserAsync(WarehouseEmail, "Mehmet Kaya", "Depo", Roles.WarehouseManager, canLogin: true);
        var staff = await CreateUserAsync(StaffEmail, "Zeynep Arslan", "Elektronik Laboratuvarı", Roles.Staff, canLogin: true);
        List<Guid> others =
        [
            await CreateUserAsync("ali.yilmaz@demo", "Ali Yılmaz", "Mekanik Atölye", Roles.Staff, canLogin: false),
            await CreateUserAsync("elif.sahin@demo", "Elif Şahin", "Elektronik Laboratuvarı", Roles.Staff, canLogin: false),
            await CreateUserAsync("burak.celik@demo", "Burak Çelik", "Mekanik Atölye", Roles.Staff, canLogin: false),
            await CreateUserAsync("selin.koc@demo", "Selin Koç", "Ar-Ge", Roles.Staff, canLogin: false),
            await CreateUserAsync("can.ozturk@demo", "Can Öztürk", "Bilgi İşlem", Roles.Staff, canLogin: false),
        ];
        cancellationToken.ThrowIfCancellationRequested();
        return new DemoUsers(admin, warehouse, staff, others);
    }

    private async Task<Guid> CreateUserAsync(string email, string fullName, string department, string role, bool canLogin)
    {
        var user = new AppUser
        {
            FullName = fullName,
            Department = department,
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            CanLogin = canLogin,
        };
        var result = canLogin ? await userManager.CreateAsync(user, Password) : await userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(user, role);
        }

        return result.Succeeded
            ? user.Id
            : throw new InvalidOperationException($"Demo user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    private DemoTree BuildTree()
    {
        var building = AddLocation("Demo Atölye", LocationType.Building, null);
        var depot = AddLocation("Ana Depo", LocationType.Room, building);
        var lab = AddLocation("Elektronik Laboratuvarı", LocationType.Room, building);
        var workshop = AddLocation("Mekanik Atölye", LocationType.Room, building);

        List<Location> Shelves(Location room, string name)
        {
            var cabinet = AddLocation(name, LocationType.Cabinet, room);
            return [.. Enumerable.Range(1, 3).Select(i => AddLocation($"Raf {i}", LocationType.Shelf, cabinet))];
        }

        var rackA = Shelves(depot, "Raf Sistemi A");
        var rackB = Shelves(depot, "Raf Sistemi B");
        var locked = Shelves(depot, "Kilitli Dolap");
        var labDevices = Shelves(lab, "Cihaz Dolabı");
        var labParts = Shelves(lab, "Komponent Dolabı");
        var tools = Shelves(workshop, "Alet Dolabı");
        var supplies = Shelves(workshop, "Sarf Dolabı");
        return new DemoTree(rackA, rackB, locked, [.. labDevices, .. tools], [.. labParts, .. supplies]);
    }

    private Location AddLocation(string name, LocationType type, Location? parent)
    {
        var location = Location.Create(_codes.Next(CodePrefix.Location), name, type, parent);
        _locations[location.Id] = location;
        return location;
    }

    private Product CreateProduct(ProductSpec spec, Category category)
    {
        var product = Product.Create(
            _codes.Next(CodePrefix.Product),
            spec.Name,
            null,
            category,
            spec.Unit,
            spec.Items > 0 ? TrackingType.Serialized : TrackingType.Quantity,
            spec.Assignable,
            spec.Min);
        if (spec.Barcode is { } digits)
        {
            product.AddBarcode(digits + Enumerable.Range(0, 10).First(d => BarcodeRules.IsCheckDigitValid($"{digits}{d}")), BarcodeFormat.Ean13);
        }

        return product;
    }

    private StockBalance Balance(Guid productId, Guid locationId) =>
        _balances.TryGetValue((productId, locationId), out var balance)
            ? balance
            : _balances[(productId, locationId)] = StockBalance.Empty(productId, locationId);

    private decimal Total(Product product) => product.TrackingType == TrackingType.Quantity
        ? _balances.Values.Where(b => b.ProductId == product.Id).Sum(b => b.Quantity)
        : _items.Count(i => i.ProductId == product.Id && i.Status == ItemStatus.InStock);

    private static DateTime At(DateOnly day, int hour, int minute) =>
        DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, minute)) - IstanbulOffset, DateTimeKind.Utc);

    private sealed record ProductSpec(
        string Name,
        int Category,
        Unit Unit,
        bool Assignable,
        decimal? Min,
        int Items = 0,
        decimal Stock = 0,
        string? Barcode = null,
        bool Secure = false,
        bool Counted = false);

    private sealed record DemoUsers(Guid Admin, Guid Warehouse, Guid Staff, IReadOnlyList<Guid> Others);

    /// <param name="WorkAreas">Lab and workshop shelves for devices and tools.</param>
    /// <param name="Supplies">Lab and workshop shelves for consumables.</param>
    private sealed record DemoTree(
        IReadOnlyList<Location> RackA,
        IReadOnlyList<Location> RackB,
        IReadOnlyList<Location> Locked,
        IReadOnlyList<Location> WorkAreas,
        IReadOnlyList<Location> Supplies)
    {
        public IReadOnlyList<Location> Depot => [.. RackA, .. RackB, .. Locked];
    }

    /// <summary>Stock the count approval changes, served from the in-memory state.</summary>
    private sealed class DemoCountStock(DemoSeeder seeder) : ICountApprovalStock
    {
        public Product Product(Guid id) => seeder._products[id];

        public Location Location(Guid id) => seeder._locations[id];

        public Item Item(Guid id) => seeder._items.Single(i => i.Id == id);

        public StockBalance Balance(Guid productId, Guid locationId) => seeder.Balance(productId, locationId);
    }

    /// <summary>Six months of workshop life, played day by day in time order.</summary>
    private sealed class History(
        DemoSeeder seeder,
        DemoTree tree,
        IReadOnlyList<(ProductSpec Spec, Product Product)> products,
        DemoUsers users,
        DateOnly today)
    {
        // Random assignment cycles end early enough that every one of them is returned.
        private readonly DateOnly _cyclesUntil = today.AddDays(-50);
        private readonly List<(DateOnly Day, Action<DateTime> Run)> _scheduled = [];
        private readonly Random _random = seeder._random;
        private readonly StockMovementService _movements = seeder._movementService;

        public CountSession? Count { get; private set; }

        private IEnumerable<Product> Quantity => products.Where(p => p.Spec.Items == 0).Select(p => p.Product);

        private MovementContext Context(DateTime at, Guid? user = null) => new(Guid.CreateVersion7(), user ?? users.Warehouse, at);

        private Product ByName(string name) => products.Single(p => p.Spec.Name == name).Product;

        private T Pick<T>(IReadOnlyList<T> list) => list[_random.Next(list.Count)];

        /// <summary>Opening stock: one receipt per product in the first two weeks.</summary>
        public void Receive(DateOnly start)
        {
            var serials = new HashSet<string>();
            foreach (var (spec, product) in products)
            {
                var at = At(start.AddDays(_random.Next(0, 10)), _random.Next(9, 17), _random.Next(60));
                var context = Context(at, users.Admin);
                if (spec.Items > 0)
                {
                    var shelf = spec.Secure ? Pick(tree.Locked) : spec.Counted ? tree.RackB[2] : Pick(tree.RackA);
                    for (var n = 0; n < spec.Items; n++)
                    {
                        string serial;
                        do
                        {
                            serial = $"SN{_random.Next(10_000_000, 99_999_999)}";
                        }
                        while (!serials.Add(serial));

                        var (item, movement) = _movements.ReceiveItem(seeder._codes.Next(CodePrefix.Item), product, shelf, serial, context);
                        seeder._items.Add(item);
                        seeder._ledger.Add(movement);
                    }
                }
                else
                {
                    var shelf = Pick(tree.RackB);
                    seeder._ledger.Add(_movements.ReceiveQuantity(product, shelf, seeder.Balance(product.Id, shelf.Id), spec.Stock, context));
                }
            }
        }

        public void Day(DateOnly day)
        {
            var offset = day.DayNumber - today.DayNumber;
            Planned(day, offset);

            foreach (var (_, run) in _scheduled.Where(s => s.Day == day).ToList())
            {
                run(At(day, 9, _random.Next(30)));
            }

            _scheduled.RemoveAll(s => s.Day == day);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return;
            }

            if (day.Day <= 7 && day.DayOfWeek == DayOfWeek.Monday)
            {
                Restock(At(day, 9, 45));
            }

            var times = Enumerable.Range(0, _random.Next(2, 6))
                .Select(_ => At(day, _random.Next(10, 17), _random.Next(60)))
                .Order();
            foreach (var at in times)
            {
                var roll = _random.Next(100);
                if (roll < 40)
                {
                    Consume(at);
                }
                else if (roll < 65)
                {
                    TransferSupplies(at);
                }
                else if (roll < 80)
                {
                    TransferItem(at);
                }
                else if (day < _cyclesUntil)
                {
                    AssignmentCycle(day, at);
                }
            }
        }

        /// <summary>Fixed events at a day offset from today; the rest of the story is random.</summary>
        private void Planned(DateOnly day, int offset)
        {
            var morning = At(day, 8, 45);
            switch (offset)
            {
                case -150:
                    var monitor = InStock(ByName("Monitör 24\""));
                    seeder._ledger.Add(_movements.DisposeItem(
                        monitor, ByName("Monitör 24\""), seeder._locations[monitor.CurrentLocationId!.Value],
                        MovementReason.Damaged, "Ekran kırıldı, onarımı ekonomik değil.", Context(morning)));
                    break;
                case -40:
                    Assign("Takım Çantası", users.Others[0], null, day, morning);
                    break;
                case -30:
                    Assign("Dizüstü Bilgisayar", users.Staff, today.AddDays(-6), day, morning);
                    break;
                case -25:
                    CountRackB(day);
                    break;
                case -21:
                    Assign("Akülü Matkap", users.Others[0], today.AddDays(-2), day, morning);
                    break;
                case -18:
                    Assign("Lehim İstasyonu", users.Staff, null, day, morning);
                    break;
                case -15:
                    Assign("Tablet", users.Others[3], today.AddDays(15), day, morning);
                    break;
                case -12:
                    Assign("Osiloskop", users.Others[1], today.AddDays(9), day, morning);
                    break;
                case -9:
                    Assign("Tork Anahtarı", users.Others[2], today.AddDays(5), day, morning);
                    break;
                case -8:
                    Assign("Termal Kamera", users.Others[1], today.AddDays(6), day, morning);
                    break;
                case -6:
                    Assign("Dijital Multimetre", users.Staff, today.AddDays(1), day, morning);
                    break;
                case -4:
                    Repair("Pens Ampermetre", "Ekranı açılmıyor.", morning);
                    Repair("Sıcak Hava Tabancası", "Isıtıcı rezistans arızalı.", morning.AddMinutes(10));
                    break;
                case -3:
                    Assign("El Tipi Barkod Okuyucu", users.Others[4], today.AddDays(27), day, morning);
                    break;
            }
        }

        /// <summary>Brings the critical product below its minimum and checks the scenario's promises.</summary>
        public void Finish()
        {
            var gloves = ByName(CriticalQuantityProduct);
            var at = At(today.AddDays(-2), 11, 20);
            foreach (var balance in seeder._balances.Values.Where(b => b.ProductId == gloves.Id && b.Quantity > 0).ToList())
            {
                var excess = seeder.Total(gloves) - CriticalQuantityTotal;
                if (excess > 0)
                {
                    seeder._ledger.Add(_movements.ConsumeQuantity(
                        gloves, seeder._locations[balance.LocationId], balance, Math.Min(excess, balance.Quantity),
                        MovementReason.Consumed, "Yıllık bakım çalışması.", Context(at)));
                }
            }

            var low = products.Where(p => p.Spec.Min is { } min && seeder.Total(p.Product) < min).Select(p => p.Spec.Name).ToList();
            var open = seeder._assignments.Count(a => a.ReturnedAt is null);
            if (low.Count != 2 || open != 10 || Count is null)
            {
                throw new InvalidOperationException($"Demo scenario drifted: low stock [{string.Join(", ", low)}], {open} open assignments.");
            }
        }

        private Item InStock(Product product, bool fromDepot = false) =>
            seeder._items.First(i => i.ProductId == product.Id && i.Status == ItemStatus.InStock
                && (!fromDepot || tree.Depot.Any(l => l.Id == i.CurrentLocationId)));

        private void Assign(string productName, Guid assignee, DateOnly? due, DateOnly day, DateTime at)
        {
            var product = ByName(productName);
            var (assignment, movement) = _movements.AssignItem(InStock(product), product, assignee, true, due, day, null, Context(at));
            seeder._assignments.Add(assignment);
            seeder._ledger.Add(movement);
        }

        private void Repair(string productName, string note, DateTime at)
        {
            var product = ByName(productName);
            seeder._ledger.Add(_movements.SendToRepair(InStock(product), product, note, Context(at)));
        }

        /// <summary>Monthly order for consumables running low (never for the critical one).</summary>
        private void Restock(DateTime at)
        {
            var context = Context(at, users.Admin);
            foreach (var (spec, product) in products.Where(p => p.Spec.Items == 0 && p.Spec.Name != CriticalQuantityProduct))
            {
                if (seeder.Total(product) < spec.Min * 3)
                {
                    var shelf = seeder._balances.Values.FirstOrDefault(b => b.ProductId == product.Id && tree.RackB.Any(l => l.Id == b.LocationId)) is { } existing
                        ? seeder._locations[existing.LocationId]
                        : tree.RackB[0];
                    seeder._ledger.Add(_movements.ReceiveQuantity(product, shelf, seeder.Balance(product.Id, shelf.Id), spec.Min!.Value * 4, context));
                }
            }
        }

        /// <summary>Consumables used up in the lab or workshop; totals stay at twice the minimum or more.</summary>
        private void Consume(DateTime at)
        {
            var candidates = seeder._balances.Values
                .Where(b => b.Quantity > 0 && tree.Supplies.Any(l => l.Id == b.LocationId))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var balance = Pick(candidates);
            var product = seeder._products[balance.ProductId];
            var min = product.MinStockLevel ?? 1;
            var quantity = Math.Min(balance.Quantity, Math.Max(1, _random.Next(1, (int)Math.Ceiling(min / 2) + 1)));
            if (seeder.Total(product) - quantity >= min * 2)
            {
                seeder._ledger.Add(_movements.ConsumeQuantity(
                    product, seeder._locations[balance.LocationId], balance, quantity, MovementReason.Consumed, null, Context(at)));
            }
        }

        /// <summary>Consumables brought from the depot to a lab or workshop shelf.</summary>
        private void TransferSupplies(DateTime at)
        {
            var product = Pick(Quantity.ToList());
            var source = seeder._balances.Values.Where(b => b.ProductId == product.Id && b.Quantity > 0 && tree.RackB.Any(l => l.Id == b.LocationId)).ToList();
            if (source.Count == 0)
            {
                return;
            }

            var from = Pick(source);
            var to = Pick(tree.Supplies);
            var min = product.MinStockLevel ?? 1;
            var quantity = Math.Min(from.Quantity, Math.Max(1, _random.Next((int)Math.Ceiling(min / 2), (int)min + 1)));
            seeder._ledger.Add(_movements.TransferQuantity(
                product, seeder._locations[from.LocationId], to, from, seeder.Balance(product.Id, to.Id), quantity, Context(at)));
        }

        private void TransferItem(DateTime at)
        {
            var movable = seeder._items
                .Where(i => i.Status == ItemStatus.InStock && !products.Single(p => p.Product.Id == i.ProductId).Spec.Secure)
                .ToList();
            var item = Pick(movable);
            var inDepot = tree.Depot.Any(l => l.Id == item.CurrentLocationId);
            var target = inDepot ? Pick(tree.WorkAreas) : Pick(tree.RackA);
            seeder._ledger.Add(_movements.TransferItem(item, seeder._products[item.ProductId], target, Context(at)));
        }

        /// <summary>A tool lent out and brought back days later; sometimes broken and repaired.</summary>
        private void AssignmentCycle(DateOnly day, DateTime at)
        {
            var assignable = seeder._items
                .Where(i => i.Status == ItemStatus.InStock && seeder._products[i.ProductId].IsAssignable
                    && seeder._products[i.ProductId].Name != "Termal Kamera")
                .ToList();
            var item = Pick(assignable);
            var product = seeder._products[item.ProductId];
            var assignee = _random.Next(3) == 0 ? users.Staff : Pick(users.Others);
            var due = _random.Next(3) == 0 ? (DateOnly?)null : day.AddDays(_random.Next(7, 21));
            var (assignment, movement) = _movements.AssignItem(item, product, assignee, true, due, day, null, Context(at));
            seeder._assignments.Add(assignment);
            seeder._ledger.Add(movement);

            var home = Pick(tree.RackA);
            var faulty = _random.Next(10) == 0;
            var returnDay = day.AddDays(_random.Next(2, 21));
            _scheduled.Add((returnDay, time =>
            {
                seeder._ledger.AddRange(_movements.ReturnItem(
                    assignment, item, product, home, faulty ? ReturnCondition.Faulty : ReturnCondition.Good,
                    faulty ? "Çalışırken durdu." : null, Context(time)));
                if (faulty)
                {
                    _scheduled.Add((returnDay.AddDays(_random.Next(4, 10)), repaired =>
                        seeder._ledger.Add(_movements.CompleteRepair(item, product, "Servisten döndü.", Context(repaired)))));
                }
            }));
        }

        /// <summary>
        /// The approved count: all of Raf Sistemi B. Two consumables differ from the books and the
        /// approver applies both differences (Adjustment movements).
        /// </summary>
        private void CountRackB(DateOnly day)
        {
            var cabinet = seeder._locations[tree.RackB[0].ParentId!.Value];
            var shelves = tree.RackB.ToDictionary(l => l.Id);
            var expected = seeder._balances.Values
                .Where(b => b.Quantity > 0 && shelves.ContainsKey(b.LocationId))
                .Select(b => new ExpectedStock(b.LocationId, b.ProductId, null, b.Quantity))
                .Concat(seeder._items
                    .Where(i => i.Status == ItemStatus.InStock && shelves.ContainsKey(i.CurrentLocationId ?? Guid.Empty))
                    .Select(i => new ExpectedStock(i.CurrentLocationId!.Value, i.ProductId, i.Id, 1)))
                .ToList();

            var started = At(day, 9, 0);
            var session = CountSession.Start(cabinet, true, "Üç aylık depo sayımı.", expected, users.Warehouse, started);
            var paper = ByName("A4 Fotokopi Kağıdı").Id;
            var ties = ByName("Kablo Bağı 200 mm").Id;
            var scanned = started;
            foreach (var line in expected)
            {
                scanned = scanned.AddSeconds(_random.Next(10, 40));
                var shelf = shelves[line.LocationId];
                if (line.ItemId is { } itemId)
                {
                    session.ScanItem(cabinet, shelf, seeder._items.Single(i => i.Id == itemId), users.Staff, scanned);
                }
                else
                {
                    var difference = line.ProductId == paper ? -3 : line.ProductId == ties ? 2 : 0;
                    session.ScanQuantity(cabinet, shelf, seeder._products[line.ProductId], line.Quantity + difference, users.Staff, scanned);
                }
            }

            session.Submit();
            var review = CountReview.Build(
                session,
                seeder._balances.ToDictionary(b => b.Key, b => b.Value.Quantity),
                seeder._items.ToDictionary(i => i.Id, i => new ItemSnapshot(i.Status, i.CurrentLocationId)));
            var decisions = review
                .Where(l => l.Options.Count > 0)
                .ToDictionary(
                    l => l.Line.Id,
                    l => l.Options.Contains(CountResolution.ApplyDifference) ? CountResolution.ApplyDifference : l.Options[0]);
            seeder._ledger.AddRange(seeder._countApproval.Approve(
                session, review, decisions, new DemoCountStock(seeder), Context(At(day, 11, 30))));
            Count = session;
        }
    }
}
