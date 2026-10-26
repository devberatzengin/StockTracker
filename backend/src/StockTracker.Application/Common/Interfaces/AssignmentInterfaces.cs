using StockTracker.Domain.Assignments;

namespace StockTracker.Application.Common.Interfaces;

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Assignment assignment);
}

/// <summary>Answers assignment questions that user and product rules depend on.</summary>
public interface IAssignmentChecker
{
    Task<bool> UserHasOpenAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ProductHasOpenAssignmentsAsync(Guid productId, CancellationToken cancellationToken);
}
