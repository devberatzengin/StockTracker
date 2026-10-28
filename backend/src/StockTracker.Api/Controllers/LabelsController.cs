using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockTracker.Api.Security;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Labels;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/labels")]
[Authorize(Policy = Permissions.CanMoveStock)]
public sealed class LabelsController(ISender sender) : ControllerBase
{
    /// <summary>A PDF of QR labels; print at 100 % / actual size.</summary>
    [HttpPost]
    [EnableRateLimiting(ApiRateLimiting.HeavyPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Print(PrintLabelsCommand command, CancellationToken cancellationToken)
    {
        var file = await sender.Send(command, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }
}
