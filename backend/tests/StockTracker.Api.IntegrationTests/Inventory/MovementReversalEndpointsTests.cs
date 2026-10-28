using System.Net;
using System.Text.Json;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Inventory;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Inventory;

[Collection(ApiCollection.Name)]
public class MovementReversalEndpointsTests(ApiFactory factory)
{
    private static async Task<JsonElement> MovementsAsync(InventoryApi api, Guid productId) =>
        (await api.JsonAsync(HttpMethod.Get, $"/api/movements?productId={productId}&sort=occurredAt")).GetProperty("items");

    private static Task<HttpResponseMessage> ReverseAsync(InventoryApi api, Guid movementId, string? note = "Yanlış girildi.") =>
        api.SendAsync(HttpMethod.Post, $"/api/movements/{movementId}/reverse", new { note });

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
        JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task WrongReceipt_IsReversed_AndBothMovementsStayInTheLedger()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var screw = await api.QuantityProductAsync(await api.CategoryAsync());
        var shelf = await api.LocationAsync("Raf");
        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 40 }));
        var receipt = (await MovementsAsync(api, screw))[0].GetProperty("id").GetGuid();

        await OkAsync(await ReverseAsync(api, receipt));

        (await api.QuantityAtAsync(screw, shelf)).Should().Be(0);
        var ledger = (await MovementsAsync(api, screw)).EnumerateArray().ToList();
        ledger.Should().HaveCount(2);
        ledger[0].GetProperty("reversedByMovementId").GetGuid().Should().Be(ledger[1].GetProperty("id").GetGuid());
        ledger[1].GetProperty("type").GetString().Should().Be(nameof(MovementType.Out));
        ledger[1].GetProperty("reversesMovementId").GetGuid().Should().Be(receipt);
        ledger[1].GetProperty("note").GetString().Should().Be("Yanlış girildi.");

        // Once only, and never the reversal itself.
        await ShouldBeDomainErrorAsync(await ReverseAsync(api, receipt), InventoryErrors.AlreadyReversed);
        await ShouldBeDomainErrorAsync(await ReverseAsync(api, ledger[1].GetProperty("id").GetGuid()), InventoryErrors.NotReversible);
    }

    [Fact]
    public async Task Transfer_IsReversed_AndOutRestoresStock()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var screw = await api.QuantityProductAsync(await api.CategoryAsync());
        var a = await api.LocationAsync("Raf A");
        var b = await api.LocationAsync("Raf B");
        await OkAsync(await api.InAsync(a, new { productId = screw, quantity = 30 }));
        await OkAsync(await api.TransferAsync(b, Quantity(screw, a, 10)));
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, a, 5)));
        var ledger = (await MovementsAsync(api, screw)).EnumerateArray().ToList();

        await OkAsync(await ReverseAsync(api, ledger[1].GetProperty("id").GetGuid()));
        await OkAsync(await ReverseAsync(api, ledger[2].GetProperty("id").GetGuid()));

        (await api.QuantityAtAsync(screw, a)).Should().Be(30);
        (await api.QuantityAtAsync(screw, b)).Should().Be(0);
    }

    [Fact]
    public async Task ItemReceipt_IsReversedByDisposal_UnlessTheItemMovedOn()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var laptop = await api.SerializedProductAsync(await api.CategoryAsync());
        var a = await api.LocationAsync("Raf A");
        var b = await api.LocationAsync("Raf B");
        var received = await OkAsync(await api.InAsync(a, new { productId = laptop, quantity = 2, serialNumbers = Array.Empty<string>() }));
        var items = received.GetProperty("createdItems").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var receipts = (await MovementsAsync(api, laptop)).EnumerateArray()
            .ToDictionary(m => m.GetProperty("itemId").GetGuid(), m => m.GetProperty("id").GetGuid());
        await OkAsync(await api.TransferAsync(b, Item(items[1])));

        await OkAsync(await ReverseAsync(api, receipts[items[0]]));
        (await api.ItemAsync(items[0])).GetProperty("status").GetString().Should().Be(nameof(ItemStatus.Disposed));

        await ShouldBeDomainErrorAsync(await ReverseAsync(api, receipts[items[1]]), InventoryErrors.ItemChangedSinceMovement);
    }

    [Fact]
    public async Task Reverse_NeedsNote_AndAnExistingMovement()
    {
        var api = await InventoryApi.CreateAsync(factory);

        (await ReverseAsync(api, Guid.NewGuid(), note: null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReverseAsync(api, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
