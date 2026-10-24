using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Catalog;

/// <summary>Product definition; aggregate root for its barcodes.</summary>
public class Product : BaseEntity, ISoftDeletable
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int CodeLength = Common.Codes.EntityCode.Length;

    private readonly List<ProductBarcode> _barcodes = [];

    private Product()
    {
    }

    /// <summary>System generated (PRD-XXXXXX); never changes.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid CategoryId { get; private set; }

    public Unit Unit { get; private set; }

    public TrackingType TrackingType { get; private set; }

    public bool IsAssignable { get; private set; }

    public decimal? MinStockLevel { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ProductBarcode> Barcodes => _barcodes.AsReadOnly();

    public static Product Create(
        string code,
        string name,
        string? description,
        Category category,
        Unit unit,
        TrackingType trackingType,
        bool isAssignable,
        decimal? minStockLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var product = new Product { Code = code };
        product.SetDetails(name, description, category, unit, trackingType, isAssignable, minStockLevel);
        return product;
    }

    /// <param name="hasStockHistory">Whether the product has stock or movements; locks the tracking type.</param>
    public void Update(
        string name,
        string? description,
        Category category,
        Unit unit,
        TrackingType trackingType,
        bool isAssignable,
        decimal? minStockLevel,
        bool hasStockHistory)
    {
        if (trackingType != TrackingType && hasStockHistory)
        {
            throw new DomainException(
                CatalogErrors.ProductTrackingTypeLocked,
                "Stoğu veya hareketi olan ürünün takip tipi değiştirilemez.");
        }

        SetDetails(name, description, category, unit, trackingType, isAssignable, minStockLevel);
    }

    public ProductBarcode AddBarcode(string value, BarcodeFormat format)
    {
        var barcode = ProductBarcode.Create(Id, value, format);
        if (_barcodes.Any(b => b.Value == barcode.Value))
        {
            throw new DomainException(CatalogErrors.ProductDuplicateBarcode, "Bu barkod ürüne zaten ekli.");
        }

        _barcodes.Add(barcode);
        return barcode;
    }

    public void RemoveBarcode(Guid barcodeId)
    {
        var barcode = _barcodes.SingleOrDefault(b => b.Id == barcodeId)
            ?? throw new DomainException(CatalogErrors.ProductBarcodeNotFound, "Barkod bu ürüne ait değil.");
        _barcodes.Remove(barcode);
    }

    /// <param name="hasStock">Whether the product has a positive balance or items in stock, assigned or in repair.</param>
    public void Deactivate(bool hasStock)
    {
        if (hasStock)
        {
            throw new DomainException(CatalogErrors.ProductHasStock, "Stoğu olan ürün pasifleştirilemez.");
        }

        IsActive = false;
    }

    private void SetDetails(
        string name,
        string? description,
        Category category,
        Unit unit,
        TrackingType trackingType,
        bool isAssignable,
        decimal? minStockLevel)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            throw new DomainException(CatalogErrors.ProductNameRequired, "Ürün adı zorunludur.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException(
                CatalogErrors.ProductNameTooLong,
                $"Ürün adı en fazla {NameMaxLength} karakter olabilir.");
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            throw new DomainException(
                CatalogErrors.ProductDescriptionTooLong,
                $"Açıklama en fazla {DescriptionMaxLength} karakter olabilir.");
        }

        // Keeping the current (inactive) category on update is allowed; moving to one is not.
        if (!category.IsActive && category.Id != CategoryId)
        {
            throw new DomainException(CatalogErrors.CategoryInactive, "Pasif kategoriye ürün eklenemez.");
        }

        if (isAssignable && trackingType != TrackingType.Serialized)
        {
            throw new DomainException(
                CatalogErrors.ProductAssignableRequiresSerialized,
                "Sadece tekil takipli ürünler zimmetlenebilir.");
        }

        if (trackingType == TrackingType.Serialized && unit != Unit.Piece)
        {
            throw new DomainException(
                CatalogErrors.ProductSerializedRequiresPiece,
                "Tekil takipli ürünlerin birimi adet olmalıdır.");
        }

        if (minStockLevel < 0)
        {
            throw new DomainException(CatalogErrors.ProductMinStockNegative, "Minimum stok negatif olamaz.");
        }

        if (trackingType == TrackingType.Serialized && minStockLevel is { } min && min != decimal.Truncate(min))
        {
            throw new DomainException(
                CatalogErrors.ProductMinStockMustBeWhole,
                "Tekil takipli ürünlerde minimum stok tam sayı olmalıdır.");
        }

        Name = trimmedName;
        Description = trimmedDescription;
        CategoryId = category.Id;
        Unit = unit;
        TrackingType = trackingType;
        IsAssignable = isAssignable;
        MinStockLevel = minStockLevel;
    }
}
