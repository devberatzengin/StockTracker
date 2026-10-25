using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;

namespace StockTracker.Domain.Tests.Inventory;

public class StockMovementTests
{
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static Action Create(
        MovementType type,
        Guid? from,
        Guid? to,
        Guid? itemId = null,
        decimal quantity = 5,
        MovementReason? reason = null,
        string? note = null,
        ItemStatus? fromStatus = null,
        ItemStatus? toStatus = null) =>
        () => StockMovement.Create(type, Product, itemId, from, to, itemId is null ? quantity : 1, reason, note, InventoryFixture.Context, fromStatus, toStatus);

    public static TheoryData<MovementType, bool, bool> ValidLocationShapes => new()
    {
        { MovementType.In, false, true },
        { MovementType.Out, true, false },
        { MovementType.Transfer, true, true },
        { MovementType.Adjustment, true, false },
        { MovementType.Adjustment, false, true },
        { MovementType.Assign, true, false },
        { MovementType.Return, false, true },
    };

    public static TheoryData<MovementType, bool, bool> InvalidLocationShapes => new()
    {
        { MovementType.In, true, true },
        { MovementType.In, false, false },
        { MovementType.Out, false, false },
        { MovementType.Out, true, true },
        { MovementType.Transfer, true, false },
        { MovementType.Transfer, false, true },
        { MovementType.Adjustment, true, true },
        { MovementType.Adjustment, false, false },
        { MovementType.Assign, false, false },
        { MovementType.Return, true, true },
        { MovementType.StatusChange, true, false },
    };

    [Theory]
    [MemberData(nameof(ValidLocationShapes))]
    public void LocationRules_AcceptValidShapes(MovementType type, bool hasFrom, bool hasTo)
    {
        var reason = type == MovementType.Out ? MovementReason.Consumed : (MovementReason?)null;

        Create(type, hasFrom ? A : null, hasTo ? B : null, reason: reason).Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(InvalidLocationShapes))]
    public void LocationRules_RejectInvalidShapes(MovementType type, bool hasFrom, bool hasTo)
    {
        var reason = type == MovementType.Out ? MovementReason.Consumed : (MovementReason?)null;

        Create(type, hasFrom ? A : null, hasTo ? B : null, reason: reason)
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidMovement);
    }

    [Fact]
    public void Transfer_ToSameLocation_IsRejected()
    {
        Create(MovementType.Transfer, A, A)
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.SameSourceAndTarget);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuantityMovement_RequiresPositiveQuantity(decimal quantity)
    {
        Create(MovementType.In, null, B, quantity: quantity)
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidQuantity);
    }

    [Fact]
    public void SerializedMovement_HasQuantityOne()
    {
        var movement = StockMovement.Create(MovementType.In, Product, Item, null, B, 1, null, null, InventoryFixture.Context);
        var wrong = () => StockMovement.Create(MovementType.In, Product, Item, null, B, 2, null, null, InventoryFixture.Context);

        movement.Quantity.Should().Be(1);
        wrong.Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidQuantity);
    }

    [Fact]
    public void Out_RequiresReason()
    {
        Create(MovementType.Out, A, null)
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.ReasonRequired);
    }

    [Fact]
    public void ReasonOther_RequiresNote()
    {
        Create(MovementType.Out, A, null, reason: MovementReason.Other, note: "  ")
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.NoteRequired);
        Create(MovementType.Out, A, null, reason: MovementReason.Other, note: "Sayımda kayıp")
            .Should().NotThrow();
    }

    [Fact]
    public void Note_IsLimitedTo500Characters()
    {
        Create(MovementType.Out, A, null, reason: MovementReason.Damaged, note: new string('a', 501))
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.NoteTooLong);
    }

    [Fact]
    public void StatusChange_RequiresItemAndBothStatuses()
    {
        Create(MovementType.StatusChange, null, null, Item, fromStatus: ItemStatus.InStock)
            .Should().Throw<DomainException>().Which.Code.Should().Be(InventoryErrors.InvalidMovement);
        Create(MovementType.StatusChange, null, null, Item, fromStatus: ItemStatus.InStock, toStatus: ItemStatus.InRepair)
            .Should().NotThrow();
    }

    [Fact]
    public void Create_CopiesContextAndTrimsNote()
    {
        var movement = StockMovement.Create(
            MovementType.Out, Product, null, A, null, 2.5m, MovementReason.Damaged, "  kırık ", InventoryFixture.Context);

        movement.ReferenceId.Should().Be(InventoryFixture.Context.ReferenceId);
        movement.UserId.Should().Be(InventoryFixture.Context.UserId);
        movement.OccurredAt.Should().Be(InventoryFixture.Context.OccurredAt);
        movement.Note.Should().Be("kırık");
    }
}
