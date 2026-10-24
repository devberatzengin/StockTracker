using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Common;

namespace StockTracker.Infrastructure.Locations;

/// <summary>LOC-XXXXXX codes via the interim <see cref="TemporaryCodes"/> helper.</summary>
public sealed class TemporaryLocationCodeGenerator(ILocationRepository locations) : ILocationCodeGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken) =>
        TemporaryCodes.GenerateAsync("LOC", locations.CodeExistsAsync, cancellationToken);
}
