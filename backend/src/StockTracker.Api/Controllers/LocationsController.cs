using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Locations;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/locations")]
public sealed class LocationsController(ISender sender) : ControllerBase
{
    [HttpGet("tree")]
    public Task<IReadOnlyList<LocationTreeNodeDto>> Tree(
        [FromQuery] GetLocationTreeQuery query,
        CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<LocationDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetLocationQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Update(Guid id, UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateLocationCommand(id, request.Name, request.Type), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/move")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Move(Guid id, MoveLocationRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new MoveLocationCommand(id, request.NewParentId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.CanManageCatalog)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateLocationCommand(id), cancellationToken);
        return NoContent();
    }
}
