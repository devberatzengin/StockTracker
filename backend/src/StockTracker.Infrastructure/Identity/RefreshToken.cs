namespace StockTracker.Infrastructure.Identity;

/// <summary>Only the SHA-256 hash of the token is stored; the raw value lives in the client cookie.</summary>
public sealed class RefreshToken
{
    public const int HashLength = 64;

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByHash { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime now, TimeSpan lifetime) =>
        new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };
}
