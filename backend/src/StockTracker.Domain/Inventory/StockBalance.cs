using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Inventory;

/// <summary>
/// Quantity of a quantity-tracked product at one location. A result of movements; a row is never
/// deleted, it stays at 0.
/// </summary>
public class StockBalance : BaseEntity
{
    private StockBalance()
    {
    }

    public Guid ProductId { get; private set; }

    public Guid LocationId { get; private set; }

    public decimal Quantity { get; private set; }

    public static StockBalance Empty(Guid productId, Guid locationId) =>
        new() { ProductId = productId, LocationId = locationId };

    internal void Increase(decimal quantity)
    {
        EnsurePositive(quantity);
        Quantity += quantity;
    }

    internal void Decrease(decimal quantity)
    {
        EnsurePositive(quantity);
        if (quantity > Quantity)
        {
            throw new DomainException(
                InventoryErrors.InsufficientStock,
                $"Yetersiz stok: mevcut {Quantity:0.###}, istenen {quantity:0.###}.",
                "quantity");
        }

        Quantity -= quantity;
    }

    private static void EnsurePositive(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException(InventoryErrors.InvalidQuantity, "Miktar sıfırdan büyük olmalıdır.", "quantity");
        }
    }
}
