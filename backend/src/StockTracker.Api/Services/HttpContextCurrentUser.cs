using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Services;

/// <summary>Reads the user from the JWT claims (inbound claim mapping is disabled, so "sub" stays "sub").</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        IsAuthenticated && Guid.TryParse(User!.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public bool HasPermission(string permission) =>
        IsAuthenticated && Permissions.RolesFor(permission).Any(User!.IsInRole);
}
