using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Inventory;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/movements")]
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

    [HttpPost("transfer")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> Transfer(StockTransferCommand command, CancellationToken cancellationToken) =>
        sender.Send(command, cancellationToken);
}
