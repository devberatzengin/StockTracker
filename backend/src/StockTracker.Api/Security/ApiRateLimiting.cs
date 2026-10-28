using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace StockTracker.Api.Security;

/// <summary>Per-minute limits; each can be changed with <c>RateLimiting__&lt;Name&gt;</c>.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login attempts per client IP.</summary>
    public int LoginPermitPerMinute { get; set; } = 10;

    /// <summary>Token refreshes per client IP.</summary>
    public int RefreshPermitPerMinute { get; set; } = 30;

    /// <summary>Excel reports, label PDFs and photo uploads per user.</summary>
    public int HeavyPermitPerMinute { get; set; } = 10;

    /// <summary>All requests of a signed-in user.</summary>
    public int UserPermitPerMinute { get; set; } = 300;

    /// <summary>All anonymous requests of a client IP.</summary>
    public int AnonymousPermitPerMinute { get; set; } = 100;
}

/// <summary>
/// Rate limits (sliding one-minute windows): a global ceiling per user (or per IP when anonymous) plus
/// stricter policies for login, token refresh and CPU-heavy endpoints. The client IP comes from
/// X-Forwarded-For set by nginx. Counters live in memory: one API instance is assumed.
/// </summary>
public static class ApiRateLimiting
{
    public const string LoginPolicy = "login";
    public const string RefreshPolicy = "refresh";
    public const string HeavyPolicy = "heavy";

    private const int Segments = 6;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                // Sliding windows give no retry time; a permit comes back at the latest after one segment.
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var hint) ? hint : Window / Segments;
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                UserId(context) is { } userId
                    ? Sliding($"user:{userId}", limits.UserPermitPerMinute)
                    : Sliding($"ip:{ClientIp(context)}", limits.AnonymousPermitPerMinute));

            options.AddPolicy(LoginPolicy, context => Sliding(ClientIp(context), limits.LoginPermitPerMinute));
            options.AddPolicy(RefreshPolicy, context => Sliding(ClientIp(context), limits.RefreshPermitPerMinute));
            options.AddPolicy(HeavyPolicy, context =>
                Sliding(UserId(context) ?? $"ip:{ClientIp(context)}", limits.HeavyPermitPerMinute));
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

    private static RateLimitPartition<string> Sliding(string key, int permitPerMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitPerMinute,
            Window = Window,
            SegmentsPerWindow = Segments,
            QueueLimit = 0,
        });

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string? UserId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true ? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) : null;
}
