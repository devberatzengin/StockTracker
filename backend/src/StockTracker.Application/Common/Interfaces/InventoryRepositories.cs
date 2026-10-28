using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Common.Interfaces;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Which of <paramref name="serialNumbers"/> are already used by items of the product.</summary>
    Task<IReadOnlySet<string>> GetUsedSerialNumbersAsync(
        Guid productId,
        IReadOnlyCollection<string> serialNumbers,
        Guid? excludeItemId,
        CancellationToken cancellationToken);

    void Add(Item item);
}

public interface IStockBalanceRepository
{
    /// <summary>Existing balances for the given (product, location) pairs, tracked for update.</summary>
    Task<IReadOnlyList<StockBalance>> GetAsync(
        IReadOnlyCollection<(Guid ProductId, Guid LocationId)> keys,
        CancellationToken cancellationToken);

    void Add(StockBalance balance);
}

/// <summary>The stock ledger is append-only: movements can only be added.</summary>
public interface IMovementRepository
{
    /// <summary>Loads a movement to reverse it; the movement itself is never changed.</summary>
    Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsReversedAsync(Guid id, CancellationToken cancellationToken);

    void AddRange(IEnumerable<StockMovement> movements);
}
