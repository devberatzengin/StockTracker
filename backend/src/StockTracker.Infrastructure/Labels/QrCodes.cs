using QRCoder;

namespace StockTracker.Infrastructure.Labels;

/// <summary>QR images for labels: error correction M, 4-module quiet zone, content "{baseUrl}/s/{code}".</summary>
public static class QrCodes
{
    // 10 px per module keeps edges sharp at 28 mm on 300 dpi printers.
    private const int PixelsPerModule = 10;

    public static string Content(string baseUrl, string code) => $"{baseUrl.TrimEnd('/')}/s/{code}";

    public static byte[] Png(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule, drawQuietZones: true);
    }
}
