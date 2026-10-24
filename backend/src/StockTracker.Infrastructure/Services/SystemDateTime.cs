using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Infrastructure.Services;

public sealed class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
