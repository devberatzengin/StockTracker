namespace StockTracker.Api.Authentication;

/// <summary>The refresh token travels only in this cookie, scoped to the auth endpoints.</summary>
public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTime expiresAt) =>
        response.Cookies.Append(Name, token, CreateOptions(expiresAt));

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, CreateOptions(expiresAt: null));

    private static CookieOptions CreateOptions(DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
