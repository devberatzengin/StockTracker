using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Users;

namespace StockTracker.Api.Controllers;

/// <summary>
/// Active users for pickers (e.g. choosing an assignee). Separate from UsersController,
/// whose class-level policy is user administration.
/// </summary>
[ApiController]
[Route("api/users/lookup")]
[Authorize(Policy = Permissions.CanMoveStock)]
public sealed class UserLookupController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserLookupDto>> Lookup([FromQuery] LookupUsersQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);
}
