using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Tests.Inventory;

/// <summary>In-memory aggregates for movement tests (no mocks, no database).</summary>
internal static class InventoryFixture
{
    public static readonly Category Category = Category.Create("Genel");

    public static readonly MovementContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc));

    public static Product Laptop() =>
        Product.Create("PRD-LAPTOP", "Laptop", null, Category, Unit.Piece, TrackingType.Serialized, true, null);

    public static Product Screw() =>
        Product.Create("PRD-SCREW1", "Vida M4", null, Category, Unit.Piece, TrackingType.Quantity, false, 100);

    public static Location Shelf(string name = "Raf A1") => Location.Create("LOC-AAAAAA", name, LocationType.Shelf, null);

    public static StockMovementService Service { get; } = new();

    public static Item ReceiveItem(Product product, Location location, string? serial = null) =>
        Service.ReceiveItem("ITM-AAAAAA", product, location, serial, Context).Item;

    public static StockBalance BalanceWith(Product product, Location location, decimal quantity)
    {
        var balance = StockBalance.Empty(product.Id, location.Id);
        if (quantity > 0)
        {
            Service.ReceiveQuantity(product, location, balance, quantity, Context);
        }

        return balance;
    }
}
