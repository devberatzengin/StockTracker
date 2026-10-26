using StockTracker.Domain.Counts;

namespace StockTracker.Application.Common.Interfaces;

/// <summary>An open count's scope, for the overlap check.</summary>
public sealed record OpenCountScope(Guid SessionId, string LocationPath, bool IncludeChildren);

public interface ICountRepository
{
    /// <summary>The session with its lines and scans.</summary>
    Task<CountSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Serializes count starts so two overlapping counts cannot start at once.</summary>
    Task LockStartsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OpenCountScope>> GetOpenScopesAsync(CancellationToken cancellationToken);

    /// <summary>Positive balances and in-stock / in-repair items at the given locations.</summary>
    Task<IReadOnlyList<ExpectedStock>> GetExpectedStockAsync(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken);

    /// <summary>The root, plus its active descendants when <paramref name="includeChildren"/> is set.</summary>
    Task<IReadOnlyList<Guid>> GetScopeLocationIdsAsync(
        Guid rootId,
        string rootPath,
        bool includeChildren,
        CancellationToken cancellationToken);

    void Add(CountSession session);
}
