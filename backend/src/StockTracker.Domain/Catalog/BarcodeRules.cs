namespace StockTracker.Domain.Catalog;

/// <summary>GS1 length and check digit rules for EAN/UPC barcodes.</summary>
public static class BarcodeRules
{
    public static int? RequiredLength(BarcodeFormat format) => format switch
    {
        BarcodeFormat.Ean13 => 13,
        BarcodeFormat.Ean8 => 8,
        BarcodeFormat.UpcA => 12,
        _ => null,
    };

    public static bool HasCheckDigit(BarcodeFormat format) => RequiredLength(format) is not null;

    /// <summary>
    /// GS1 mod-10: from the right (excluding the check digit), digits are weighted 3,1,3,1...
    /// </summary>
    public static bool IsCheckDigitValid(string digits)
    {
        if (digits.Length < 2 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        var position = 0;
        for (var i = digits.Length - 2; i >= 0; i--, position++)
        {
            var digit = digits[i] - '0';
            sum += position % 2 == 0 ? digit * 3 : digit;
        }

        var expected = (10 - (sum % 10)) % 10;
        return digits[^1] - '0' == expected;
    }
}
