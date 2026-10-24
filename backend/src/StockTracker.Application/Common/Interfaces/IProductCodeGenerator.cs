namespace StockTracker.Application.Common.Interfaces;

/// <summary>Generates a unique PRD-XXXXXX product code.</summary>
public interface IProductCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}
