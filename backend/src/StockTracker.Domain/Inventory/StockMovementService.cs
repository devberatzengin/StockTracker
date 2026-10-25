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
