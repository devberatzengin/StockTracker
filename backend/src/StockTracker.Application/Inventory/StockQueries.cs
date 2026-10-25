using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Locations;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

public sealed record GetProductStockQuery(Guid ProductId) : IRequest<ProductStockDto>;

/// <summary>Stock held at a location; with IncludeChildren also in its whole subtree. Not paged.</summary>
public sealed record GetLocationContentsQuery(Guid LocationId, bool IncludeChildren = false) : IRequest<LocationContentsDto>;

public sealed class GetProductStockQueryHandler(IAppDbContext db) : IRequestHandler<GetProductStockQuery, ProductStockDto>
{
    public async Task<ProductStockDto> Handle(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var product = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.TrackingType })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ürün", request.ProductId);

        if (product.TrackingType == TrackingType.Quantity)
        {
            var balances = await db.StockBalances.AsNoTracking()
                .Where(b => b.ProductId == product.Id && b.Quantity > 0)
                .Select(b => new { b.LocationId, b.Quantity })
                .ToListAsync(cancellationToken);
            var paths = await LocationPaths.BuildAsync(db, balances.Select(b => b.LocationId), cancellationToken);

            return new ProductStockDto(
                product.Id,
                product.TrackingType,
                balances.Sum(b => b.Quantity),
                [.. balances
                    .Select(b => new LocationQuantityDto(b.LocationId, paths.GetValueOrDefault(b.LocationId, "?"), b.Quantity))
                    .OrderBy(b => b.LocationPath, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true))],
                []);
        }

        var items = db.Items.AsNoTracking().Where(i => i.ProductId == product.Id);
        var statusCounts = await items
            .GroupBy(i => i.Status)
            .Select(g => new ItemStatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
        var perLocation = await items
            .Where(i => i.CurrentLocationId != null)
            .GroupBy(i => i.CurrentLocationId!.Value)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var itemPaths = await LocationPaths.BuildAsync(db, perLocation.Select(l => l.LocationId), cancellationToken);

        return new ProductStockDto(
            product.Id,
            product.TrackingType,
            statusCounts.Where(c => c.Status != ItemStatus.Disposed).Sum(c => c.Count),
            [.. perLocation
                .Select(l => new LocationQuantityDto(l.LocationId, itemPaths.GetValueOrDefault(l.LocationId, "?"), l.Count))
                .OrderBy(l => l.LocationPath, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true))],
            [.. Enum.GetValues<ItemStatus>()
                .Select(s => new ItemStatusCountDto(s, statusCounts.FirstOrDefault(c => c.Status == s)?.Count ?? 0))]);
    }
}

public sealed class GetLocationContentsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetLocationContentsQuery, LocationContentsDto>
{
    public async Task<LocationContentsDto> Handle(GetLocationContentsQuery request, CancellationToken cancellationToken)
    {
        var path = await db.Locations.IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.Id == request.LocationId)
            .Select(l => l.Path)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Lokasyon", request.LocationId);

        var locationIds = request.IncludeChildren
            ? db.Locations.IgnoreQueryFilters().Where(l => l.Path.StartsWith(path)).Select(l => l.Id)
            : db.Locations.IgnoreQueryFilters().Where(l => l.Id == request.LocationId).Select(l => l.Id);

        var products = db.Products.IgnoreQueryFilters();

        var balances = await db.StockBalances.AsNoTracking()
            .Where(b => b.Quantity > 0 && locationIds.Contains(b.LocationId))
            .Select(b => new
            {
                b.ProductId,
                b.LocationId,
                b.Quantity,
                Product = products.Where(p => p.Id == b.ProductId).Select(p => new { p.Code, p.Name, p.Unit }).First(),
            })
            .ToListAsync(cancellationToken);

        var items = await db.Items.AsNoTracking()
            .Where(i => i.CurrentLocationId != null && locationIds.Contains(i.CurrentLocationId.Value))
            .OrderBy(i => i.Code)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.ProductId,
                i.SerialNumber,
                i.Status,
                LocationId = i.CurrentLocationId!.Value,
                ProductName = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First(),
            })
            .ToListAsync(cancellationToken);

        var paths = await LocationPaths.BuildAsync(
            db,
            balances.Select(b => b.LocationId).Concat(items.Select(i => i.LocationId)),
            cancellationToken);

        return new LocationContentsDto(
            request.LocationId,
            request.IncludeChildren,
            [.. balances
                .OrderBy(b => b.Product.Name)
                .Select(b => new ContentBalanceDto(
                    b.ProductId, b.Product.Code, b.Product.Name, b.Product.Unit, b.LocationId, paths.GetValueOrDefault(b.LocationId, "?"), b.Quantity))],
            [.. items.Select(i => new ContentItemDto(
                i.Id, i.Code, i.ProductId, i.ProductName, i.SerialNumber, i.Status, i.LocationId, paths.GetValueOrDefault(i.LocationId, "?")))]);
    }
}
