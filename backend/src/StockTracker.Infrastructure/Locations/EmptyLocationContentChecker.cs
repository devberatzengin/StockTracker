using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Infrastructure.Locations;

/// <summary>
/// Stock balances and items are not modelled yet, so no location can hold content.
/// Replaced by a query-backed implementation when those tables are introduced.
/// </summary>
public sealed class EmptyLocationContentChecker : ILocationContentChecker
{
    public Task<bool> HasContentAsync(string subtreePath, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
