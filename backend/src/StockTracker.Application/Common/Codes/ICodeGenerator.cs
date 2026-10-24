using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Common.Codes;

/// <summary>Generates unique entity codes such as PRD-7F3K9Q.</summary>
public interface ICodeGenerator
{
    Task<string> GenerateAsync(CodePrefix prefix, CancellationToken cancellationToken);
}

/// <summary>Checks whether a code is already used in the table that owns the prefix.</summary>
public interface ICodeUniquenessCheck
{
    CodePrefix Prefix { get; }

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);
}
