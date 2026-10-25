using System.Security.Cryptography;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Common.Codes;

/// <summary>
/// Random Crockford Base32 bodies (~1 billion combinations). Candidates are checked against the
/// owning table's unique index in batches; colliding ones are regenerated, at most 3 rounds.
/// The index itself still guards against concurrent inserts.
/// </summary>
public sealed class CodeGenerator(IEnumerable<ICodeUniquenessCheck> checks) : ICodeGenerator
{
    public const int MaxAttempts = 3;

    private readonly IReadOnlyDictionary<CodePrefix, ICodeUniquenessCheck> _checks =
        checks.ToDictionary(c => c.Prefix);

    public async Task<string> GenerateAsync(CodePrefix prefix, CancellationToken cancellationToken) =>
        (await GenerateManyAsync(prefix, 1, cancellationToken))[0];

    public async Task<IReadOnlyList<string>> GenerateManyAsync(
        CodePrefix prefix,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (!_checks.TryGetValue(prefix, out var check))
        {
            throw new InvalidOperationException($"No uniqueness check is registered for {prefix} codes.");
        }

        var accepted = new HashSet<string>();
        for (var attempt = 0; attempt < MaxAttempts && accepted.Count < count; attempt++)
        {
            var candidates = new HashSet<string>();
            while (candidates.Count < count - accepted.Count)
            {
                var code = EntityCode.Format(prefix, RandomNumberGenerator.GetString(EntityCode.Alphabet, EntityCode.BodyLength));
                if (!accepted.Contains(code))
                {
                    candidates.Add(code);
                }
            }

            var existing = await check.ExistingAsync(candidates, cancellationToken);
            accepted.UnionWith(candidates.Where(c => !existing.Contains(c)));
        }

        if (accepted.Count < count)
        {
            throw new InvalidOperationException($"Could not generate {count} unique {prefix} codes in {MaxAttempts} attempts.");
        }

        return [.. accepted];
    }
}
