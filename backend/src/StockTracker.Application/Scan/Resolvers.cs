using StockTracker.Application.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Scan;

public sealed class ProductCodeScanResolver(IAppDbContext db, IFileStorage storage) : IScanCodeResolver
{
    public CodePrefix Prefix => CodePrefix.Product;

    public Task<ScanResultDto?> ResolveAsync(string code, CancellationToken cancellationToken) =>
        ProductScanProjection.FindAsync(db, storage, db.Products.IgnoreQueryFilters().Where(p => p.Code == code), cancellationToken);
}

public sealed class LocationCodeScanResolver(IAppDbContext db) : IScanCodeResolver
{
    public CodePrefix Prefix => CodePrefix.Location;

    public async Task<ScanResultDto?> ResolveAsync(string code, CancellationToken cancellationToken)
    {
        var locations = db.Locations.IgnoreQueryFilters().AsNoTracking();
        var location = await locations
            .Where(l => l.Code == code)
            .Select(l => new { l.Id, l.Code, l.Name, l.Path, l.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        var ancestorIds = location.Path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .Where(id => id != location.Id)
            .ToList();

        var ancestorNames = await locations
            .Where(l => ancestorIds.Contains(l.Id))
            .OrderBy(l => l.Depth)
            .Select(l => l.Name)
            .ToListAsync(cancellationToken);

        return new ScanResultDto(
            ScanResultTypes.Location,
            location.Id,
            location.Code,
            location.Name,
            ancestorNames.Count > 0 ? string.Join(" › ", ancestorNames) : null,
            location.IsActive,
            Status: null);
    }
}

public sealed class ItemCodeScanResolver(IAppDbContext db, IFileStorage storage) : IScanCodeResolver
{
    public CodePrefix Prefix => CodePrefix.Item;

    public async Task<ScanResultDto?> ResolveAsync(string code, CancellationToken cancellationToken)
    {
        var products = db.Products.IgnoreQueryFilters();
        var item = await db.Items.AsNoTracking()
            .Where(i => i.Code == code)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.SerialNumber,
                i.Status,
                i.CurrentLocationId,
                ProductName = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First(),
                Thumbnail = db.ProductPhotos.Where(f => f.ProductId == i.ProductId && f.IsPrimary).Select(f => f.ThumbnailKey).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        var location = item.CurrentLocationId is { } locationId
            ? (await Locations.LocationPaths.BuildAsync(db, [locationId], cancellationToken)).GetValueOrDefault(locationId)
            : null;

        return new ScanResultDto(
            ScanResultTypes.Item,
            item.Id,
            item.Code,
            item.SerialNumber is null ? item.Code : $"{item.Code} · {item.SerialNumber}",
            location is null ? item.ProductName : $"{item.ProductName} · {location}",
            item.Status != Domain.Inventory.ItemStatus.Disposed,
            item.Status.ToString(),
            storage.Read(item.Thumbnail));
    }
}

/// <summary>Finds the product owning a barcode (barcodes have no prefix).</summary>
public sealed class ProductBarcodeScanResolver(IAppDbContext db, IFileStorage storage)
{
    /// <param name="barcode">Whitespace-free value as scanned.</param>
    public Task<ScanResultDto?> ResolveAsync(string barcode, CancellationToken cancellationToken)
    {
        // Codes are upper-cased during normalization, but Code 128 values may contain lower case.
        var upper = barcode.ToUpperInvariant();
        var productIds = db.ProductBarcodes.Where(b => b.Value == barcode || b.Value == upper).Select(b => b.ProductId);

        return ProductScanProjection.FindAsync(
            db,
            storage,
            db.Products.IgnoreQueryFilters().Where(p => productIds.Contains(p.Id)),
            cancellationToken);
    }
}

internal static class ProductScanProjection
{
    public static async Task<ScanResultDto?> FindAsync(
        IAppDbContext db,
        IFileStorage storage,
        IQueryable<Domain.Catalog.Product> products,
        CancellationToken cancellationToken)
    {
        var found = await products
            .AsNoTracking()
            .Select(p => new
            {
                Result = new ScanResultDto(
                    ScanResultTypes.Product,
                    p.Id,
                    p.Code,
                    p.Name,
                    db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault(),
                    p.IsActive,
                    null,
                    null),
                Thumbnail = p.Photos.Where(f => f.IsPrimary).Select(f => f.ThumbnailKey).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? null : found.Result with { ThumbnailUrl = storage.Read(found.Thumbnail) };
    }
}
