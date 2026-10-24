using MediatR;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Auth;

public sealed record LogoutCommand(string? RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(IAuthService authService) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        authService.LogoutAsync(request.RefreshToken, cancellationToken);
}
