using StockTracker.Application.Common.Exceptions;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Scan;

/// <summary>
/// 404 for a scan. When the input is a valid EAN/UPC, the client may offer to create a product
/// with that barcode.
/// </summary>
public sealed class ScanNotFoundException : NotFoundException
{
    public ScanNotFoundException(string input, string? suggestedBarcode, BarcodeFormat? suggestedFormat)
        : base("Kayıt", input)
    {
        SuggestedBarcode = suggestedBarcode;
        SuggestedFormat = suggestedFormat;
    }

    public bool SuggestCreateProduct => SuggestedBarcode is not null;

    public string? SuggestedBarcode { get; }

    public BarcodeFormat? SuggestedFormat { get; }
}
