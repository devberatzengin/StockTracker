using MediatR;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Users;

public sealed record DeactivateUserCommand(Guid Id) : IRequest;

public sealed class DeactivateUserCommandHandler(ICurrentUser currentUser, IUserAccountService users)
    : IRequestHandler<DeactivateUserCommand>
{
    public Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken) =>
        users.DeactivateAsync(currentUser.RequireUserId(), request.Id, cancellationToken);
}
