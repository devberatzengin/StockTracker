using MediatR;
using Microsoft.Extensions.Logging;

namespace StockTracker.Application.Notifications;

/// <summary>
/// Published after a committed operation changed product totals (In, Out, Adjustment, and assign /
/// return / status changes, which change the in-stock count of serialized products). Transfers do not.
/// </summary>
public sealed record StockLevelsChanged(IReadOnlyCollection<Guid> ProductIds) : INotification;

/// <summary>Published after an assignment was returned.</summary>
public sealed record AssignmentReturned(Guid AssignmentId) : INotification;

/// <summary>
/// The operation is already saved, so a failure here must not fail the request:
/// it is logged and the daily check catches up.
/// </summary>
public sealed class NotificationEventHandlers(
    LowStockMonitor lowStock,
    AssignmentReminderMonitor reminders,
    ILogger<NotificationEventHandlers> logger)
    : INotificationHandler<StockLevelsChanged>, INotificationHandler<AssignmentReturned>
{
    public async Task Handle(StockLevelsChanged notification, CancellationToken cancellationToken)
    {
        try
        {
            await lowStock.CheckAsync(notification.ProductIds, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Low stock check failed for {ProductIds}", notification.ProductIds);
        }
    }

    public async Task Handle(AssignmentReturned notification, CancellationToken cancellationToken)
    {
        try
        {
            await reminders.ResolveAsync(notification.AssignmentId, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Resolving reminders failed for assignment {AssignmentId}", notification.AssignmentId);
        }
    }
}

/// <summary>The daily 08:00 run: every product and every open assignment.</summary>
public sealed class DailyNotificationRun(LowStockMonitor lowStock, AssignmentReminderMonitor reminders)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await lowStock.CheckAsync(null, cancellationToken);
        await reminders.CheckAsync(cancellationToken);
    }
}
