using System.Security.Cryptography;

namespace StockTracker.Infrastructure.Common;

/// <summary>
/// Prefix followed by 6 random Crockford Base32 characters, retried on collision.
/// Interim helper until the shared code generator for products, items and locations exists.
/// </summary>
internal static class TemporaryCodes
{
    // Crockford Base32: no I, L, O, U to avoid confusion with 1 and 0.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int MaxAttempts = 5;

    public static async Task<string> GenerateAsync(
        string prefix,
        Func<string, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = $"{prefix}-{RandomNumberGenerator.GetString(Alphabet, 6)}";
            if (!await exists(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique {prefix} code in {MaxAttempts} attempts.");
    }
}
