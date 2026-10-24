namespace StockTracker.Domain.Common.Codes;

/// <summary>
/// Format and parsing of entity codes: prefix + "-" + 6 Crockford Base32 characters.
/// Parsing is forgiving about how people type or scanners read codes.
/// </summary>
public static class EntityCode
{
    /// <summary>Crockford Base32: no I, L, O, U to avoid confusion with 1 and 0.</summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int BodyLength = 6;

    /// <summary>"PRD-" + 6 characters.</summary>
    public const int Length = 10;

    private static readonly IReadOnlyDictionary<CodePrefix, string> Prefixes = new Dictionary<CodePrefix, string>
    {
        [CodePrefix.Product] = "PRD",
        [CodePrefix.Item] = "ITM",
        [CodePrefix.Location] = "LOC",
    };

    public static string PrefixOf(CodePrefix prefix) => Prefixes[prefix];

    public static string Format(CodePrefix prefix, string body) => $"{Prefixes[prefix]}-{body}";

    /// <summary>
    /// Recognizes a prefixed code: trims, upper-cases, adds a missing dash and fixes O→0 and I/L→1
    /// in the body. Returns false when the input does not start with a known prefix.
    /// <paramref name="code"/> is the normalized code; <paramref name="isWellFormed"/> tells whether
    /// its body is 6 valid Crockford characters.
    /// </summary>
    public static bool TryParsePrefixed(string input, out CodePrefix prefix, out string code, out bool isWellFormed)
    {
        var text = (input ?? string.Empty).Trim().ToUpperInvariant();

        foreach (var (candidate, value) in Prefixes)
        {
            if (!text.StartsWith(value, StringComparison.Ordinal))
            {
                continue;
            }

            var body = text[value.Length..].TrimStart('-')
                .Replace('O', '0')
                .Replace('I', '1')
                .Replace('L', '1');

            prefix = candidate;
            code = $"{value}-{body}";
            isWellFormed = body.Length == BodyLength && body.All(Alphabet.Contains);
            return true;
        }

        prefix = default;
        code = text;
        isWellFormed = false;
        return false;
    }
}
