using System.Net;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Catalog;

[Collection(ApiCollection.Name)]
public class CatalogEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    // Unique per test run so tests do not collide in the shared database.
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..Math.Min(100, name.Length + 33)];

    private async Task<string> AdminTokenAsync() => (await LoginAsync(_client)).AccessToken;

    private async Task<Guid> CreateCategoryAsync(string token, string? name = null)
    {
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/categories", token, new
        {
            name = name ?? Unique("Kategori"),
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CreateProductRawAsync(string token, object body) =>
        _client.SendAsync(Authorized(HttpMethod.Post, "/api/products", token, body));

    private static object Laptop(Guid categoryId, string barcode) => new
    {
        name = "Laptop",
        description = "14 inç iş bilgisayarı",
        categoryId,
        unit = "Piece",
        trackingType = "Serialized",
        isAssignable = true,
        minStockLevel = 2,
        barcodes = new[] { new { value = barcode, format = "Ean13" } },
    };

    private static object Screw(Guid categoryId, bool isAssignable = false) => new
    {
        name = "Vida M4",
        categoryId,
        unit = "Piece",
        trackingType = "Quantity",
        isAssignable,
        minStockLevel = 100,
    };

    /// <summary>Valid EAN-13 built from a random 12-digit prefix so tests never share a barcode.</summary>
    private static string NewEan13()
    {
        var prefix = string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(10)));
        for (var check = 0; check < 10; check++)
        {
            if (BarcodeRules.IsCheckDigitValid(prefix + check))
            {
                return prefix + check;
            }
        }

        throw new InvalidOperationException();
    }

    [Fact]
    public async Task AcceptanceScenario_LaptopAndScrew_AreCreated()
    {
        var token = await AdminTokenAsync();
        var categoryId = await CreateCategoryAsync(token);
        var barcode = NewEan13();

        var laptop = await CreateProductRawAsync(token, Laptop(categoryId, barcode));
        var screw = await CreateProductRawAsync(token, Screw(categoryId));

        laptop.StatusCode.Should().Be(HttpStatusCode.Created);
        screw.StatusCode.Should().Be(HttpStatusCode.Created);

        var laptopId = (await ReadJsonAsync(laptop)).GetProperty("id").GetGuid();
        var detail = await ReadJsonAsync(await _client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/products/{laptopId}", token)));

        detail.GetProperty("code").GetString().Should().MatchRegex("^PRD-[0-9A-HJKMNP-TV-Z]{6}$");
        detail.GetProperty("trackingType").GetString().Should().Be("Serialized");
        detail.GetProperty("unit").GetString().Should().Be("Piece");
        detail.GetProperty("isAssignable").GetBoolean().Should().BeTrue();
        detail.GetProperty("trackingTypeLocked").GetBoolean().Should().BeFalse();
        detail.GetProperty("barcodes")[0].GetProperty("value").GetString().Should().Be(barcode);
        detail.GetProperty("barcodes")[0].GetProperty("format").GetString().Should().Be("Ean13");
    }

    [Fact]
    public async Task QuantityProduct_CannotBeAssignable()
    {
        var token = await AdminTokenAsync();
        var categoryId = await CreateCategoryAsync(token);

        var created = await CreateProductRawAsync(token, Screw(categoryId, isAssignable: true));
        await ShouldBeDomainErrorAsync(created, CatalogErrors.ProductAssignableRequiresSerialized);

        var screw = await CreateProductRawAsync(token, Screw(categoryId));
        var screwId = (await ReadJsonAsync(screw)).GetProperty("id").GetGuid();
        var update = await _client.SendAsync(Authorized(HttpMethod.Put, $"/api/products/{screwId}", token, Screw(categoryId, isAssignable: true)));
        await ShouldBeDomainErrorAsync(update, CatalogErrors.ProductAssignableRequiresSerialized);
    }

    [Fact]
    public async Task SameBarcode_OnTwoProducts_IsRejected()
    {
        var token = await AdminTokenAsync();
        var categoryId = await CreateCategoryAsync(token);
        var barcode = NewEan13();
        (await CreateProductRawAsync(token, Laptop(categoryId, barcode))).StatusCode.Should().Be(HttpStatusCode.Created);

        // On create...
        var second = await CreateProductRawAsync(token, Laptop(categoryId, barcode));
        await ShouldBeDomainErrorAsync(second, CatalogErrors.BarcodeDuplicate);

        // ...and when adding to an existing product (spaces are ignored).
        var screw = await CreateProductRawAsync(token, Screw(categoryId));
        var screwId = (await ReadJsonAsync(screw)).GetProperty("id").GetGuid();
        var spaced = $"{barcode[..6]} {barcode[6..]}";
        var add = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{screwId}/barcodes", token, new
        {
            value = spaced,
            format = "Ean13",
        }));
        await ShouldBeDomainErrorAsync(add, CatalogErrors.BarcodeDuplicate);
    }

    [Fact]
    public async Task Barcodes_CanBeAddedAndRemovedOnExistingProduct()
    {
        var token = await AdminTokenAsync();
        var categoryId = await CreateCategoryAsync(token);
        var screwId = (await ReadJsonAsync(await CreateProductRawAsync(token, Screw(categoryId)))).GetProperty("id").GetGuid();

        var invalid = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{screwId}/barcodes", token, new
        {
            value = "4006381333932",
            format = "Ean13",
        }));
        await ShouldBeDomainErrorAsync(invalid, CatalogErrors.BarcodeInvalidCheckDigit);

        var add = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{screwId}/barcodes", token, new
        {
            value = "VIDA-M4-100",
            format = "Code128",
        }));
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());
        var barcodeId = (await ReadJsonAsync(add)).GetProperty("id").GetGuid();

        var remove = await _client.SendAsync(Authorized(HttpMethod.Delete, $"/api/products/{screwId}/barcodes/{barcodeId}", token));
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/products/{screwId}", token)));
        detail.GetProperty("barcodes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task InactiveCategory_RejectsNewProducts_AndCategoryWithActiveProductsCannotBeDeactivated()
    {
        var token = await AdminTokenAsync();
        var usedCategory = await CreateCategoryAsync(token);
        var emptyCategory = await CreateCategoryAsync(token);
        var screwId = (await ReadJsonAsync(await CreateProductRawAsync(token, Screw(usedCategory)))).GetProperty("id").GetGuid();

        var deactivateUsed = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/categories/{usedCategory}/deactivate", token));
        await ShouldBeDomainErrorAsync(deactivateUsed, CatalogErrors.CategoryHasActiveProducts);

        var deactivateEmpty = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/categories/{emptyCategory}/deactivate", token));
        deactivateEmpty.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var created = await CreateProductRawAsync(token, Screw(emptyCategory));
        await ShouldBeDomainErrorAsync(created, CatalogErrors.CategoryInactive);

        // Once its product is deactivated, the category can be deactivated too.
        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{screwId}/deactivate", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/categories/{usedCategory}/deactivate", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CategoryNames_AreUniqueAmongActiveCategories_CaseInsensitive()
    {
        var token = await AdminTokenAsync();
        var name = Unique("Sarf");
        await CreateCategoryAsync(token, name);

        var duplicate = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/categories", token, new { name = name.ToUpperInvariant() }));

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(duplicate)).GetProperty("errors").TryGetProperty("Name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProductList_SearchesByNameCodeOrBarcode_AndFilters()
    {
        var token = await AdminTokenAsync();
        var categoryId = await CreateCategoryAsync(token);
        var barcode = NewEan13();
        var laptopId = (await ReadJsonAsync(await CreateProductRawAsync(token, Laptop(categoryId, barcode)))).GetProperty("id").GetGuid();
        var screwId = (await ReadJsonAsync(await CreateProductRawAsync(token, Screw(categoryId)))).GetProperty("id").GetGuid();
        var code = (await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/products/{laptopId}", token))))
            .GetProperty("code").GetString()!;

        async Task<List<Guid>> SearchAsync(string query)
        {
            var response = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/products?{query}", token));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var body = await ReadJsonAsync(response);
            body.GetProperty("pageSize").GetInt32().Should().Be(25);
            return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        }

        (await SearchAsync($"categoryId={categoryId}")).Should().BeEquivalentTo([laptopId, screwId]);
        (await SearchAsync($"categoryId={categoryId}&trackingType=Quantity")).Should().Equal(screwId);
        (await SearchAsync($"search={barcode}")).Should().Equal(laptopId);
        (await SearchAsync($"search={code.ToLowerInvariant()}")).Should().Equal(laptopId);

        (await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/products/{screwId}/deactivate", token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SearchAsync($"categoryId={categoryId}")).Should().Equal(laptopId);
        (await SearchAsync($"categoryId={categoryId}&includeInactive=true")).Should().HaveCount(2);
    }

    [Fact]
    public async Task Staff_CanViewButNotManageCatalog()
    {
        var admin = await AdminTokenAsync();
        var email = UniqueEmail();
        await CreateUserAsync(_client, admin, Roles.Staff, email: email);
        var staff = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;

        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/products", staff))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/categories", staff))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.SendAsync(Authorized(HttpMethod.Post, "/api/categories", staff, new { name = Unique("X") })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateProductRawAsync(staff, Screw(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be(code);
    }
}
