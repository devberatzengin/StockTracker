using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

public sealed record MovementDto(
    Guid Id,
    MovementType Type,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Unit Unit,
    Guid? ItemId,
    string? ItemCode,
    string? SerialNumber,
    Guid? FromLocationId,
    string? FromLocationName,
    Guid? ToLocationId,
    string? ToLocationName,
    decimal Quantity,
    MovementReason? Reason,
    string? Note,
    Guid ReferenceId,
    Guid UserId,
    string UserName,
    ItemStatus? FromStatus,
    ItemStatus? ToStatus,
    string? AssigneeName,
    Guid? ReversesMovementId,
    Guid? ReversedByMovementId);

public sealed record ItemListItemDto(
    Guid Id,
    string Code,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string? SerialNumber,
    ItemStatus Status,
    Guid? CurrentLocationId,
    string? LocationPath);

public sealed record ItemDto(
    Guid Id,
    string Code,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    bool ProductIsAssignable,
    string? SerialNumber,
    ItemStatus Status,
    Guid? CurrentLocationId,
    string? LocationPath,
    DateTime CreatedAt,
    IReadOnlyList<ItemAction> AvailableActions,
    ItemOpenAssignmentDto? OpenAssignment);

/// <summary>The item's current holder, when assigned.</summary>
public sealed record ItemOpenAssignmentDto(
    Guid Id,
    Guid AssigneeUserId,
    string AssigneeName,
    DateTime AssignedAt,
    DateOnly? DueDate,
    bool IsOverdue);

public sealed record LocationQuantityDto(Guid LocationId, string LocationPath, decimal Quantity);

public sealed record ItemStatusCountDto(ItemStatus Status, int Count);

/// <summary>
/// Quantity products: quantity per location. Serialized products: item counts per status and
/// located items per location.
/// </summary>
public sealed record ProductStockDto(
    Guid ProductId,
    TrackingType TrackingType,
    decimal TotalQuantity,
    IReadOnlyList<LocationQuantityDto> Locations,
    IReadOnlyList<ItemStatusCountDto> StatusCounts);

public sealed record ContentBalanceDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Unit Unit,
    Guid LocationId,
    string LocationPath,
    decimal Quantity);

public sealed record ContentItemDto(
    Guid ItemId,
    string Code,
    Guid ProductId,
    string ProductName,
    string? SerialNumber,
    ItemStatus Status,
    Guid LocationId,
    string LocationPath);

public sealed record LocationContentsDto(
    Guid LocationId,
    bool IncludeChildren,
    IReadOnlyList<ContentBalanceDto> Balances,
    IReadOnlyList<ContentItemDto> Items);
