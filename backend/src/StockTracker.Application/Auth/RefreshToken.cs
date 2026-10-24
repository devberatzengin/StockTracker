using MediatR;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Auth;

public sealed record RefreshTokenCommand(string? RefreshToken) : IRequest<AuthTokens>;

public sealed class RefreshTokenCommandHandler(IAuthService authService)
    : IRequestHandler<RefreshTokenCommand, AuthTokens>
{
    public Task<AuthTokens> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(request.RefreshToken, cancellationToken);
}
