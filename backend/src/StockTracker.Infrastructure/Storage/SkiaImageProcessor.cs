using SkiaSharp;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Infrastructure.Storage;

/// <summary>
/// Applies the EXIF orientation and produces WebP main (1600 px) and thumbnail (400 px) images.
/// Re-encoding drops all metadata (location, device, …): Skia writes no EXIF.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int MainSize = 1600;
    public const int ThumbnailSize = 400;

    // Guards against decompression bombs: a small file can declare a huge canvas.
    public const long MaxPixels = 40_000_000;

    private const string WebPContentType = "image/webp";
    private const string Unreadable = "Fotoğraf okunamadı; dosya bozuk ya da desteklenmeyen biçimde.";

    public Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken cancellationToken)
    {
        using var data = SKData.Create(input);
        using var codec = data is null ? null : SKCodec.Create(data);
        if (codec is null)
        {
            throw new InvalidImageException(Unreadable);
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new InvalidImageException("Fotoğraf çözünürlüğü çok yüksek (en fazla 40 megapiksel).");
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidImageException(Unreadable);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var oriented = Orient(decoded, codec.EncodedOrigin);

        return Task.FromResult(new ProcessedImage(
            Encode(oriented, MainSize, 82),
            Encode(oriented, ThumbnailSize, 75),
            WebPContentType));
    }

    /// <summary>Turns the pixels so the image is upright (EXIF orientation 1).</summary>
    public static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        int w = source.Width, h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var target = new SKBitmap(swap ? h : w, swap ? w : h, source.ColorType, source.AlphaType);

        using var canvas = new SKCanvas(target);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirror horizontally
                canvas.Translate(w, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // 180°
                canvas.Translate(w, h);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // mirror vertically
                canvas.Translate(0, h);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // transpose
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // 90° clockwise
                canvas.Translate(h, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // transverse
                canvas.Translate(h, w);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // 90° counter-clockwise
                canvas.Translate(0, w);
                canvas.RotateDegrees(270);
                break;
        }

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return target;
    }

    private static byte[] Encode(SKBitmap image, int maxSide, int quality)
    {
        // Only shrink: a small photo keeps its size.
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(image.Width, image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));

        using var resized = scale < 1
            ? image.Resize(new SKImageInfo(width, height, image.ColorType, image.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell))
            : image.Copy();
        using var encoded = SKImage.FromBitmap(resized).Encode(SKEncodedImageFormat.Webp, quality);
        return encoded.ToArray();
    }
}
