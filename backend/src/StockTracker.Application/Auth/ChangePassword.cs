using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Validation;

namespace StockTracker.Application.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<AuthTokens>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut şifre zorunludur.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .Password()
            .NotEqual(x => x.CurrentPassword).WithMessage("Yeni şifre mevcut şifreden farklı olmalıdır.");
    }
}

/// <summary>Revokes all sessions of the user and returns a fresh token pair for the caller.</summary>
public sealed class ChangePasswordCommandHandler(ICurrentUser currentUser, IAuthService authService)
    : IRequestHandler<ChangePasswordCommand, AuthTokens>
{
    public Task<AuthTokens> Handle(ChangePasswordCommand request, CancellationToken cancellationToken) =>
        authService.ChangePasswordAsync(
            currentUser.RequireUserId(),
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
}
