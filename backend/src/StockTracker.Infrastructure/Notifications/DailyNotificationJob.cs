using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockTracker.Application.Notifications;

namespace StockTracker.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Turned off in tests, which run the check directly.</summary>
    public bool DailyCheckEnabled { get; set; } = true;

    /// <summary>Local time (Europe/Istanbul) of the daily check.</summary>
    public TimeSpan DailyCheckTime { get; set; } = new(8, 0, 0);

    /// <summary>Also checked shortly after startup, so a run missed while the app was down is not lost.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Every day at 08:00 Istanbul time (UTC+3, no DST) checks all products and open assignments.
/// Deduplication makes extra runs harmless.
/// </summary>
public sealed class DailyNotificationJob(
    IServiceScopeFactory scopes,
    IOptions<NotificationOptions> options,
    TimeProvider clock,
    ILogger<DailyNotificationJob> logger) : BackgroundService
{
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.DailyCheckEnabled)
        {
            return;
        }

        try
        {
            await Task.Delay(settings.StartupDelay, clock, stoppingToken);
            await RunAsync(stoppingToken);

            await Task.Delay(UntilNext(clock.GetUtcNow(), settings.DailyCheckTime), clock, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromDays(1), clock);
            do
            {
                await RunAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>Time from <paramref name="nowUtc"/> to the next <paramref name="localTime"/> in Istanbul.</summary>
    public static TimeSpan UntilNext(DateTimeOffset nowUtc, TimeSpan localTime)
    {
        var local = nowUtc.ToOffset(IstanbulOffset);
        var next = new DateTimeOffset(local.Date + localTime, IstanbulOffset);
        if (next <= local)
        {
            next = next.AddDays(1);
        }

        return next - local;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DailyNotificationRun>().RunAsync(cancellationToken);
            logger.LogInformation("Daily notification check finished");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Daily notification check failed");
        }
    }
}
