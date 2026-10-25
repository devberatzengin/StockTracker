using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Inventory;

public class ItemStateTests
{
    private static Item NewItem() => ReceiveItem(Laptop(), Shelf());

    private static void ShouldBeInvalid(Action act) =>
        act.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidItemTransition);

    [Fact]
    public void ReceivedItem_IsInStockAtLocation()
    {
        var shelf = Shelf();
        var item = ReceiveItem(Laptop(), shelf, "  SN-1 ");

        item.Status.Should().Be(ItemStatus.InStock);
        item.CurrentLocationId.Should().Be(shelf.Id);
        item.SerialNumber.Should().Be("SN-1");
    }

    [Fact]
    public void AssignAndReturn_CycleThroughInStock()
    {
        var item = NewItem();
        var returnShelf = Shelf("Raf B2");

        item.Assign();
        item.Status.Should().Be(ItemStatus.Assigned);
        item.CurrentLocationId.Should().BeNull();

        item.Return(returnShelf);
        item.Status.Should().Be(ItemStatus.InStock);
        item.CurrentLocationId.Should().Be(returnShelf.Id);
    }

    [Fact]
    public void Repair_KeepsLocation()
    {
        var item = NewItem();
        var location = item.CurrentLocationId;

        item.SendToRepair();
        item.Status.Should().Be(ItemStatus.InRepair);
        item.CurrentLocationId.Should().Be(location);

        item.CompleteRepair();
        item.Status.Should().Be(ItemStatus.InStock);
    }

    [Fact]
    public void AssignedItem_AllowsOnlyReturn()
    {
        var item = NewItem();
        item.Assign();

        ShouldBeInvalid(item.Assign);
        ShouldBeInvalid(item.SendToRepair);
        ShouldBeInvalid(item.CompleteRepair);
    }

    [Fact]
    public void InRepairItem_CannotBeAssignedOrReturned()
    {
        var item = NewItem();
        item.SendToRepair();

        ShouldBeInvalid(item.Assign);
        ShouldBeInvalid(() => item.Return(Shelf()));
        ShouldBeInvalid(item.SendToRepair);
    }

    [Fact]
    public void InStockItem_CannotBeReturnedOrCompleteRepair()
    {
        var item = NewItem();

        ShouldBeInvalid(() => item.Return(Shelf()));
        ShouldBeInvalid(item.CompleteRepair);
    }

    [Fact]
    public void DisposedItem_IsFinal()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        Service.DisposeItem(item, laptop, Shelf(), MovementReason.Damaged, null, Context);

        ShouldBeInvalid(item.Assign);
        ShouldBeInvalid(() => item.Return(Shelf()));
        ShouldBeInvalid(item.SendToRepair);
        ShouldBeInvalid(item.CompleteRepair);
        ShouldBeInvalid(() => Service.DisposeItem(item, laptop, null, MovementReason.Damaged, null, Context));
        ShouldBeInvalid(() => Service.TransferItem(item, laptop, Shelf("Raf B2"), Context));
    }

    [Fact]
    public void SerialNumber_IsLimitedTo100Characters()
    {
        var item = NewItem();

        var act = () => item.ChangeSerialNumber(new string('S', Item.SerialNumberMaxLength + 1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.SerialNumberTooLong);
    }

    [Fact]
    public void SerialNumber_BlankBecomesNull()
    {
        var item = ReceiveItem(Laptop(), Shelf(), "SN-1");

        item.ChangeSerialNumber("   ");

        item.SerialNumber.Should().BeNull();
    }
}
