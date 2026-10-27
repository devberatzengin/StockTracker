using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Scan;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/scan")]
[Authorize]
public sealed class ScanController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Resolves a scanned or typed value: a label URL (…/s/{code}), an entity code or a barcode.
    /// The catch-all segment allows URLs; clients should URL-encode the input.
    /// </summary>
    [HttpGet("{**input}")]
    [ProducesResponseType<ScanResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ScanResultDto> Scan(string input, CancellationToken cancellationToken) =>
        sender.Send(new ScanQuery(input), cancellationToken);
}
