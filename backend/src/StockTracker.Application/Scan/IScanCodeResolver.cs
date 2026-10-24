using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Scan;

/// <summary>Looks up an entity by its normalized code; one implementation per code prefix.</summary>
public interface IScanCodeResolver
{
    CodePrefix Prefix { get; }

    Task<ScanResultDto?> ResolveAsync(string code, CancellationToken cancellationToken);
}
