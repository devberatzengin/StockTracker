using System.Net;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;

namespace StockTracker.Api.IntegrationTests.Endpoints;

/// <summary>Success paths of endpoints the feature tests did not call directly.</summary>
[Collection(ApiCollection.Name)]
public class RemainingEndpointsTests(ApiFactory factory)
{
    [Fact]
    public async Task Category_CanBeRenamed()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var name = $"Yeni ad {Guid.NewGuid():N}"[..20];

        await api.JsonAsync(HttpMethod.Put, $"/api/categories/{category}", new { name }, HttpStatusCode.NoContent);

        var categories = await api.JsonAsync(HttpMethod.Get, $"/api/categories?search={Uri.EscapeDataString(name)}");
        categories.GetProperty("items").EnumerateArray().Should().ContainSingle(c => c.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Location_CanBeRenamedAndRetyped()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var location = await api.LocationAsync("Raf");

        await api.JsonAsync(HttpMethod.Put, $"/api/locations/{location}", new { name = "Dolap 9", type = "Cabinet" }, HttpStatusCode.NoContent);

        var updated = await api.JsonAsync(HttpMethod.Get, $"/api/locations/{location}");
        updated.GetProperty("name").GetString().Should().Be("Dolap 9");
        updated.GetProperty("type").GetString().Should().Be("Cabinet");
    }

    [Fact]
    public async Task Counts_AreListedNewestFirst_AndFilteredByStatus()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var first = await api.LocationAsync("Raf");
        var second = await api.LocationAsync("Raf");
        var a = (await api.JsonAsync(HttpMethod.Post, "/api/counts", new { locationId = first }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var b = (await api.JsonAsync(HttpMethod.Post, "/api/counts", new { locationId = second }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await api.JsonAsync(HttpMethod.Post, $"/api/counts/{a}/cancel", expected: HttpStatusCode.NoContent);

        var counting = await api.JsonAsync(HttpMethod.Get, "/api/counts?status=Counting&pageSize=100");
        var ids = counting.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(b).And.NotContain(a);

        var all = (await api.JsonAsync(HttpMethod.Get, "/api/counts?pageSize=100")).GetProperty("items").EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid()).ToList();
        all.IndexOf(b).Should().BeLessThan(all.IndexOf(a), "newest first");
    }
}
