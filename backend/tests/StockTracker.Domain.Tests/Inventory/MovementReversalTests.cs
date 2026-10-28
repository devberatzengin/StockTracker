using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Inventory;

public class MovementReversalTests
{
    private const string Note = "Yanlış girildi.";

    private static void ShouldFail(Action act, string code)
    {
        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void QuantityIn_IsReversedWithOutFromTheSameShelf()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = StockBalance.Empty(screw.Id, shelf.Id);
        var original = Service.ReceiveQuantity(screw, shelf, balance, 50, Context);

        var reversal = Service.Reverse(original, false, screw, null, null, shelf, null, balance, Note, Context);

        reversal.Type.Should().Be(MovementType.Out);
        reversal.FromLocationId.Should().Be(shelf.Id);
        reversal.Quantity.Should().Be(50);
        reversal.Reason.Should().Be(MovementReason.Other);
        reversal.Note.Should().Be(Note);
        reversal.ReversesMovementId.Should().Be(original.Id);
        balance.Quantity.Should().Be(0);
    }

    [Fact]
    public void QuantityIn_AlreadyConsumed_CannotBeReversed()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = StockBalance.Empty(screw.Id, shelf.Id);
        var original = Service.ReceiveQuantity(screw, shelf, balance, 50, Context);
        Service.ConsumeQuantity(screw, shelf, balance, 20, MovementReason.Consumed, null, Context);

        ShouldFail(
            () => Service.Reverse(original, false, screw, null, null, shelf, null, balance, Note, Context),
            InventoryErrors.InsufficientStock);
        balance.Quantity.Should().Be(30);
    }

    [Fact]
    public void QuantityOut_IsReversedWithInToTheSameShelf()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 40);
        var original = Service.ConsumeQuantity(screw, shelf, balance, 15, MovementReason.Damaged, null, Context);

        var reversal = Service.Reverse(original, false, screw, null, shelf, null, balance, null, Note, Context);

        reversal.Type.Should().Be(MovementType.In);
        reversal.ToLocationId.Should().Be(shelf.Id);
        reversal.ReversesMovementId.Should().Be(original.Id);
        balance.Quantity.Should().Be(40);
    }

    [Fact]
    public void QuantityTransfer_IsReversedWithTransferBack()
    {
        var screw = Screw();
        var a = Shelf("A");
        var b = Shelf("B");
        var balanceA = BalanceWith(screw, a, 30);
        var balanceB = StockBalance.Empty(screw.Id, b.Id);
        var original = Service.TransferQuantity(screw, a, b, balanceA, balanceB, 10, Context);

        var reversal = Service.Reverse(original, false, screw, null, a, b, balanceA, balanceB, Note, Context);

        reversal.Type.Should().Be(MovementType.Transfer);
        reversal.FromLocationId.Should().Be(b.Id);
        reversal.ToLocationId.Should().Be(a.Id);
        balanceA.Quantity.Should().Be(30);
        balanceB.Quantity.Should().Be(0);
    }

    [Fact]
    public void ItemIn_IsReversedByDisposingTheItem()
    {
        var laptop = Laptop();
        var shelf = Shelf();
        var (item, original) = Service.ReceiveItem("ITM-BBBBBB", laptop, shelf, null, Context);

        var reversal = Service.Reverse(original, false, laptop, item, null, shelf, null, null, Note, Context);

        reversal.Type.Should().Be(MovementType.Out);
        reversal.ItemId.Should().Be(item.Id);
        reversal.FromLocationId.Should().Be(shelf.Id);
        item.Status.Should().Be(ItemStatus.Disposed);
    }

    [Fact]
    public void ItemTransfer_IsReversedWithTransferBack()
    {
        var laptop = Laptop();
        var a = Shelf("A");
        var b = Shelf("B");
        var item = ReceiveItem(laptop, a);
        var original = Service.TransferItem(item, laptop, b, Context);

        var reversal = Service.Reverse(original, false, laptop, item, a, b, null, null, Note, Context);

        reversal.FromLocationId.Should().Be(b.Id);
        reversal.ToLocationId.Should().Be(a.Id);
        item.CurrentLocationId.Should().Be(a.Id);
    }

    [Fact]
    public void ItemMovedAgain_CannotBeReversed()
    {
        var laptop = Laptop();
        var a = Shelf("A");
        var b = Shelf("B");
        var c = Shelf("C");
        var item = ReceiveItem(laptop, a);
        var original = Service.TransferItem(item, laptop, b, Context);
        Service.TransferItem(item, laptop, c, Context);

        ShouldFail(
            () => Service.Reverse(original, false, laptop, item, a, b, null, null, Note, Context),
            InventoryErrors.ItemChangedSinceMovement);
        item.CurrentLocationId.Should().Be(c.Id);
    }

    [Fact]
    public void ItemInRepair_CannotBeReversed()
    {
        var laptop = Laptop();
        var shelf = Shelf();
        var (item, original) = Service.ReceiveItem("ITM-CCCCCC", laptop, shelf, null, Context);
        Service.SendToRepair(item, laptop, "Arızalı", Context);

        ShouldFail(
            () => Service.Reverse(original, false, laptop, item, null, shelf, null, null, Note, Context),
            InventoryErrors.ItemChangedSinceMovement);
    }

    [Fact]
    public void Rules_AlreadyReversed_ReversalOfReversal_NoteAndType()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 40);
        var original = Service.ConsumeQuantity(screw, shelf, balance, 5, MovementReason.Consumed, null, Context);

        ShouldFail(
            () => Service.Reverse(original, true, screw, null, shelf, null, balance, null, Note, Context),
            InventoryErrors.AlreadyReversed);
        ShouldFail(
            () => Service.Reverse(original, false, screw, null, shelf, null, balance, null, "  ", Context),
            InventoryErrors.NoteRequired);

        var reversal = Service.Reverse(original, false, screw, null, shelf, null, balance, null, Note, Context);
        ShouldFail(
            () => Service.Reverse(reversal, false, screw, null, null, shelf, null, balance, Note, Context),
            InventoryErrors.NotReversible);

        var adjustment = Service.AdjustQuantity(screw, shelf, balance, 3, Context);
        ShouldFail(
            () => Service.Reverse(adjustment, false, screw, null, null, shelf, null, balance, Note, Context),
            InventoryErrors.NotReversible);
    }

    [Fact]
    public void DisposedItemOut_CannotBeReversed()
    {
        var laptop = Laptop();
        var shelf = Shelf();
        var item = ReceiveItem(laptop, shelf);
        var original = Service.DisposeItem(item, laptop, shelf, MovementReason.Lost, null, Context);

        ShouldFail(
            () => Service.Reverse(original, false, laptop, item, shelf, null, null, null, Note, Context),
            InventoryErrors.NotReversible);
    }

    [Fact]
    public void InactiveLocation_CannotReceiveTheReversal()
    {
        var screw = Screw();
        var shelf = Shelf();
        var balance = BalanceWith(screw, shelf, 10);
        var original = Service.ConsumeQuantity(screw, shelf, balance, 10, MovementReason.Consumed, null, Context);
        shelf.Deactivate(hasContent: false);

        ShouldFail(
            () => Service.Reverse(original, false, screw, null, shelf, null, balance, null, Note, Context),
            InventoryErrors.LocationInactive);
    }
}
