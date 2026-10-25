using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Locations;

/// <summary>Builds "Bina › Oda › Raf A1" labels for locations from their materialized paths.</summary>
public static class LocationPaths
{
    public const string Separator = " › ";

    public static async Task<IReadOnlyDictionary<Guid, string>> BuildAsync(
        IAppDbContext db,
        IEnumerable<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        var ids = locationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var locations = db.Locations.IgnoreQueryFilters().AsNoTracking();
        var paths = await locations
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.Path })
            .ToListAsync(cancellationToken);

        var pathIds = paths.ToDictionary(
            p => p.Id,
            p => p.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        var allIds = pathIds.Values.SelectMany(x => x).Distinct().ToList();
        var names = await locations
            .Where(l => allIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);

        return pathIds.ToDictionary(
            p => p.Key,
            p => string.Join(Separator, p.Value.Select(id => names.GetValueOrDefault(id, "?"))));
    }
}
