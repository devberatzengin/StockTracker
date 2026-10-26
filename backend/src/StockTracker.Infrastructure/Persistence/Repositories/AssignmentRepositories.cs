using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Assignments;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository(AppDbContext db) : IAssignmentRepository
{
    public Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Assignments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(Assignment assignment) => db.Assignments.Add(assignment);
}

public sealed class AssignmentChecker(AppDbContext db) : IAssignmentChecker
{
    public Task<bool> UserHasOpenAssignmentsAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Assignments.AnyAsync(a => a.AssigneeUserId == userId && a.ReturnedAt == null, cancellationToken);

    public Task<bool> ProductHasOpenAssignmentsAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Assignments.AnyAsync(
            a => a.ReturnedAt == null && db.Items.Any(i => i.Id == a.ItemId && i.ProductId == productId),
            cancellationToken);
}
