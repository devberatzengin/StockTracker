using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Assignments;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize]
public sealed class AssignmentsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<AssignmentResultDto> Assign(AssignItemCommand command, CancellationToken cancellationToken) =>
        sender.Send(command, cancellationToken);

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<AssignmentResultDto> Return(Guid id, ReturnAssignmentRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReturnAssignmentCommand(id, request.ReturnLocationId, request.Condition, request.Note), cancellationToken);

    [HttpGet]
    [Authorize(Policy = Permissions.CanMoveStock)]
    public Task<PagedResult<AssignmentDto>> List([FromQuery] GetAssignmentsQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("mine")]
    public Task<IReadOnlyList<AssignmentDto>> Mine(CancellationToken cancellationToken) =>
        sender.Send(new GetMyAssignmentsQuery(), cancellationToken);

    /// <summary>Users who can move stock see any assignment; others only their own.</summary>
    [HttpGet("{id:guid}")]
    public Task<AssignmentDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetAssignmentQuery(id), cancellationToken);
}
