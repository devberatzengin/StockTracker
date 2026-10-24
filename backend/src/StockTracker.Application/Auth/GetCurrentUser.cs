using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Security;

namespace StockTracker.Application.Auth;

public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Department,
    string Role,
    IReadOnlyList<string> Permissions);

public sealed record GetCurrentUserQuery : IRequest<CurrentUserDto>;

public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserAccountService users)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var profile = await users.GetProfileAsync(currentUser.RequireUserId(), cancellationToken)
            ?? throw new UnauthorizedException("Oturum açmanız gerekiyor.");

        return new CurrentUserDto(
            profile.Id,
            profile.FullName,
            profile.Email,
            profile.Department,
            profile.Role,
            Permissions.ForRole(profile.Role));
    }
}
