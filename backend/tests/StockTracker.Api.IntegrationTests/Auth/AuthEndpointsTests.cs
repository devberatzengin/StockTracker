using System.Net;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests(ApiFactory factory)
{
    private const string InvalidCredentials = "E-posta veya şifre hatalı.";

    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Login_SetsSecureHttpOnlyRefreshCookie()
    {
        var response = await LoginRawAsync(_client, TestSettings.AdminEmail, TestSettings.AdminPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = GetRefreshCookie(response);
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Strict);
        cookie.Path.ToString().Should().Be("/api/auth");
        cookie.Expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        var body = await ReadJsonAsync(response);
        body.GetProperty("expiresAt").GetDateTime()
            .Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Me_ReturnsProfileWithPermissions()
    {
        var session = await LoginAsync(_client);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", session.AccessToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await ReadJsonAsync(response);
        me.GetProperty("email").GetString().Should().Be(TestSettings.AdminEmail);
        me.GetProperty("role").GetString().Should().Be(Roles.Admin);
        me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString())
            .Should().Contain(["CanManageUsers", "CanViewAudit"]);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseOfOldTokenRevokesAllTokens()
    {
        var first = await LoginAsync(_client);

        var rotated = await RefreshRawAsync(_client, first.RefreshToken);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await ReadSessionAsync(rotated);
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        // Replaying the already rotated token is treated as theft.
        (await RefreshRawAsync(_client, first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...which also kills the legitimate successor.
        (await RefreshRawAsync(_client, second.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithUnknownOrMissingToken_Returns401()
    {
        (await RefreshRawAsync(_client, "not-a-real-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokenAndClearsCookie()
    {
        var session = await LoginAsync(_client);

        var logout = await LogoutRawAsync(_client, session.RefreshToken);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        GetRefreshCookie(logout).Expires.Should().BeBefore(DateTimeOffset.UtcNow);
        (await RefreshRawAsync(_client, session.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericMessage()
    {
        var response = await LoginRawAsync(_client, TestSettings.AdminEmail, "Wrong12345");

        await ShouldBeInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_DeactivatedUser_IsRejectedWithSameMessage_AndSessionsAreRevoked()
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        var userId = await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, email: email);
        var userSession = await LoginAsync(_client, email, "Passw0rd!");

        var deactivate = await _client.SendAsync(
            Authorized(HttpMethod.Post, $"/api/users/{userId}/deactivate", admin.AccessToken));
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldBeInvalidCredentialsAsync(await LoginRawAsync(_client, email, "Passw0rd!"));
        (await RefreshRawAsync(_client, userSession.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UserWithoutLoginPermission_IsRejectedWithSameMessage()
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/users", admin.AccessToken, new
        {
            fullName = "Saha Personeli",
            email,
            password = "Passw0rd!",
            role = Roles.Staff,
            canLogin = false,
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await ShouldBeInvalidCredentialsAsync(await LoginRawAsync(_client, email, "Passw0rd!"));
    }

    [Fact]
    public async Task Login_AfterFiveFailures_IsLockedOut_UntilAdminResetsPassword()
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        var userId = await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, email: email);

        for (var i = 0; i < 5; i++)
        {
            await LoginRawAsync(_client, email, "Wrong12345");
        }

        await ShouldBeInvalidCredentialsAsync(await LoginRawAsync(_client, email, "Passw0rd!"));

        var reset = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/users/{userId}/reset-password",
            admin.AccessToken,
            new { newPassword = "NewPassw0rd" }));
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LoginRawAsync(_client, email, "NewPassw0rd")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_RevokesOldSessionsAndReturnsNewTokens()
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, email: email);
        var otherDevice = await LoginAsync(_client, email, "Passw0rd!");
        var session = await LoginAsync(_client, email, "Passw0rd!");

        var wrongCurrent = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/auth/change-password",
            session.AccessToken,
            new { currentPassword = "Wrong12345", newPassword = "Changed123" }));
        wrongCurrent.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/auth/change-password",
            session.AccessToken,
            new { currentPassword = "Passw0rd!", newPassword = "Changed123" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = await ReadSessionAsync(response);
        (await RefreshRawAsync(_client, renewed.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginRawAsync(_client, email, "Changed123")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Last: presenting a revoked token counts as reuse and revokes every session of the user.
        (await RefreshRawAsync(_client, otherDevice.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task ShouldBeInvalidCredentialsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadJsonAsync(response)).GetProperty("detail").GetString().Should().Be(InvalidCredentials);
        response.Headers.Contains(HeaderNames.SetCookie).Should().BeFalse();
    }
}
