namespace StockTracker.Application.Scan;

public static class ScanResultTypes
{
    public const string Product = "product";
    public const string Item = "item";
    public const string Location = "location";
}

/// <param name="Type">One of <see cref="ScanResultTypes"/>; the frontend opens the matching detail page.</param>
/// <param name="Status">Item status; null for products and locations.</param>
public sealed record ScanResultDto(
    string Type,
    Guid Id,
    string Code,
    string Title,
    string? Subtitle,
    bool IsActive,
    string? Status);
