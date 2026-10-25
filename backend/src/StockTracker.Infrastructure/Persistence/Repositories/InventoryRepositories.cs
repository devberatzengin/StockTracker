using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Inventory;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class ItemRepository(AppDbContext db) : IItemRepository
{
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Items.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Item>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.Items.Where(i => ids.Contains(i.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> GetUsedSerialNumbersAsync(
        Guid productId,
        IReadOnlyCollection<string> serialNumbers,
        Guid? excludeItemId,
        CancellationToken cancellationToken) =>
        (await db.Items
            .Where(i => i.ProductId == productId
                && i.SerialNumber != null
                && serialNumbers.Contains(i.SerialNumber)
                && i.Id != excludeItemId)
            .Select(i => i.SerialNumber!)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public void Add(Item item) => db.Items.Add(item);
}

public sealed class StockBalanceRepository(AppDbContext db) : IStockBalanceRepository
{
    public async Task<IReadOnlyList<StockBalance>> GetAsync(
        IReadOnlyCollection<(Guid ProductId, Guid LocationId)> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var productIds = keys.Select(k => k.ProductId).Distinct().ToList();
        var locationIds = keys.Select(k => k.LocationId).Distinct().ToList();
        var candidates = await db.StockBalances
            .Where(b => productIds.Contains(b.ProductId) && locationIds.Contains(b.LocationId))
            .ToListAsync(cancellationToken);

        var wanted = keys.ToHashSet();
        return [.. candidates.Where(b => wanted.Contains((b.ProductId, b.LocationId)))];
    }

    public void Add(StockBalance balance) => db.StockBalances.Add(balance);
}

public sealed class MovementRepository(AppDbContext db) : IMovementRepository
{
    public void AddRange(IEnumerable<StockMovement> movements) => db.StockMovements.AddRange(movements);
}
