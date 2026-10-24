using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Validation;

namespace StockTracker.Application.Users;

/// <summary>
/// Password is optional and sets a new password; it is required when enabling login for a user without one.
/// </summary>
public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? Department,
    string? Email,
    string? Password,
    string Role,
    bool CanLogin) : IRequest, IUserFields;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        Include(new UserFieldsValidator());
        RuleFor(x => x.Password).Password().When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public sealed class UpdateUserCommandHandler(ICurrentUser currentUser, IUserAccountService users)
    : IRequestHandler<UpdateUserCommand>
{
    public Task Handle(UpdateUserCommand request, CancellationToken cancellationToken) =>
        users.UpdateAsync(
            currentUser.RequireUserId(),
            request.Id,
            new UserUpdateData(
                request.FullName.Trim(),
                CreateUserCommandHandler.NormalizeOptional(request.Department),
                CreateUserCommandHandler.NormalizeOptional(request.Email),
                CreateUserCommandHandler.NormalizeOptional(request.Password),
                request.Role,
                request.CanLogin),
            cancellationToken);
}
