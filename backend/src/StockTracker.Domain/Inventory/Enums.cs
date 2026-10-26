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

/// <summary>Operations an item's current status allows; read from the item state.</summary>
public enum ItemAction
{
    Transfer,
    Assign,
    Return,
    SendToRepair,
    ReturnFromRepair,
    Dispose,
}
