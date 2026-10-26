namespace StockTracker.Domain.Counts;

public enum CountStatus
{
    Counting,
    Reviewing,
    Approved,
    Cancelled,
}

public enum CountLineType
{
    /// <summary>Snapshot of the scope when the count started.</summary>
    Expected,

    /// <summary>Opened by a scan of something the snapshot did not expect there.</summary>
    Unexpected,
}

/// <summary>The approver's decision for a line with a difference (see the plan's decision table).</summary>
public enum CountResolution
{
    /// <summary>Quantity: adjust the balance to the counted quantity.</summary>
    ApplyDifference,

    /// <summary>Quantity never counted: treat it as 0 and adjust.</summary>
    AcceptZero,

    /// <summary>Quantity never counted: it was not part of this count; no movement.</summary>
    OutOfScope,

    /// <summary>Expected item not found: take it out as lost.</summary>
    MarkLost,

    /// <summary>Unexpected in-stock item: transfer it to where it was counted.</summary>
    MoveHere,

    /// <summary>Leave the stock as it is.</summary>
    Ignore,
}
