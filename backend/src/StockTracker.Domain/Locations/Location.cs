using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Locations;

/// <summary>
/// Node of the free-form location tree (Building › Room › Cabinet › Shelf …).
/// <see cref="Path"/> lists the ids from the root down to this node: <c>/{rootId}/…/{id}/</c>.
/// </summary>
public class Location : BaseEntity, ISoftDeletable
{
    public const int NameMaxLength = 100;
    public const int CodeLength = 10;
    public const int MaxDepth = 8;

    // 9 levels × ("/" + 36-char Guid) + trailing "/".
    public const int PathMaxLength = 400;

    private Location()
    {
    }

    /// <summary>System generated (LOC-XXXXXX); never changes.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name for case-insensitive uniqueness among active siblings.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public LocationType Type { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Path { get; private set; } = string.Empty;

    /// <summary>Root = 0.</summary>
    public int Depth { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public static Location Create(string code, string name, LocationType type, Location? parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var location = new Location { Code = code };
        location.Update(name, type);
        location.PlaceUnder(parent, subtreeHeight: 0);
        return location;
    }

    public void Update(string name, LocationType type)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException(LocationErrors.NameRequired, "Lokasyon adı zorunludur.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException(
                LocationErrors.NameTooLong,
                $"Lokasyon adı en fazla {NameMaxLength} karakter olabilir.");
        }

        Name = trimmed;
        NormalizedName = Normalize(trimmed);
        Type = type;
    }

    /// <summary>
    /// Moves this node (with its subtree) under <paramref name="newParent"/>, or to the root when null.
    /// Descendant paths are rewritten separately in the same transaction.
    /// </summary>
    /// <param name="subtreeHeight">Deepest descendant depth minus this node's depth (0 for a leaf).</param>
    public void MoveTo(Location? newParent, int subtreeHeight)
    {
        if (!IsActive)
        {
            throw new DomainException(LocationErrors.Inactive, "Pasif lokasyon taşınamaz.");
        }

        if (newParent is not null && newParent.Path.StartsWith(Path, StringComparison.Ordinal))
        {
            throw new DomainException(
                LocationErrors.MoveIntoOwnSubtree,
                "Lokasyon kendisinin ya da alt lokasyonlarından birinin altına taşınamaz.");
        }

        PlaceUnder(newParent, subtreeHeight);
    }

    /// <param name="hasContent">Whether this node or its subtree holds stock or items.</param>
    public void Deactivate(bool hasContent)
    {
        if (hasContent)
        {
            throw new DomainException(
                LocationErrors.HasContent,
                "İçinde ya da alt lokasyonlarında stok bulunan lokasyon pasifleştirilemez.");
        }

        IsActive = false;
    }

    private void PlaceUnder(Location? parent, int subtreeHeight)
    {
        if (parent is { IsActive: false })
        {
            throw new DomainException(LocationErrors.ParentInactive, "Pasif bir lokasyonun altına lokasyon eklenemez.");
        }

        var depth = parent is null ? 0 : parent.Depth + 1;
        if (depth + subtreeHeight > MaxDepth)
        {
            throw new DomainException(
                LocationErrors.MaxDepthExceeded,
                $"Lokasyon ağacı en fazla {MaxDepth + 1} seviye olabilir.");
        }

        ParentId = parent?.Id;
        Depth = depth;
        Path = $"{parent?.Path ?? "/"}{Id}/";
    }
}
