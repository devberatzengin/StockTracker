using System.Net;
using System.Text.Json;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Labels;

[Collection(ApiCollection.Name)]
public class LabelEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private static async Task<byte[]> PdfAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task ItemsOfAStockIn_PrintOn3x8()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var laptop = await api.SerializedProductAsync(await api.CategoryAsync());
        var shelf = await api.LocationAsync("Raf");
        var received = await OkAsync(await api.InAsync(shelf, new { productId = laptop, quantity = 3, serialNumbers = new[] { "SN-1", "SN-2", "SN-3" } }));

        var pdf = await PdfAsync(await api.SendAsync(HttpMethod.Post, "/api/labels", new
        {
            template = "Sheet3x8",
            referenceId = received.GetProperty("referenceId").GetGuid(),
        }));

        LabelRendererTests.CountPages(pdf).Should().Be(1);
    }

    [Fact]
    public async Task LocationSubtree_On2x4_FromPosition5_UsesTwoPages()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var room = await api.LocationAsync("Oda", type: "Room");
        for (var i = 1; i <= 4; i++)
        {
            await api.LocationAsync($"Raf B{i}", room);
        }

        // 5 labels (room + 4 shelves) starting at cell 5 of 8: 4 on the first sheet, 1 on the second.
        var pdf = await PdfAsync(await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Sheet2x4", startPosition = 5, locationId = room }));

        LabelRendererTests.CountPages(pdf).Should().Be(2);
    }

    [Fact]
    public async Task InvalidRequests_AreRejected()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var product = await api.QuantityProductAsync(await api.CategoryAsync());

        // Two targets, a start position past the sheet, too many labels, unknown product.
        (await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Sheet3x8", productIds = new[] { product }, locationId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Sheet3x8", startPosition = 25, productIds = new[] { product } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Sheet3x8", productIds = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unknown = await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Sheet5x13", productIds = new[] { product, Guid.NewGuid() } });
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonDocument.Parse(await unknown.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString().Should().Be("Labels.NotFound");

        var thermal = await PdfAsync(await api.SendAsync(HttpMethod.Post, "/api/labels", new { template = "Thermal50x30", productIds = new[] { product, product } }));
        LabelRendererTests.CountPages(thermal).Should().Be(1, "a product listed twice prints once");
    }

    [Fact]
    public async Task StaffCannotPrint()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var email = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.Staff, email: email);
        var staff = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;

        (await _client.SendAsync(Authorized(HttpMethod.Post, "/api/labels", staff, new { template = "Sheet3x8", productIds = new[] { Guid.NewGuid() } })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
