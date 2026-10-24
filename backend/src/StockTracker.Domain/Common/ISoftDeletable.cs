namespace StockTracker.Domain.Common;

public interface ISoftDeletable
{
    bool IsActive { get; }
}
