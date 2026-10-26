using StockTracker.Domain.Inventory;

namespace StockTracker.Domain.Counts;

/// <summary>How a line compares with the stock at review time.</summary>
public enum CountLineState
{
    /// <summary>Counted as the current stock; nothing to decide.</summary>
    Matched,

    /// <summary>Quantity never counted and nothing in stock there; nothing to decide.</summary>
    NotCountedEmpty,

    /// <summary>Counted quantity differs from the current balance.</summary>
    QuantityDifference,

    /// <summary>Quantity in stock but never counted.</summary>
    QuantityNotCounted,

    /// <summary>Expected item not scanned and still recorded at the line's location.</summary>
    ItemMissing,

    /// <summary>Expected item not scanned here but scanned at another location of this count.</summary>
    ItemFoundElsewhere,

    /// <summary>Expected item not scanned, but it left the location (or changed status) during the count.</summary>
    ItemMovedDuringCount,

    /// <summary>Unexpected in-stock item recorded at another location.</summary>
    UnexpectedMovable,

    /// <summary>Unexpected item that is now recorded here anyway (moved during the count).</summary>
    UnexpectedAlreadyHere,

    /// <summary>Unexpected item that is assigned, in repair or disposed; only noted.</summary>
    UnexpectedNoteOnly,
}

/// <summary>An item's current status and location at review time.</summary>
public sealed record ItemSnapshot(ItemStatus Status, Guid? LocationId);

/// <param name="CurrentQuantity">Balance (quantity lines) or 1/0 (item lines: recorded at the line's location or not).</param>
/// <param name="Difference">Change approving the line would make; 0 when nothing to decide.</param>
/// <param name="Options">Allowed decisions; empty when the line needs none.</param>
public sealed record CountReviewLine(
    CountLine Line,
    CountLineState State,
    decimal CurrentQuantity,
    decimal Difference,
    IReadOnlyList<CountResolution> Options,
    ItemSnapshot? Item);

/// <summary>
/// Compares each line with the stock at review time (not the start snapshot), following the plan's decision table.
/// </summary>
public static class CountReview
{
    private static readonly CountResolution[] QuantityDifferenceOptions = [CountResolution.ApplyDifference, CountResolution.Ignore];
    private static readonly CountResolution[] NotCountedOptions = [CountResolution.AcceptZero, CountResolution.OutOfScope];
    private static readonly CountResolution[] MissingOptions = [CountResolution.MarkLost, CountResolution.Ignore];
    private static readonly CountResolution[] MovableOptions = [CountResolution.MoveHere, CountResolution.Ignore];

    public static IReadOnlyList<CountReviewLine> Build(
        CountSession session,
        IReadOnlyDictionary<(Guid ProductId, Guid LocationId), decimal> balances,
        IReadOnlyDictionary<Guid, ItemSnapshot> items)
    {
        var scannedItems = session.Lines
            .Where(l => l.LineType == CountLineType.Unexpected && l.ItemId is not null && l.IsCounted)
            .Select(l => l.ItemId!.Value)
            .ToHashSet();

        return [.. session.Lines.Select(line => line.ItemId is { } itemId
            ? ItemLine(line, items[itemId], scannedItems.Contains(itemId))
            : QuantityLine(line, balances.GetValueOrDefault((line.ProductId, line.LocationId))))];
    }

    private static CountReviewLine QuantityLine(CountLine line, decimal current)
    {
        if (line.CountedQuantity is not { } counted)
        {
            return current > 0
                ? new(line, CountLineState.QuantityNotCounted, current, -current, NotCountedOptions, null)
                : new(line, CountLineState.NotCountedEmpty, current, 0, [], null);
        }

        var difference = counted - current;
        return difference != 0
            ? new(line, CountLineState.QuantityDifference, current, difference, QuantityDifferenceOptions, null)
            : new(line, CountLineState.Matched, current, 0, [], null);
    }

    private static CountReviewLine ItemLine(CountLine line, ItemSnapshot item, bool scannedElsewhere)
    {
        var isHere = item.LocationId == line.LocationId;
        var current = isHere ? 1 : 0;

        if (line.LineType == CountLineType.Expected)
        {
            if (line.IsCounted)
            {
                return new(line, CountLineState.Matched, current, 0, [], item);
            }

            if (scannedElsewhere)
            {
                return new(line, CountLineState.ItemFoundElsewhere, current, 0, [], item);
            }

            return isHere && item.Status is ItemStatus.InStock or ItemStatus.InRepair
                ? new(line, CountLineState.ItemMissing, current, -1, MissingOptions, item)
                : new(line, CountLineState.ItemMovedDuringCount, current, 0, [], item);
        }

        if (item.Status != ItemStatus.InStock)
        {
            return new(line, CountLineState.UnexpectedNoteOnly, current, 0, [], item);
        }

        return isHere
            ? new(line, CountLineState.UnexpectedAlreadyHere, current, 0, [], item)
            : new(line, CountLineState.UnexpectedMovable, current, 1, MovableOptions, item);
    }
}
