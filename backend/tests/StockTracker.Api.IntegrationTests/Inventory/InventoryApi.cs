using System.Net;
using System.Text.Json;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Inventory;

/// <summary>Builds a small catalog and location tree per test and wraps the stock endpoints.</summary>
public sealed class InventoryApi(HttpClient client, string token)
{
    public string Token => token;

    public static async Task<InventoryApi> CreateAsync(ApiFactory factory)
    {
        var client = factory.CreateApiClient();
        return new InventoryApi(client, (await LoginAsync(client)).AccessToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null) =>
        client.SendAsync(Authorized(method, url, token, body));

    /// <summary>Sends with an <c>Idempotency-Key</c> header.</summary>
    public Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, string key, object? body = null)
    {
        var request = Authorized(method, url, token, body);
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    public async Task<JsonElement> JsonAsync(HttpMethod method, string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await SendAsync(method, url, body);
        response.StatusCode.Should().Be(expected, $"{method} {url}: {await response.Content.ReadAsStringAsync()}");
        return response.StatusCode == HttpStatusCode.NoContent ? default : await ReadJsonAsync(response);
    }

    private async Task<Guid> CreatedIdAsync(string url, object body) =>
        (await JsonAsync(HttpMethod.Post, url, body, HttpStatusCode.Created)).GetProperty("id").GetGuid();

    public Task<Guid> CategoryAsync() => CreatedIdAsync("/api/categories", new { name = $"Stok {Guid.NewGuid():N}"[..20] });

    public Task<Guid> SerializedProductAsync(Guid categoryId, string name = "Laptop") => CreatedIdAsync("/api/products", new
    {
        name,
        categoryId,
        unit = "Piece",
        trackingType = "Serialized",
        isAssignable = true,
    });

    public Task<Guid> QuantityProductAsync(Guid categoryId, string name = "Vida M4") => CreatedIdAsync("/api/products", new
    {
        name,
        categoryId,
        unit = "Piece",
        trackingType = "Quantity",
        isAssignable = false,
        minStockLevel = 100,
    });

    public Task<Guid> LocationAsync(string name, Guid? parentId = null, string type = "Shelf") =>
        CreatedIdAsync("/api/locations", new { name = parentId is null ? $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)] : name, type, parentId });

    public Task<HttpResponseMessage> InAsync(Guid toLocationId, params object[] lines) =>
        SendAsync(HttpMethod.Post, "/api/movements/in", new { toLocationId, lines });

    public Task<HttpResponseMessage> TransferAsync(Guid toLocationId, params object[] lines) =>
        SendAsync(HttpMethod.Post, "/api/movements/transfer", new { toLocationId, lines });

    public Task<HttpResponseMessage> OutAsync(string reason, string? note, params object[] lines) =>
        SendAsync(HttpMethod.Post, "/api/movements/out", new { reason, note, lines });

    public static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    public async Task<decimal> QuantityAtAsync(Guid productId, Guid locationId)
    {
        var stock = await JsonAsync(HttpMethod.Get, $"/api/products/{productId}/stock");
        return stock.GetProperty("locations").EnumerateArray()
            .Where(l => l.GetProperty("locationId").GetGuid() == locationId)
            .Select(l => l.GetProperty("quantity").GetDecimal())
            .SingleOrDefault();
    }

    public async Task<int> MovementCountAsync(Guid productId) =>
        (await JsonAsync(HttpMethod.Get, $"/api/movements?productId={productId}&pageSize=100")).GetProperty("totalCount").GetInt32();

    public async Task<JsonElement> ItemAsync(Guid itemId) => await JsonAsync(HttpMethod.Get, $"/api/items/{itemId}");

    public static object Item(Guid itemId) => new { itemId };

    public static object Quantity(Guid productId, Guid fromLocationId, decimal quantity) => new { productId, fromLocationId, quantity };

    public static async Task ShouldBeLineErrorAsync(HttpResponseMessage response, HttpStatusCode status, string key, string? code = null)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(status, body);
        var problem = JsonDocument.Parse(body).RootElement;
        problem.GetProperty("errors").TryGetProperty(key, out _).Should().BeTrue($"expected error key '{key}' in {body}");
        if (code is not null)
        {
            problem.GetProperty("code").GetString().Should().Be(code);
        }
    }
}
