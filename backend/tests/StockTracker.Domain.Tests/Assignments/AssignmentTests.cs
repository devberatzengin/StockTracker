using FluentAssertions;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Assignments;

public class AssignmentTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);
    private static readonly Guid Assignee = Guid.NewGuid();

    private static void ShouldFail(Action act, string code, string? target = null)
    {
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Code.Should().Be(code);
        if (target is not null)
        {
            exception.Target.Should().Be(target);
        }
    }

    private static (Assignment Assignment, StockMovement Movement) Assign(
        Item item,
        Product product,
        bool assigneeIsActive = true,
        DateOnly? dueDate = null,
        string? note = null) =>
        Service.AssignItem(item, product, Assignee, assigneeIsActive, dueDate, Today, note, Context);

    private static MovementContext NewContext() => Context with { ReferenceId = Guid.NewGuid() };

    [Fact]
    public void Assign_CreatesOpenAssignmentAndEmptiesLocation()
    {
        var laptop = Laptop();
        var shelf = Shelf();
        var item = ReceiveItem(laptop, shelf);

        var (assignment, movement) = Assign(item, laptop, dueDate: Today.AddDays(7), note: " teslim ");

        item.Status.Should().Be(ItemStatus.Assigned);
        item.CurrentLocationId.Should().BeNull();
        assignment.ItemId.Should().Be(item.Id);
        assignment.AssigneeUserId.Should().Be(Assignee);
        assignment.AssignedByUserId.Should().Be(Context.UserId);
        assignment.AssignedAt.Should().Be(Context.OccurredAt);
        assignment.DueDate.Should().Be(Today.AddDays(7));
        assignment.Note.Should().Be("teslim");
        assignment.AssignReferenceId.Should().Be(Context.ReferenceId);
        assignment.IsOpen.Should().BeTrue();
        movement.Type.Should().Be(MovementType.Assign);
        movement.FromLocationId.Should().Be(shelf.Id, "the source is the item's last location");
        movement.ToLocationId.Should().BeNull();
    }

    [Fact]
    public void Assign_NonAssignableProduct_IsRejected()
    {
        var product = Product.Create("PRD-MONITR", "Monitör", null, Inventory.InventoryFixture.Category, Unit.Piece, TrackingType.Serialized, false, null);
        var item = ReceiveItem(product, Shelf());

        ShouldFail(() => Assign(item, product), AssignmentErrors.ProductNotAssignable, "itemId");
        item.Status.Should().Be(ItemStatus.InStock);
    }

    [Fact]
    public void Assign_AlreadyAssignedItem_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        Assign(item, laptop);

        ShouldFail(() => Assign(item, laptop), InventoryErrors.InvalidItemTransition);
    }

    [Fact]
    public void Assign_ItemInRepair_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        Service.SendToRepair(item, laptop, "ekran", Context);

        ShouldFail(() => Assign(item, laptop), InventoryErrors.InvalidItemTransition);
    }

    [Fact]
    public void Assign_InactiveAssignee_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        ShouldFail(() => Assign(item, laptop, assigneeIsActive: false), AssignmentErrors.AssigneeInactive, "assigneeUserId");
        item.Status.Should().Be(ItemStatus.InStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Assign_DueDateNotAfterToday_IsRejected(int days)
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        ShouldFail(() => Assign(item, laptop, dueDate: Today.AddDays(days)), AssignmentErrors.DueDateNotInFuture, "dueDate");
        item.Status.Should().Be(ItemStatus.InStock);
    }

    [Fact]
    public void Assign_NoteTooLong_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());

        ShouldFail(() => Assign(item, laptop, note: new string('x', 501)), AssignmentErrors.NoteTooLong, "note");
    }

    [Fact]
    public void AssignedItem_CannotBeTransferred()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        Assign(item, laptop);

        ShouldFail(() => Service.TransferItem(item, laptop, Shelf("Raf B2"), Context), InventoryErrors.InvalidItemTransition);
    }

    [Fact]
    public void ReturnGood_PutsItemInStockAtReturnLocation()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);
        var returnShelf = Shelf("Raf B2");
        var context = NewContext();

        var movements = Service.ReturnItem(assignment, item, laptop, returnShelf, ReturnCondition.Good, null, context);

        item.Status.Should().Be(ItemStatus.InStock);
        item.CurrentLocationId.Should().Be(returnShelf.Id);
        movements.Should().ContainSingle();
        movements[0].Type.Should().Be(MovementType.Return);
        movements[0].ToLocationId.Should().Be(returnShelf.Id);
        assignment.IsOpen.Should().BeFalse();
        assignment.ReturnedAt.Should().Be(context.OccurredAt);
        assignment.ReturnedByUserId.Should().Be(context.UserId);
        assignment.ReturnLocationId.Should().Be(returnShelf.Id);
        assignment.ReturnCondition.Should().Be(ReturnCondition.Good);
        assignment.ReturnReferenceId.Should().Be(context.ReferenceId);
    }

    [Fact]
    public void ReturnFaulty_RequiresNote()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);

        ShouldFail(
            () => Service.ReturnItem(assignment, item, laptop, Shelf(), ReturnCondition.Faulty, " ", NewContext()),
            AssignmentErrors.ReturnNoteRequired,
            "note");
        item.Status.Should().Be(ItemStatus.Assigned);
        assignment.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void ReturnFaulty_SendsItemToRepairWithSameReference()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);
        var context = NewContext();

        var movements = Service.ReturnItem(assignment, item, laptop, Shelf(), ReturnCondition.Faulty, "ekran kırık", context);

        item.Status.Should().Be(ItemStatus.InRepair);
        item.CurrentLocationId.Should().NotBeNull();
        movements.Select(m => m.Type).Should().Equal(MovementType.Return, MovementType.StatusChange);
        movements.Should().OnlyContain(m => m.ReferenceId == context.ReferenceId);
        movements[1].FromStatus.Should().Be(ItemStatus.InStock);
        movements[1].ToStatus.Should().Be(ItemStatus.InRepair);
        movements[1].Note.Should().Be("ekran kırık");
        assignment.ReturnNote.Should().Be("ekran kırık");
    }

    [Fact]
    public void Return_Twice_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);
        Service.ReturnItem(assignment, item, laptop, Shelf(), ReturnCondition.Good, null, NewContext());

        ShouldFail(
            () => Service.ReturnItem(assignment, item, laptop, Shelf(), ReturnCondition.Good, null, NewContext()),
            AssignmentErrors.AlreadyReturned);
    }

    [Fact]
    public void Return_ToInactiveLocation_IsRejected()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);
        var inactive = Shelf("Eski raf");
        inactive.Deactivate(hasContent: false);

        ShouldFail(
            () => Service.ReturnItem(assignment, item, laptop, inactive, ReturnCondition.Good, null, NewContext()),
            InventoryErrors.LocationInactive,
            "returnLocationId");
        item.Status.Should().Be(ItemStatus.Assigned);
    }

    [Fact]
    public void IsOverdue_IsComputedFromDueDateAndReturn()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop, dueDate: Today.AddDays(2));

        assignment.IsOverdue(Today.AddDays(2)).Should().BeFalse("the due day itself is not late");
        assignment.IsOverdue(Today.AddDays(3)).Should().BeTrue();

        Service.ReturnItem(assignment, item, laptop, Shelf(), ReturnCondition.Good, null, NewContext());
        assignment.IsOverdue(Today.AddDays(3)).Should().BeFalse();
    }

    [Fact]
    public void WithoutDueDate_IsNeverOverdue()
    {
        var laptop = Laptop();
        var item = ReceiveItem(laptop, Shelf());
        var (assignment, _) = Assign(item, laptop);

        assignment.IsOverdue(Today.AddYears(1)).Should().BeFalse();
    }
}
