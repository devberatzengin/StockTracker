using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Domain.Notifications;

namespace StockTracker.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string EntityType,
    Guid EntityId,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public sealed record UnreadCountDto(int Count);

/// <summary>The current user's notifications, newest first.</summary>
public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20, bool UnreadOnly = false)
    : IRequest<PagedResult<NotificationDto>>, IPagedQuery;

/// <summary>Unread and still open: what the bell counts.</summary>
public sealed record GetUnreadCountQuery : IRequest<UnreadCountDto>;

public sealed record MarkNotificationReadCommand(Guid Id) : IRequest;

public sealed record MarkAllNotificationsReadCommand : IRequest;

public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator() => Include(new PagedQueryValidator());
}

public sealed class NotificationQueryHandlers(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDto>>,
      IRequestHandler<GetUnreadCountQuery, UnreadCountDto>
{
    public Task<PagedResult<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead && n.ResolvedAt == null);
        }

        return query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.EntityType, n.EntityId, n.IsRead, n.CreatedAt, n.ResolvedAt))
            .ToPagedResultAsync(request, cancellationToken);
    }

    public async Task<UnreadCountDto> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        return new UnreadCountDto(await db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && n.ResolvedAt == null, cancellationToken));
    }
}

public sealed class NotificationCommandHandlers(
    INotificationRepository notifications,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<MarkNotificationReadCommand>, IRequestHandler<MarkAllNotificationsReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetForUserAsync(request.Id, currentUser.RequireUserId(), cancellationToken)
            ?? throw new NotFoundException("Bildirim", request.Id);
        notification.MarkRead();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken) =>
        notifications.MarkAllReadAsync(currentUser.RequireUserId(), cancellationToken);
}
