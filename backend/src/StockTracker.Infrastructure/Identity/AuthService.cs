using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Identity;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    TokenService tokenService,
    IDateTime dateTime,
    ILogger<AuthService> logger) : IAuthService
{
    // Same message for unknown email, wrong password, inactive, login disabled and locked out,
    // so the response never reveals whether an account exists.
    public const string InvalidCredentialsMessage = "E-posta veya şifre hatalı.";
    public const string SessionExpiredMessage = "Oturum süresi doldu. Lütfen tekrar giriş yapın.";

    public async Task<AuthTokens> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        // Inactive users are hidden by the soft-delete query filter.
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.CanLogin || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedException(SessionExpiredMessage);
        }

        var now = dateTime.UtcNow;
        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw new UnauthorizedException(SessionExpiredMessage);
        }

        if (stored.RevokedAt is not null)
        {
            await RejectRevokedAsync(stored.UserId, stored.ReplacedByHash, now, cancellationToken);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException(SessionExpiredMessage);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.CanLogin)
        {
            await db.RevokeAllRefreshTokensAsync(stored.UserId, now, cancellationToken);
            throw new UnauthorizedException(SessionExpiredMessage);
        }

        var newToken = TokenService.GenerateRefreshToken();
        var newHash = TokenService.HashRefreshToken(newToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Conditional update: if a concurrent request already rotated this token, nothing is updated
        // and the request is treated as reuse.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedByHash, newHash),
                cancellationToken);

        if (rotated == 0)
        {
            // Revoked by a concurrent request: rotation (reuse) or logout/password change (plain 401).
            await transaction.RollbackAsync(cancellationToken);
            var replacedByHash = await db.RefreshTokens
                .Where(t => t.Id == stored.Id)
                .Select(t => t.ReplacedByHash)
                .SingleAsync(cancellationToken);
            await RejectRevokedAsync(stored.UserId, replacedByHash, now, cancellationToken);
        }

        var tokens = await IssueTokensAsync(user, newToken, newHash, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return tokens;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var hash = TokenService.HashRefreshToken(refreshToken);
        var now = dateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    public async Task<AuthTokens> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException(SessionExpiredMessage);

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            throw IdentityErrors.Validation("currentPassword", "Mevcut şifre hatalı.");
        }

        result.ThrowIfFailed("newPassword");

        await db.RevokeAllRefreshTokensAsync(userId, dateTime.UtcNow, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// A rotated token (ReplacedByHash set) presented again means it was copied: revoke every session.
    /// Tokens revoked by logout, password change or deactivation are simply rejected.
    /// </summary>
    private async Task RejectRevokedAsync(
        Guid userId,
        string? replacedByHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (replacedByHash is null)
        {
            throw new UnauthorizedException(SessionExpiredMessage);
        }

        var revoked = await db.RevokeAllRefreshTokensAsync(userId, now, cancellationToken);
        logger.LogWarning(
            "Refresh token reuse detected for user {UserId}; revoked {RevokedCount} active tokens",
            userId,
            revoked);
        throw new UnauthorizedException(SessionExpiredMessage);
    }

    private Task<AuthTokens> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var token = TokenService.GenerateRefreshToken();
        return IssueTokensAsync(user, token, TokenService.HashRefreshToken(token), dateTime.UtcNow, cancellationToken);
    }

    private async Task<AuthTokens> IssueTokensAsync(
        AppUser user,
        string refreshToken,
        string refreshTokenHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Staff;
        var (accessToken, accessExpiresAt) = tokenService.CreateAccessToken(user, role, now);

        var stored = RefreshToken.Issue(user.Id, refreshTokenHash, now, tokenService.RefreshTokenLifetime);
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken, accessExpiresAt, refreshToken, stored.ExpiresAt);
    }
}
