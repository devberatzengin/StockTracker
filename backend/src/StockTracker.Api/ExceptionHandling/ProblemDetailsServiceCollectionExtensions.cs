using System.Diagnostics;

namespace StockTracker.Api.ExceptionHandling;

public static class ProblemDetailsServiceCollectionExtensions
{
    public const string TraceIdKey = "traceId";

    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions[TraceIdKey] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
