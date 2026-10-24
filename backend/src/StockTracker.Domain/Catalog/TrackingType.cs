namespace StockTracker.Domain.Catalog;

public enum TrackingType
{
    /// <summary>Each unit is an Item with its own code (and optional serial number).</summary>
    Serialized,

    /// <summary>Tracked as a quantity per location (StockBalance).</summary>
    Quantity,
}
