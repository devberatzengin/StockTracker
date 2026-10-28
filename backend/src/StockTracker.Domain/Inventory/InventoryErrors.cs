namespace StockTracker.Domain.Inventory;

/// <summary>Stable error codes returned with 422 responses.</summary>
public static class InventoryErrors
{
    public const string InvalidItemTransition = "Item.InvalidTransition";
    public const string SerialNumberTooLong = "Item.SerialNumberTooLong";
    public const string SerialNumberTaken = "Item.SerialNumberTaken";
    public const string ItemNotFound = "Item.NotFound";
    public const string ProductNotSerialized = "Movement.ProductNotSerialized";
    public const string ProductNotQuantity = "Movement.ProductNotQuantity";
    public const string ProductInactive = "Movement.ProductInactive";
    public const string LocationInactive = "Movement.LocationInactive";
    public const string LocationNotFound = "Movement.LocationNotFound";
    public const string ProductNotFound = "Movement.ProductNotFound";
    public const string SameSourceAndTarget = "Movement.SameSourceAndTarget";
    public const string InsufficientStock = "Stock.Insufficient";
    public const string InvalidQuantity = "Movement.InvalidQuantity";
    public const string TooManyItems = "Movement.TooManyItems";
    public const string InvalidMovement = "Movement.Invalid";
    public const string ReasonRequired = "Movement.ReasonRequired";
    public const string NoteRequired = "Movement.NoteRequired";
    public const string NoteTooLong = "Movement.NoteTooLong";
    public const string MovementNotFound = "Movement.NotFound";
    public const string NotReversible = "Movement.NotReversible";
    public const string AlreadyReversed = "Movement.AlreadyReversed";
    public const string ItemChangedSinceMovement = "Movement.ItemChangedSinceMovement";
}
