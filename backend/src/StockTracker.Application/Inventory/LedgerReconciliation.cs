using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Notifications;
using StockTracker.Domain.Users;

namespace StockTracker.Application.Inventory;

/// <param name="Problems">One line per balance or item that does not match the ledger.</param>
public sealed record LedgerReport(int BalancesChecked, int ItemsChecked, IReadOnlyList<string> Problems)
{
    public bool IsConsistent => Problems.Count == 0;
}

/// <summary>
/// Proves the ledger is the source of truth: every balance must equal its movements' sum, and every item's
/// status and location must be what its last movements say. A difference opens one "Defter tutarsızlığı"
/// notification for the admins (details go to the log); it is resolved once the data matches again.
/// Nothing is corrected automatically.
/// </summary>
public sealed class LedgerReconciliation(
    IAppDbContext db,
    INotificationRepository notifications,
    IUserAccountService users,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    ILogger<LedgerReconciliation> logger)
{
    private const int LoggedProblems = 50;

    public async Task<LedgerReport> CheckAsync(CancellationToken cancellationToken)
    {
        var report = new LedgerReport(
            await db.StockBalances.CountAsync(cancellationToken),
            await db.Items.IgnoreQueryFilters().CountAsync(cancellationToken),
            [.. await BalanceProblemsAsync(cancellationToken), .. await ItemProblemsAsync(cancellationToken)]);

        await NotifyAsync(report, cancellationToken);
        return report;
    }

    private async Task<List<string>> BalanceProblemsAsync(CancellationToken cancellationToken)
    {
        var quantityMovements = db.StockMovements.AsNoTracking().Where(m => m.ItemId == null);
        var inbound = await quantityMovements
            .Where(m => m.ToLocationId != null)
            .GroupBy(m => new { m.ProductId, LocationId = m.ToLocationId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);
        var outbound = await quantityMovements
            .Where(m => m.FromLocationId != null)
            .GroupBy(m => new { m.ProductId, LocationId = m.FromLocationId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);

        var expected = new Dictionary<(Guid, Guid), decimal>();
        foreach (var x in inbound)
        {
            expected[(x.ProductId, x.LocationId)] = expected.GetValueOrDefault((x.ProductId, x.LocationId)) + x.Quantity;
        }

        foreach (var x in outbound)
        {
            expected[(x.ProductId, x.LocationId)] = expected.GetValueOrDefault((x.ProductId, x.LocationId)) - x.Quantity;
        }

        var balances = await db.StockBalances.AsNoTracking()
            .Select(b => new { b.ProductId, b.LocationId, b.Quantity })
            .ToDictionaryAsync(b => (b.ProductId, b.LocationId), b => b.Quantity, cancellationToken);

        return [.. expected.Keys.Union(balances.Keys)
            .Select(key => (Key: key, Ledger: expected.GetValueOrDefault(key), Balance: balances.GetValueOrDefault(key)))
            .Where(x => x.Ledger != x.Balance)
            .Select(x => $"Bakiye: ürün {x.Key.Item1}, lokasyon {x.Key.Item2}: kayıtlı {x.Balance:0.###}, hareketlere göre {x.Ledger:0.###}")];
    }

    private async Task<List<string>> ItemProblemsAsync(CancellationToken cancellationToken)
    {
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.ItemId != null)
            .Select(m => new { ItemId = m.ItemId!.Value, m.Id, m.Type, m.ToLocationId, m.ToStatus, m.OccurredAt })
            .ToListAsync(cancellationToken);
        var items = await db.Items.IgnoreQueryFilters().AsNoTracking()
            .Select(i => new { i.Id, i.Code, i.Status, i.CurrentLocationId })
            .ToListAsync(cancellationToken);

        // Same order as the movement list: a status change follows the movement of the same operation.
        var byItem = movements
            .GroupBy(m => m.ItemId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(m => m.OccurredAt)
                .ThenBy(m => m.Type == MovementType.StatusChange)
                .ThenBy(m => m.Id)
                .ToList());

        var problems = new List<string>();
        foreach (var item in items)
        {
            if (!byItem.TryGetValue(item.Id, out var ledger))
            {
                problems.Add($"Adet {item.Code}: hiç hareketi yok.");
                continue;
            }

            var last = ledger[^1];
            var status = last.Type switch
            {
                MovementType.Assign => ItemStatus.Assigned,
                MovementType.Out => ItemStatus.Disposed,
                MovementType.StatusChange => last.ToStatus!.Value,
                _ => ItemStatus.InStock,
            };
            var placing = ledger.LastOrDefault(m => m.Type != MovementType.StatusChange);
            var location = placing?.Type is MovementType.In or MovementType.Transfer or MovementType.Return
                ? placing.ToLocationId
                : null;

            if (item.Status != status || item.CurrentLocationId != location)
            {
                problems.Add(
                    $"Adet {item.Code}: kayıtlı {item.Status} @ {item.CurrentLocationId?.ToString() ?? "-"}, " +
                    $"hareketlere göre {status} @ {location?.ToString() ?? "-"}");
            }
        }

        return problems;
    }

    private async Task NotifyAsync(LedgerReport report, CancellationToken cancellationToken)
    {
        var open = await notifications.GetOpenAsync([Notification.LedgerMismatchKey], cancellationToken);
        var now = dateTime.UtcNow;

        if (report.IsConsistent)
        {
            foreach (var notification in open)
            {
                notification.Resolve(now);
            }
        }
        else
        {
            logger.LogError(
                "Ledger reconciliation found {Count} problems: {Problems}",
                report.Problems.Count,
                string.Join(" | ", report.Problems.Take(LoggedProblems)));

            var admins = await users.GetActiveLoginUsersInRolesAsync([Roles.Admin], cancellationToken);
            foreach (var admin in admins.Where(a => open.All(n => n.UserId != a)))
            {
                notifications.Add(Notification.Create(
                    admin,
                    NotificationType.LedgerMismatch,
                    "Defter tutarsızlığı",
                    $"{report.Problems.Count} bakiye ya da adet hareket defteriyle uyuşmuyor. Ayrıntılar sunucu loglarında.",
                    "Ledger",
                    Guid.Empty,
                    Notification.LedgerMismatchKey,
                    now));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
