using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Files;

namespace StockTracker.Api.Controllers;

/// <summary>
/// Signed file links for img tags, which cannot send the in-memory access token.
/// The signature (exp + sig) is the authorization.
/// </summary>
[ApiController]
[Route("api/files")]
[AllowAnonymous]
public sealed class FilesController(ISender sender) : ControllerBase
{
    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(string key, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken cancellationToken)
    {
        var file = await sender.Send(new GetSignedFileQuery(key, exp, sig), cancellationToken);
        Response.Headers.CacheControl = $"private, max-age={(int)file.MaxAge.TotalSeconds}";
        return File(file.Content, file.ContentType);
    }
}
