namespace StockTracker.Application.Common.Interfaces;

public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

/// <summary>Token based authentication backed by ASP.NET Core Identity (Infrastructure).</summary>
public interface IAuthService
{
    Task<AuthTokens> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthTokens> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<AuthTokens> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
}
