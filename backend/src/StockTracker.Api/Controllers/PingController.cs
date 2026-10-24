using MediatR;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Ping;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/ping")]
public sealed class PingController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Produces("text/plain")]
    public async Task<ActionResult<string>> Get(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PingQuery(), cancellationToken));
}
