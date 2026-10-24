namespace StockTracker.Application.Common.Interfaces;

/// <summary>Generates a unique LOC-XXXXXX location code.</summary>
public interface ILocationCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}
