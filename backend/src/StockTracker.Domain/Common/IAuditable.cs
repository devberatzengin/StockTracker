namespace StockTracker.Domain.Common;

/// <summary>
/// Audit fields filled on save. Implemented by <see cref="BaseEntity"/> and by entities that
/// cannot derive from it (the Identity user).
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }

    Guid? CreatedBy { get; }

    DateTime? UpdatedAt { get; }

    Guid? UpdatedBy { get; }
}
