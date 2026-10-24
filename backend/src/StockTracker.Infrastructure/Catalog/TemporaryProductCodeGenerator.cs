using System.Security.Cryptography;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Infrastructure.Catalog;

/// <summary>
/// PRD- followed by 6 random Crockford Base32 characters, retried on collision.
/// Interim implementation until the shared code generator for products, items and locations exists.
/// </summary>
public sealed class TemporaryProductCodeGenerator(IProductRepository products) : IProductCodeGenerator
{
    // Crockford Base32: no I, L, O, U to avoid confusion with 1 and 0.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int MaxAttempts = 5;

    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = $"PRD-{RandomNumberGenerator.GetString(Alphabet, 6)}";
            if (!await products.CodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique product code in {MaxAttempts} attempts.");
    }
}
