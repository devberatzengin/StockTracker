using StockTracker.Domain.Assignments;

namespace StockTracker.Application.Assignments;

/// <summary>Computed from ReturnedAt and DueDate; never stored.</summary>
public enum AssignmentStatus
{
    Open,
    Overdue,
    Returned,
}

public sealed record AssignmentDto(
    Guid Id,
    AssignmentStatus Status,
    Guid ItemId,
    string ItemCode,
    string? SerialNumber,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid AssigneeUserId,
    string AssigneeName,
    Guid AssignedByUserId,
    string AssignedByName,
    DateTime AssignedAt,
    DateOnly? DueDate,
    string? Note,
    Guid AssignReferenceId,
    DateTime? ReturnedAt,
    Guid? ReturnedByUserId,
    string? ReturnedByName,
    Guid? ReturnLocationId,
    string? ReturnLocationPath,
    ReturnCondition? ReturnCondition,
    string? ReturnNote,
    Guid? ReturnReferenceId);

/// <summary>Result of an assign or return: the assignment and the reference of its movements.</summary>
public sealed record AssignmentResultDto(Guid AssignmentId, Guid ReferenceId);
