namespace StockTracker.Domain.Catalog;

/// <summary>Stable error codes returned with 422 responses.</summary>
public static class CatalogErrors
{
    public const string CategoryNameRequired = "Category.NameRequired";
    public const string CategoryNameTooLong = "Category.NameTooLong";
    public const string CategoryInactive = "Category.Inactive";
    public const string CategoryHasActiveProducts = "Category.HasActiveProducts";
    public const string CategoryNameTaken = "Category.NameTaken";

    public const string ProductNameRequired = "Product.NameRequired";
    public const string ProductNameTooLong = "Product.NameTooLong";
    public const string ProductDescriptionTooLong = "Product.DescriptionTooLong";
    public const string ProductAssignableRequiresSerialized = "Product.AssignableRequiresSerialized";
    public const string ProductSerializedRequiresPiece = "Product.SerializedRequiresPiece";
    public const string ProductMinStockNegative = "Product.MinStockNegative";
    public const string ProductMinStockMustBeWhole = "Product.MinStockMustBeWhole";
    public const string ProductTrackingTypeLocked = "Product.TrackingTypeLocked";
    public const string ProductHasStock = "Product.HasStock";
    public const string ProductHasOpenAssignments = "Product.HasOpenAssignments";
    public const string ProductPhotoLimit = "Product.PhotoLimit";
    public const string ProductPhotoNotFound = "Product.PhotoNotFound";
    public const string ProductPhotoOrderInvalid = "Product.PhotoOrderInvalid";
    public const string ProductDuplicateBarcode = "Product.DuplicateBarcode";
    public const string ProductBarcodeNotFound = "Product.BarcodeNotFound";

    public const string BarcodeRequired = "Barcode.Required";
    public const string BarcodeTooLong = "Barcode.TooLong";
    public const string BarcodeInvalidFormat = "Barcode.InvalidFormat";
    public const string BarcodeInvalidCheckDigit = "Barcode.InvalidCheckDigit";
    public const string BarcodeDuplicate = "Barcode.Duplicate";
}
