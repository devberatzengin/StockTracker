using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class CountRepository(AppDbContext db) : ICountRepository
{
    // pg_advisory_xact_lock key for starting counts ("StockCnt").
    private const long StartLockKey = 0x5374_6f63_6b43_6e74;

    public Task<CountSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.CountSessions
            .Include(c => c.Lines)
            .Include(c => c.Scans)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task LockStartsAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({StartLockKey})", cancellationToken);

    public async Task<IReadOnlyList<OpenCountScope>> GetOpenScopesAsync(CancellationToken cancellationToken) =>
        await db.CountSessions.AsNoTracking()
            .Where(c => c.Status == CountStatus.Counting || c.Status == CountStatus.Reviewing)
            .Select(c => new OpenCountScope(
                c.Id,
                db.Locations.IgnoreQueryFilters().Where(l => l.Id == c.LocationId).Select(l => l.Path).First(),
                c.IncludeChildren))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpectedStock>> GetExpectedStockAsync(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        var balances = await db.StockBalances.AsNoTracking()
            .Where(b => locationIds.Contains(b.LocationId) && b.Quantity > 0)
            .Select(b => new ExpectedStock(b.LocationId, b.ProductId, null, b.Quantity))
            .ToListAsync(cancellationToken);
        var items = await db.Items.AsNoTracking()
            .Where(i => i.CurrentLocationId != null
                && locationIds.Contains(i.CurrentLocationId.Value)
                && (i.Status == ItemStatus.InStock || i.Status == ItemStatus.InRepair))
            .Select(i => new ExpectedStock(i.CurrentLocationId!.Value, i.ProductId, i.Id, 1))
            .ToListAsync(cancellationToken);

        return [.. balances, .. items];
    }

    public async Task<IReadOnlyList<Guid>> GetScopeLocationIdsAsync(
        Guid rootId,
        string rootPath,
        bool includeChildren,
        CancellationToken cancellationToken) =>
        includeChildren
            ? await db.Locations.Where(l => l.Path.StartsWith(rootPath)).Select(l => l.Id).ToListAsync(cancellationToken)
            : [rootId];

    public void Add(CountSession session) => db.CountSessions.Add(session);
}
