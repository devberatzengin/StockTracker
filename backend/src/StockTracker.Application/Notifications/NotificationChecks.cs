using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Catalog;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Inventory;
using StockTracker.Domain.Notifications;
using StockTracker.Domain.Users;

namespace StockTracker.Application.Notifications;

/// <summary>
/// Low stock: total &lt; MinStockLevel (Quantity: sum of balances; Serialized: in-stock items).
/// Opens one notification per admin and warehouse manager; resolves them when the total is back.
/// </summary>
public sealed class LowStockMonitor(
    IAppDbContext db,
    INotificationRepository notifications,
    IUserAccountService users,
    IDateTime dateTime,
    IUnitOfWork unitOfWork)
{
    private static readonly string[] Recipients = [Roles.Admin, Roles.WarehouseManager];

    /// <param name="productIds">Products to check; null checks every product (daily run).</param>
    public async Task CheckAsync(IReadOnlyCollection<Guid>? productIds, CancellationToken cancellationToken)
    {
        var query = db.Products.IgnoreQueryFilters().AsNoTracking();
        if (productIds is not null)
        {
            query = query.Where(p => productIds.Contains(p.Id));
        }

        var products = await StockTotals.Query(db, query).ToListAsync(cancellationToken);

        var low = products.Where(StockTotals.IsLow).ToList();
        var keys = products.Select(p => Notification.LowStockKey(p.ProductId)).ToList();
        var open = await notifications.GetOpenAsync(keys, cancellationToken);
        var now = dateTime.UtcNow;

        // Resolve where the cause went away (refilled, minimum removed, product deactivated).
        var lowKeys = low.Select(p => Notification.LowStockKey(p.ProductId)).ToHashSet();
        foreach (var notification in open.Where(n => !lowKeys.Contains(n.DedupKey)))
        {
            notification.Resolve(now);
        }

        if (low.Count > 0)
        {
            var recipients = await users.GetActiveLoginUsersInRolesAsync(Recipients, cancellationToken);
            foreach (var product in low)
            {
                var key = Notification.LowStockKey(product.ProductId);
                var unit = CatalogLabels.Unit(product.Unit);
                foreach (var userId in recipients.Where(u => !open.Any(n => n.DedupKey == key && n.UserId == u)))
                {
                    notifications.Add(Notification.Create(
                        userId,
                        NotificationType.LowStock,
                        $"Kritik stok: {product.Name}",
                        $"{product.Name} ({product.Code}) stoğu {product.Total:0.###} {unit}; minimum {product.MinStockLevel:0.###} {unit}.",
                        "Product",
                        product.ProductId,
                        key,
                        now));
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Due tomorrow → the assignee (if they can log in). Overdue → the assignee and warehouse managers.
/// Both are resolved when the item is returned.
/// </summary>
public sealed class AssignmentReminderMonitor(
    IAppDbContext db,
    INotificationRepository notifications,
    IUserAccountService users,
    IDateTime dateTime,
    IUnitOfWork unitOfWork)
{
    private static readonly string[] OverdueRecipients = [Roles.WarehouseManager];
    private static readonly NotificationType[] ReminderTypes = [NotificationType.AssignmentDueSoon, NotificationType.AssignmentOverdue];

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        var today = dateTime.Today();
        var tomorrow = today.AddDays(1);
        var now = dateTime.UtcNow;
        var products = db.Products.IgnoreQueryFilters();

        var due = await db.Assignments.AsNoTracking()
            .Where(a => a.ReturnedAt == null && a.DueDate != null && a.DueDate <= tomorrow)
            .Select(a => new
            {
                a.Id,
                a.AssigneeUserId,
                DueDate = a.DueDate!.Value,
                Item = db.Items.Where(i => i.Id == a.ItemId)
                    .Select(i => new { i.Code, Product = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First() })
                    .First(),
            })
            .ToListAsync(cancellationToken);

        // Clean up reminders of assignments returned without the return event (e.g. before this feature).
        var reminders = await notifications.GetOpenOfTypesAsync(ReminderTypes, cancellationToken);
        var remindedIds = reminders.Select(n => n.EntityId).Distinct().ToList();
        var returnedIds = await db.Assignments
            .Where(a => remindedIds.Contains(a.Id) && a.ReturnedAt != null)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        foreach (var notification in reminders.Where(n => returnedIds.Contains(n.EntityId)))
        {
            notification.Resolve(now);
        }

        var keys = due.SelectMany(a => new[] { Notification.DueSoonKey(a.Id), Notification.OverdueKey(a.Id) }).ToList();
        var open = await notifications.GetOpenAsync(keys, cancellationToken);
        var managers = due.Any(a => a.DueDate < today)
            ? await users.GetActiveLoginUsersInRolesAsync(OverdueRecipients, cancellationToken)
            : [];

        foreach (var assignment in due)
        {
            var assignee = await users.GetAsync(assignment.AssigneeUserId, cancellationToken);
            var assigneeCanRead = assignee is { IsActive: true, CanLogin: true };
            var what = $"{assignment.Item.Code} ({assignment.Item.Product})";

            if (assignment.DueDate == tomorrow && assigneeCanRead)
            {
                Add(open, assignee!.Id, NotificationType.AssignmentDueSoon, Notification.DueSoonKey(assignment.Id), assignment.Id,
                    $"Teslim yarın: {what}", $"{what} için teslim tarihi yarın. Lütfen depoya iade edin.", now);
            }

            if (assignment.DueDate < today)
            {
                var recipients = managers.ToList();
                if (assigneeCanRead)
                {
                    recipients.Add(assignee!.Id);
                }

                foreach (var userId in recipients.Distinct())
                {
                    var forAssignee = assignee is not null && userId == assignee.Id;
                    Add(open, userId, NotificationType.AssignmentOverdue, Notification.OverdueKey(assignment.Id), assignment.Id,
                        $"Gecikmiş zimmet: {what}",
                        forAssignee
                            ? $"{what} için teslim tarihi geçti. Lütfen depoya iade edin."
                            : $"{what} {assignee?.FullName ?? "?"} üzerinde; teslim tarihi geçti.",
                        now);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void Add(
        IReadOnlyList<Notification> open,
        Guid userId,
        NotificationType type,
        string key,
        Guid assignmentId,
        string title,
        string message,
        DateTime now)
    {
        if (!open.Any(n => n.DedupKey == key && n.UserId == userId))
        {
            notifications.Add(Notification.Create(userId, type, title, message, "Assignment", assignmentId, key, now));
        }
    }

    /// <summary>The item came back: both reminders of the assignment are resolved.</summary>
    public async Task ResolveAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var open = await notifications.GetOpenAsync([Notification.DueSoonKey(assignmentId), Notification.OverdueKey(assignmentId)], cancellationToken);
        foreach (var notification in open)
        {
            notification.Resolve(dateTime.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
