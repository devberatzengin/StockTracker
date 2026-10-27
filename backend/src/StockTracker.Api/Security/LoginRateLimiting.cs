using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace StockTracker.Api.Security;

/// <summary>
/// Login attempts per client IP (PLAN 4.3): a fixed one-minute window, 10 attempts by default
/// (<c>RateLimiting__LoginPermitPerMinute</c>). The client IP comes from X-Forwarded-For set by nginx.
/// </summary>
public static class LoginRateLimiting
{
    public const string PolicyName = "login";
    public const int DefaultPermitPerMinute = 10;

    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permit = configuration.GetValue("RateLimiting:LoginPermitPerMinute", DefaultPermitPerMinute);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                return ValueTask.CompletedTask;
            };
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        // nginx (and cloudflared in front of it) run on private Docker networks; trust only those.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(IPNetwork.Parse("192.168.0.0/16"));
        });

        return services;
    }
}
