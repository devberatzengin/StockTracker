using StockTracker.Domain.Locations;

namespace StockTracker.Application.Common.Interfaces;

public interface ILocationRepository
{
    /// <summary>Includes inactive locations.</summary>
    Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether another active location under the same parent (or among roots) uses the name.</summary>
    Task<bool> IsNameTakenAmongSiblingsAsync(
        Guid? parentId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken cancellationToken);

    /// <summary>Deepest descendant depth minus the node's own depth (0 for a leaf).</summary>
    Task<int> GetSubtreeHeightAsync(Location location, CancellationToken cancellationToken);

    /// <summary>
    /// Serializes tree restructuring (moves, deactivation) for the current transaction so two
    /// concurrent moves cannot create a cycle.
    /// </summary>
    Task LockTreeAsync(CancellationToken cancellationToken);

    /// <summary>Rewrites Path and Depth of all descendants with a single UPDATE.</summary>
    Task MoveDescendantsAsync(
        Guid locationId,
        string oldPath,
        string newPath,
        int depthDelta,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken);

    /// <summary>Deactivates all descendants with a single UPDATE.</summary>
    Task DeactivateDescendantsAsync(
        Guid locationId,
        string path,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken);

    void Add(Location location);
}
