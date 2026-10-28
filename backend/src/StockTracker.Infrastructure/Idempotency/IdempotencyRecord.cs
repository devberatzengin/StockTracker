namespace StockTracker.Infrastructure.Idempotency;

/// <summary>A key used for an idempotent command and, once it finished, the response it returned.</summary>
public sealed class IdempotencyRecord
{
    public Guid UserId { get; init; }

    public string Key { get; init; } = string.Empty;

    public string RequestType { get; init; } = string.Empty;

    /// <summary>SHA-256 of the command; the same key with another command is rejected.</summary>
    public string RequestHash { get; init; } = string.Empty;

    public string? ResponseJson { get; set; }

    public DateTime CreatedAt { get; init; }
}
