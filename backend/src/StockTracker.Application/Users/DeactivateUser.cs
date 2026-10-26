using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Application.Users;

public sealed record DeactivateUserCommand(Guid Id) : IRequest;

public sealed class DeactivateUserCommandHandler(
    ICurrentUser currentUser,
    IUserAccountService users,
    IAssignmentChecker assignments) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (await assignments.UserHasOpenAssignmentsAsync(request.Id, cancellationToken))
        {
            throw new DomainException(
                AssignmentErrors.UserHasOpenAssignments,
                "Açık zimmeti olan kullanıcı pasifleştirilemez. Önce zimmetleri iade alın.");
        }

        await users.DeactivateAsync(currentUser.RequireUserId(), request.Id, cancellationToken);
    }
}
