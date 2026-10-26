using StockTracker.Domain.Assignments;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Inventory;

/// <summary>
/// Coordinates products, locations, items and balances for stock movements. Every method validates
/// the rules of its movement type, updates the item or balance and returns the ledger entry; the
/// caller persists all of them in one transaction.
/// </summary>
public sealed class StockMovementService
{
    public const int MaxItemsPerLine = 100;

    /// <summary>Creates a new in-stock item at <paramref name="target"/> with its In movement.</summary>
    public (Item Item, StockMovement Movement) ReceiveItem(
        string code,
        Product product,
        Location target,
        string? serialNumber,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Serialized);
        EnsureActive(target, "toLocationId");

        var item = Item.Receive(code, product, serialNumber, target);
        var movement = StockMovement.Create(
            MovementType.In, product.Id, item.Id, null, target.Id, 1, null, null, context);
        return (item, movement);
    }

    /// <summary>Adds <paramref name="quantity"/> to the balance at <paramref name="target"/>.</summary>
    public StockMovement ReceiveQuantity(
        Product product,
        Location target,
        StockBalance balance,
        decimal quantity,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Quantity);
        EnsureActive(target, "toLocationId");
        EnsureBelongs(balance, product, target);

        var movement = StockMovement.Create(
            MovementType.In, product.Id, null, null, target.Id, quantity, null, null, context);
        balance.Increase(quantity);
        return movement;
    }

    /// <summary>Takes an item out of stock for good (Disposed); it leaves its location.</summary>
    public StockMovement DisposeItem(
        Item item,
        Product product,
        Location? source,
        MovementReason reason,
        string? note,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Serialized);
        EnsureBelongs(item, product);
        item.EnsureCanDispose();
        if (source is not null)
        {
            EnsureActive(source, "itemId");
        }

        // Validate the ledger entry before changing the item.
        var movement = StockMovement.Create(
            MovementType.Out, product.Id, item.Id, item.CurrentLocationId, null, 1, reason, note, context);
        item.Dispose();
        return movement;
    }

    /// <summary>Removes <paramref name="quantity"/> from the balance at <paramref name="source"/>.</summary>
    public StockMovement ConsumeQuantity(
        Product product,
        Location source,
        StockBalance? balance,
        decimal quantity,
        MovementReason reason,
        string? note,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Quantity);
        EnsureActive(source, "fromLocationId");

        var movement = StockMovement.Create(
            MovementType.Out, product.Id, null, source.Id, null, quantity, reason, note, context);
        Decrease(balance, product, source, quantity);
        return movement;
    }

    /// <summary>Moves an in-stock item to <paramref name="target"/>.</summary>
    public StockMovement TransferItem(
        Item item,
        Product product,
        Location target,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Serialized);
        EnsureBelongs(item, product);
        EnsureActive(target, "toLocationId");
        item.EnsureCanTransfer();

        var movement = StockMovement.Create(
            MovementType.Transfer, product.Id, item.Id, item.CurrentLocationId, target.Id, 1, null, null, context);
        item.MoveTo(target);
        return movement;
    }

    /// <summary>Moves <paramref name="quantity"/> from the source balance to the target balance.</summary>
    public StockMovement TransferQuantity(
        Product product,
        Location source,
        Location target,
        StockBalance? sourceBalance,
        StockBalance targetBalance,
        decimal quantity,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Quantity);
        EnsureActive(source, "fromLocationId");
        EnsureActive(target, "toLocationId");
        EnsureBelongs(targetBalance, product, target);

        var movement = StockMovement.Create(
            MovementType.Transfer, product.Id, null, source.Id, target.Id, quantity, null, null, context);
        Decrease(sourceBalance, product, source, quantity);
        targetBalance.Increase(quantity);
        return movement;
    }

    /// <summary>
    /// Count correction: changes the balance by <paramref name="delta"/> with an Adjustment movement
    /// (target set for an increase, source for a decrease).
    /// </summary>
    public StockMovement AdjustQuantity(
        Product product,
        Location location,
        StockBalance balance,
        decimal delta,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Quantity);
        EnsureBelongs(balance, product, location);
        if (delta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "An adjustment needs a non-zero difference.");
        }

        var movement = delta > 0
            ? StockMovement.Create(MovementType.Adjustment, product.Id, null, null, location.Id, delta, null, null, context)
            : StockMovement.Create(MovementType.Adjustment, product.Id, null, location.Id, null, -delta, null, null, context);
        if (delta > 0)
        {
            balance.Increase(delta);
        }
        else
        {
            balance.Decrease(-delta);
        }

        return movement;
    }

    /// <summary>
    /// Hands an in-stock item of an assignable product to an active user. The item leaves its
    /// location; the Assign movement's source is that last location.
    /// </summary>
    /// <param name="assigneeIsActive">Whether the receiving user exists and is active.</param>
    /// <param name="today">Current calendar day (Europe/Istanbul) for the due date rule.</param>
    public (Assignment Assignment, StockMovement Movement) AssignItem(
        Item item,
        Product product,
        Guid assigneeUserId,
        bool assigneeIsActive,
        DateOnly? dueDate,
        DateOnly today,
        string? note,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Serialized);
        EnsureBelongs(item, product);
        if (!product.IsAssignable)
        {
            throw new DomainException(AssignmentErrors.ProductNotAssignable, "Bu ürün zimmetlenebilir değil.", "itemId");
        }

        item.EnsureCanAssign();
        if (!assigneeIsActive)
        {
            throw new DomainException(AssignmentErrors.AssigneeInactive, "Pasif kullanıcıya zimmet verilemez.", "assigneeUserId");
        }

        // Validate the assignment and the ledger entry before changing the item.
        var assignment = Assignment.Create(item, assigneeUserId, dueDate, today, note, context);
        var movement = StockMovement.Create(
            MovementType.Assign, product.Id, item.Id, item.CurrentLocationId, null, 1, null, null, context);
        item.Assign();
        return (assignment, movement);
    }

    /// <summary>
    /// Takes an assigned item back into <paramref name="target"/>. A faulty return also sends the item
    /// to repair: a Return movement followed by a StatusChange carrying the return note.
    /// </summary>
    public IReadOnlyList<StockMovement> ReturnItem(
        Assignment assignment,
        Item item,
        Product product,
        Location target,
        ReturnCondition condition,
        string? note,
        MovementContext context)
    {
        EnsureBelongs(item, product);
        if (assignment.ItemId != item.Id)
        {
            throw new ArgumentException("Assignment does not belong to the item.", nameof(assignment));
        }

        var trimmedNote = Assignment.ValidateReturn(assignment, condition, note);
        EnsureActive(target, "returnLocationId");
        item.EnsureCanReturn();

        var movements = new List<StockMovement>
        {
            StockMovement.Create(MovementType.Return, product.Id, item.Id, null, target.Id, 1, null, trimmedNote, context),
        };
        if (condition == ReturnCondition.Faulty)
        {
            movements.Add(StockMovement.Create(
                MovementType.StatusChange, product.Id, item.Id, null, null, 1, null, trimmedNote, context,
                ItemStatus.InStock, ItemStatus.InRepair));
        }

        assignment.Return(target, condition, trimmedNote, context);
        item.Return(target);
        if (condition == ReturnCondition.Faulty)
        {
            item.SendToRepair();
        }

        return movements;
    }

    /// <summary>InStock → InRepair; the item keeps its location.</summary>
    public StockMovement SendToRepair(Item item, Product product, string? note, MovementContext context) =>
        ChangeStatus(item, product, note, context, ItemState.For(item.Status).SendToRepair(), item.SendToRepair);

    /// <summary>InRepair → InStock; the item keeps its location.</summary>
    public StockMovement CompleteRepair(Item item, Product product, string? note, MovementContext context) =>
        ChangeStatus(item, product, note, context, ItemState.For(item.Status).CompleteRepair(), item.CompleteRepair);

    private static StockMovement ChangeStatus(
        Item item,
        Product product,
        string? note,
        MovementContext context,
        ItemStatus toStatus,
        Action apply)
    {
        EnsureBelongs(item, product);
        var movement = StockMovement.Create(
            MovementType.StatusChange, product.Id, item.Id, null, null, 1, null, note, context, item.Status, toStatus);
        apply();
        return movement;
    }

    private static void Decrease(StockBalance? balance, Product product, Location location, decimal quantity)
    {
        if (balance is null)
        {
            throw new DomainException(
                InventoryErrors.InsufficientStock,
                "Bu lokasyonda bu ürünün stoğu yok.",
                "quantity");
        }

        EnsureBelongs(balance, product, location);
        balance.Decrease(quantity);
    }

    private static void EnsureUsable(Product product, TrackingType expected)
    {
        if (!product.IsActive)
        {
            throw new DomainException(InventoryErrors.ProductInactive, "Pasif ürün harekete giremez.", "productId");
        }

        if (product.TrackingType != expected)
        {
            throw expected == TrackingType.Serialized
                ? new DomainException(
                    InventoryErrors.ProductNotSerialized,
                    "Bu işlem tekil takipli ürün gerektirir.",
                    "productId")
                : new DomainException(
                    InventoryErrors.ProductNotQuantity,
                    "Tekil takipli ürünler adet (Item) ile işlem görür.",
                    "productId");
        }
    }

    private static void EnsureActive(Location location, string target)
    {
        if (!location.IsActive)
        {
            throw new DomainException(InventoryErrors.LocationInactive, "Pasif lokasyona hareket yapılamaz.", target);
        }
    }

    private static void EnsureBelongs(StockBalance balance, Product product, Location location)
    {
        if (balance.ProductId != product.Id || balance.LocationId != location.Id)
        {
            throw new ArgumentException("Balance does not belong to the product and location.", nameof(balance));
        }
    }

    private static void EnsureBelongs(Item item, Product product)
    {
        if (item.ProductId != product.Id)
        {
            throw new ArgumentException("Item does not belong to the product.", nameof(item));
        }
    }
}
