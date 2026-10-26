using StockTracker.Domain.Common;

namespace StockTracker.Domain.Counts;

/// <summary>
/// One thing to count at one location: a quantity product (ItemId null) or a single item.
/// Expected lines come from the start snapshot; unexpected lines from scans.
/// </summary>
public sealed class CountLine : BaseEntity
{
    private CountLine()
    {
    }

    public Guid CountSessionId { get; private set; }

    /// <summary>Where the line is counted (expected location, or the scan's context location).</summary>
    public Guid LocationId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? ItemId { get; private set; }

    /// <summary>Snapshot at the start; 0 for unexpected lines.</summary>
    public decimal ExpectedQuantity { get; private set; }

    /// <summary>Sum of the line's scans; null while nothing has been scanned.</summary>
    public decimal? CountedQuantity { get; private set; }

    public CountLineType LineType { get; private set; }

    public CountResolution? Resolution { get; private set; }

    public bool IsItem => ItemId is not null;

    public bool IsCounted => CountedQuantity is not null;

    internal static CountLine Expected(Guid sessionId, Guid locationId, Guid productId, Guid? itemId, decimal quantity) =>
        new()
        {
            CountSessionId = sessionId,
            LocationId = locationId,
            ProductId = productId,
            ItemId = itemId,
            ExpectedQuantity = itemId is null ? quantity : 1,
            LineType = CountLineType.Expected,
        };

    internal static CountLine Unexpected(Guid sessionId, Guid locationId, Guid productId, Guid? itemId) =>
        new()
        {
            CountSessionId = sessionId,
            LocationId = locationId,
            ProductId = productId,
            ItemId = itemId,
            ExpectedQuantity = 0,
            LineType = CountLineType.Unexpected,
        };

    internal void SetCounted(decimal? quantity) => CountedQuantity = quantity;

    internal void Resolve(CountResolution? resolution) => Resolution = resolution;
}

/// <summary>One scan of a line; kept so the last scan can be undone and recent scans listed.</summary>
public sealed class CountScan
{
    private CountScan()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid CountSessionId { get; private init; }

    public Guid CountLineId { get; private init; }

    /// <summary>Increasing within the session; orders scans reliably.</summary>
    public int Sequence { get; private init; }

    public decimal Quantity { get; private init; }

    public Guid ScannedBy { get; private init; }

    public DateTime ScannedAt { get; private init; }

    internal static CountScan Create(Guid sessionId, Guid lineId, int sequence, decimal quantity, Guid userId, DateTime at) =>
        new()
        {
            CountSessionId = sessionId,
            CountLineId = lineId,
            Sequence = sequence,
            Quantity = quantity,
            ScannedBy = userId,
            ScannedAt = at,
        };
}
