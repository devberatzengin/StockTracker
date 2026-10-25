using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Common.Codes;

/// <summary>Generates unique entity codes such as PRD-7F3K9Q.</summary>
public interface ICodeGenerator
{
    Task<string> GenerateAsync(CodePrefix prefix, CancellationToken cancellationToken);

    /// <summary>Generates <paramref name="count"/> distinct unused codes with one lookup per round.</summary>
    Task<IReadOnlyList<string>> GenerateManyAsync(CodePrefix prefix, int count, CancellationToken cancellationToken);
}

/// <summary>Checks which codes are already used in the table that owns the prefix.</summary>
public interface ICodeUniquenessCheck
{
    CodePrefix Prefix { get; }

    Task<IReadOnlySet<string>> ExistingAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);
}
