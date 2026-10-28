using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Inventory;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/movements")]
[Authorize]
public sealed class MovementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<MovementDto>> List([FromQuery] GetMovementsQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpPost("in")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> In(StockInCommand command, CancellationToken cancellationToken) =>
        sender.Send(command, cancellationToken);

    [HttpPost("out")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> Out(StockOutCommand command, CancellationToken cancellationToken) =>
        sender.Send(command, cancellationToken);

    /// <summary>Cancels out a wrong movement with an opposite one (the original stays).</summary>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> Reverse(Guid id, ReverseMovementRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReverseMovementCommand(id, request.Note), cancellationToken);

    [HttpPost("transfer")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> Transfer(StockTransferCommand command, CancellationToken cancellationToken) =>
        sender.Send(command, cancellationToken);
}
