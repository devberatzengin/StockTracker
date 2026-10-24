namespace StockTracker.Api.Contracts;

public sealed record AccessTokenResponse(string AccessToken, DateTime ExpiresAt);
