using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Auditing;

[Collection(ApiCollection.Name)]
public class AuditEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private static async Task<List<JsonElement>> HistoryAsync(InventoryApi api, string entityType, Guid entityId) =>
        [.. (await api.JsonAsync(HttpMethod.Get, $"/api/audit?entityType={entityType}&entityId={entityId}&pageSize=100"))
            .GetProperty("items").EnumerateArray()];

    private static Dictionary<string, (string? Old, string? New)> Changes(JsonElement row) =>
        row.GetProperty("changes").EnumerateArray().ToDictionary(
            c => c.GetProperty("field").GetString()!,
            c => (Text(c, "old"), Text(c, "new")));

    private static string? Text(JsonElement change, string name) =>
        change.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static object Laptop(Guid categoryId, string name) => new
    {
        name,
        categoryId,
        unit = "Piece",
        trackingType = "Serialized",
        isAssignable = true,
    };

    [Fact]
    public async Task RenamingAProduct_WritesOneRowWithOldAndNewName()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var product = await api.SerializedProductAsync(category, "Laptop");

        var created = (await HistoryAsync(api, "Product", product)).Single();
        created.GetProperty("action").GetString().Should().Be("Created");
        created.GetProperty("userName").GetString().Should().Be("Sistem Yöneticisi");
        Changes(created).Should().ContainKey("name").WhoseValue.Should().Be((null, "Laptop"));
        Changes(created).Should().NotContainKey("createdAt");

        await api.JsonAsync(HttpMethod.Put, $"/api/products/{product}", Laptop(category, "Laptop Pro"), HttpStatusCode.NoContent);
        // Saving the same values again changes nothing and writes nothing.
        await api.JsonAsync(HttpMethod.Put, $"/api/products/{product}", Laptop(category, "Laptop Pro"), HttpStatusCode.NoContent);

        var history = await HistoryAsync(api, "Product", product);
        history.Should().HaveCount(2);
        var updated = history[0];
        updated.GetProperty("action").GetString().Should().Be("Updated");
        updated.GetProperty("entityLabel").GetString().Should().Be("Laptop Pro");
        Changes(updated).Should().Equal(new Dictionary<string, (string?, string?)> { ["name"] = ("Laptop", "Laptop Pro") });
    }

    [Fact]
    public async Task ProductHistory_IncludesItsBarcodes_AndDeactivation()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var product = await api.QuantityProductAsync(category, "Kablo");

        var barcode = await api.JsonAsync(HttpMethod.Post, $"/api/products/{product}/barcodes", new { value = "Kablo-128-X", format = "Code128" }, HttpStatusCode.Created);
        await api.JsonAsync(HttpMethod.Delete, $"/api/products/{product}/barcodes/{barcode.GetProperty("id").GetGuid()}", expected: HttpStatusCode.NoContent);
        await api.JsonAsync(HttpMethod.Post, $"/api/products/{product}/deactivate", expected: HttpStatusCode.NoContent);

        var history = await HistoryAsync(api, "Product", product);
        history.Select(h => (h.GetProperty("entityType").GetString(), h.GetProperty("action").GetString()))
            .Should().Equal(("Product", "Deactivated"), ("ProductBarcode", "Deleted"), ("ProductBarcode", "Created"), ("Product", "Created"));
        Changes(history[0]).Should().ContainKey("isActive");
        history[1].GetProperty("parentEntityId").GetGuid().Should().Be(product);
    }

    [Fact]
    public async Task UserChanges_AreAudited_ButNeverPasswords()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var email = UniqueEmail();
        var userId = await CreateUserAsync(_client, api.Token, Roles.Staff, email: email);

        await api.JsonAsync(HttpMethod.Post, $"/api/users/{userId}/reset-password", new { newPassword = "Yeni12345" }, HttpStatusCode.NoContent);
        await api.JsonAsync(HttpMethod.Put, $"/api/users/{userId}", new
        {
            fullName = "Test Depocu",
            department = "Depo",
            email,
            password = (string?)null,
            role = Roles.WarehouseManager,
            canLogin = true,
        }, HttpStatusCode.NoContent);

        var history = await HistoryAsync(api, "User", userId);
        var update = history.First(h => Changes(h).ContainsKey("role"));
        Changes(update)["role"].Should().Be((Roles.Staff, Roles.WarehouseManager));
        Changes(history.First(h => Changes(h).ContainsKey("fullName")))["fullName"].Should().Be(("Test Staff", "Test Depocu"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leaks = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM audit_logs WHERE changes::text ILIKE '%password%' OR changes::text ILIKE '%securityStamp%'")
            .SingleAsync();
        leaks.Should().Be(0);
    }

    [Fact]
    public async Task OnlyAdminsReadTheAuditLog()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var email = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.WarehouseManager, email: email);
        var manager = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;

        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/audit", manager))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var all = await api.JsonAsync(HttpMethod.Get, "/api/audit?entityType=Category&pageSize=5");
        all.GetProperty("items").EnumerateArray().Should().OnlyContain(a => a.GetProperty("entityType").GetString() == "Category");
    }
}
