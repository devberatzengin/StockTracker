namespace StockTracker.Domain.Notifications;

public enum NotificationType
{
    LowStock,
    AssignmentDueSoon,
    AssignmentOverdue,
    LedgerMismatch,
}

/// <summary>
/// A message to one user. While open (ResolvedAt is null) at most one exists per user and DedupKey;
/// it is resolved automatically when its cause goes away (stock refilled, item returned).
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 500;
    public const int DedupKeyMaxLength = 200;
    public const int EntityTypeMaxLength = 64;

    private Notification()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid UserId { get; private init; }

    public NotificationType Type { get; private init; }

    public string Title { get; private init; } = string.Empty;

    public string Message { get; private init; } = string.Empty;

    /// <summary>What the notification is about ("Product", "Assignment"), for links.</summary>
    public string EntityType { get; private init; } = string.Empty;

    public Guid EntityId { get; private init; }

    /// <summary>e.g. "LowStock:{productId}"; one open notification per user and key.</summary>
    public string DedupKey { get; private init; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTime CreatedAt { get; private init; }

    public DateTime? ResolvedAt { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string entityType,
        Guid entityId,
        string dedupKey,
        DateTime now) =>
        new()
        {
            UserId = userId,
            Type = type,
            Title = Truncate(title, TitleMaxLength),
            Message = Truncate(message, MessageMaxLength),
            EntityType = entityType,
            EntityId = entityId,
            DedupKey = dedupKey,
            CreatedAt = now,
        };

    public void MarkRead() => IsRead = true;

    /// <summary>The cause went away; resolving twice keeps the first time.</summary>
    public void Resolve(DateTime now) => ResolvedAt ??= now;

    public static string LowStockKey(Guid productId) => $"LowStock:{productId}";

    public static string DueSoonKey(Guid assignmentId) => $"AssignmentDueSoon:{assignmentId}";

    public static string OverdueKey(Guid assignmentId) => $"AssignmentOverdue:{assignmentId}";

    public const string LedgerMismatchKey = "LedgerMismatch";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
