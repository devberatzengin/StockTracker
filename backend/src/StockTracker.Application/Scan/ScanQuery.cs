using FluentValidation;
using MediatR;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Scan;

/// <summary>Resolves scanned or typed input (URL, entity code or barcode) to a record.</summary>
public sealed record ScanQuery(string Input) : IRequest<ScanResultDto>;

public sealed class ScanQueryValidator : AbstractValidator<ScanQuery>
{
    public ScanQueryValidator()
    {
        RuleFor(x => x.Input)
            .NotEmpty().WithMessage("Taranacak değer boş olamaz.")
            .MaximumLength(512).WithMessage("Taranan değer çok uzun.");
    }
}

public sealed class ScanQueryHandler(
    IEnumerable<IScanCodeResolver> codeResolvers,
    ProductBarcodeScanResolver barcodeResolver) : IRequestHandler<ScanQuery, ScanResultDto>
{
    private readonly IReadOnlyDictionary<CodePrefix, IScanCodeResolver> _codeResolvers =
        codeResolvers.ToDictionary(r => r.Prefix);

    public async Task<ScanResultDto> Handle(ScanQuery request, CancellationToken cancellationToken)
    {
        // 1. A URL contributes only its /s/{code} part.
        var input = ScanInput.ExtractFromUrl(request.Input).Trim();

        // 2–3. Prefixed entity codes are normalized and looked up in their own table.
        if (EntityCode.TryParsePrefixed(input, out var prefix, out var code, out var isWellFormed))
        {
            var result = isWellFormed && _codeResolvers.TryGetValue(prefix, out var resolver)
                ? await resolver.ResolveAsync(code, cancellationToken)
                : null;

            return result ?? throw new ScanNotFoundException(code, suggestedBarcode: null, suggestedFormat: null);
        }

        // 4. Anything else is treated as a product barcode.
        var barcode = ProductBarcode.Normalize(input);
        var product = await barcodeResolver.ResolveAsync(barcode, cancellationToken);

        // 5. Not found: offer to create a product when it is a valid retail barcode.
        if (product is not null)
        {
            return product;
        }

        var retailFormat = ScanInput.RetailFormat(barcode);
        throw new ScanNotFoundException(barcode, retailFormat is null ? null : barcode, retailFormat);
    }
}

public static class ScanInput
{
    private const string ShortLinkSegment = "/s/";

    /// <summary>Returns the {code} of ".../s/{code}" (QR label URLs); other input is returned unchanged.</summary>
    public static string ExtractFromUrl(string input)
    {
        var text = Uri.UnescapeDataString(input ?? string.Empty).Trim();
        var index = text.LastIndexOf(ShortLinkSegment, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return text;
        }

        var code = text[(index + ShortLinkSegment.Length)..];
        var end = code.IndexOfAny(['/', '?', '#']);
        return end < 0 ? code : code[..end];
    }

    /// <summary>EAN-13, EAN-8 or UPC-A with a valid check digit; null otherwise.</summary>
    public static BarcodeFormat? RetailFormat(string barcode)
    {
        if (!barcode.All(char.IsAsciiDigit) || !BarcodeRules.IsCheckDigitValid(barcode))
        {
            return null;
        }

        return barcode.Length switch
        {
            13 => BarcodeFormat.Ean13,
            8 => BarcodeFormat.Ean8,
            12 => BarcodeFormat.UpcA,
            _ => null,
        };
    }
}
