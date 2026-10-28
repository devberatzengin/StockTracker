using System.Net;
using System.Text.Json;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Application.Common.Behaviors;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Idempotency;

/// <summary>Stock commands sent with an Idempotency-Key run at most once per user and key.</summary>
[Collection(ApiCollection.Name)]
public class IdempotencyTests(ApiFactory factory)
{
    private static async Task<(InventoryApi Api, Guid Screw, Guid Shelf)> SetupAsync(ApiFactory factory)
    {
        var api = await InventoryApi.CreateAsync(factory);
        var screw = await api.QuantityProductAsync(await api.CategoryAsync());
        var shelf = await api.LocationAsync("Raf");
        return (api, screw, shelf);
    }

    private static object Receipt(Guid shelf, Guid screw, decimal quantity) =>
        new { toLocationId = shelf, lines = new[] { new { productId = screw, quantity } } };

    private static string ReferenceOf(JsonElement result) => result.GetProperty("referenceId").GetString()!;

    [Fact]
    public async Task Repeat_ReturnsTheFirstResponse_WithoutASecondMovement()
    {
        var (api, screw, shelf) = await SetupAsync(factory);
        var key = Guid.NewGuid().ToString();

        var first = await OkAsync(await api.SendOnceAsync(HttpMethod.Post, "/api/movements/in", key, Receipt(shelf, screw, 10)));
        var second = await OkAsync(await api.SendOnceAsync(HttpMethod.Post, "/api/movements/in", key, Receipt(shelf, screw, 10)));

        ReferenceOf(second).Should().Be(ReferenceOf(first));
        (await api.MovementCountAsync(screw)).Should().Be(1);
        (await api.QuantityAtAsync(screw, shelf)).Should().Be(10);
    }

    [Fact]
    public async Task ConcurrentDuplicates_CreateOneMovement()
    {
        var (api, screw, shelf) = await SetupAsync(factory);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            api.SendOnceAsync(HttpMethod.Post, "/api/movements/in", key, Receipt(shelf, screw, 7))));

        var ok = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        ok.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        var references = await Task.WhenAll(ok.Select(async r => ReferenceOf(await OkAsync(r))));
        references.Distinct().Should().HaveCount(1);
        (await api.MovementCountAsync(screw)).Should().Be(1);
        (await api.QuantityAtAsync(screw, shelf)).Should().Be(7);
    }

    [Fact]
    public async Task SameKey_WithAnotherRequest_IsRejected()
    {
        var (api, screw, shelf) = await SetupAsync(factory);
        var key = Guid.NewGuid().ToString();
        await OkAsync(await api.SendOnceAsync(HttpMethod.Post, "/api/movements/in", key, Receipt(shelf, screw, 10)));

        var response = await api.SendOnceAsync(HttpMethod.Post, "/api/movements/in", key, Receipt(shelf, screw, 11));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("code").GetString().Should().Be(IdempotencyBehavior<object, object>.KeyReused);
    }

    [Fact]
    public async Task FailedCommand_DoesNotUseUpTheKey()
    {
        var (api, screw, shelf) = await SetupAsync(factory);
        var key = Guid.NewGuid().ToString();

        // 5 out of an empty shelf fails; after a receipt, the same key goes through.
        var failed = await api.SendOnceAsync(HttpMethod.Post, "/api/movements/out", key,
            new { reason = "Consumed", note = (string?)null, lines = new[] { Quantity(screw, shelf, 5) } });
        failed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 20 }));

        await OkAsync(await api.SendOnceAsync(HttpMethod.Post, "/api/movements/out", key,
            new { reason = "Consumed", note = (string?)null, lines = new[] { Quantity(screw, shelf, 5) } }));
        (await api.QuantityAtAsync(screw, shelf)).Should().Be(15);
    }

    [Fact]
    public async Task WithoutKey_EveryRequestRuns()
    {
        var (api, screw, shelf) = await SetupAsync(factory);

        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 3 }));
        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 3 }));

        (await api.MovementCountAsync(screw)).Should().Be(2);
    }

    [Fact]
    public async Task Reversal_IsIdempotentToo()
    {
        var (api, screw, shelf) = await SetupAsync(factory);
        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 30 }));
        var receipt = (await api.JsonAsync(HttpMethod.Get, $"/api/movements?productId={screw}"))
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        var key = Guid.NewGuid().ToString();

        var first = await OkAsync(await api.SendOnceAsync(HttpMethod.Post, $"/api/movements/{receipt}/reverse", key, new { note = "Yanlış" }));
        var second = await OkAsync(await api.SendOnceAsync(HttpMethod.Post, $"/api/movements/{receipt}/reverse", key, new { note = "Yanlış" }));

        ReferenceOf(second).Should().Be(ReferenceOf(first));
        (await api.MovementCountAsync(screw)).Should().Be(2);
    }
}
