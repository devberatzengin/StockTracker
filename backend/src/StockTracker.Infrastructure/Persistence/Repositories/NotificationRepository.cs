using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Notifications;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetOpenAsync(IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken) =>
        dedupKeys.Count == 0
            ? []
            : await db.Notifications.Where(n => n.ResolvedAt == null && dedupKeys.Contains(n.DedupKey)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetOpenOfTypesAsync(IReadOnlyCollection<NotificationType> types, CancellationToken cancellationToken) =>
        await db.Notifications.Where(n => n.ResolvedAt == null && types.Contains(n.Type)).ToListAsync(cancellationToken);

    public Task<Notification?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}
