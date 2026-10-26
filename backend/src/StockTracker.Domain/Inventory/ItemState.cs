using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Inventory;

/// <summary>
/// State pattern for item statuses. Each state allows only its own transitions:
/// InStock → Assigned → InStock (return) · InStock → InRepair → InStock · InStock/InRepair → Disposed.
/// Only InStock items can be transferred; Disposed is final.
/// </summary>
public abstract class ItemState
{
    private static readonly IReadOnlyDictionary<ItemStatus, ItemState> States = new Dictionary<ItemStatus, ItemState>
    {
        [ItemStatus.InStock] = new InStockState(),
        [ItemStatus.Assigned] = new AssignedState(),
        [ItemStatus.InRepair] = new InRepairState(),
        [ItemStatus.Disposed] = new DisposedState(),
    };

    public abstract ItemStatus Status { get; }

    public static ItemState For(ItemStatus status) => States[status];

    /// <summary>The operations this state allows; each one matches a method that does not throw.</summary>
    public abstract IReadOnlyList<ItemAction> Actions { get; }

    public virtual ItemStatus Assign() => throw Invalid("zimmetlenemez");

    public virtual ItemStatus Return() => throw Invalid("iade alınamaz");

    public virtual ItemStatus SendToRepair() => throw Invalid("tamire gönderilemez");

    public virtual ItemStatus CompleteRepair() => throw Invalid("tamirden dönemez");

    public virtual ItemStatus Dispose() => throw Invalid("çıkışı yapılamaz");

    public virtual void EnsureCanTransfer() => throw Invalid("transfer edilemez");

    public static string Label(ItemStatus status) => status switch
    {
        ItemStatus.InStock => "stokta",
        ItemStatus.Assigned => "zimmetli",
        ItemStatus.InRepair => "tamirde",
        ItemStatus.Disposed => "hurdaya ayrılmış",
        _ => status.ToString(),
    };

    private DomainException Invalid(string action) =>
        new(InventoryErrors.InvalidItemTransition, $"Durumu {Label(Status)} olan adet {action}.", "itemId");

    private sealed class InStockState : ItemState
    {
        public override ItemStatus Status => ItemStatus.InStock;

        public override IReadOnlyList<ItemAction> Actions { get; } =
            [ItemAction.Transfer, ItemAction.Assign, ItemAction.SendToRepair, ItemAction.Dispose];

        public override ItemStatus Assign() => ItemStatus.Assigned;

        public override ItemStatus SendToRepair() => ItemStatus.InRepair;

        public override ItemStatus Dispose() => ItemStatus.Disposed;

        public override void EnsureCanTransfer()
        {
        }
    }

    private sealed class AssignedState : ItemState
    {
        public override ItemStatus Status => ItemStatus.Assigned;

        public override IReadOnlyList<ItemAction> Actions { get; } = [ItemAction.Return];

        public override ItemStatus Return() => ItemStatus.InStock;
    }

    private sealed class InRepairState : ItemState
    {
        public override ItemStatus Status => ItemStatus.InRepair;

        public override IReadOnlyList<ItemAction> Actions { get; } = [ItemAction.ReturnFromRepair, ItemAction.Dispose];

        public override ItemStatus CompleteRepair() => ItemStatus.InStock;

        public override ItemStatus Dispose() => ItemStatus.Disposed;
    }

    private sealed class DisposedState : ItemState
    {
        public override ItemStatus Status => ItemStatus.Disposed;

        public override IReadOnlyList<ItemAction> Actions { get; } = [];
    }
}
