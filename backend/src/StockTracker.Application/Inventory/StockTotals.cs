using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

/// <summary>A product's total stock as low-stock rules see it.</summary>
public sealed class ProductStockTotal
{
    public Guid ProductId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Unit Unit { get; init; }

    public TrackingType TrackingType { get; init; }

    public bool IsActive { get; init; }

    public decimal? MinStockLevel { get; init; }

    public decimal Total { get; init; }
}

/// <summary>
/// Total stock per product: Quantity products sum their balances, Serialized products count in-stock
/// items. A product is low when it is active, has a minimum and the total is below it.
/// Shared by the low-stock notifications, the dashboard and the stock report.
/// </summary>
public static class StockTotals
{
    public static IQueryable<ProductStockTotal> Query(IAppDbContext db, IQueryable<Product> products)
    {
        var balances = db.StockBalances;
        var items = db.Items;
        return products.AsNoTracking().Select(p => new ProductStockTotal
        {
            ProductId = p.Id,
            Code = p.Code,
            Name = p.Name,
            CategoryId = p.CategoryId,
            Unit = p.Unit,
            TrackingType = p.TrackingType,
            IsActive = p.IsActive,
            MinStockLevel = p.MinStockLevel,
            Total = p.TrackingType == TrackingType.Quantity
                ? balances.Where(b => b.ProductId == p.Id).Sum(b => (decimal?)b.Quantity) ?? 0
                : items.Count(i => i.ProductId == p.Id && i.Status == ItemStatus.InStock),
        });
    }

    /// <summary>Server-side filter for low products (same rule as <see cref="IsLow"/>).</summary>
    public static IQueryable<ProductStockTotal> WhereLow(this IQueryable<ProductStockTotal> totals) =>
        totals.Where(t => t.IsActive && t.MinStockLevel != null && t.Total < t.MinStockLevel);

    public static bool IsLow(ProductStockTotal total) =>
        total.IsActive && total.MinStockLevel is { } min && total.Total < min;
}
