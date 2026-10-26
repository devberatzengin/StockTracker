using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Counts;

/// <summary>Raf B2 under a building, 200 screws and one laptop there; in-memory stock for approvals.</summary>
internal sealed class CountFixture : ICountApprovalStock
{
    public static readonly Guid UserId = Guid.NewGuid();
    public static readonly DateTime Now = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    public CountFixture()
    {
        Building = Location.Create("LOC-BBBBBB", "Bina", LocationType.Building, null);
        ShelfB2 = Location.Create("LOC-B20000", "Raf B2", LocationType.Shelf, Building);
        ShelfA1 = Location.Create("LOC-A10000", "Raf A1", LocationType.Shelf, Building);
        Elsewhere = Location.Create("LOC-EEEEEE", "Depo 2", LocationType.Room, null);
        Screw = Screw();
        Laptop = Laptop();
        ScrewB2 = BalanceWith(Screw, ShelfB2, 200);
        LaptopB2 = ReceiveItem(Laptop, ShelfB2, "SN-B2");
        LaptopA1 = ReceiveItem(Laptop, ShelfA1, "SN-A1");
    }

    public Location Building { get; }

    public Location ShelfB2 { get; }

    public Location ShelfA1 { get; }

    public Location Elsewhere { get; }

    public Product Screw { get; }

    public Product Laptop { get; }

    public StockBalance ScrewB2 { get; }

    public Item LaptopB2 { get; }

    public Item LaptopA1 { get; }

    public List<StockBalance> CreatedBalances { get; } = [];

    public CountApprovalService Approval { get; } = new(Service);

    /// <summary>A count of Raf B2 with the snapshot taken from the fixture's stock.</summary>
    public CountSession StartB2() =>
        CountSession.Start(
            ShelfB2,
            includeChildren: false,
            note: null,
            [new ExpectedStock(ShelfB2.Id, Screw.Id, null, ScrewB2.Quantity), new ExpectedStock(ShelfB2.Id, Laptop.Id, LaptopB2.Id, 1)],
            UserId,
            Now);

    public IReadOnlyList<CountReviewLine> Review(CountSession session) =>
        CountReview.Build(
            session,
            new[] { ScrewB2 }.Concat(CreatedBalances).ToDictionary(b => (b.ProductId, b.LocationId), b => b.Quantity),
            new[] { LaptopB2, LaptopA1 }.ToDictionary(i => i.Id, i => new ItemSnapshot(i.Status, i.CurrentLocationId)));

    public static MovementContext ApprovalContext() => new(Guid.NewGuid(), UserId, Now.AddHours(1));

    Product ICountApprovalStock.Product(Guid id) => id == Screw.Id ? Screw : Laptop;

    Location ICountApprovalStock.Location(Guid id) =>
        new[] { Building, ShelfB2, ShelfA1, Elsewhere }.Single(l => l.Id == id);

    Item ICountApprovalStock.Item(Guid id) => new[] { LaptopB2, LaptopA1 }.Single(i => i.Id == id);

    StockBalance ICountApprovalStock.Balance(Guid productId, Guid locationId)
    {
        if (ScrewB2.ProductId == productId && ScrewB2.LocationId == locationId)
        {
            return ScrewB2;
        }

        var balance = CreatedBalances.SingleOrDefault(b => b.ProductId == productId && b.LocationId == locationId);
        if (balance is null)
        {
            balance = StockBalance.Empty(productId, locationId);
            CreatedBalances.Add(balance);
        }

        return balance;
    }
}
