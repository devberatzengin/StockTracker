using StockTracker.Application.Common.Exceptions;

namespace StockTracker.Application.Common.Interfaces;

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedException("Oturum açmanız gerekiyor.");
}
