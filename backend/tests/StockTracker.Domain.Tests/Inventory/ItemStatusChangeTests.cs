using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Inventory;

public class ItemStatusChangeTests
{
    [Fact]
    public void SendToRepair_ThenCompleteRepair_WritesStatusChanges()
    {
        var laptop = Laptop();
        var shelf = Shelf();
        var item = ReceiveItem(laptop, shelf);

        var sent = Service.SendToRepair(item, laptop, " fan sesi ", Context);
        item.Status.Should().Be(ItemStatus.InRepair);
        item.CurrentLocationId.Should().Be(shelf.Id, "the location does not change");

        var back = Service.CompleteRepair(item, laptop, "fan değişti", Context);
        item.Status.Should().Be(ItemStatus.InStock);

        sent.Type.Should().Be(MovementType.StatusChange);
        sent.FromStatus.Should().Be(ItemStatus.InStock);
        sent.ToStatus.Should().Be(ItemStatus.InRepair);
        sent.Note.Should().Be("fan sesi");
        sent.FromLocationId.Should().BeNull();
        sent.ToLocationId.Should().BeNull();
        back.FromStatus.Should().Be(ItemStatus.InRepair);
        back.ToStatus.Should().Be(ItemStatus.InStock);
    }

    [Fact]
    public void StatusChange_WithoutNote_IsRejectedAndKeepsStatus()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        var act = () => Service.SendToRepair(item, laptop, null, Context);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.NoteRequired);
        item.Status.Should().Be(ItemStatus.InStock);
    }

    [Fact]
    public void CompleteRepair_OnInStockItem_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        var act = () => Service.CompleteRepair(item, laptop, "not", Context);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidItemTransition);
    }

    [Fact]
    public void SendToRepair_OnAssignedItem_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        Service.AssignItem(item, laptop, Guid.NewGuid(), true, null, new DateOnly(2026, 10, 25), null, Context);

        var act = () => Service.SendToRepair(item, laptop, "not", Context);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidItemTransition);
    }

    public static TheoryData<ItemStatus> AllStatuses => new() { ItemStatus.InStock, ItemStatus.Assigned, ItemStatus.InRepair, ItemStatus.Disposed };

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Actions_MatchTheTransitionsEachStateAllows(ItemStatus status)
    {
        var state = ItemState.For(status);
        var checks = new Dictionary<ItemAction, Action>
        {
            [ItemAction.Transfer] = state.EnsureCanTransfer,
            [ItemAction.Assign] = () => state.Assign(),
            [ItemAction.Return] = () => state.Return(),
            [ItemAction.SendToRepair] = () => state.SendToRepair(),
            [ItemAction.ReturnFromRepair] = () => state.CompleteRepair(),
            [ItemAction.Dispose] = () => state.Dispose(),
        };

        foreach (var (action, check) in checks)
        {
            if (state.Actions.Contains(action))
            {
                check.Should().NotThrow($"{status} lists {action}");
            }
            else
            {
                check.Should().Throw<DomainException>($"{status} does not list {action}");
            }
        }
    }
}
