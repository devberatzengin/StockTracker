using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Products;

public sealed record ProductBarcodeDto(Guid Id, string Value, BarcodeFormat Format);

public sealed record ProductListItemDto(
    Guid Id,
    string Code,
    string Name,
    Guid CategoryId,
    string CategoryName,
    Unit Unit,
    TrackingType TrackingType,
    bool IsAssignable,
    decimal? MinStockLevel,
    bool IsActive,
    IReadOnlyList<string> Barcodes);

public sealed record ProductDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    bool CategoryIsActive,
    Unit Unit,
    TrackingType TrackingType,
    bool IsAssignable,
    decimal? MinStockLevel,
    bool IsActive,
    bool TrackingTypeLocked,
    IReadOnlyList<ProductBarcodeDto> Barcodes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record BarcodeInput(string Value, BarcodeFormat Format);
