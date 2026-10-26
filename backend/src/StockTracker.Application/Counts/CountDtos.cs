using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Counts;

public sealed record CountListItemDto(
    Guid Id,
    Guid LocationId,
    string LocationPath,
    bool IncludeChildren,
    CountStatus Status,
    string StartedByName,
    DateTime StartedAt,
    DateTime? ApprovedAt,
    int ExpectedLines,
    int CountedExpectedLines,
    int UnexpectedLines);

public sealed record CountLineDto(
    Guid Id,
    CountLineType LineType,
    Guid LocationId,
    string LocationName,
    string LocationPath,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Unit Unit,
    Guid? ItemId,
    string? ItemCode,
    string? SerialNumber,
    decimal ExpectedQuantity,
    decimal? CountedQuantity,
    CountResolution? Resolution);

/// <param name="CanUndo">Last scan of its line while the count is still counting.</param>
public sealed record CountScanDto(
    Guid Id,
    Guid LineId,
    int Sequence,
    decimal Quantity,
    DateTime ScannedAt,
    string ScannedByName,
    string ProductName,
    Unit Unit,
    string? ItemCode,
    string LocationName,
    CountLineType LineType,
    bool CanUndo);

/// <param name="ExpectedLines">Lines of the start snapshot.</param>
/// <param name="CountedExpectedLines">Snapshot lines scanned at least once.</param>
public sealed record CountProgressDto(int ExpectedLines, int CountedExpectedLines, int UnexpectedLines);

public sealed record CountDto(
    Guid Id,
    Guid LocationId,
    string LocationName,
    string LocationPath,
    bool IncludeChildren,
    CountStatus Status,
    string? Note,
    Guid StartedBy,
    string StartedByName,
    DateTime StartedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    Guid? ApprovalReferenceId,
    CountProgressDto Progress,
    IReadOnlyList<CountLineDto> Lines,
    IReadOnlyList<CountScanDto> RecentScans);

public sealed record CountReviewLineDto(
    CountLineDto Line,
    CountLineState State,
    decimal CurrentQuantity,
    decimal Difference,
    IReadOnlyList<CountResolution> Options,
    ItemStatus? ItemStatus,
    string? ItemLocationPath);

/// <param name="MovementsSinceStart">Movements in the scope after the count started (excluding the approval's own).</param>
public sealed record CountReviewDto(
    Guid CountId,
    CountStatus Status,
    IReadOnlyList<CountReviewLineDto> Lines,
    int MovementsSinceStart);

public enum CountScanKind
{
    /// <summary>A location label: the client switches its context location; nothing changes.</summary>
    Location,

    /// <summary>A quantity product without a quantity: the client asks for it and scans again.</summary>
    QuantityRequired,

    Counted,
    Unexpected,
    AlreadyCounted,
}

/// <param name="ItemStatus">Scanned item's status (unexpected items: assigned or disposed items get a warning).</param>
/// <param name="ItemLocationPath">Where the scanned item is recorded now.</param>
public sealed record CountScanResultDto(
    CountScanKind Kind,
    string Title,
    Guid? LocationId,
    string? LocationPath,
    Guid? ProductId,
    Unit? Unit,
    Guid? LineId,
    decimal? CountedQuantity,
    ItemStatus? ItemStatus,
    string? ItemLocationPath,
    CountProgressDto Progress);

public sealed record CountDecision(Guid LineId, CountResolution Resolution);

public sealed record CountApprovalResultDto(Guid ReferenceId, int MovementCount);
