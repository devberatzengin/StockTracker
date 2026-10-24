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

                // Also replaces the English defaults on framework-generated problems
                // (unknown route, missing token, model binding errors).
                var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
                if (ProblemTitles.For(status) is { } title)
                {
                    context.ProblemDetails.Title = title;
                }

                context.ProblemDetails.Extensions[TraceIdKey] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
