using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockTracker.Application.Notifications;
using StockTracker.Infrastructure.Persistence;

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
/// Every day at 08:00 Istanbul time (UTC+3, no DST) checks all products and open assignments, reconciles
/// the ledger and drops old idempotency keys. With several API instances only one runs it (advisory lock);
/// deduplication makes extra runs harmless anyway.
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

    /// <summary>Arbitrary constant naming the daily job's PostgreSQL advisory lock.</summary>
    public const long LockKey = 7_301_202_610;

    /// <summary>
    /// Runs <paramref name="run"/> only if this connection gets the session-level advisory lock; the
    /// connection stays open for the whole run, so the lock is held until it ends.
    /// </summary>
    public static async Task<bool> TryRunExclusiveAsync(AppDbContext db, Func<Task> run, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = await db.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (!acquired)
            {
                return false;
            }

            try
            {
                await run();
                return true;
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = scope.ServiceProvider.GetRequiredService<DailyNotificationRun>();
            if (await TryRunExclusiveAsync(db, () => run.RunAsync(cancellationToken), cancellationToken))
            {
                logger.LogInformation("Daily notification check finished");
            }
            else
            {
                logger.LogInformation("Daily notification check skipped: another instance is running it");
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Daily notification check failed");
        }
    }
}
