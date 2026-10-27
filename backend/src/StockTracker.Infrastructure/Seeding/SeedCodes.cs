using StockTracker.Domain.Common.Codes;

namespace StockTracker.Infrastructure.Seeding;

/// <summary>Unique entity codes for seed data (no database round trips).</summary>
/// <param name="scramble">Spread consecutive codes over the code space so they look random.</param>
internal sealed class SeedCodes(int start = 0, bool scramble = false)
{
    // 32^6 codes; multiplying by an odd number is a bijection modulo a power of two.
    private const long Space = 1L << 30;
    private const long Spread = 387_420_489;

    private int _next = start;

    public string Next(CodePrefix prefix)
    {
        var value = scramble ? (int)(_next++ * Spread % Space) : _next++;
        var body = new char[EntityCode.BodyLength];
        for (var i = body.Length - 1; i >= 0; i--)
        {
            body[i] = EntityCode.Alphabet[value % EntityCode.Alphabet.Length];
            value /= EntityCode.Alphabet.Length;
        }

        return EntityCode.Format(prefix, new string(body));
    }
}
