using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Assignments;

/// <summary>
/// An item handed to a user. An item has at most one open assignment (ReturnedAt is null);
/// there is no handover: the item is returned first, then assigned again.
/// </summary>
public sealed class Assignment : BaseEntity
{
    public const int NoteMaxLength = 500;

    private Assignment()
    {
    }

    public Guid ItemId { get; private set; }

    /// <summary>Any active user; logging in is not required.</summary>
    public Guid AssigneeUserId { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public DateTime AssignedAt { get; private set; }

    /// <summary>Calendar day (Europe/Istanbul) the item is expected back.</summary>
    public DateOnly? DueDate { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Reference of the Assign movement.</summary>
    public Guid AssignReferenceId { get; private set; }

    public DateTime? ReturnedAt { get; private set; }

    public Guid? ReturnedByUserId { get; private set; }

    public Guid? ReturnLocationId { get; private set; }

    public ReturnCondition? ReturnCondition { get; private set; }

    public string? ReturnNote { get; private set; }

    /// <summary>Reference of the Return movement (and the StatusChange of a faulty return).</summary>
    public Guid? ReturnReferenceId { get; private set; }

    public bool IsOpen => ReturnedAt is null;

    /// <summary>Overdue is computed, never stored.</summary>
    public bool IsOverdue(DateOnly today) => IsOpen && DueDate < today;

    internal static Assignment Create(
        Item item,
        Guid assigneeUserId,
        DateOnly? dueDate,
        DateOnly today,
        string? note,
        MovementContext context)
    {
        if (dueDate <= today)
        {
            throw new DomainException(
                AssignmentErrors.DueDateNotInFuture,
                "Teslim tarihi bugünden sonra olmalıdır.",
                "dueDate");
        }

        return new Assignment
        {
            ItemId = item.Id,
            AssigneeUserId = assigneeUserId,
            AssignedByUserId = context.UserId,
            AssignedAt = context.OccurredAt,
            DueDate = dueDate,
            Note = NormalizeNote(note, "note"),
            AssignReferenceId = context.ReferenceId,
        };
    }

    /// <summary>Checks the return input without changing the assignment.</summary>
    internal static string? ValidateReturn(Assignment assignment, ReturnCondition condition, string? note)
    {
        if (!assignment.IsOpen)
        {
            throw new DomainException(AssignmentErrors.AlreadyReturned, "Bu zimmet zaten iade alınmış.");
        }

        var trimmed = NormalizeNote(note, "note");
        if (condition == Assignments.ReturnCondition.Faulty && trimmed is null)
        {
            throw new DomainException(
                AssignmentErrors.ReturnNoteRequired,
                "Arızalı iadede arızayı açıklayan not zorunludur.",
                "note");
        }

        return trimmed;
    }

    internal void Return(Location location, ReturnCondition condition, string? note, MovementContext context)
    {
        var trimmed = ValidateReturn(this, condition, note);

        ReturnedAt = context.OccurredAt;
        ReturnedByUserId = context.UserId;
        ReturnLocationId = location.Id;
        ReturnCondition = condition;
        ReturnNote = trimmed;
        ReturnReferenceId = context.ReferenceId;
    }

    private static string? NormalizeNote(string? note, string target)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > NoteMaxLength)
        {
            throw new DomainException(AssignmentErrors.NoteTooLong, $"Not en fazla {NoteMaxLength} karakter olabilir.", target);
        }

        return trimmed;
    }
}
