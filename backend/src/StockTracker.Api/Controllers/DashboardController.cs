using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Dashboard;

namespace StockTracker.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Permissions.CanViewReports)]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken cancellationToken) =>
        sender.Send(new GetDashboardQuery(), cancellationToken);
}
