using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Infrastructure.Catalog;

/// <summary>
/// Stock balances, items and movements are not modelled yet, so no product can have stock or history.
/// Replaced by a query-backed implementation when those tables are introduced.
/// </summary>
public sealed class NoStockProductStockChecker : IProductStockChecker
{
    public Task<bool> HasStockAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasStockOrMovementsAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
