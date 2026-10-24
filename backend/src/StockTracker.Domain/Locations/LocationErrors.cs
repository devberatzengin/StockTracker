namespace StockTracker.Domain.Locations;

/// <summary>Stable error codes returned with 422 responses.</summary>
public static class LocationErrors
{
    public const string NameRequired = "Location.NameRequired";
    public const string NameTooLong = "Location.NameTooLong";
    public const string NameTaken = "Location.NameTaken";
    public const string MaxDepthExceeded = "Location.MaxDepthExceeded";
    public const string ParentInactive = "Location.ParentInactive";
    public const string Inactive = "Location.Inactive";
    public const string MoveIntoOwnSubtree = "Location.MoveIntoOwnSubtree";
    public const string HasContent = "Location.HasContent";
}
