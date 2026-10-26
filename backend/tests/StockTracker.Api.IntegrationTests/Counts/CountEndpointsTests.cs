using System.Net;
using System.Text.Json;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Counts;

[Collection(ApiCollection.Name)]
public class CountEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    /// <summary>Bina › Raf A1 / Raf B2; 200 screws and laptop "B2" on Raf B2, laptop "A1" on Raf A1.</summary>
    private sealed record Setup(
        InventoryApi Api,
        Guid Screw,
        Guid Building,
        Guid A1,
        Guid B2,
        string B2Code,
        Guid LaptopA1,
        string LaptopA1Code,
        Guid LaptopB2,
        string LaptopB2Code,
        string ScrewCode);

    private async Task<Setup> SetupAsync()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var laptop = await api.SerializedProductAsync(category);
        var screw = await api.QuantityProductAsync(category);
        var building = await api.LocationAsync("Bina", type: "Building");
        var a1 = await api.LocationAsync("Raf A1", building);
        var b2 = await api.LocationAsync("Raf B2", building);
        await OkAsync(await api.InAsync(b2, new { productId = screw, quantity = 200 }));
        var inB2 = (await OkAsync(await api.InAsync(b2, new { productId = laptop, quantity = 1 }))).GetProperty("createdItems")[0];
        var inA1 = (await OkAsync(await api.InAsync(a1, new { productId = laptop, quantity = 1 }))).GetProperty("createdItems")[0];

        return new Setup(
            api,
            screw,
            building,
            a1,
            b2,
            await CodeAsync(api, $"/api/locations/{b2}"),
            inA1.GetProperty("id").GetGuid(),
            inA1.GetProperty("code").GetString()!,
            inB2.GetProperty("id").GetGuid(),
            inB2.GetProperty("code").GetString()!,
            await CodeAsync(api, $"/api/products/{screw}"));
    }

    private static async Task<string> CodeAsync(InventoryApi api, string url) =>
        (await api.JsonAsync(HttpMethod.Get, url)).GetProperty("code").GetString()!;

    private async Task<string> StaffTokenAsync(string adminToken)
    {
        var email = UniqueEmail();
        await CreateUserAsync(_client, adminToken, Roles.Staff, email: email);
        return (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;
    }

    private static async Task<Guid> StartAsync(InventoryApi api, Guid locationId, bool includeChildren = false) =>
        (await api.JsonAsync(HttpMethod.Post, "/api/counts", new { locationId, includeChildren }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

    private Task<HttpResponseMessage> ScanAsync(string token, Guid countId, Guid? locationId, string code, decimal? quantity = null) =>
        _client.SendAsync(Authorized(HttpMethod.Post, $"/api/counts/{countId}/scan", token, new { locationId, code, quantity }));

    private static Task<HttpResponseMessage> ApproveAsync(InventoryApi api, Guid countId, params object[] decisions) =>
        api.SendAsync(HttpMethod.Post, $"/api/counts/{countId}/approve", new { decisions });

    private static object Decision(Guid lineId, string resolution) => new { lineId, resolution };

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
        JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    private static JsonElement ReviewLine(JsonElement review, Func<JsonElement, bool> match) =>
        review.GetProperty("lines").EnumerateArray().Single(match);

    private static bool IsQuantity(JsonElement r) => r.GetProperty("line").GetProperty("itemId").ValueKind == JsonValueKind.Null;

    private static bool IsItem(JsonElement r, Guid itemId) =>
        r.GetProperty("line").TryGetProperty("itemId", out var id) && id.ValueKind == JsonValueKind.String && id.GetGuid() == itemId;

    [Fact]
    public async Task StaffCounts_ManagerApproves_DifferencesBecomeMovementsWithOneReference()
    {
        var s = await SetupAsync();
        var staff = await StaffTokenAsync(s.Api.Token);
        (await _client.SendAsync(Authorized(HttpMethod.Post, "/api/counts", staff, new { locationId = s.B2 }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var countId = await StartAsync(s.Api, s.B2);
        var count = await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}");
        count.GetProperty("progress").GetProperty("expectedLines").GetInt32().Should().Be(2);

        // The shelf label sets the context; it changes nothing.
        var context = await OkAsync(await ScanAsync(staff, countId, null, s.B2Code));
        context.GetProperty("kind").GetString().Should().Be("Location");
        context.GetProperty("locationId").GetGuid().Should().Be(s.B2);

        // Quantity product: asks for the quantity, then adds up 100 + 85.
        (await OkAsync(await ScanAsync(staff, countId, s.B2, s.ScrewCode))).GetProperty("kind").GetString().Should().Be("QuantityRequired");
        await OkAsync(await ScanAsync(staff, countId, s.B2, s.ScrewCode, 100));
        var screw = await OkAsync(await ScanAsync(staff, countId, s.B2, s.ScrewCode, 85));
        screw.GetProperty("kind").GetString().Should().Be("Counted");
        screw.GetProperty("countedQuantity").GetDecimal().Should().Be(185);

        // A laptop that belongs on Raf A1.
        var unexpected = await OkAsync(await ScanAsync(staff, countId, s.B2, s.LaptopA1Code));
        unexpected.GetProperty("kind").GetString().Should().Be("Unexpected");
        unexpected.GetProperty("itemStatus").GetString().Should().Be("InStock");
        unexpected.GetProperty("itemLocationPath").GetString().Should().EndWith("› Raf A1");

        var recent = (await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}")).GetProperty("recentScans").EnumerateArray().ToList();
        recent.Should().HaveCount(3);
        recent[0].GetProperty("itemCode").GetString().Should().Be(s.LaptopA1Code);

        // Submit locks the count; staff cannot review.
        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/counts/{countId}/submit", staff))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeDomainErrorAsync(await ScanAsync(staff, countId, s.B2, s.ScrewCode, 1), CountErrors.NotCounting);
        (await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/counts/{countId}/review", staff))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var review = await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}/review");
        var screwLine = ReviewLine(review, IsQuantity);
        screwLine.GetProperty("state").GetString().Should().Be("QuantityDifference");
        screwLine.GetProperty("difference").GetDecimal().Should().Be(-15);
        var movable = ReviewLine(review, r => IsItem(r, s.LaptopA1));
        movable.GetProperty("state").GetString().Should().Be("UnexpectedMovable");
        var missing = ReviewLine(review, r => IsItem(r, s.LaptopB2));
        missing.GetProperty("state").GetString().Should().Be("ItemMissing");

        Guid Id(JsonElement line) => line.GetProperty("line").GetProperty("id").GetGuid();

        // Undecided line → 422, nothing written.
        await ShouldBeDomainErrorAsync(
            await ApproveAsync(s.Api, countId, Decision(Id(screwLine), "ApplyDifference")),
            CountErrors.UndecidedLines);
        (await s.Api.QuantityAtAsync(s.Screw, s.B2)).Should().Be(200);

        var approved = await OkAsync(await ApproveAsync(
            s.Api,
            countId,
            Decision(Id(screwLine), "ApplyDifference"),
            Decision(Id(movable), "MoveHere"),
            Decision(Id(missing), "MarkLost")));
        approved.GetProperty("movementCount").GetInt32().Should().Be(3);
        var reference = approved.GetProperty("referenceId").GetGuid();

        (await s.Api.QuantityAtAsync(s.Screw, s.B2)).Should().Be(185);
        (await s.Api.ItemAsync(s.LaptopA1)).GetProperty("currentLocationId").GetGuid().Should().Be(s.B2);
        (await s.Api.ItemAsync(s.LaptopB2)).GetProperty("status").GetString().Should().Be("Disposed");

        var ledger = (await s.Api.JsonAsync(HttpMethod.Get, $"/api/movements?referenceId={reference}")).GetProperty("items").EnumerateArray().ToList();
        ledger.Select(m => m.GetProperty("type").GetString()).Should().BeEquivalentTo("Adjustment", "Transfer", "Out");
        ledger.Single(m => m.GetProperty("type").GetString() == "Out").GetProperty("reason").GetString().Should().Be("Lost");
        ledger.Single(m => m.GetProperty("type").GetString() == "Adjustment").GetProperty("quantity").GetDecimal().Should().Be(15);

        var done = await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}");
        done.GetProperty("status").GetString().Should().Be("Approved");
        done.GetProperty("approvalReferenceId").GetGuid().Should().Be(reference);

        // The approval's own movements are not "movements since start".
        (await s.Api.JsonAsync(HttpMethod.Get, $"/api/movements?countId={countId}")).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task TransferDuringCount_DifferenceUsesTheCurrentBalance()
    {
        var s = await SetupAsync();
        var countId = await StartAsync(s.Api, s.B2);

        // 20 screws leave Raf B2 after the snapshot (200); the counter then finds 180.
        await OkAsync(await s.Api.TransferAsync(s.A1, Quantity(s.Screw, s.B2, 20)));
        await OkAsync(await ScanAsync(s.Api.Token, countId, s.B2, s.ScrewCode, 180));
        await OkAsync(await ScanAsync(s.Api.Token, countId, s.B2, s.LaptopB2Code));
        await s.Api.JsonAsync(HttpMethod.Post, $"/api/counts/{countId}/submit", expected: HttpStatusCode.NoContent);

        var review = await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}/review");
        var screw = ReviewLine(review, IsQuantity);
        screw.GetProperty("line").GetProperty("expectedQuantity").GetDecimal().Should().Be(200);
        screw.GetProperty("currentQuantity").GetDecimal().Should().Be(180);
        screw.GetProperty("difference").GetDecimal().Should().Be(0);
        screw.GetProperty("state").GetString().Should().Be("Matched");
        review.GetProperty("movementsSinceStart").GetInt32().Should().Be(1);

        var since = (await s.Api.JsonAsync(HttpMethod.Get, $"/api/movements?countId={countId}")).GetProperty("items").EnumerateArray().ToList();
        since.Should().ContainSingle().Which.GetProperty("type").GetString().Should().Be("Transfer");

        // Nothing to decide: approval writes no movement.
        (await OkAsync(await ApproveAsync(s.Api, countId))).GetProperty("movementCount").GetInt32().Should().Be(0);
        (await s.Api.QuantityAtAsync(s.Screw, s.B2)).Should().Be(180);
    }

    [Fact]
    public async Task OverlappingCounts_AreRejected()
    {
        var s = await SetupAsync();

        var building = await StartAsync(s.Api, s.Building, includeChildren: true);
        await ShouldBeDomainErrorAsync(
            await s.Api.SendAsync(HttpMethod.Post, "/api/counts", new { locationId = s.B2 }),
            CountErrors.Overlapping);

        await s.Api.JsonAsync(HttpMethod.Post, $"/api/counts/{building}/cancel", expected: HttpStatusCode.NoContent);
        var shelf = await StartAsync(s.Api, s.B2);
        await ShouldBeDomainErrorAsync(
            await s.Api.SendAsync(HttpMethod.Post, "/api/counts", new { locationId = s.Building, includeChildren = true }),
            CountErrors.Overlapping);

        // The building alone (without children) does not overlap the shelf.
        var buildingOnly = await StartAsync(s.Api, s.Building);
        buildingOnly.Should().NotBe(shelf);
    }

    [Fact]
    public async Task Undo_Cancel_AndScanRules()
    {
        var s = await SetupAsync();
        var countId = await StartAsync(s.Api, s.B2);

        await ShouldBeDomainErrorAsync(await ScanAsync(s.Api.Token, countId, null, s.LaptopB2Code), CountErrors.ContextRequired);
        await ShouldBeDomainErrorAsync(await ScanAsync(s.Api.Token, countId, s.A1, s.LaptopA1Code), CountErrors.LocationOutOfScope);
        await ShouldBeDomainErrorAsync(await ScanAsync(s.Api.Token, countId, s.B2, "ITM-ZZZZZZ"), CountErrors.CodeNotFound);

        var unexpected = await OkAsync(await ScanAsync(s.Api.Token, countId, s.B2, s.LaptopA1Code));
        var lineId = unexpected.GetProperty("lineId").GetGuid();
        (await OkAsync(await ScanAsync(s.Api.Token, countId, s.B2, s.LaptopA1Code))).GetProperty("kind").GetString().Should().Be("AlreadyCounted");

        await s.Api.JsonAsync(HttpMethod.Delete, $"/api/counts/{countId}/lines/{lineId}/last-scan", expected: HttpStatusCode.NoContent);
        var count = await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}");
        count.GetProperty("progress").GetProperty("unexpectedLines").GetInt32().Should().Be(0);
        count.GetProperty("recentScans").GetArrayLength().Should().Be(0);

        var movementsBefore = await s.Api.MovementCountAsync(s.Screw);
        await s.Api.JsonAsync(HttpMethod.Post, $"/api/counts/{countId}/cancel", expected: HttpStatusCode.NoContent);
        (await s.Api.JsonAsync(HttpMethod.Get, $"/api/counts/{countId}")).GetProperty("status").GetString().Should().Be("Cancelled");
        (await s.Api.MovementCountAsync(s.Screw)).Should().Be(movementsBefore);
        await ShouldBeDomainErrorAsync(await ApproveAsync(s.Api, countId), CountErrors.InvalidTransition);
    }
}
