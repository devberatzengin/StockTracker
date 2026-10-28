using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockTracker.Api.Security;
using StockTracker.Application.Assignments;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Reports;

namespace StockTracker.Api.Controllers;

/// <summary>Excel downloads; dates in the files are Europe/Istanbul.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Permissions.CanViewReports)]
[EnableRateLimiting(ApiRateLimiting.HeavyPolicy)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("stock.xlsx")]
    [Produces(ReportLimits.ContentType)]
    public async Task<IActionResult> Stock(CancellationToken cancellationToken) =>
        Excel(await sender.Send(new GetStockReportQuery(), cancellationToken));

    /// <param name="from">First Istanbul day (yyyy-MM-dd).</param>
    /// <param name="to">Last Istanbul day, inclusive.</param>
    [HttpGet("movements.xlsx")]
    [Produces(ReportLimits.ContentType)]
    public async Task<IActionResult> Movements([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Excel(await sender.Send(new GetMovementReportQuery(from, to), cancellationToken));

    [HttpGet("assignments.xlsx")]
    [Produces(ReportLimits.ContentType)]
    public async Task<IActionResult> Assignments([FromQuery] AssignmentStatus? status, CancellationToken cancellationToken) =>
        Excel(await sender.Send(new GetAssignmentReportQuery(status), cancellationToken));

    private FileContentResult Excel(ReportFile file) => File(file.Content, ReportLimits.ContentType, file.FileName);
}
