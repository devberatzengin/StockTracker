using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockTracker.Api.Authentication;
using StockTracker.Api.Contracts;
using StockTracker.Api.Security;
using StockTracker.Application.Auth;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimiting.PolicyName)]
    public async Task<AccessTokenResponse> Login(LoginCommand command, CancellationToken cancellationToken) =>
        IssueTokens(await sender.Send(command, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<AccessTokenResponse> Refresh(CancellationToken cancellationToken) =>
        IssueTokens(await sender.Send(new RefreshTokenCommand(RefreshTokenCookie.Read(Request)), cancellationToken));

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(RefreshTokenCookie.Read(Request)), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken cancellationToken) =>
        sender.Send(new GetCurrentUserQuery(), cancellationToken);

    [HttpPost("change-password")]
    public async Task<AccessTokenResponse> ChangePassword(
        ChangePasswordCommand command,
        CancellationToken cancellationToken) =>
        IssueTokens(await sender.Send(command, cancellationToken));

    private AccessTokenResponse IssueTokens(AuthTokens tokens)
    {
        RefreshTokenCookie.Write(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        return new AccessTokenResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt);
    }
}
