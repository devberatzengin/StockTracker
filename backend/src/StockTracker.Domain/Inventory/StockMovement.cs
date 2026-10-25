using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Inventory;

/// <summary>
/// One entry of the stock ledger, the single source of truth. Append-only: never updated or deleted.
/// Balances and item locations are results of these entries.
/// </summary>
public sealed class StockMovement
{
    public const int NoteMaxLength = 500;

    private StockMovement()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public MovementType Type { get; private init; }

    public Guid ProductId { get; private init; }

    /// <summary>Set for serialized products only.</summary>
    public Guid? ItemId { get; private init; }

    public Guid? FromLocationId { get; private init; }

    public Guid? ToLocationId { get; private init; }

    /// <summary>Always 1 for serialized movements.</summary>
    public decimal Quantity { get; private init; }

    public MovementReason? Reason { get; private init; }

    public string? Note { get; private init; }

    /// <summary>Groups the movements created by one operation.</summary>
    public Guid ReferenceId { get; private init; }

    public Guid UserId { get; private init; }

    public DateTime OccurredAt { get; private init; }

    /// <summary>Item status before a status change (phase 2).</summary>
    public ItemStatus? FromStatus { get; private init; }

    /// <summary>Item status after a status change (phase 2).</summary>
    public ItemStatus? ToStatus { get; private init; }

    /// <summary>Creates a movement after checking the field rules of its type.</summary>
    public static StockMovement Create(
        MovementType type,
        Guid productId,
        Guid? itemId,
        Guid? fromLocationId,
        Guid? toLocationId,
        decimal quantity,
        MovementReason? reason,
        string? note,
        MovementContext context,
        ItemStatus? fromStatus = null,
        ItemStatus? toStatus = null)
    {
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        ValidateLocations(type, fromLocationId, toLocationId);
        ValidateQuantity(itemId, quantity);
        ValidateReason(type, reason, trimmedNote);

        if (type == MovementType.StatusChange && (fromStatus is null || toStatus is null || itemId is null))
        {
            throw Invalid("Durum değişikliği bir adet ile önceki ve yeni durumu gerektirir.");
        }

        return new StockMovement
        {
            Type = type,
            ProductId = productId,
            ItemId = itemId,
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            Quantity = quantity,
            Reason = reason,
            Note = trimmedNote,
            ReferenceId = context.ReferenceId,
            UserId = context.UserId,
            OccurredAt = context.OccurredAt,
            FromStatus = fromStatus,
            ToStatus = toStatus,
        };
    }

    private static void ValidateLocations(MovementType type, Guid? from, Guid? to)
    {
        var valid = type switch
        {
            MovementType.In => from is null && to is not null,
            MovementType.Out => from is not null && to is null,
            MovementType.Transfer => from is not null && to is not null && from != to,
            MovementType.Adjustment => from is null ^ to is null,
            MovementType.Assign => from is not null && to is null,
            MovementType.Return => from is null && to is not null,
            MovementType.StatusChange => from is null && to is null,
            _ => false,
        };

        if (!valid)
        {
            throw type == MovementType.Transfer && from is not null && from == to
                ? new DomainException(InventoryErrors.SameSourceAndTarget, "Kaynak ve hedef lokasyon aynı olamaz.", "toLocationId")
                : Invalid($"{type} hareketi için kaynak/hedef lokasyon kuralı sağlanmadı.");
        }
    }

    private static void ValidateQuantity(Guid? itemId, decimal quantity)
    {
        if (itemId is not null ? quantity != 1 : quantity <= 0)
        {
            throw new DomainException(
                InventoryErrors.InvalidQuantity,
                itemId is not null ? "Tekil hareketlerde miktar 1 olmalıdır." : "Miktar sıfırdan büyük olmalıdır.",
                "quantity");
        }
    }

    private static void ValidateReason(MovementType type, MovementReason? reason, string? note)
    {
        if (type == MovementType.Out && reason is null)
        {
            throw new DomainException(InventoryErrors.ReasonRequired, "Çıkış için sebep zorunludur.", "reason");
        }

        if (reason == MovementReason.Other && note is null)
        {
            throw new DomainException(InventoryErrors.NoteRequired, "Sebep \"Diğer\" ise not zorunludur.", "note");
        }

        if (note?.Length > NoteMaxLength)
        {
            throw new DomainException(InventoryErrors.NoteTooLong, $"Not en fazla {NoteMaxLength} karakter olabilir.", "note");
        }
    }

    private static DomainException Invalid(string message) => new(InventoryErrors.InvalidMovement, message);
}

/// <summary>Who did an operation, when, and the reference shared by its movements.</summary>
public sealed record MovementContext(Guid ReferenceId, Guid UserId, DateTime OccurredAt);
