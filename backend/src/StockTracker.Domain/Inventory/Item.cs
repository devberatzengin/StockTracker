using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Inventory;

/// <summary>A single tracked unit of a serialized product. Its location changes only through movements.</summary>
public class Item : BaseEntity
{
    public const int SerialNumberMaxLength = 100;
    public const int CodeLength = Common.Codes.EntityCode.Length;

    private Item()
    {
    }

    /// <summary>System generated (ITM-XXXXXX); never changes.</summary>
    public string Code { get; private set; } = string.Empty;

    public Guid ProductId { get; private set; }

    /// <summary>Optional; unique within the product when set.</summary>
    public string? SerialNumber { get; private set; }

    public ItemStatus Status { get; private set; }

    /// <summary>Null while assigned or after disposal.</summary>
    public Guid? CurrentLocationId { get; private set; }

    private ItemState State => ItemState.For(Status);

    internal static Item Receive(string code, Product product, string? serialNumber, Location location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var item = new Item
        {
            Code = code,
            ProductId = product.Id,
            Status = ItemStatus.InStock,
            CurrentLocationId = location.Id,
        };
        item.ChangeSerialNumber(serialNumber);
        return item;
    }

    public static string? NormalizeSerialNumber(string? serialNumber) =>
        string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();

    public void ChangeSerialNumber(string? serialNumber)
    {
        var normalized = NormalizeSerialNumber(serialNumber);
        if (normalized?.Length > SerialNumberMaxLength)
        {
            throw new DomainException(
                InventoryErrors.SerialNumberTooLong,
                $"Seri numarası en fazla {SerialNumberMaxLength} karakter olabilir.",
                "serialNumbers");
        }

        SerialNumber = normalized;
    }

    internal void EnsureCanTransfer() => State.EnsureCanTransfer();

    internal void EnsureCanDispose() => _ = State.Dispose();

    internal void MoveTo(Location location)
    {
        State.EnsureCanTransfer();
        CurrentLocationId = location.Id;
    }

    internal void Dispose()
    {
        Status = State.Dispose();
        CurrentLocationId = null;
    }

    /// <summary>Hands the item to a user; it leaves its location.</summary>
    public void Assign()
    {
        Status = State.Assign();
        CurrentLocationId = null;
    }

    /// <summary>Takes an assigned item back into <paramref name="location"/>.</summary>
    public void Return(Location location)
    {
        Status = State.Return();
        CurrentLocationId = location.Id;
    }

    /// <summary>The item stays at its location while in repair.</summary>
    public void SendToRepair() => Status = State.SendToRepair();

    public void CompleteRepair() => Status = State.CompleteRepair();
}
