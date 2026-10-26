using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Api.IntegrationTests.Storage;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Storage;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Photos;

[Collection(ApiCollection.Name)]
public class ProductPhotoEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private Task<HttpResponseMessage> UploadAsync(string token, Guid productId, byte[] content, string fileName = "photo.jpg")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", fileName);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/products/{productId}/photos") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> OkJsonAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private static async Task ShouldBeFileErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(status, body);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty("file", out _).Should().BeTrue(body);
    }

    /// <summary>Signed links need no access token.</summary>
    private Task<HttpResponseMessage> GetAnonymousAsync(string url) => _client.GetAsync(url);

    private async Task<(InventoryApi Api, Guid Product)> ProductAsync()
    {
        var api = await InventoryApi.CreateAsync(factory);
        return (api, await api.SerializedProductAsync(await api.CategoryAsync()));
    }

    [Fact]
    public async Task Upload_StoresWebPAndThumbnail_ReadableThroughSignedLinks()
    {
        var (api, product) = await ProductAsync();

        var photo = await OkJsonAsync(await UploadAsync(api.Token, product, TestImages.RotatedJpeg()));
        photo.GetProperty("isPrimary").GetBoolean().Should().BeTrue();

        var main = await GetAnonymousAsync(photo.GetProperty("url").GetString()!);
        main.StatusCode.Should().Be(HttpStatusCode.OK);
        main.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        main.Headers.CacheControl!.Private.Should().BeTrue();
        TestImages.Describe(await main.Content.ReadAsByteArrayAsync()).Should().Be((800, 1600, "Webp"));

        var thumb = await GetAnonymousAsync(photo.GetProperty("thumbnailUrl").GetString()!);
        TestImages.Describe(await thumb.Content.ReadAsByteArrayAsync()).Should().Be((200, 400, "Webp"));

        // Product detail, list and scan show the photo.
        var detail = await api.JsonAsync(HttpMethod.Get, $"/api/products/{product}");
        detail.GetProperty("photos").GetArrayLength().Should().Be(1);
        var code = detail.GetProperty("code").GetString();
        var listed = (await api.JsonAsync(HttpMethod.Get, $"/api/products?search={code}")).GetProperty("items")[0];
        listed.GetProperty("thumbnailUrl").GetString().Should().StartWith("/api/files/products/");
        (await api.JsonAsync(HttpMethod.Get, $"/api/scan/{code}")).GetProperty("thumbnailUrl").GetString().Should().Contain("_thumb.webp");
    }

    [Fact]
    public async Task FakeOrOversizedFiles_AreRejected()
    {
        var (api, product) = await ProductAsync();

        await ShouldBeFileErrorAsync(await UploadAsync(api.Token, product, "not really a jpeg"u8.ToArray(), "photo.jpg"), HttpStatusCode.BadRequest);
        await ShouldBeFileErrorAsync(await UploadAsync(api.Token, product, "%PDF-1.7 fake"u8.ToArray(), "photo.jpg"), HttpStatusCode.BadRequest);

        var big = new byte[(5 * 1024 * 1024) + 1];
        TestImages.SmallPng().CopyTo(big, 0);
        await ShouldBeFileErrorAsync(await UploadAsync(api.Token, product, big), HttpStatusCode.BadRequest);

        (await api.JsonAsync(HttpMethod.Get, $"/api/products/{product}")).GetProperty("photos").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ExpiredOrTamperedLinks_Return403()
    {
        var (api, product) = await ProductAsync();
        var photo = await OkJsonAsync(await UploadAsync(api.Token, product, TestImages.SmallPng(), "a.png"));
        var url = new Uri(new Uri("https://localhost"), photo.GetProperty("url").GetString());
        var key = url.AbsolutePath["/api/files/".Length..];

        var signer = new UrlSigner(TestSettings.StorageSigningKey);
        var past = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        (await GetAnonymousAsync($"/api/files/{key}?exp={past}&sig={signer.Sign(key, past)}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var query = HttpUtility.ParseQueryString(url.Query);
        (await GetAnonymousAsync($"/api/files/{key}?exp={query["exp"]}&sig=AAAA")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetAnonymousAsync($"/api/files/{key}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gallery_PrimaryOrderDeleteAndLimit()
    {
        var (api, product) = await ProductAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < Product.MaxPhotos; i++)
        {
            ids.Add((await OkJsonAsync(await UploadAsync(api.Token, product, TestImages.SmallPng(), "a.png"))).GetProperty("id").GetGuid());
        }

        var eleventh = await UploadAsync(api.Token, product, TestImages.SmallPng(), "a.png");
        eleventh.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonDocument.Parse(await eleventh.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString().Should().Be(CatalogErrors.ProductPhotoLimit);

        await api.JsonAsync(HttpMethod.Post, $"/api/products/{product}/photos/{ids[2]}/primary", expected: HttpStatusCode.NoContent);
        var reversed = Enumerable.Reverse(ids).ToList();
        await api.JsonAsync(HttpMethod.Put, $"/api/products/{product}/photos/order", new { photoIds = reversed }, HttpStatusCode.NoContent);

        var photos = (await api.JsonAsync(HttpMethod.Get, $"/api/products/{product}")).GetProperty("photos").EnumerateArray().ToList();
        photos.Select(p => p.GetProperty("id").GetGuid()).Should().Equal(reversed);
        photos.Single(p => p.GetProperty("isPrimary").GetBoolean()).GetProperty("id").GetGuid().Should().Be(ids[2]);

        // Deleting the primary photo promotes the next one in order and removes the files.
        var files = Directory.GetFiles(factory.UploadsPath, $"{ids[2]}*", SearchOption.AllDirectories);
        files.Should().HaveCount(2);
        await api.JsonAsync(HttpMethod.Delete, $"/api/products/{product}/photos/{ids[2]}", expected: HttpStatusCode.NoContent);
        files.Should().OnlyContain(f => !File.Exists(f));

        photos = (await api.JsonAsync(HttpMethod.Get, $"/api/products/{product}")).GetProperty("photos").EnumerateArray().ToList();
        photos.Should().HaveCount(9);
        photos.Single(p => p.GetProperty("isPrimary").GetBoolean()).GetProperty("id").GetGuid().Should().Be(reversed.Where(id => id != ids[2]).First());
        photos.Select(p => p.GetProperty("sortOrder").GetInt32()).Should().Equal(Enumerable.Range(0, 9));
    }

    [Fact]
    public async Task OnlyCatalogManagersUpload()
    {
        var (api, product) = await ProductAsync();
        var email = UniqueEmail();
        await CreateUserAsync(_client, api.Token, Roles.Staff, email: email);
        var staff = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;

        (await UploadAsync(staff, product, TestImages.SmallPng(), "a.png")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
