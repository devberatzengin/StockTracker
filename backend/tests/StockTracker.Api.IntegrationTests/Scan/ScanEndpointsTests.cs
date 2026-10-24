using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Catalog;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Scan;

[Collection(ApiCollection.Name)]
public class ScanEndpointsTests(ApiFactory factory)
{
    // Contains 0 and 1, so the O/I/L confusion fixes can be exercised.
    private const string KnownProductCode = "PRD-10A0B1";

    private readonly HttpClient _client = factory.CreateApiClient();

    private async Task<string> TokenAsync() => (await LoginAsync(_client)).AccessToken;

    private async Task<HttpResponseMessage> ScanRawAsync(string token, string input) =>
        await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/scan/{Uri.EscapeDataString(input)}", token));

    private async Task<JsonElement> ScanAsync(string token, string input)
    {
        var response = await ScanRawAsync(token, input);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"input '{input}': {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }

    private async Task<Guid> CreateCategoryAsync(string token)
    {
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/categories", token, new
        {
            name = $"Tarama {Guid.NewGuid():N}"[..20],
        }));
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid Id, string Code)> CreateProductAsync(string token, Guid categoryId, string? barcode = null)
    {
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/products", token, new
        {
            name = "Taranan Ürün",
            categoryId,
            unit = "Piece",
            trackingType = "Quantity",
            isAssignable = false,
            barcodes = barcode is null ? null : new[] { new { value = barcode, format = "Code128" } },
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var id = (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
        var detail = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/products/{id}", token)));
        return (id, detail.GetProperty("code").GetString()!);
    }

    /// <summary>Inserts a product with a fixed code (the generator is random).</summary>
    private async Task<Guid> EnsureKnownProductAsync(string token)
    {
        var categoryId = await CreateCategoryAsync(token);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = db.Products.FirstOrDefault(p => p.Code == KnownProductCode);
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = await db.Categories.FindAsync(categoryId);
        var product = Product.Create(KnownProductCode, "Sabit Kodlu Ürün", null, category!, Unit.Piece, TrackingType.Quantity, false, null);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    [Fact]
    public async Task ProductCode_ResolvesExactlyFromUrlAndWithoutDash()
    {
        var token = await TokenAsync();
        var (id, code) = await CreateProductAsync(token, await CreateCategoryAsync(token));

        var exact = await ScanAsync(token, code);
        exact.GetProperty("type").GetString().Should().Be("product");
        exact.GetProperty("id").GetGuid().Should().Be(id);
        exact.GetProperty("code").GetString().Should().Be(code);
        exact.GetProperty("title").GetString().Should().Be("Taranan Ürün");
        exact.GetProperty("subtitle").GetString().Should().StartWith("Tarama");
        exact.GetProperty("isActive").GetBoolean().Should().BeTrue();

        (await ScanAsync(token, $"https://abc.trycloudflare.com/s/{code}")).GetProperty("id").GetGuid().Should().Be(id);
        (await ScanAsync(token, code.Replace("-", string.Empty).ToLowerInvariant())).GetProperty("id").GetGuid().Should().Be(id);
    }

    [Theory]
    [InlineData("PRD-IOAOBL")]
    [InlineData("prd1oa0bi")]
    [InlineData("  prdLOAOB1 ")]
    public async Task ProductCode_FixesOAndIConfusion(string input)
    {
        var token = await TokenAsync();
        var id = await EnsureKnownProductAsync(token);

        var result = await ScanAsync(token, input);

        result.GetProperty("id").GetGuid().Should().Be(id);
        result.GetProperty("code").GetString().Should().Be(KnownProductCode);
    }

    [Fact]
    public async Task Barcode_ResolvesProduct_IgnoringSpaces()
    {
        var token = await TokenAsync();
        var barcode = $"VIDA-{Guid.NewGuid():N}"[..20];
        var (id, _) = await CreateProductAsync(token, await CreateCategoryAsync(token), barcode);

        var result = await ScanAsync(token, $" {barcode[..8]} {barcode[8..]} ");

        result.GetProperty("type").GetString().Should().Be("product");
        result.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task InactiveProduct_IsReturnedWithInactiveFlag()
    {
        var token = await TokenAsync();
        var (id, code) = await CreateProductAsync(token, await CreateCategoryAsync(token));
        await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{id}/deactivate", token));

        var result = await ScanAsync(token, code);

        result.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task LocationCode_ResolvesWithPathAsSubtitle()
    {
        var token = await TokenAsync();
        var buildingName = $"Tarama Bina {Guid.NewGuid():N}"[..24];
        var building = (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Post, "/api/locations", token, new
        {
            name = buildingName,
            type = "Building",
        })))).GetProperty("id").GetGuid();
        var shelf = (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Post, "/api/locations", token, new
        {
            name = "Raf A1",
            type = "Shelf",
            parentId = building,
        })))).GetProperty("id").GetGuid();
        var code = (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/locations/{shelf}", token))))
            .GetProperty("code").GetString()!;

        var result = await ScanAsync(token, $"https://stok.example.com/s/{code}");

        result.GetProperty("type").GetString().Should().Be("location");
        result.GetProperty("id").GetGuid().Should().Be(shelf);
        result.GetProperty("title").GetString().Should().Be("Raf A1");
        result.GetProperty("subtitle").GetString().Should().Be(buildingName);
    }

    [Fact]
    public async Task UnknownRetailBarcode_Returns404WithCreateSuggestion()
    {
        var token = await TokenAsync();

        var response = await ScanRawAsync(token, "4006381 333931");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("suggestCreateProduct").GetBoolean().Should().BeTrue();
        problem.GetProperty("barcode").GetString().Should().Be("4006381333931");
        problem.GetProperty("barcodeFormat").GetString().Should().Be("Ean13");
    }

    [Theory]
    [InlineData("rastgele metin")]
    [InlineData("4006381333932")]
    [InlineData("ITM-7F3K9Q")]
    [InlineData("PRD-ZZZZZZ")]
    [InlineData("PRD-12")]
    public async Task UnknownInput_Returns404WithoutSuggestion(string input)
    {
        var token = await TokenAsync();

        var response = await ScanRawAsync(token, input);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("suggestCreateProduct").GetBoolean().Should().BeFalse();
        problem.GetProperty("title").GetString().Should().Be("Kayıt bulunamadı.");
    }

    [Fact]
    public async Task Scan_RequiresSignIn()
    {
        (await _client.GetAsync("/api/scan/PRD-ABCDEF")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
