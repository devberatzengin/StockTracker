using StockTracker.Domain.Notifications;

namespace StockTracker.Application.Common.Interfaces;

public interface INotificationRepository
{
    /// <summary>Open notifications (any user) with one of the keys.</summary>
    Task<IReadOnlyList<Notification>> GetOpenAsync(IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken);

    /// <summary>Open notifications of the types (daily clean-up of returned assignments).</summary>
    Task<IReadOnlyList<Notification>> GetOpenOfTypesAsync(IReadOnlyCollection<NotificationType> types, CancellationToken cancellationToken);

    Task<Notification?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Notification notification);
}
