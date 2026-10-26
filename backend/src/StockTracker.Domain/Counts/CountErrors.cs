namespace StockTracker.Domain.Counts;

/// <summary>Stable error codes returned with 422 responses.</summary>
public static class CountErrors
{
    public const string InvalidTransition = "Count.InvalidTransition";
    public const string NotCounting = "Count.NotCounting";
    public const string Overlapping = "Count.Overlapping";
    public const string LocationInactive = "Count.LocationInactive";
    public const string LocationOutOfScope = "Count.LocationOutOfScope";
    public const string ScanItemLabel = "Count.ScanItemLabel";
    public const string ProductInactive = "Count.ProductInactive";
    public const string InvalidQuantity = "Count.InvalidQuantity";
    public const string NothingToUndo = "Count.NothingToUndo";
    public const string LineNotFound = "Count.LineNotFound";
    public const string UndecidedLines = "Count.UndecidedLines";
    public const string InvalidResolution = "Count.InvalidResolution";
    public const string NoteTooLong = "Count.NoteTooLong";
    public const string LocationNotFound = "Count.LocationNotFound";
    public const string CodeNotFound = "Count.CodeNotFound";
    public const string ContextRequired = "Count.ContextRequired";
}
