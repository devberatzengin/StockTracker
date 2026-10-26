namespace StockTracker.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deactivated,
    Reactivated,

    /// <summary>Physically deleted rows (photos, barcodes).</summary>
    Deleted,
}

/// <summary>
/// One change of one entity, written by the persistence layer in the same SaveChanges as the change.
/// Append-only: never updated or deleted.
/// </summary>
public sealed class AuditLog
{
    public const int EntityTypeMaxLength = 64;

    private AuditLog()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>"Product", "Item", "Location", "User", …</summary>
    public string EntityType { get; private init; } = string.Empty;

    public Guid EntityId { get; private init; }

    /// <summary>Owner of a child row (the product of a barcode or photo), so the parent's history includes it.</summary>
    public Guid? ParentEntityId { get; private init; }

    public AuditAction Action { get; private init; }

    /// <summary>JSON object <c>{ field: { old, new } }</c> (stored as jsonb).</summary>
    public string Changes { get; private init; } = "{}";

    /// <summary>Null for system work (seeding).</summary>
    public Guid? UserId { get; private init; }

    public DateTime CreatedAt { get; private init; }

    public static AuditLog Create(
        string entityType,
        Guid entityId,
        Guid? parentEntityId,
        AuditAction action,
        string changes,
        Guid? userId,
        DateTime createdAt) =>
        new()
        {
            EntityType = entityType,
            EntityId = entityId,
            ParentEntityId = parentEntityId,
            Action = action,
            Changes = changes,
            UserId = userId,
            CreatedAt = createdAt,
        };
}
