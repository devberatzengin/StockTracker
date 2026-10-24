using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace StockTracker.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await next();

        logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds:0.0} ms",
            requestName,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        return response;
    }
}
