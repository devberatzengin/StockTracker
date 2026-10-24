using Microsoft.EntityFrameworkCore;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Identity;

internal static class RefreshTokenQueries
{
    public static Task<int> RevokeAllRefreshTokensAsync(
        this AppDbContext db,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
