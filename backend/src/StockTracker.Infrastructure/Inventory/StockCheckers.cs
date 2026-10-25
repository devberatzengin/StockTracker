using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Inventory;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Inventory;

public sealed class ProductStockChecker(AppDbContext db) : IProductStockChecker
{
    public async Task<bool> HasStockAsync(Guid productId, CancellationToken cancellationToken) =>
        await db.StockBalances.AnyAsync(b => b.ProductId == productId && b.Quantity > 0, cancellationToken)
        || await db.Items.AnyAsync(i => i.ProductId == productId && i.Status != ItemStatus.Disposed, cancellationToken);

    public async Task<bool> HasStockOrMovementsAsync(Guid productId, CancellationToken cancellationToken) =>
        await db.StockMovements.AnyAsync(m => m.ProductId == productId, cancellationToken)
        || await HasStockAsync(productId, cancellationToken);
}

public sealed class LocationContentChecker(AppDbContext db) : ILocationContentChecker
{
    public async Task<bool> HasContentAsync(string subtreePath, CancellationToken cancellationToken)
    {
        var subtree = db.Locations.IgnoreQueryFilters().Where(l => l.Path.StartsWith(subtreePath)).Select(l => l.Id);

        // Assigned and disposed items have no location, so any located item is content (in stock or in repair).
        return await db.StockBalances.AnyAsync(b => b.Quantity > 0 && subtree.Contains(b.LocationId), cancellationToken)
            || await db.Items.AnyAsync(i => i.CurrentLocationId != null && subtree.Contains(i.CurrentLocationId.Value), cancellationToken);
    }
}
