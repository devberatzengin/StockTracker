using System.Net;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Users;

/// <summary>Runs on its own database so the set of admins is known.</summary>
[Collection(AdminRulesCollection.Name)]
public class AdminRulesTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Admin_CannotDemoteSelf()
    {
        var admin = await LoginAsync(_client);
        var me = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", admin.AccessToken)));

        var response = await _client.SendAsync(Authorized(
            HttpMethod.Put,
            $"/api/users/{me.GetProperty("id").GetGuid()}",
            admin.AccessToken,
            new { fullName = "Admin", email = TestSettings.AdminEmail, role = Roles.Staff, canLogin = true }));

        await ShouldBeDomainErrorAsync(response, UserAdministrationRules.CannotDemoteSelf);
    }

    [Fact]
    public async Task Admin_CannotDeactivateSelf()
    {
        var admin = await LoginAsync(_client);
        var me = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", admin.AccessToken)));

        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/users/{me.GetProperty("id").GetGuid()}/deactivate",
            admin.AccessToken));

        await ShouldBeDomainErrorAsync(response, UserAdministrationRules.CannotDeactivateSelf);
    }

    [Fact]
    public async Task DeactivatingTheLastActiveAdmin_IsRejected()
    {
        // Admin B still holds an Admin access token after A demotes B, so B can attempt
        // to deactivate A, who is now the only active admin.
        var adminA = await LoginAsync(_client);
        var adminAId = (await ReadJsonAsync(await _client.SendAsync(
            Authorized(HttpMethod.Get, "/api/auth/me", adminA.AccessToken)))).GetProperty("id").GetGuid();
        var emailB = UniqueEmail();
        var adminBId = await CreateUserAsync(_client, adminA.AccessToken, Roles.Admin, email: emailB);
        var adminB = await LoginAsync(_client, emailB, "Passw0rd!");

        var demote = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/users/{adminBId}", adminA.AccessToken, new
        {
            fullName = "Eski Admin",
            email = emailB,
            role = Roles.Staff,
            canLogin = true,
        }));
        demote.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/users/{adminAId}/deactivate",
            adminB.AccessToken));

        await ShouldBeDomainErrorAsync(response, UserAdministrationRules.LastActiveAdmin);
    }

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be(code);
    }
}
