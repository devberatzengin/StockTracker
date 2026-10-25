using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Locations;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class LocationRepository(AppDbContext db) : ILocationRepository
{
    // pg_advisory_xact_lock key for tree restructuring ("StockLoc").
    private const long TreeLockKey = 0x5374_6f63_6b4c_6f63;

    public Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Locations.IgnoreQueryFilters().SingleOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Location>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.Locations.IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToListAsync(cancellationToken);

    public Task<bool> IsNameTakenAmongSiblingsAsync(
        Guid? parentId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        db.Locations.AnyAsync(
            l => l.ParentId == parentId && l.NormalizedName == normalizedName && l.Id != excludeId,
            cancellationToken);

    public async Task<int> GetSubtreeHeightAsync(Location location, CancellationToken cancellationToken)
    {
        // Includes inactive descendants: they move with the subtree.
        var deepest = await db.Locations.IgnoreQueryFilters()
            .Where(l => l.Path.StartsWith(location.Path))
            .MaxAsync(l => l.Depth, cancellationToken);
        return deepest - location.Depth;
    }

    public Task LockTreeAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({TreeLockKey})", cancellationToken);

    public Task MoveDescendantsAsync(
        Guid locationId,
        string oldPath,
        string newPath,
        int depthDelta,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        // Paths only contain Guids and slashes, so the prefix needs no LIKE escaping.
        var pattern = oldPath + "%";
        var remainderStart = oldPath.Length + 1;
        return db.Database.ExecuteSqlAsync(
            $"""
            UPDATE locations
            SET path = {newPath} || substring(path from {remainderStart}),
                depth = depth + {depthDelta},
                updated_at = {now},
                updated_by = {userId}
            WHERE path LIKE {pattern} AND id <> {locationId}
            """,
            cancellationToken);
    }

    public Task DeactivateDescendantsAsync(
        Guid locationId,
        string path,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var pattern = path + "%";
        return db.Database.ExecuteSqlAsync(
            $"""
            UPDATE locations
            SET is_active = false, updated_at = {now}, updated_by = {userId}
            WHERE path LIKE {pattern} AND id <> {locationId} AND is_active
            """,
            cancellationToken);
    }

    public void Add(Location location) => db.Locations.Add(location);
}
