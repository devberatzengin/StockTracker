using FluentAssertions;
using SkiaSharp;
using StockTracker.Application.Catalog.Products;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Storage;

namespace StockTracker.Api.IntegrationTests.Storage;

public class StorageUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, true)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, false)] // %PDF
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, false)] // GIF8
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x41, 0x56, 0x49, 0x20 }, false)] // RIFF AVI
    public void ImageSignature_AcceptsOnlyJpegPngWebP(byte[] header, bool expected) =>
        ImageSignature.IsSupported(header).Should().Be(expected);

    [Fact]
    public void UrlSigner_RejectsTamperingAndExpiry()
    {
        var signer = new UrlSigner("a-signing-key-that-is-long-enough-1234");
        var exp = Now.AddHours(1).ToUnixTimeSeconds();
        var sig = signer.Sign("products/a/b.webp", exp);

        signer.IsValid("products/a/b.webp", exp, sig, Now).Should().BeTrue();
        signer.IsValid("products/a/c.webp", exp, sig, Now).Should().BeFalse("another key");
        signer.IsValid("products/a/b.webp", exp + 1, sig, Now).Should().BeFalse("another expiry");
        signer.IsValid("products/a/b.webp", exp, sig, Now.AddHours(2)).Should().BeFalse("expired");
        signer.IsValid("products/a/b.webp", exp, "not-base64!!", Now).Should().BeFalse();
        new UrlSigner("another-signing-key-that-is-long-enough").IsValid("products/a/b.webp", exp, sig, Now).Should().BeFalse();
    }

    [Theory]
    [InlineData("products/a/b.webp", true)]
    [InlineData("../etc/passwd", false)]
    [InlineData("products/../../x", false)]
    [InlineData("/products/a.webp", false)]
    [InlineData("Products/a.webp", false)]
    [InlineData("", false)]
    public void FileKeys_AllowOnlyGeneratedKeys(string key, bool expected) => FileKeys.IsValid(key).Should().Be(expected);

    [Fact]
    public async Task LocalStorage_SignedUrlWorksUntilItExpires()
    {
        var root = Path.Combine(Path.GetTempPath(), $"st-local-{Guid.NewGuid():N}");
        var clock = new FixedClock(Now);
        var storage = new LocalFileStorage(
            Microsoft.Extensions.Options.Options.Create(new StorageOptions { LocalPath = root, SigningKey = "a-signing-key-that-is-long-enough-1234" }),
            clock);
        try
        {
            await storage.SaveAsync("products/p/1.webp", new MemoryStream([1, 2, 3]), "image/webp", default);
            var url = new Uri("http://x" + storage.GetReadUrl("products/p/1.webp", TimeSpan.FromHours(1)));
            var query = System.Web.HttpUtility.ParseQueryString(url.Query);
            url.AbsolutePath.Should().Be("/api/files/products/p/1.webp");

            var file = await storage.OpenSignedAsync("products/p/1.webp", long.Parse(query["exp"]!), query["sig"], default);
            file!.ContentType.Should().Be("image/webp");
            file.MaxAge.Should().Be(TimeSpan.FromHours(1));
            await file.Content.DisposeAsync();

            clock.Now = Now.AddHours(1).AddSeconds(1);
            var expired = () => storage.OpenSignedAsync("products/p/1.webp", long.Parse(query["exp"]!), query["sig"], default);
            await expired.Should().ThrowAsync<ForbiddenException>();

            await storage.DeleteAsync("products/p/1.webp", default);
            File.Exists(Path.Combine(root, "products/p/1.webp")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void R2Storage_PresignsWithoutNetwork()
    {
        using var storage = new R2FileStorage(
            Microsoft.Extensions.Options.Options.Create(new StorageOptions
            {
                Provider = StorageProvider.R2,
                R2 = new R2Options { AccountId = "acc123", AccessKeyId = "AKIATEST", SecretAccessKey = "secret", Bucket = "photos" },
            }),
            TimeProvider.System);

        var url = storage.GetReadUrl("products/p/1.webp", TimeSpan.FromHours(1));

        url.Should().StartWith("https://acc123.r2.cloudflarestorage.com/photos/products/p/1.webp?");
        url.Should().Contain("X-Amz-Expires=3600").And.Contain("X-Amz-Signature=");
    }

    [Fact]
    public void Options_RequireKeysForTheSelectedProvider()
    {
        new StorageOptions { SigningKey = "short" }.Validate().Should().NotBeEmpty();
        new StorageOptions { SigningKey = new string('k', 32) }.Validate().Should().BeEmpty();
        new StorageOptions { Provider = StorageProvider.R2 }.Validate().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ImageProcessor_OrientsStripsAndResizes()
    {
        var processor = new SkiaImageProcessor();

        var result = await processor.ProcessAsync(new MemoryStream(TestImages.RotatedJpeg()), default);

        result.ContentType.Should().Be("image/webp");
        TestImages.Describe(result.Main).Should().Be((800, 1600, "Webp"), "the EXIF rotation is applied before resizing");
        TestImages.Describe(result.Thumbnail).Should().Be((200, 400, "Webp"));
        using var codec = SKCodec.Create(SKData.CreateCopy(result.Main));
        codec.EncodedOrigin.Should().Be(SKEncodedOrigin.TopLeft, "no orientation (or any EXIF) is written");
    }

    [Fact]
    public void Orient_RotatesClockwise()
    {
        // 4x2 image whose top-left pixel is blue; after a 90° clockwise turn it is the top-right pixel of a 2x4 image.
        using var source = new SKBitmap(4, 2);
        source.Erase(SKColors.Red);
        source.SetPixel(0, 0, SKColors.Blue);

        using var turned = SkiaImageProcessor.Orient(source, SKEncodedOrigin.RightTop);

        (turned.Width, turned.Height).Should().Be((2, 4));
        turned.GetPixel(1, 0).Should().Be(SKColors.Blue);
        turned.GetPixel(0, 0).Should().Be(SKColors.Red);
    }

    [Fact]
    public async Task ImageProcessor_DoesNotEnlargeSmallImages()
    {
        var result = await new SkiaImageProcessor().ProcessAsync(new MemoryStream(TestImages.SmallPng()), default);

        TestImages.Describe(result.Main).Should().Be((120, 80, "Webp"));
    }

    [Fact]
    public async Task ImageProcessor_RejectsCorruptData()
    {
        var corrupt = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5 };

        var act = () => new SkiaImageProcessor().ProcessAsync(new MemoryStream(corrupt), default);

        await act.Should().ThrowAsync<InvalidImageException>();
    }
}
