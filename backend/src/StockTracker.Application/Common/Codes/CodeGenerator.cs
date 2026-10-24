using System.Security.Cryptography;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Common.Codes;

/// <summary>
/// Random Crockford Base32 bodies (~1 billion combinations). Each candidate is checked against the
/// owning table's unique index; the index itself still guards against concurrent inserts.
/// </summary>
public sealed class CodeGenerator(IEnumerable<ICodeUniquenessCheck> checks) : ICodeGenerator
{
    public const int MaxAttempts = 3;

    private readonly IReadOnlyDictionary<CodePrefix, ICodeUniquenessCheck> _checks =
        checks.ToDictionary(c => c.Prefix);

    public async Task<string> GenerateAsync(CodePrefix prefix, CancellationToken cancellationToken)
    {
        if (!_checks.TryGetValue(prefix, out var check))
        {
            throw new InvalidOperationException($"No uniqueness check is registered for {prefix} codes.");
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = EntityCode.Format(prefix, RandomNumberGenerator.GetString(EntityCode.Alphabet, EntityCode.BodyLength));
            if (!await check.ExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique {prefix} code in {MaxAttempts} attempts.");
    }
}
