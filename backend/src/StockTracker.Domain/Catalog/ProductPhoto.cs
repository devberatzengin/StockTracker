using StockTracker.Domain.Common;

namespace StockTracker.Domain.Catalog;

/// <summary>
/// A product photo (processed WebP plus thumbnail in file storage). Unlike other records, photos are
/// physically deleted: no movement refers to them.
/// </summary>
public sealed class ProductPhoto : BaseEntity
{
    public const int KeyMaxLength = 300;
    public const int ContentTypeMaxLength = 50;

    private ProductPhoto()
    {
    }

    public Guid ProductId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string ThumbnailKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>0-based position in the gallery.</summary>
    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    internal static ProductPhoto Create(
        Guid id,
        Guid productId,
        string storageKey,
        string thumbnailKey,
        string contentType,
        long sizeBytes,
        int sortOrder,
        bool isPrimary) =>
        new()
        {
            Id = id,
            ProductId = productId,
            StorageKey = storageKey,
            ThumbnailKey = thumbnailKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            SortOrder = sortOrder,
            IsPrimary = isPrimary,
        };

    internal void MoveTo(int sortOrder) => SortOrder = sortOrder;

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
