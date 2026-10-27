using StockTracker.Domain.Common.Codes;

namespace StockTracker.Infrastructure.Seeding;

/// <summary>Sequential, unique entity codes for seed data (no database round trips).</summary>
internal sealed class SeedCodes(int start = 0)
{
    private int _next = start;

    public string Next(CodePrefix prefix)
    {
        var value = _next++;
        var body = new char[EntityCode.BodyLength];
        for (var i = body.Length - 1; i >= 0; i--)
        {
            body[i] = EntityCode.Alphabet[value % EntityCode.Alphabet.Length];
            value /= EntityCode.Alphabet.Length;
        }

        return EntityCode.Format(prefix, new string(body));
    }
}
