using StockTracker.Domain.Catalog;
using Unit = StockTracker.Domain.Catalog.Unit;

namespace StockTracker.Api.Contracts;

public sealed record CategoryRequest(string Name);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    Unit Unit,
    TrackingType TrackingType,
    bool IsAssignable,
    decimal? MinStockLevel);

public sealed record AddBarcodeRequest(string Value, BarcodeFormat Format);
