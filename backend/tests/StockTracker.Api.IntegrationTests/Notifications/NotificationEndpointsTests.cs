using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Notifications;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Notifications;

[Collection(ApiCollection.Name)]
public class NotificationEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private async Task<List<JsonElement>> MineAsync(string token, Guid entityId)
    {
        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/notifications?pageSize=100", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("entityId").GetGuid() == entityId)];
    }

    private async Task<int> UnreadAsync(string token) =>
        (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/notifications/unread-count", token))))
            .GetProperty("count").GetInt32();

    private async Task<(string Email, string Token, Guid Id)> UserAsync(string adminToken, string role)
    {
        var email = UniqueEmail();
        var id = await CreateUserAsync(_client, adminToken, role, email: email);
        return (email, (await LoginAsync(_client, email, "Passw0rd!")).AccessToken, id);
    }

    private async Task RunDailyAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailyNotificationRun>().RunAsync(default);
    }

    [Fact]
    public async Task LowStock_OneNotificationPerUser_NoDuplicate_ClosesWhenRefilled()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var manager = await UserAsync(api.Token, Roles.WarehouseManager);
        var staff = await UserAsync(api.Token, Roles.Staff);
        var screw = await api.QuantityProductAsync(await api.CategoryAsync(), "Vida M4"); // minimum 100
        var a1 = await api.LocationAsync("Raf A1");
        var b2 = await api.LocationAsync("Raf B2");

        await OkAsync(await api.InAsync(a1, new { productId = screw, quantity = 150 }));
        (await MineAsync(manager.Token, screw)).Should().BeEmpty();

        // Below the minimum: one notification for the manager (and admins), none for staff.
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, a1, 60)));
        var opened = (await MineAsync(manager.Token, screw)).Should().ContainSingle().Subject;
        opened.GetProperty("type").GetString().Should().Be("LowStock");
        opened.GetProperty("message").GetString().Should().Contain("90 adet").And.Contain("minimum 100");
        (await MineAsync(staff.Token, screw)).Should().BeEmpty();
        (await MineAsync(api.Token, screw)).Should().ContainSingle();

        // Another out and a transfer: still one; the daily run does not duplicate it either.
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, a1, 10)));
        await OkAsync(await api.TransferAsync(b2, Quantity(screw, a1, 30)));
        await RunDailyAsync();
        (await MineAsync(manager.Token, screw)).Should().ContainSingle();

        // Refilled to the minimum: resolved and no longer counted.
        await OkAsync(await api.InAsync(a1, new { productId = screw, quantity = 20 }));
        var resolved = (await MineAsync(manager.Token, screw)).Should().ContainSingle().Subject;
        resolved.GetProperty("resolvedAt").ValueKind.Should().Be(JsonValueKind.String);
        var unreadOnly = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/notifications?unreadOnly=true&pageSize=100", manager.Token));
        (await ReadJsonAsync(unreadOnly)).GetProperty("items").EnumerateArray()
            .Should().NotContain(n => n.GetProperty("entityId").GetGuid() == screw, "resolved notifications are not counted as unread");

        // Dropping again opens a new one.
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, b2, 1)));
        (await MineAsync(manager.Token, screw)).Should().HaveCount(2);
    }

    [Fact]
    public async Task SerializedTotal_CountsInStockItems_SoAssigningCanMakeItLow()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var laptop = (await api.JsonAsync(HttpMethod.Post, "/api/products", new
        {
            name = "Laptop",
            categoryId = category,
            unit = "Piece",
            trackingType = "Serialized",
            isAssignable = true,
            minStockLevel = 2,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var shelf = await api.LocationAsync("Raf");
        var received = await OkAsync(await api.InAsync(shelf, new { productId = laptop, quantity = 2 }));
        (await MineAsync(api.Token, laptop)).Should().BeEmpty();

        var staff = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        var item = received.GetProperty("createdItems")[0].GetProperty("id").GetGuid();
        await OkAsync(await api.SendAsync(HttpMethod.Post, "/api/assignments", new { itemId = item, assigneeUserId = staff }));

        (await MineAsync(api.Token, laptop)).Should().ContainSingle().Which.GetProperty("message").GetString().Should().Contain("1 adet");
    }

    [Fact]
    public async Task AssignmentReminders_DueSoonAndOverdue_ClosedOnReturn()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var manager = await UserAsync(api.Token, Roles.WarehouseManager);
        var assignee = await UserAsync(api.Token, Roles.Staff);
        var noLogin = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        var laptop = await api.SerializedProductAsync(await api.CategoryAsync());
        var shelf = await api.LocationAsync("Raf");
        var items = (await OkAsync(await api.InAsync(shelf, new { productId = laptop, quantity = 2 }))).GetProperty("createdItems")
            .EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(1).ToString("yyyy-MM-dd");

        var assignment = (await OkAsync(await api.SendAsync(HttpMethod.Post, "/api/assignments", new { itemId = items[0], assigneeUserId = assignee.Id, dueDate = tomorrow })))
            .GetProperty("assignmentId").GetGuid();
        var silent = (await OkAsync(await api.SendAsync(HttpMethod.Post, "/api/assignments", new { itemId = items[1], assigneeUserId = noLogin, dueDate = tomorrow })))
            .GetProperty("assignmentId").GetGuid();

        await RunDailyAsync();
        (await MineAsync(assignee.Token, assignment)).Should().ContainSingle().Which.GetProperty("type").GetString().Should().Be("AssignmentDueSoon");
        (await MineAsync(manager.Token, assignment)).Should().BeEmpty("only the assignee is told before the due date");
        (await MineAsync(manager.Token, silent)).Should().BeEmpty();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(-1);
            await db.Database.ExecuteSqlAsync($"UPDATE assignments SET due_date = {yesterday} WHERE id IN ({assignment}, {silent})");
        }

        await RunDailyAsync();
        await RunDailyAsync();
        (await MineAsync(assignee.Token, assignment)).Select(n => n.GetProperty("type").GetString()).Should().BeEquivalentTo("AssignmentDueSoon", "AssignmentOverdue");
        (await MineAsync(manager.Token, assignment)).Should().ContainSingle().Which.GetProperty("type").GetString().Should().Be("AssignmentOverdue");
        (await MineAsync(manager.Token, silent)).Should().ContainSingle("managers hear about overdue items of users who cannot log in");

        await OkAsync(await api.SendAsync(HttpMethod.Post, $"/api/assignments/{assignment}/return", new { returnLocationId = shelf, condition = "Good" }));
        (await MineAsync(assignee.Token, assignment)).Should().OnlyContain(n => n.GetProperty("resolvedAt").ValueKind == JsonValueKind.String);
        (await MineAsync(manager.Token, assignment)).Should().OnlyContain(n => n.GetProperty("resolvedAt").ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task ReadMarks_AreOwnedByTheUser()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var manager = await UserAsync(api.Token, Roles.WarehouseManager);
        var other = await UserAsync(api.Token, Roles.WarehouseManager);
        var category = await api.CategoryAsync();
        var shelf = await api.LocationAsync("Raf");
        var first = await api.QuantityProductAsync(category, "Vida A");
        var second = await api.QuantityProductAsync(category, "Vida B");
        await OkAsync(await api.InAsync(shelf, new { productId = first, quantity = 1 }, new { productId = second, quantity = 1 }));

        var notification = (await MineAsync(manager.Token, first)).Single().GetProperty("id").GetGuid();
        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/notifications/{notification}/read", other.Token))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var before = await UnreadAsync(manager.Token);
        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/notifications/{notification}/read", manager.Token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(manager.Token)).Should().Be(before - 1);

        (await _client.SendAsync(Authorized(HttpMethod.Post, "/api/notifications/read-all", manager.Token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(manager.Token)).Should().Be(0);
        (await UnreadAsync(other.Token)).Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("2026-10-27T04:59:00Z", 0, 1)]   // 07:59 Istanbul → 1 minute
    [InlineData("2026-10-27T05:00:00Z", 24, 0)]  // exactly 08:00 → next day
    [InlineData("2026-10-27T21:30:00Z", 7, 30)]  // 00:30 Istanbul (next day) → 07:30
    public void DailyJob_WaitsUntil0800Istanbul(string now, int hours, int minutes) =>
        DailyNotificationJob.UntilNext(DateTimeOffset.Parse(now), new TimeSpan(8, 0, 0)).Should().Be(new TimeSpan(hours, minutes, 0));
}
