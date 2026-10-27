using Microsoft.Extensions.Logging;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common.Codes;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Seeding;

/// <summary>
/// Performance data (PLAN 4.2): 1,000 products, 5,000 items, 200 locations and 50,000 movements over
/// six months. Movements go through the domain service, so balances and item locations stay consistent.
/// </summary>
public sealed class LoadSeeder(AppDbContext db, StockMovementService movements, TimeProvider clock, ILogger<LoadSeeder> logger)
{
    public const int Products = 1_000;
    public const int ItemsPerSerializedProduct = 10;
    public const int Locations = 200;
    public const int Movements = 50_000;

    private readonly Random _random = new(42);
    private readonly SeedCodes _codes = new(start: 1_000);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var userId = await SeedGuard.EnsureEmptyAndGetAdminAsync(db, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var start = now.AddDays(-180);

        // Catalog and tree: 20 categories; 1 building › 9 rooms › 30 cabinets › 160 shelves.
        var categories = Enumerable.Range(1, 20).Select(i => Category.Create($"Kategori {i:D2}")).ToList();
        var building = Location.Create(_codes.Next(CodePrefix.Location), "Merkez Depo", LocationType.Building, null);
        var rooms = Enumerable.Range(1, 9).Select(i => Location.Create(_codes.Next(CodePrefix.Location), $"Oda {i}", LocationType.Room, building)).ToList();
        var cabinets = Enumerable.Range(1, 30).Select(i => Location.Create(_codes.Next(CodePrefix.Location), $"Dolap {i}", LocationType.Cabinet, rooms[i % rooms.Count])).ToList();
        var shelves = Enumerable.Range(1, 160).Select(i => Location.Create(_codes.Next(CodePrefix.Location), $"Raf {i}", LocationType.Shelf, cabinets[i % cabinets.Count])).ToList();

        var products = Enumerable.Range(1, Products).Select(i =>
        {
            var serialized = i % 2 == 0;
            return Product.Create(
                _codes.Next(CodePrefix.Product),
                serialized ? $"Cihaz {i:D4}" : $"Sarf malzeme {i:D4}",
                null,
                categories[i % categories.Count],
                Unit.Piece,
                serialized ? TrackingType.Serialized : TrackingType.Quantity,
                serialized && i % 4 == 0,
                serialized ? 3 : _random.Next(50, 200));
        }).ToList();

        db.Categories.AddRange(categories);
        db.Locations.AddRange([building, .. rooms, .. cabinets, .. shelves]);
        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var ledger = new List<StockMovement>(Movements);
        var items = new List<Item>();
        var balances = new Dictionary<(Guid, Guid), StockBalance>();
        StockBalance Balance(Product p, Location l) =>
            balances.TryGetValue((p.Id, l.Id), out var b) ? b : balances[(p.Id, l.Id)] = StockBalance.Empty(p.Id, l.Id);
        MovementContext Context(DateTime at) => new(Guid.CreateVersion7(), userId, at);
        Location AnyShelf() => shelves[_random.Next(shelves.Count)];

        // First two months: receive everything (items one operation per product, quantities 4 times).
        foreach (var product in products)
        {
            var at = start.AddMinutes(_random.Next(0, 60 * 24 * 60));
            if (product.TrackingType == TrackingType.Serialized)
            {
                var context = Context(at);
                var shelf = AnyShelf();
                for (var n = 0; n < ItemsPerSerializedProduct; n++)
                {
                    var (item, movement) = movements.ReceiveItem(_codes.Next(CodePrefix.Item), product, shelf, $"SN-{items.Count + 1:D6}", context);
                    items.Add(item);
                    ledger.Add(movement);
                }
            }
            else
            {
                for (var n = 0; n < 4; n++)
                {
                    var shelf = AnyShelf();
                    ledger.Add(movements.ReceiveQuantity(product, shelf, Balance(product, shelf), _random.Next(100, 500), Context(at.AddMinutes(n))));
                }
            }
        }

        // Then four months of daily work, in time order: quantity transfers and outs, item transfers.
        var quantityProducts = products.Where(p => p.TrackingType == TrackingType.Quantity).ToDictionary(p => p.Id);
        var productsById = products.ToDictionary(p => p.Id);
        var shelvesById = shelves.ToDictionary(s => s.Id);
        var remaining = Movements - ledger.Count;
        var times = Enumerable.Range(0, remaining)
            .Select(_ => start.AddDays(60).AddMinutes(_random.Next(0, 60 * 24 * 120)))
            .Order()
            .ToList();

        foreach (var at in times)
        {
            var roll = _random.Next(100);
            if (roll < 15)
            {
                var item = items[_random.Next(items.Count)];
                var target = AnyShelf();
                if (item.CurrentLocationId != target.Id)
                {
                    ledger.Add(movements.TransferItem(item, productsById[item.ProductId], target, Context(at)));
                    continue;
                }
            }

            var stocked = balances.Values.Where(b => b.Quantity > 1).ElementAt(_random.Next(balances.Values.Count(b => b.Quantity > 1)));
            var product = quantityProducts[stocked.ProductId];
            var source = shelvesById[stocked.LocationId];
            var quantity = Math.Min(stocked.Quantity - 1, _random.Next(1, 30));
            if (roll < 70)
            {
                var target = AnyShelf();
                if (target.Id == source.Id)
                {
                    target = shelves[(shelves.IndexOf(source) + 1) % shelves.Count];
                }

                ledger.Add(movements.TransferQuantity(product, source, target, stocked, Balance(product, target), quantity, Context(at)));
            }
            else
            {
                ledger.Add(movements.ConsumeQuantity(product, source, stocked, quantity, MovementReason.Consumed, null, Context(at)));
            }
        }

        db.Items.AddRange(items);
        db.StockBalances.AddRange(balances.Values);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        foreach (var chunk in ledger.Chunk(5_000))
        {
            db.StockMovements.AddRange(chunk);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;
        logger.LogInformation(
            "Load data: {Products} products, {Items} items, {Locations} locations, {Movements} movements",
            products.Count, items.Count, 1 + rooms.Count + cabinets.Count + shelves.Count, ledger.Count);
    }
}
