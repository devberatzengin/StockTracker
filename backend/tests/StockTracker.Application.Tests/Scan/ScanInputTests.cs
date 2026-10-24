using FluentAssertions;
using StockTracker.Application.Scan;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Tests.Scan;

public class ScanInputTests
{
    [Theory]
    [InlineData("https://abc.trycloudflare.com/s/ITM-7F3K9Q", "ITM-7F3K9Q")]
    [InlineData("https://stok.example.com/s/LOC-AB12CD?ref=label#x", "LOC-AB12CD")]
    [InlineData("https%3A%2F%2Fhost%2Fs%2FPRD-ABCDEF", "PRD-ABCDEF")]
    [InlineData("  PRD-ABCDEF ", "PRD-ABCDEF")]
    [InlineData("4006381333931", "4006381333931")]
    public void ExtractFromUrl_TakesTheShortLinkCode(string input, string expected)
    {
        ScanInput.ExtractFromUrl(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("4006381333931", BarcodeFormat.Ean13)]
    [InlineData("73513537", BarcodeFormat.Ean8)]
    [InlineData("036000291452", BarcodeFormat.UpcA)]
    [InlineData("4006381333932", null)]
    [InlineData("ABC123", null)]
    [InlineData("12345", null)]
    public void RetailFormat_RecognizesValidEanAndUpc(string barcode, BarcodeFormat? expected)
    {
        ScanInput.RetailFormat(barcode).Should().Be(expected);
    }
}
