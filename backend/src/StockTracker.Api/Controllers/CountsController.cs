using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Api.Contracts;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Counts;

namespace StockTracker.Api.Controllers;

/// <summary>Anyone can count; starting, reviewing, approving and cancelling need CanApproveCount.</summary>
[ApiController]
[Route("api/counts")]
public sealed class CountsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.CanApproveCount)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Start(StartCountCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreatedResponse(id));
    }

    [HttpGet]
    public Task<PagedResult<CountListItemDto>> List([FromQuery] GetCountsQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CountDto> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetCountQuery(id), cancellationToken);

    [HttpPost("{id:guid}/scan")]
    public Task<CountScanResultDto> Scan(Guid id, CountScanRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ScanCountCommand(id, request.LocationId, request.Code, request.Quantity), cancellationToken);

    [HttpDelete("{id:guid}/lines/{lineId:guid}/last-scan")]
    public async Task<IActionResult> UndoLastScan(Guid id, Guid lineId, CancellationToken cancellationToken)
    {
        await sender.Send(new UndoCountScanCommand(id, lineId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SubmitCountCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/review")]
    [Authorize(Policy = Permissions.CanApproveCount)]
    public Task<CountReviewDto> Review(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetCountReviewQuery(id), cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.CanApproveCount)]
    public Task<CountApprovalResultDto> Approve(Guid id, ApproveCountRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ApproveCountCommand(id, request.Decisions), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.CanApproveCount)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelCountCommand(id), cancellationToken);
        return NoContent();
    }
}
