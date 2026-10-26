using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Catalog;

/// <summary>Product definition; aggregate root for its barcodes.</summary>
public class Product : BaseEntity, ISoftDeletable
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int CodeLength = Common.Codes.EntityCode.Length;

    public const int MaxPhotos = 10;

    private readonly List<ProductBarcode> _barcodes = [];
    private readonly List<ProductPhoto> _photos = [];

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

    /// <summary>Gallery in display order; exactly one is primary while any exist.</summary>
    public IReadOnlyList<ProductPhoto> Photos => [.. _photos.OrderBy(p => p.SortOrder)];

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
    /// <param name="hasOpenAssignments">Whether an item of the product is assigned; keeps it assignable.</param>
    public void Update(
        string name,
        string? description,
        Category category,
        Unit unit,
        TrackingType trackingType,
        bool isAssignable,
        decimal? minStockLevel,
        bool hasStockHistory,
        bool hasOpenAssignments = false)
    {
        if (IsAssignable && !isAssignable && hasOpenAssignments)
        {
            throw new DomainException(
                CatalogErrors.ProductHasOpenAssignments,
                "Açık zimmeti olan üründe zimmetlenebilirlik kapatılamaz.");
        }

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

    /// <summary>Adds a photo at the end of the gallery; the first photo becomes the primary one.</summary>
    public ProductPhoto AddPhoto(Guid photoId, string storageKey, string thumbnailKey, string contentType, long sizeBytes)
    {
        if (_photos.Count >= MaxPhotos)
        {
            throw new DomainException(
                CatalogErrors.ProductPhotoLimit,
                $"Bir ürüne en fazla {MaxPhotos} fotoğraf eklenebilir.",
                "file");
        }

        var photo = ProductPhoto.Create(
            photoId,
            Id,
            storageKey,
            thumbnailKey,
            contentType,
            sizeBytes,
            _photos.Count,
            isPrimary: _photos.Count == 0);
        _photos.Add(photo);
        return photo;
    }

    /// <summary>Removes a photo; when it was the primary one, the next photo in order takes over.</summary>
    public ProductPhoto RemovePhoto(Guid photoId)
    {
        var photo = FindPhoto(photoId);
        _photos.Remove(photo);
        Renumber(_photos.OrderBy(p => p.SortOrder).ToList());

        if (photo.IsPrimary && _photos.Count > 0)
        {
            _photos.MinBy(p => p.SortOrder)!.SetPrimary(true);
        }

        return photo;
    }

    public void SetPrimaryPhoto(Guid photoId)
    {
        var photo = FindPhoto(photoId);
        foreach (var other in _photos)
        {
            other.SetPrimary(other == photo);
        }
    }

    /// <summary>Puts the gallery in the given order; every photo must be listed exactly once.</summary>
    public void ReorderPhotos(IReadOnlyList<Guid> photoIds)
    {
        if (photoIds.Count != _photos.Count
            || photoIds.Distinct().Count() != photoIds.Count
            || !photoIds.All(id => _photos.Any(p => p.Id == id)))
        {
            throw new DomainException(
                CatalogErrors.ProductPhotoOrderInvalid,
                "Sıralama ürünün tüm fotoğraflarını birer kez içermelidir.",
                "photoIds");
        }

        Renumber([.. photoIds.Select(FindPhoto)]);
    }

    private ProductPhoto FindPhoto(Guid photoId) =>
        _photos.SingleOrDefault(p => p.Id == photoId)
        ?? throw new DomainException(CatalogErrors.ProductPhotoNotFound, "Fotoğraf bu ürüne ait değil.", "photoId");

    private static void Renumber(IReadOnlyList<ProductPhoto> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].MoveTo(i);
        }
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
