namespace StockTracker.Domain.Inventory;

public enum ItemStatus
{
    InStock,
    Assigned,
    InRepair,
    Disposed,
}

public enum MovementType
{
    In,
    Out,
    Transfer,
    Adjustment,
    Assign,
    Return,
    StatusChange,
}

public enum MovementReason
{
    Consumed,
    Damaged,
    Lost,
    Other,
}
