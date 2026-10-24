namespace StockTracker.Domain.Common;

public abstract class BaseEntity : IAuditable
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }
}
