using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Validation;

namespace StockTracker.Application.Users;

/// <summary>Email and password are only required when the user can log in.</summary>
public sealed record CreateUserCommand(
    string FullName,
    string? Department,
    string? Email,
    string? Password,
    string Role,
    bool CanLogin) : IRequest<Guid>, IUserFields;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        Include(new UserFieldsValidator());

        RuleFor(x => x.Password)
            .NotEmpty().When(x => x.CanLogin).WithMessage("Giriş yetkisi olan kullanıcı için şifre zorunludur.");
        RuleFor(x => x.Password).Password().When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public sealed class CreateUserCommandHandler(IUserAccountService users) : IRequestHandler<CreateUserCommand, Guid>
{
    public Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken) =>
        users.CreateAsync(
            new NewUserData(
                request.FullName.Trim(),
                NormalizeOptional(request.Department),
                NormalizeOptional(request.Email),
                NormalizeOptional(request.Password),
                request.Role,
                request.CanLogin),
            cancellationToken);

    internal static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
