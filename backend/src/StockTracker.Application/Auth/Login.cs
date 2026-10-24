using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthTokens>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Şifre zorunludur.");
    }
}

public sealed class LoginCommandHandler(IAuthService authService) : IRequestHandler<LoginCommand, AuthTokens>
{
    public Task<AuthTokens> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request.Email, request.Password, cancellationToken);
}
