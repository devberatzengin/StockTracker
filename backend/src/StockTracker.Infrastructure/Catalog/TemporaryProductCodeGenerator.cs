using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Common;

namespace StockTracker.Infrastructure.Catalog;

/// <summary>PRD-XXXXXX codes via the interim <see cref="TemporaryCodes"/> helper.</summary>
public sealed class TemporaryProductCodeGenerator(IProductRepository products) : IProductCodeGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken) =>
        TemporaryCodes.GenerateAsync("PRD", products.CodeExistsAsync, cancellationToken);
}
