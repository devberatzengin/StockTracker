using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Net.Http.Headers;

namespace StockTracker.Api.IntegrationTests.Infrastructure;

public sealed record AuthSession(string AccessToken, string RefreshToken);

/// <summary>HTTP helpers for the auth and user endpoints. Cookies are passed explicitly.</summary>
public static class AuthApi
{
    public const string RefreshCookie = "refresh_token";

    public static Task<HttpResponseMessage> LoginRawAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<AuthSession> LoginAsync(
        HttpClient client,
        string email = TestSettings.AdminEmail,
        string password = TestSettings.AdminPassword)
    {
        var response = await LoginRawAsync(client, email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadSessionAsync(response);
    }

    public static Task<HttpResponseMessage> RefreshRawAsync(HttpClient client, string refreshToken) =>
        SendWithCookieAsync(client, "/api/auth/refresh", refreshToken);

    public static Task<HttpResponseMessage> LogoutRawAsync(HttpClient client, string refreshToken) =>
        SendWithCookieAsync(client, "/api/auth/logout", refreshToken);

    public static async Task<AuthSession> ReadSessionAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new AuthSession(body.GetProperty("accessToken").GetString()!, GetRefreshCookie(response).Value.ToString());
    }

    public static SetCookieHeaderValue GetRefreshCookie(HttpResponseMessage response) =>
        SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList())
            .Single(c => c.Name == RefreshCookie);

    public static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static async Task<Guid> CreateUserAsync(
        HttpClient client,
        string adminToken,
        string role,
        bool canLogin = true,
        string? email = null,
        string password = "Passw0rd!")
    {
        email ??= canLogin ? UniqueEmail() : null;
        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/users", adminToken, new
        {
            fullName = $"Test {role}",
            department = "Depo",
            email,
            password = canLogin ? password : null,
            role,
            canLogin,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@stocktracker.test";

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string url, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add(HeaderNames.Cookie, $"{RefreshCookie}={refreshToken}");
        return client.SendAsync(request);
    }
}
