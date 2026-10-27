using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Inventory;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public sealed class ItemsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ItemListItemDto>> List([FromQuery] GetItemsQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ItemDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetItemQuery(id), cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public async Task<IActionResult> Update(Guid id, UpdateItemRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateItemCommand(id, request.SerialNumber), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/send-to-repair")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> SendToRepair(Guid id, ItemStatusChangeRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SendItemToRepairCommand(id, request.Note), cancellationToken);

    [HttpPost("{id:guid}/return-from-repair")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<MovementResultDto> ReturnFromRepair(Guid id, ItemStatusChangeRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReturnItemFromRepairCommand(id, request.Note), cancellationToken);
}
