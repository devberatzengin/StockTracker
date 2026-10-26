using StockTracker.Domain.Assignments;

namespace StockTracker.Api.Contracts;

public sealed record ReturnAssignmentRequest(Guid ReturnLocationId, ReturnCondition Condition, string? Note);
