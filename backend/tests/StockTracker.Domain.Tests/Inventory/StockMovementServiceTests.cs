using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Inventory;

public class StockMovementServiceTests
{
    private static void ShouldFail(Action act, string code, string? target = null)
    {
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Code.Should().Be(code);
        if (target is not null)
        {
            exception.Target.Should().Be(target);
        }
    }

    [Fact]
    public void ReceiveItem_CreatesInStockItemAndInMovement()
    {
        var laptop = Laptop();
        var shelf = Shelf();

        var (item, movement) = Service.ReceiveItem("ITM-111111", laptop, shelf, "SN-1", Context);

        item.Code.Should().Be("ITM-111111");
        movement.Type.Should().Be(MovementType.In);
        movement.ItemId.Should().Be(item.Id);
        movement.ToLocationId.Should().Be(shelf.Id);
        movement.FromLocationId.Should().BeNull();
        movement.Quantity.Should().Be(1);
    }

    [Fact]
    public void ReceiveQuantity_IncreasesBalance()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = StockBalance.Empty(screw.Id, shelf.Id);

        var movement = Service.ReceiveQuantity(screw, shelf, balance, 500, Context);

        balance.Quantity.Should().Be(500);
        movement.Type.Should().Be(MovementType.In);
        movement.ItemId.Should().BeNull();
        movement.Quantity.Should().Be(500);
    }

    [Fact]
    public void TrackingTypeMismatch_IsRejected()
    {
        var shelf = Shelf();
        var screw = Screw();

        ShouldFail(() => Service.ReceiveItem("ITM-111111", screw, shelf, null, Context), InventoryErrors.ProductNotSerialized, "productId");
        ShouldFail(
            () => Service.ReceiveQuantity(Laptop(), shelf, StockBalance.Empty(Guid.NewGuid(), shelf.Id), 1, Context),
            InventoryErrors.ProductNotQuantity,
            "productId");
    }

    [Fact]
    public void InactiveProductOrLocation_IsRejected()
    {
        var screw = Screw();
        var shelf = Shelf();
        var inactiveShelf = Shelf("Eski");
        inactiveShelf.Deactivate(hasContent: false);
        var inactiveScrew = Screw();
        inactiveScrew.Deactivate(hasStock: false);

        ShouldFail(
            () => Service.ReceiveQuantity(inactiveScrew, shelf, StockBalance.Empty(inactiveScrew.Id, shelf.Id), 1, Context),
            InventoryErrors.ProductInactive);
        ShouldFail(
            () => Service.ReceiveQuantity(screw, inactiveShelf, StockBalance.Empty(screw.Id, inactiveShelf.Id), 1, Context),
            InventoryErrors.LocationInactive,
            "toLocationId");
    }

    [Fact]
    public void ConsumeQuantity_DecreasesBalance_AndZeroRowStays()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 200);

        var movement = Service.ConsumeQuantity(screw, shelf, balance, 200, MovementReason.Consumed, null, Context);

        balance.Quantity.Should().Be(0);
        movement.Type.Should().Be(MovementType.Out);
        movement.FromLocationId.Should().Be(shelf.Id);
        movement.Reason.Should().Be(MovementReason.Consumed);
    }

    [Fact]
    public void ConsumeQuantity_BeyondBalance_IsRejectedAndLeavesBalance()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 10);

        ShouldFail(
            () => Service.ConsumeQuantity(screw, shelf, balance, 10.5m, MovementReason.Consumed, null, Context),
            InventoryErrors.InsufficientStock,
            "quantity");
        balance.Quantity.Should().Be(10);
    }

    [Fact]
    public void ConsumeQuantity_WithoutBalance_IsInsufficient()
    {
        ShouldFail(
            () => Service.ConsumeQuantity(Screw(), Shelf(), null, 1, MovementReason.Consumed, null, Context),
            InventoryErrors.InsufficientStock);
    }

    [Fact]
    public void ConsumeQuantity_WithoutReason_IsRejectedBeforeChangingBalance()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 10);

        ShouldFail(
            () => Service.ConsumeQuantity(screw, shelf, balance, 5, MovementReason.Other, null, Context),
            InventoryErrors.NoteRequired);
        balance.Quantity.Should().Be(10);
    }

    [Fact]
    public void TransferQuantity_MovesBetweenBalances()
    {
        var screw = Screw();
        var a1 = Shelf("Raf A1");
        var b2 = Shelf("Raf B2");
        var source = BalanceWith(screw, a1, 500);
        var target = StockBalance.Empty(screw.Id, b2.Id);

        var movement = Service.TransferQuantity(screw, a1, b2, source, target, 200, Context);

        source.Quantity.Should().Be(300);
        target.Quantity.Should().Be(200);
        movement.Type.Should().Be(MovementType.Transfer);
        movement.FromLocationId.Should().Be(a1.Id);
        movement.ToLocationId.Should().Be(b2.Id);
    }

    [Fact]
    public void TransferQuantity_NegativeOrSameLocation_IsRejected()
    {
        var screw = Screw();
        var a1 = Shelf();
        var b2 = Shelf("Raf B2");
        var source = BalanceWith(screw, a1, 5);
        var target = StockBalance.Empty(screw.Id, b2.Id);

        ShouldFail(() => Service.TransferQuantity(screw, a1, b2, source, target, 6, Context), InventoryErrors.InsufficientStock);
        source.Quantity.Should().Be(5);
        target.Quantity.Should().Be(0);

        var sameTarget = StockBalance.Empty(screw.Id, a1.Id);
        ShouldFail(() => Service.TransferQuantity(screw, a1, a1, source, sameTarget, 1, Context), InventoryErrors.SameSourceAndTarget);
    }

    [Fact]
    public void TransferItem_MovesInStockItem()
    {
        var laptop = Laptop();
        var a1 = Shelf();
        var b2 = Shelf("Raf B2");
        var item = ReceiveItem(laptop, a1);

        var movement = Service.TransferItem(item, laptop, b2, Context);

        item.CurrentLocationId.Should().Be(b2.Id);
        movement.FromLocationId.Should().Be(a1.Id);
        movement.ToLocationId.Should().Be(b2.Id);
        movement.ItemId.Should().Be(item.Id);
    }

    [Fact]
    public void TransferItem_ToItsOwnLocation_IsRejected()
    {
        var laptop = Laptop();
        var a1 = Shelf();
        var item = ReceiveItem(laptop, a1);

        ShouldFail(() => Service.TransferItem(item, laptop, a1, Context), InventoryErrors.SameSourceAndTarget);
    }

    [Fact]
    public void TransferItem_AssignedOrInRepair_IsRejected()
    {
        var laptop = Laptop();
        var assigned = ReceiveItem(laptop, Shelf());
        assigned.Assign();
        var inRepair = ReceiveItem(laptop, Shelf());
        inRepair.SendToRepair();

        ShouldFail(() => Service.TransferItem(assigned, laptop, Shelf("Raf B2"), Context), InventoryErrors.InvalidItemTransition, "itemId");
        ShouldFail(() => Service.TransferItem(inRepair, laptop, Shelf("Raf B2"), Context), InventoryErrors.InvalidItemTransition, "itemId");
    }

    [Fact]
    public void DisposeItem_DisposesAndClearsLocation()
    {
        var laptop = Laptop();
        var a1 = Shelf();
        var item = ReceiveItem(laptop, a1);

        var movement = Service.DisposeItem(item, laptop, a1, MovementReason.Lost, null, Context);

        item.Status.Should().Be(ItemStatus.Disposed);
        item.CurrentLocationId.Should().BeNull();
        movement.Type.Should().Be(MovementType.Out);
        movement.FromLocationId.Should().Be(a1.Id);
    }

    [Fact]
    public void DisposeItem_InRepair_IsAllowed_Assigned_IsNot()
    {
        var laptop = Laptop();
        var inRepair = ReceiveItem(laptop, Shelf());
        inRepair.SendToRepair();
        var assigned = ReceiveItem(laptop, Shelf());
        assigned.Assign();

        Service.DisposeItem(inRepair, laptop, Shelf(), MovementReason.Damaged, null, Context);
        inRepair.Status.Should().Be(ItemStatus.Disposed);

        ShouldFail(() => Service.DisposeItem(assigned, laptop, null, MovementReason.Damaged, null, Context), InventoryErrors.InvalidItemTransition);
        assigned.Status.Should().Be(ItemStatus.Assigned);
    }

    [Fact]
    public void DisposeItem_WithInvalidReason_LeavesItemUnchanged()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        ShouldFail(() => Service.DisposeItem(item, laptop, Shelf(), MovementReason.Other, null, Context), InventoryErrors.NoteRequired);
        item.Status.Should().Be(ItemStatus.InStock);
    }
}
