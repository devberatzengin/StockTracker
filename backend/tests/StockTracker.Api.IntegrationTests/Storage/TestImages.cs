using SkiaSharp;

namespace StockTracker.Api.IntegrationTests.Storage;

/// <summary>Generated test images (no binary fixtures in the repository).</summary>
public static class TestImages
{
    /// <summary>
    /// A landscape JPEG with an EXIF block saying "rotate 90° clockwise" (orientation 6), as phone cameras write.
    /// Skia does not write EXIF, so the APP1 segment is inserted by hand after the SOI marker.
    /// </summary>
    public static byte[] RotatedJpeg(int width = 2000, int height = 1000)
    {
        var jpeg = Encode(width, height, SKEncodedImageFormat.Jpeg);
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, // APP1, length 34
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00, // "Exif\0\0"
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // TIFF header, little endian, IFD at 8
            0x01, 0x00, // 1 entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, // Orientation (SHORT) = 6
            0x00, 0x00, 0x00, 0x00, // no next IFD
        ];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    public static byte[] SmallPng() => Encode(120, 80, SKEncodedImageFormat.Png);

    public static (int Width, int Height, string Format) Describe(byte[] image)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(image));
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat.ToString());
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(200, 30, 30));
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 4f, height / 4f, paint); // marks the top-left corner
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
