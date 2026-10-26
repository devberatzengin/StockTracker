using FluentAssertions;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Domain.Tests.Counts.CountFixture;
using static StockTracker.Domain.Tests.Inventory.InventoryFixture;

namespace StockTracker.Domain.Tests.Counts;

public class CountApprovalTests
{
    private readonly CountFixture _f = new();

    private CountReviewLine LineFor(IReadOnlyList<CountReviewLine> review, CountLine line) =>
        review.Single(r => r.Line.Id == line.Id);

    private IReadOnlyList<StockMovement> Approve(CountSession session, Dictionary<Guid, CountResolution> decisions, MovementContext? context = null) =>
        _f.Approval.Approve(session, _f.Review(session), decisions, _f, context ?? ApprovalContext());

    [Fact]
    public void Review_FollowsTheDecisionTable()
    {
        var session = _f.StartB2();
        var screw = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 185, UserId, Now).Line;
        var unexpected = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopA1, UserId, Now).Line;
        var expectedLaptop = session.Lines.Single(l => l.ItemId == _f.LaptopB2.Id);
        session.Submit();

        var review = _f.Review(session);

        var screwReview = LineFor(review, screw);
        screwReview.State.Should().Be(CountLineState.QuantityDifference);
        screwReview.Difference.Should().Be(-15);
        screwReview.Options.Should().Equal(CountResolution.ApplyDifference, CountResolution.Ignore);

        var missing = LineFor(review, expectedLaptop);
        missing.State.Should().Be(CountLineState.ItemMissing);
        missing.Options.Should().Equal(CountResolution.MarkLost, CountResolution.Ignore);

        var movable = LineFor(review, unexpected);
        movable.State.Should().Be(CountLineState.UnexpectedMovable);
        movable.Options.Should().Equal(CountResolution.MoveHere, CountResolution.Ignore);
    }

    [Fact]
    public void Review_UncountedQuantity_OffersZeroOrOutOfScope()
    {
        var session = _f.StartB2();
        session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopB2, UserId, Now);
        session.Submit();

        var screw = LineFor(_f.Review(session), session.Lines.Single(l => l.ItemId is null));

        screw.State.Should().Be(CountLineState.QuantityNotCounted);
        screw.Difference.Should().Be(-200);
        screw.Options.Should().Equal(CountResolution.AcceptZero, CountResolution.OutOfScope);
    }

    [Fact]
    public void Review_UsesCurrentBalance_NotTheSnapshot()
    {
        var session = _f.StartB2();
        session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 185, UserId, Now);
        session.Submit();

        // 20 screws were moved away after the count started: 185 counted vs 180 now.
        Service.TransferQuantity(_f.Screw, _f.ShelfB2, _f.ShelfA1, _f.ScrewB2, StockBalance.Empty(_f.Screw.Id, _f.ShelfA1.Id), 20, Context);

        var screw = _f.Review(session).Single(r => r.Line.ItemId is null);
        screw.CurrentQuantity.Should().Be(180);
        screw.Difference.Should().Be(5);

        var movements = Approve(session, new() { [screw.Line.Id] = CountResolution.ApplyDifference, [session.Lines.Single(l => l.ItemId is not null).Id] = CountResolution.Ignore });
        _f.ScrewB2.Quantity.Should().Be(185);
        movements.Should().ContainSingle().Which.ToLocationId.Should().Be(_f.ShelfB2.Id);
    }

    [Fact]
    public void Review_ItemFoundOnAnotherShelf_IsNotMissing()
    {
        var session = CountSession.Start(
            _f.Building, true, null, [new ExpectedStock(_f.ShelfB2.Id, _f.Laptop.Id, _f.LaptopB2.Id, 1)], UserId, Now);
        session.ScanItem(_f.Building, _f.ShelfA1, _f.LaptopB2, UserId, Now);
        session.Submit();

        var review = _f.Review(session);

        review.Single(r => r.Line.LineType == CountLineType.Expected).State.Should().Be(CountLineState.ItemFoundElsewhere);
        review.Single(r => r.Line.LineType == CountLineType.Unexpected).State.Should().Be(CountLineState.UnexpectedMovable);
    }

    [Fact]
    public void Review_AssignedUnexpectedItem_IsOnlyNoted()
    {
        var session = _f.StartB2();
        Service.AssignItem(_f.LaptopA1, _f.Laptop, Guid.NewGuid(), true, null, new DateOnly(2026, 10, 26), null, Context);
        var unexpected = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopA1, UserId, Now).Line;
        session.Submit();

        var line = LineFor(_f.Review(session), unexpected);

        line.State.Should().Be(CountLineState.UnexpectedNoteOnly);
        line.Options.Should().BeEmpty();
        line.Item!.Status.Should().Be(ItemStatus.Assigned);
    }

    [Fact]
    public void Approve_WithUndecidedLine_IsRejected_AndChangesNothing()
    {
        var session = _f.StartB2();
        var screw = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 185, UserId, Now).Line;
        var laptop = session.Lines.Single(l => l.ItemId is not null);
        session.Submit();

        var act = () => Approve(session, new() { [screw.Id] = CountResolution.ApplyDifference });

        var error = act.Should().Throw<DomainException>().Which;
        error.Code.Should().Be(CountErrors.UndecidedLines);
        error.Target.Should().Be($"decisions[{laptop.Id}]");
        session.Status.Should().Be(CountStatus.Reviewing);
        _f.ScrewB2.Quantity.Should().Be(200);
    }

    [Fact]
    public void Approve_WithDecisionNotOffered_IsRejected()
    {
        var session = _f.StartB2();
        var screw = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 185, UserId, Now).Line;
        var laptop = session.Lines.Single(l => l.ItemId is not null);
        session.Submit();

        var act = () => Approve(session, new() { [screw.Id] = CountResolution.MarkLost, [laptop.Id] = CountResolution.Ignore });

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CountErrors.InvalidResolution);
    }

    [Fact]
    public void Approve_WhileCounting_IsRejected()
    {
        var session = _f.StartB2();

        var act = () => Approve(session, []);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CountErrors.InvalidTransition);
    }

    [Fact]
    public void Approve_TurnsDecisionsIntoMovements_WithOneReference()
    {
        var session = _f.StartB2();
        var screw = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 185, UserId, Now).Line;
        var unexpected = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopA1, UserId, Now).Line;
        var missing = session.Lines.Single(l => l.ItemId == _f.LaptopB2.Id);
        session.Submit();
        var context = ApprovalContext();

        var movements = Approve(session, new()
        {
            [screw.Id] = CountResolution.ApplyDifference,
            [unexpected.Id] = CountResolution.MoveHere,
            [missing.Id] = CountResolution.MarkLost,
        }, context);

        movements.Should().OnlyContain(m => m.ReferenceId == context.ReferenceId);

        var adjustment = movements.Single(m => m.Type == MovementType.Adjustment);
        adjustment.FromLocationId.Should().Be(_f.ShelfB2.Id);
        adjustment.Quantity.Should().Be(15);
        _f.ScrewB2.Quantity.Should().Be(185);

        var lost = movements.Single(m => m.Type == MovementType.Out);
        lost.Reason.Should().Be(MovementReason.Lost);
        lost.ItemId.Should().Be(_f.LaptopB2.Id);
        _f.LaptopB2.Status.Should().Be(ItemStatus.Disposed);

        var transfer = movements.Single(m => m.Type == MovementType.Transfer);
        transfer.FromLocationId.Should().Be(_f.ShelfA1.Id);
        transfer.ToLocationId.Should().Be(_f.ShelfB2.Id);
        _f.LaptopA1.CurrentLocationId.Should().Be(_f.ShelfB2.Id);

        session.Status.Should().Be(CountStatus.Approved);
        session.ApprovalReferenceId.Should().Be(context.ReferenceId);
        session.ApprovedBy.Should().Be(UserId);
        screw.Resolution.Should().Be(CountResolution.ApplyDifference);
        missing.Resolution.Should().Be(CountResolution.MarkLost);
    }

    [Fact]
    public void Approve_IgnoreAndOutOfScope_CreateNoMovements()
    {
        var session = _f.StartB2();
        session.Submit();
        var screw = session.Lines.Single(l => l.ItemId is null);
        var laptop = session.Lines.Single(l => l.ItemId is not null);

        var movements = Approve(session, new() { [screw.Id] = CountResolution.OutOfScope, [laptop.Id] = CountResolution.Ignore });

        movements.Should().BeEmpty();
        _f.ScrewB2.Quantity.Should().Be(200);
        _f.LaptopB2.Status.Should().Be(ItemStatus.InStock);
        session.Status.Should().Be(CountStatus.Approved);
    }

    [Fact]
    public void AcceptZero_TakesTheWholeBalanceOut()
    {
        var session = _f.StartB2();
        session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopB2, UserId, Now);
        session.Submit();
        var screw = session.Lines.Single(l => l.ItemId is null);

        var movements = Approve(session, new() { [screw.Id] = CountResolution.AcceptZero });

        movements.Should().ContainSingle().Which.Quantity.Should().Be(200);
        _f.ScrewB2.Quantity.Should().Be(0);
    }

    [Fact]
    public void UnexpectedQuantity_IsAddedToANewBalance()
    {
        var session = CountSession.Start(_f.ShelfA1, false, null, [], UserId, Now);
        var line = session.ScanQuantity(_f.ShelfA1, _f.ShelfA1, _f.Screw, 12, UserId, Now).Line;
        session.Submit();

        var movements = Approve(session, new() { [line.Id] = CountResolution.ApplyDifference });

        movements.Should().ContainSingle().Which.ToLocationId.Should().Be(_f.ShelfA1.Id);
        _f.CreatedBalances.Should().ContainSingle().Which.Quantity.Should().Be(12);
    }
}
