using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Users;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Permissions.CanManageUsers)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] GetUsersQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<UserDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetUserQuery(id), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(id));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateUserCommand(
                id,
                request.FullName,
                request.Department,
                request.Email,
                request.Password,
                request.Role,
                request.CanLogin),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateUserCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ResetPasswordCommand(id, request.NewPassword), cancellationToken);
        return NoContent();
    }
}
