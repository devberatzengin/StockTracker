using System.Text.RegularExpressions;
using FluentAssertions;
using SkiaSharp;
using StockTracker.Application.Labels;
using StockTracker.Infrastructure.Labels;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace StockTracker.Api.IntegrationTests.Labels;

public partial class LabelRendererTests
{
    private const string BaseUrl = "https://stok.example.com";

    private static IReadOnlyList<LabelData> Labels(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new LabelData($"ITM-{i:D6}", $"Laptop {i} çok uzun bir ürün adı ğüşiöç", $"SN-{i:D3}"))];

    /// <summary>Page objects of the PDF ("/Type /Page", not "/Pages").</summary>
    public static int CountPages(byte[] pdf) =>
        PageObject().Matches(System.Text.Encoding.Latin1.GetString(pdf)).Count;

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageObject();

    [Theory]
    [InlineData(LabelTemplate.Sheet3x8, 30, 1, 2)]
    [InlineData(LabelTemplate.Sheet3x8, 24, 1, 1)]
    [InlineData(LabelTemplate.Sheet3x8, 24, 2, 2)]
    [InlineData(LabelTemplate.Sheet2x4, 4, 5, 1)]
    [InlineData(LabelTemplate.Sheet2x4, 5, 5, 2)]
    [InlineData(LabelTemplate.Sheet5x13, 66, 1, 2)]
    [InlineData(LabelTemplate.Thermal50x30, 3, 1, 3)]
    public void EveryTemplate_RendersTheExpectedPageCount(LabelTemplate template, int count, int start, int pages)
    {
        var pdf = new QuestPdfLabelRenderer().Render(template, Labels(count), start, BaseUrl);

        pdf.AsSpan(0, 5).ToArray().Should().Equal("%PDF-"u8.ToArray());
        CountPages(pdf).Should().Be(pages);
        LabelSheets.PageCount(template, count, start).Should().Be(pages);
    }

    [Fact]
    public void Layouts_FitOnTheirPaper()
    {
        foreach (var (template, l) in QuestPdfLabelRenderer.Layouts)
        {
            (l.MarginLeft + (l.Columns * l.CellWidth)).Should().BeLessThanOrEqualTo(l.PageWidth, template.ToString());
            (l.MarginTop + (l.Rows * l.CellHeight)).Should().BeLessThanOrEqualTo(l.PageHeight, template.ToString());
            (l.Columns * l.Rows).Should().Be(LabelSheets.CellsPerPage(template));
        }

        QuestPdfLabelRenderer.Layouts[LabelTemplate.Sheet3x8].Should().Match<QuestPdfLabelRenderer.Layout>(l => l.CellWidth == 70 && l.CellHeight == 37 && l.QrSize == 28);
        QuestPdfLabelRenderer.Layouts[LabelTemplate.Sheet2x4].Should().Match<QuestPdfLabelRenderer.Layout>(l => l.CellWidth == 105 && l.CellHeight == 74 && l.QrSize == 50);
        QuestPdfLabelRenderer.Layouts[LabelTemplate.Sheet5x13].Should().Match<QuestPdfLabelRenderer.Layout>(l => l.CellWidth == 38 && l.CellHeight == 21 && l.QrSize == 17);
        QuestPdfLabelRenderer.Layouts[LabelTemplate.Thermal50x30].Should().Match<QuestPdfLabelRenderer.Layout>(l => l.PageWidth == 50 && l.PageHeight == 30 && l.QrSize == 24);
    }

    [Fact]
    public void QrCode_OpensTheShortLink_WithLevelMAndQuietZone()
    {
        var content = QrCodes.Content(BaseUrl + "/", "ITM-7F3K9Q");
        content.Should().Be("https://stok.example.com/s/ITM-7F3K9Q");

        var png = QrCodes.Png(content);
        using var bitmap = SKBitmap.Decode(png);
        var luminance = new RGBLuminanceSource(
            bitmap.Pixels.SelectMany(p => new[] { p.Red, p.Green, p.Blue }).ToArray(),
            bitmap.Width,
            bitmap.Height,
            RGBLuminanceSource.BitmapFormat.RGB24);
        var result = new MultiFormatReader().decode(
            new BinaryBitmap(new HybridBinarizer(luminance)),
            new Dictionary<DecodeHintType, object> { [DecodeHintType.POSSIBLE_FORMATS] = new[] { BarcodeFormat.QR_CODE } });

        result.Text.Should().Be(content);
        result.ResultMetadata[ResultMetadataType.ERROR_CORRECTION_LEVEL].Should().Be(ErrorCorrectionLevel.M.ToString());

        // 4 white modules (10 px each) around the code.
        Enumerable.Range(0, 40).Should().OnlyContain(i => bitmap.GetPixel(i, i) == SKColors.White);
    }
}
