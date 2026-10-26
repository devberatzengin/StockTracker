using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Auditing;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = Permissions.CanViewAudit)]
public sealed class AuditController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> List([FromQuery] GetAuditLogsQuery query, CancellationToken cancellationToken) =>
        sender.Send(query, cancellationToken);
}
