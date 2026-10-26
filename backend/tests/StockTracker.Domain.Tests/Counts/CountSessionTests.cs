using FluentAssertions;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Locations;
using static StockTracker.Domain.Tests.Counts.CountFixture;

namespace StockTracker.Domain.Tests.Counts;

public class CountSessionTests
{
    private readonly CountFixture _f = new();

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
    public void Start_SnapshotsExpectedLines()
    {
        var session = _f.StartB2();

        session.Status.Should().Be(CountStatus.Counting);
        session.StartedBy.Should().Be(UserId);
        session.Lines.Should().HaveCount(2).And.OnlyContain(l => l.LineType == CountLineType.Expected && !l.IsCounted);
        session.Lines.Single(l => l.ItemId is null).ExpectedQuantity.Should().Be(200);
        session.Lines.Single(l => l.ItemId is not null).ExpectedQuantity.Should().Be(1);
    }

    [Fact]
    public void Start_OnInactiveLocation_IsRejected()
    {
        var empty = Location.Create("LOC-CCCCCC", "Boş", LocationType.Shelf, null);
        empty.Deactivate(hasContent: false);

        ShouldFail(() => CountSession.Start(empty, false, null, [], UserId, Now), CountErrors.LocationInactive);
    }

    [Theory]
    [InlineData("/a/", false, "/a/", false, true)]
    [InlineData("/a/", true, "/a/b/", false, true)]
    [InlineData("/a/b/", false, "/a/", true, true)]
    [InlineData("/a/", false, "/a/b/", false, false)]
    [InlineData("/a/b/", true, "/a/c/", true, false)]
    [InlineData("/a/b/", true, "/a/", false, false)]
    public void ScopesOverlap_SameOrContainedLocations(string a, bool aChildren, string b, bool bChildren, bool expected) =>
        CountSession.ScopesOverlap(a, aChildren, b, bChildren).Should().Be(expected);

    [Fact]
    public void StateMachine_AllowsOnlyPlannedTransitions()
    {
        var session = _f.StartB2();

        session.Submit();
        session.Status.Should().Be(CountStatus.Reviewing);
        ShouldFail(session.Submit, CountErrors.InvalidTransition);

        session.Cancel();
        session.Status.Should().Be(CountStatus.Cancelled);
        ShouldFail(session.Cancel, CountErrors.InvalidTransition);
        ShouldFail(session.Submit, CountErrors.InvalidTransition);

        var counting = _f.StartB2();
        counting.Cancel();
        counting.Status.Should().Be(CountStatus.Cancelled);
    }

    [Fact]
    public void Scan_AfterSubmit_IsRejected()
    {
        var session = _f.StartB2();
        session.Submit();

        ShouldFail(() => session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopB2, UserId, Now), CountErrors.NotCounting);
        ShouldFail(() => session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 5, UserId, Now), CountErrors.NotCounting);
    }

    [Fact]
    public void ScanItem_CountsExpectedLine_OnceOnly()
    {
        var session = _f.StartB2();

        var first = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopB2, UserId, Now);
        var again = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopB2, UserId, Now);

        first.Outcome.Should().Be(CountScanOutcome.Counted);
        first.Line.CountedQuantity.Should().Be(1);
        again.Outcome.Should().Be(CountScanOutcome.AlreadyCounted);
        again.Scan.Should().BeNull();
        session.Scans.Should().ContainSingle();
    }

    [Fact]
    public void ScanQuantity_AddsUpRepeatedScans()
    {
        var session = _f.StartB2();

        session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 100, UserId, Now);
        var result = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 85, UserId, Now);

        result.Outcome.Should().Be(CountScanOutcome.Counted);
        result.Line.CountedQuantity.Should().Be(185);
        session.Scans.Select(s => s.Sequence).Should().Equal(1, 2);
    }

    [Fact]
    public void ScanItem_NotExpectedHere_OpensUnexpectedLine()
    {
        var session = _f.StartB2();

        var result = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopA1, UserId, Now);

        result.Outcome.Should().Be(CountScanOutcome.Unexpected);
        result.Line.LineType.Should().Be(CountLineType.Unexpected);
        result.Line.LocationId.Should().Be(_f.ShelfB2.Id);
        result.Line.ExpectedQuantity.Should().Be(0);
        result.Line.CountedQuantity.Should().Be(1);
    }

    [Fact]
    public void ScanQuantity_UnexpectedProduct_OpensUnexpectedLine()
    {
        var session = CountSession.Start(_f.ShelfB2, false, null, [], UserId, Now);

        var result = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 7, UserId, Now);

        result.Outcome.Should().Be(CountScanOutcome.Unexpected);
        result.Line.CountedQuantity.Should().Be(7);
    }

    [Fact]
    public void Scan_OutsideScope_IsRejected()
    {
        var single = _f.StartB2();
        ShouldFail(() => single.ScanItem(_f.ShelfB2, _f.ShelfA1, _f.LaptopA1, UserId, Now), CountErrors.LocationOutOfScope, "locationId");

        var building = CountSession.Start(_f.Building, true, null, [], UserId, Now);
        building.ScanItem(_f.Building, _f.ShelfA1, _f.LaptopA1, UserId, Now).Outcome.Should().Be(CountScanOutcome.Unexpected);
        ShouldFail(() => building.ScanItem(_f.Building, _f.Elsewhere, _f.LaptopA1, UserId, Now), CountErrors.LocationOutOfScope);
    }

    [Fact]
    public void ScanQuantity_OfSerializedProduct_AsksForItemLabel()
    {
        var session = _f.StartB2();

        ShouldFail(() => session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Laptop, 1, UserId, Now), CountErrors.ScanItemLabel, "code");
    }

    [Fact]
    public void UndoLastScan_RemovesOnlyTheLastScan_AndEmptyUnexpectedLines()
    {
        var session = _f.StartB2();
        var screwLine = session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 100, UserId, Now).Line;
        session.ScanQuantity(_f.ShelfB2, _f.ShelfB2, _f.Screw, 90, UserId, Now);
        var unexpected = session.ScanItem(_f.ShelfB2, _f.ShelfB2, _f.LaptopA1, UserId, Now).Line;

        session.UndoLastScan(screwLine.Id);
        screwLine.CountedQuantity.Should().Be(100);
        session.UndoLastScan(screwLine.Id);
        screwLine.CountedQuantity.Should().BeNull("an expected line without scans is not counted");
        session.Lines.Should().Contain(screwLine);
        ShouldFail(() => session.UndoLastScan(screwLine.Id), CountErrors.NothingToUndo);

        session.UndoLastScan(unexpected.Id);
        session.Lines.Should().NotContain(unexpected);
    }
}
