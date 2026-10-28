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

    /// <summary>
    /// Cancels out a wrong movement with a new one in the opposite direction; the original stays in the
    /// ledger. Quantity In/Out/Transfer and item In/Transfer can be reversed, once, with a note. Items must
    /// still be in stock where the original movement left them.
    /// </summary>
    /// <param name="from">The original movement's source location (null when it had none).</param>
    /// <param name="to">The original movement's target location (null when it had none).</param>
    /// <param name="fromBalance">Balance of (product, <paramref name="from"/>) for quantity movements.</param>
    /// <param name="toBalance">Balance of (product, <paramref name="to"/>) for quantity movements.</param>
    public StockMovement Reverse(
        StockMovement original,
        bool alreadyReversed,
        Product product,
        Item? item,
        Location? from,
        Location? to,
        StockBalance? fromBalance,
        StockBalance? toBalance,
        string? note,
        MovementContext context)
    {
        if (original.ReversesMovementId is not null)
        {
            throw new DomainException(InventoryErrors.NotReversible, "Ters kaydın kendisi ters çevrilemez.");
        }

        if (!original.IsReversibleType)
        {
            throw new DomainException(
                InventoryErrors.NotReversible,
                "Bu hareket ters çevrilemez; zimmet, iade, durum değişikliği ve sayım düzeltmelerinin kendi işlemleri var.");
        }

        if (alreadyReversed)
        {
            throw new DomainException(InventoryErrors.AlreadyReversed, "Bu hareket zaten ters çevrildi.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException(InventoryErrors.NoteRequired, "Ters kayıt için not zorunludur.", "note");
        }

        if (product.Id != original.ProductId)
        {
            throw new ArgumentException("Product does not belong to the movement.", nameof(product));
        }

        var movement = original.ItemId is null
            ? ReverseQuantity(original, product, from, to, fromBalance, toBalance, note, context)
            : ReverseItem(original, product, item ?? throw new ArgumentNullException(nameof(item)), from, to, note, context);
        movement.MarkAsReversalOf(original);
        return movement;
    }

    private static StockMovement ReverseQuantity(
        StockMovement original,
        Product product,
        Location? from,
        Location? to,
        StockBalance? fromBalance,
        StockBalance? toBalance,
        string note,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Quantity);
        var quantity = original.Quantity;
        switch (original.Type)
        {
            case MovementType.In:
            {
                var target = Required(to, original.ToLocationId);
                EnsureActive(target, "movementId");
                var movement = StockMovement.Create(
                    MovementType.Out, product.Id, null, target.Id, null, quantity, MovementReason.Other, note, context);
                Decrease(toBalance, product, target, quantity);
                return movement;
            }

            case MovementType.Out:
            {
                var source = Required(from, original.FromLocationId);
                EnsureActive(source, "movementId");
                var balance = fromBalance ?? throw new ArgumentNullException(nameof(fromBalance));
                EnsureBelongs(balance, product, source);
                var movement = StockMovement.Create(
                    MovementType.In, product.Id, null, null, source.Id, quantity, null, note, context);
                balance.Increase(quantity);
                return movement;
            }

            default:
            {
                var source = Required(from, original.FromLocationId);
                var target = Required(to, original.ToLocationId);
                EnsureActive(source, "movementId");
                EnsureActive(target, "movementId");
                var back = fromBalance ?? throw new ArgumentNullException(nameof(fromBalance));
                EnsureBelongs(back, product, source);
                var movement = StockMovement.Create(
                    MovementType.Transfer, product.Id, null, target.Id, source.Id, quantity, null, note, context);
                Decrease(toBalance, product, target, quantity);
                back.Increase(quantity);
                return movement;
            }
        }
    }

    private static StockMovement ReverseItem(
        StockMovement original,
        Product product,
        Item item,
        Location? from,
        Location? to,
        string note,
        MovementContext context)
    {
        EnsureUsable(product, TrackingType.Serialized);
        EnsureBelongs(item, product);
        if (item.Id != original.ItemId)
        {
            throw new ArgumentException("Item does not belong to the movement.", nameof(item));
        }

        // The item must still be where this movement put it; anything later would be undone silently.
        if (item.Status != ItemStatus.InStock || item.CurrentLocationId != original.ToLocationId)
        {
            throw new DomainException(
                InventoryErrors.ItemChangedSinceMovement,
                "Adet bu hareketten sonra taşınmış ya da durumu değişmiş; ters kayıt yapılamaz.");
        }

        var target = Required(to, original.ToLocationId);
        if (original.Type == MovementType.In)
        {
            // A wrongly received item leaves stock for good.
            var movement = StockMovement.Create(
                MovementType.Out, product.Id, item.Id, target.Id, null, 1, MovementReason.Other, note, context);
            item.Dispose();
            return movement;
        }

        var source = Required(from, original.FromLocationId);
        EnsureActive(source, "movementId");
        var back = StockMovement.Create(
            MovementType.Transfer, product.Id, item.Id, target.Id, source.Id, 1, null, note, context);
        item.MoveTo(source);
        return back;
    }

    private static Location Required(Location? location, Guid? expectedId) =>
        location is not null && location.Id == expectedId
            ? location
            : throw new ArgumentException("Location does not match the movement.", nameof(location));

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
