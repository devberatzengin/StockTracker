using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Users;

[Collection(ApiCollection.Name)]
public class UsersEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Theory]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.WarehouseManager)]
    public async Task NonAdminRoles_Get403OnUserManagement(string role)
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        await CreateUserAsync(_client, admin.AccessToken, role, email: email);
        var session = await LoginAsync(_client, email, "Passw0rd!");

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users", session.AccessToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserManagement_WithoutToken_Returns401()
    {
        (await _client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_WithoutLogin_NeedsNoEmailOrPassword_AndIsListed()
    {
        var admin = await LoginAsync(_client);
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/users", admin.AccessToken, new
        {
            fullName = "Zimmet Alıcısı Mehmet",
            department = "Saha",
            role = Roles.Staff,
            canLogin = false,
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/users?search=Zimmet%20Al%C4%B1c%C4%B1s%C4%B1&pageSize=10",
            admin.AccessToken));

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(list);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        var user = body.GetProperty("items")[0];
        user.GetProperty("canLogin").GetBoolean().Should().BeFalse();
        user.GetProperty("email").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        user.GetProperty("role").GetString().Should().Be(Roles.Staff);
        user.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_WithLoginButNoPassword_Returns400()
    {
        var admin = await LoginAsync(_client);

        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/users", admin.AccessToken, new
        {
            fullName = "Ali",
            email = UniqueEmail(),
            role = Roles.Staff,
            canLogin = true,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("Password", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Returns400()
    {
        var admin = await LoginAsync(_client);
        var email = UniqueEmail();
        await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, email: email);

        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/users", admin.AccessToken, new
        {
            fullName = "Kopya",
            email = email.ToUpperInvariant(),
            password = "Passw0rd!",
            role = Roles.Staff,
            canLogin = true,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("Email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_ChangesRole_AndEnablingLoginRequiresPassword()
    {
        var admin = await LoginAsync(_client);
        var userId = await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, canLogin: false);
        var email = UniqueEmail();

        var withoutPassword = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/users/{userId}", admin.AccessToken, new
        {
            fullName = "Terfi Eden",
            email,
            role = Roles.WarehouseManager,
            canLogin = true,
        }));
        withoutPassword.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var update = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/users/{userId}", admin.AccessToken, new
        {
            fullName = "Terfi Eden",
            email,
            password = "Passw0rd!",
            role = Roles.WarehouseManager,
            canLogin = true,
        }));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var session = await LoginAsync(_client, email, "Passw0rd!");
        var me = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", session.AccessToken)));
        me.GetProperty("role").GetString().Should().Be(Roles.WarehouseManager);
        me.GetProperty("fullName").GetString().Should().Be("Terfi Eden");
    }

    [Fact]
    public async Task UserChanges_FillAuditFieldsWithTheActingAdmin()
    {
        var admin = await LoginAsync(_client);
        var adminId = (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", admin.AccessToken))))
            .GetProperty("id").GetGuid();
        var userId = await CreateUserAsync(_client, admin.AccessToken, Roles.Staff, canLogin: false);

        var created = await LoadUserAsync(userId);
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        created.CreatedBy.Should().Be(adminId);

        var update = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/users/{userId}", admin.AccessToken, new
        {
            fullName = "Güncellenen",
            role = Roles.Staff,
            canLogin = false,
        }));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await LoadUserAsync(userId);
        updated.CreatedAt.Should().Be(created.CreatedAt);
        // Identity's role assignment already touches the user during creation, so compare against that.
        updated.UpdatedAt.Should().BeAfter(created.UpdatedAt ?? created.CreatedAt);
        updated.UpdatedBy.Should().Be(adminId);
    }

    private async Task<StockTracker.Infrastructure.Identity.AppUser> LoadUserAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task UpdateUser_UnknownId_Returns404()
    {
        var admin = await LoginAsync(_client);

        var response = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/users/{Guid.NewGuid()}", admin.AccessToken, new
        {
            fullName = "Yok",
            role = Roles.Staff,
            canLogin = false,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListUsers_RejectsPageSizeOver100()
    {
        var admin = await LoginAsync(_client);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users?pageSize=101", admin.AccessToken));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
