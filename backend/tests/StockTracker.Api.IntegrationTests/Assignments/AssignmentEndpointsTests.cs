using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Assignments;

[Collection(ApiCollection.Name)]
public class AssignmentEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private sealed record Setup(InventoryApi Api, Guid Category, Guid Laptop, Guid Shelf, Guid ReturnShelf, Guid[] Items);

    /// <summary>Two laptops (assignable, serialized) in Raf A1; Raf B2 as return location.</summary>
    private async Task<Setup> SetupAsync()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var laptop = await api.SerializedProductAsync(category);
        var building = await api.LocationAsync("Bina", type: "Building");
        var shelf = await api.LocationAsync("Raf A1", building);
        var returnShelf = await api.LocationAsync("Raf B2", building);
        var received = await OkAsync(await api.InAsync(shelf, new { productId = laptop, quantity = 2, serialNumbers = new[] { "SN-1", "SN-2" } }));
        var items = received.GetProperty("createdItems").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        return new Setup(api, category, laptop, shelf, returnShelf, items);
    }

    private static string InDays(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(days).ToString("yyyy-MM-dd");

    private static Task<HttpResponseMessage> AssignAsync(InventoryApi api, Guid itemId, Guid assigneeUserId, string? dueDate = null, string? note = null) =>
        api.SendAsync(HttpMethod.Post, "/api/assignments", new { itemId, assigneeUserId, dueDate, note });

    private static Task<HttpResponseMessage> ReturnAsync(InventoryApi api, Guid assignmentId, Guid returnLocationId, string condition, string? note = null) =>
        api.SendAsync(HttpMethod.Post, $"/api/assignments/{assignmentId}/return", new { returnLocationId, condition, note });

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
        JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    private static List<string> Actions(JsonElement item) =>
        [.. item.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()!)];

    [Fact]
    public async Task Assign_FaultyReturn_Repair_WritesTheWholeHistory()
    {
        var (api, _, _, shelf, returnShelf, items) = await SetupAsync();
        var noLoginStaff = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        var item = items[0];

        Actions(await api.ItemAsync(item)).Should().BeEquivalentTo("Transfer", "Assign", "SendToRepair", "Dispose");

        // 1. Assign to a staff member who cannot log in, due in a week.
        var assigned = await OkAsync(await AssignAsync(api, item, noLoginStaff, InDays(7), "Saha ekibi"));
        var assignmentId = assigned.GetProperty("assignmentId").GetGuid();

        var assignedItem = await api.ItemAsync(item);
        assignedItem.GetProperty("status").GetString().Should().Be("Assigned");
        assignedItem.GetProperty("currentLocationId").ValueKind.Should().Be(JsonValueKind.Null);
        Actions(assignedItem).Should().Equal("Return");
        var open = assignedItem.GetProperty("openAssignment");
        open.GetProperty("id").GetGuid().Should().Be(assignmentId);
        open.GetProperty("assigneeName").GetString().Should().Be("Test Staff");
        open.GetProperty("dueDate").GetString().Should().Be(InDays(7));
        open.GetProperty("isOverdue").GetBoolean().Should().BeFalse();

        // Transfer of an assigned item is rejected.
        await ShouldBeLineErrorAsync(await api.TransferAsync(returnShelf, Item(item)), HttpStatusCode.UnprocessableEntity, "lines[0].itemId", InventoryErrors.InvalidItemTransition);

        // 2. Faulty return needs a note (validation), then goes to repair.
        (await ReturnAsync(api, assignmentId, returnShelf, "Faulty")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var returned = await OkAsync(await ReturnAsync(api, assignmentId, returnShelf, "Faulty", "Ekran kırık"));
        var returnRef = returned.GetProperty("referenceId").GetGuid();

        var inRepair = await api.ItemAsync(item);
        inRepair.GetProperty("status").GetString().Should().Be("InRepair");
        inRepair.GetProperty("currentLocationId").GetGuid().Should().Be(returnShelf);
        inRepair.GetProperty("openAssignment").ValueKind.Should().Be(JsonValueKind.Null);
        Actions(inRepair).Should().BeEquivalentTo("ReturnFromRepair", "Dispose");

        var assignment = await api.JsonAsync(HttpMethod.Get, $"/api/assignments/{assignmentId}");
        assignment.GetProperty("status").GetString().Should().Be("Returned");
        assignment.GetProperty("returnCondition").GetString().Should().Be("Faulty");
        assignment.GetProperty("returnNote").GetString().Should().Be("Ekran kırık");
        assignment.GetProperty("returnLocationPath").GetString().Should().EndWith("› Raf B2");

        // 3. Back from repair: note required, then InStock.
        (await api.SendAsync(HttpMethod.Post, $"/api/items/{item}/return-from-repair", new { note = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await api.JsonAsync(HttpMethod.Post, $"/api/items/{item}/return-from-repair", new { note = "Ekran değişti" });
        (await api.ItemAsync(item)).GetProperty("status").GetString().Should().Be("InStock");

        // History, newest first: StatusChange, the return operation (Return + StatusChange, one reference), Assign, In.
        var history = (await api.JsonAsync(HttpMethod.Get, $"/api/movements?itemId={item}")).GetProperty("items").EnumerateArray().ToList();
        history.Should().HaveCount(5);
        history[0].GetProperty("type").GetString().Should().Be("StatusChange");
        history[0].GetProperty("fromStatus").GetString().Should().Be("InRepair");
        history[0].GetProperty("note").GetString().Should().Be("Ekran değişti");

        // The faulty return: Return, then the StatusChange it caused (newest first), one reference.
        var toRepair = history[1];
        var returnMovement = history[2];
        toRepair.GetProperty("type").GetString().Should().Be("StatusChange");
        returnMovement.GetProperty("type").GetString().Should().Be("Return");
        toRepair.GetProperty("referenceId").GetGuid().Should().Be(returnRef);
        returnMovement.GetProperty("referenceId").GetGuid().Should().Be(returnRef);
        returnMovement.GetProperty("assigneeName").GetString().Should().Be("Test Staff");
        toRepair.GetProperty("toStatus").GetString().Should().Be("InRepair");

        history[3].GetProperty("type").GetString().Should().Be("Assign");
        history[3].GetProperty("assigneeName").GetString().Should().Be("Test Staff");
        history[3].GetProperty("fromLocationId").GetGuid().Should().Be(shelf);
        history[4].GetProperty("type").GetString().Should().Be("In");

        var itemAssignments = await api.JsonAsync(HttpMethod.Get, $"/api/assignments?itemId={item}");
        itemAssignments.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Assign_Rules_AreEnforced()
    {
        var (api, category, _, _, _, items) = await SetupAsync();
        var staff = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);

        await ShouldBeDomainErrorAsync(await AssignAsync(api, items[0], staff, InDays(0)), AssignmentErrors.DueDateNotInFuture);
        await ShouldBeDomainErrorAsync(await AssignAsync(api, items[0], Guid.NewGuid()), AssignmentErrors.AssigneeNotFound);

        await OkAsync(await AssignAsync(api, items[0], staff));
        await ShouldBeDomainErrorAsync(await AssignAsync(api, items[0], staff), InventoryErrors.InvalidItemTransition);

        // Not assignable product.
        var monitor = await api.JsonAsync(HttpMethod.Post, "/api/products", new
        {
            name = "Monitör",
            categoryId = category,
            unit = "Piece",
            trackingType = "Serialized",
            isAssignable = false,
        }, HttpStatusCode.Created);
        var shelf = await api.LocationAsync("Raf C");
        var received = await OkAsync(await api.InAsync(shelf, new { productId = monitor.GetProperty("id").GetGuid(), quantity = 1 }));
        var monitorItem = received.GetProperty("createdItems")[0].GetProperty("id").GetGuid();
        await ShouldBeDomainErrorAsync(await AssignAsync(api, monitorItem, staff), AssignmentErrors.ProductNotAssignable);

        // Inactive assignee.
        var leaver = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        await api.JsonAsync(HttpMethod.Post, $"/api/users/{leaver}/deactivate", expected: HttpStatusCode.NoContent);
        await ShouldBeDomainErrorAsync(await AssignAsync(api, items[1], leaver), AssignmentErrors.AssigneeInactive);
    }

    [Fact]
    public async Task OpenAssignment_BlocksUserDeactivationAndTurningOffAssignable()
    {
        var (api, category, laptop, _, returnShelf, items) = await SetupAsync();
        var staff = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        var assignmentId = (await OkAsync(await AssignAsync(api, items[0], staff))).GetProperty("assignmentId").GetGuid();

        await ShouldBeDomainErrorAsync(
            await api.SendAsync(HttpMethod.Post, $"/api/users/{staff}/deactivate"),
            AssignmentErrors.UserHasOpenAssignments);

        var notAssignable = new { name = "Laptop", categoryId = category, unit = "Piece", trackingType = "Serialized", isAssignable = false };
        await ShouldBeDomainErrorAsync(
            await api.SendAsync(HttpMethod.Put, $"/api/products/{laptop}", notAssignable),
            CatalogErrors.ProductHasOpenAssignments);

        await OkAsync(await ReturnAsync(api, assignmentId, returnShelf, "Good"));
        await api.JsonAsync(HttpMethod.Put, $"/api/products/{laptop}", notAssignable, HttpStatusCode.NoContent);
        await api.JsonAsync(HttpMethod.Post, $"/api/users/{staff}/deactivate", expected: HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Mine_ListFilters_AndVisibility()
    {
        var (api, _, laptop, _, returnShelf, items) = await SetupAsync();
        var email = UniqueEmail();
        var staff = await CreateUserAsync(_client, api.Token, Roles.Staff, email: email);
        var staffToken = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;
        var otherEmail = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.Staff, email: otherEmail);
        var otherToken = (await LoginAsync(_client, otherEmail, "Passw0rd!")).AccessToken;

        var first = (await OkAsync(await AssignAsync(api, items[0], staff, InDays(3)))).GetProperty("assignmentId").GetGuid();
        var second = (await OkAsync(await AssignAsync(api, items[1], staff, InDays(10)))).GetProperty("assignmentId").GetGuid();

        // Make the first one overdue (due dates cannot be in the past through the API).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(-1);
            await db.Database.ExecuteSqlAsync($"UPDATE assignments SET due_date = {yesterday} WHERE id = {first}");
        }

        // The staff member sees their own open assignments, earliest due date first.
        var mine = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/assignments/mine", staffToken));
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        var mineItems = (await ReadJsonAsync(mine)).EnumerateArray().ToList();
        mineItems.Select(a => a.GetProperty("id").GetGuid()).Should().Equal(first, second);
        mineItems[0].GetProperty("status").GetString().Should().Be("Overdue");

        // Only own assignments and no list for staff.
        (await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/assignments/{first}", staffToken))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/assignments/{first}", otherToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/assignments", staffToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authorized(HttpMethod.Post, "/api/assignments", staffToken, new { itemId = items[0], assigneeUserId = staff })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Status filters for the warehouse.
        await OkAsync(await ReturnAsync(api, second, returnShelf, "Good"));
        async Task<List<Guid>> ListAsync(string status) =>
            [.. (await api.JsonAsync(HttpMethod.Get, $"/api/assignments?status={status}&productId={laptop}")).GetProperty("items").EnumerateArray().Select(a => a.GetProperty("id").GetGuid())];
        (await ListAsync("Open")).Should().Equal(first);
        (await ListAsync("Overdue")).Should().Equal(first);
        (await ListAsync("Returned")).Should().Equal(second);
        (await api.JsonAsync(HttpMethod.Get, $"/api/assignments?userId={staff}")).GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UserLookup_ListsActiveUsersForWarehouse()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var managerEmail = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.WarehouseManager, email: managerEmail);
        var managerToken = (await LoginAsync(_client, managerEmail, "Passw0rd!")).AccessToken;
        var staffEmail = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.Staff, email: staffEmail);
        var staffToken = (await LoginAsync(_client, staffEmail, "Passw0rd!")).AccessToken;
        var leaver = await CreateUserAsync(_client, api.Token, Roles.Staff, canLogin: false);
        await api.JsonAsync(HttpMethod.Post, $"/api/users/{leaver}/deactivate", expected: HttpStatusCode.NoContent);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users/lookup?search=Test&limit=50", managerToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await ReadJsonAsync(response)).EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToList();
        ids.Should().NotContain(leaver);
        ids.Should().NotBeEmpty();

        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users/lookup", staffToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/users/{leaver}", managerToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.JsonAsync(HttpMethod.Get, $"/api/users/{leaver}")).GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SendToRepair_RequiresInStockAndNote()
    {
        var (api, _, _, shelf, _, items) = await SetupAsync();

        (await api.SendAsync(HttpMethod.Post, $"/api/items/{items[0]}/send-to-repair", new { note = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ShouldBeDomainErrorAsync(
            await api.SendAsync(HttpMethod.Post, $"/api/items/{items[0]}/return-from-repair", new { note = "x" }),
            InventoryErrors.InvalidItemTransition);

        await api.JsonAsync(HttpMethod.Post, $"/api/items/{items[0]}/send-to-repair", new { note = "Fan sesi" });
        var item = await api.ItemAsync(items[0]);
        item.GetProperty("status").GetString().Should().Be("InRepair");
        item.GetProperty("currentLocationId").GetGuid().Should().Be(shelf);
    }
}
