using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Catalog;

/// <summary>Owned by <see cref="Product"/>. Values are unique across the whole system.</summary>
public class ProductBarcode : BaseEntity
{
    public const int ValueMaxLength = 64;

    private ProductBarcode()
    {
    }

    public Guid ProductId { get; private set; }

    public string Value { get; private set; } = string.Empty;

    public BarcodeFormat Format { get; private set; }

    /// <summary>Removes all whitespace (scanners and manual entry often add spaces).</summary>
    public static string Normalize(string value) =>
        string.Concat((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));

    internal static ProductBarcode Create(Guid productId, string value, BarcodeFormat format)
    {
        var normalized = Normalize(value);

        if (normalized.Length == 0)
        {
            throw new DomainException(CatalogErrors.BarcodeRequired, "Barkod değeri zorunludur.");
        }

        if (normalized.Length > ValueMaxLength)
        {
            throw new DomainException(
                CatalogErrors.BarcodeTooLong,
                $"Barkod en fazla {ValueMaxLength} karakter olabilir.");
        }

        if (BarcodeRules.RequiredLength(format) is { } length)
        {
            if (normalized.Length != length || !normalized.All(char.IsAsciiDigit))
            {
                throw new DomainException(
                    CatalogErrors.BarcodeInvalidFormat,
                    $"{format} barkodu {length} rakamdan oluşmalıdır.");
            }

            if (!BarcodeRules.IsCheckDigitValid(normalized))
            {
                throw new DomainException(CatalogErrors.BarcodeInvalidCheckDigit, "Barkodun kontrol hanesi hatalı.");
            }
        }

        return new ProductBarcode { ProductId = productId, Value = normalized, Format = format };
    }
}
