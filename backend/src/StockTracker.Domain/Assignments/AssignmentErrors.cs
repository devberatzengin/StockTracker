namespace StockTracker.Domain.Assignments;

/// <summary>Stable error codes returned with 422 responses.</summary>
public static class AssignmentErrors
{
    public const string NotFound = "Assignment.NotFound";
    public const string ProductNotAssignable = "Assignment.ProductNotAssignable";
    public const string AssigneeNotFound = "Assignment.AssigneeNotFound";
    public const string AssigneeInactive = "Assignment.AssigneeInactive";
    public const string DueDateNotInFuture = "Assignment.DueDateNotInFuture";
    public const string NoteTooLong = "Assignment.NoteTooLong";
    public const string ReturnNoteRequired = "Assignment.ReturnNoteRequired";
    public const string AlreadyReturned = "Assignment.AlreadyReturned";
    public const string UserHasOpenAssignments = "Assignment.UserHasOpenAssignments";
}
