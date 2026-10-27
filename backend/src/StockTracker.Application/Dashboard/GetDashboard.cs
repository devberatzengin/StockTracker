using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Inventory;
using StockTracker.Domain.Inventory;
using Unit = StockTracker.Domain.Catalog.Unit;

namespace StockTracker.Application.Dashboard;

public sealed record DashboardCardsDto(
    int ActiveProducts,
    int TotalItems,
    int LowStockProducts,
    int OpenAssignments,
    int OverdueAssignments,
    int ItemsInRepair);

/// <summary>Movement counts of one Istanbul day, per type (stacked bar chart row).</summary>
public sealed record DailyMovementsDto(
    DateOnly Date,
    int In,
    int Out,
    int Transfer,
    int Adjustment,
    int Assign,
    int Return,
    int StatusChange);

public sealed record TopProductDto(Guid ProductId, string Code, string Name, int MovementCount);

public sealed record ItemStatusCountsDto(int InStock, int Assigned, int InRepair);

public sealed record LowStockProductDto(
    Guid ProductId,
    string Code,
    string Name,
    Unit Unit,
    decimal Total,
    decimal MinStockLevel,
    decimal Shortage);

public sealed record OverdueAssignmentDto(
    Guid AssignmentId,
    Guid ItemId,
    string ItemCode,
    string ProductName,
    string AssigneeName,
    DateOnly DueDate,
    int DaysOverdue);

public sealed record DashboardDto(
    DashboardCardsDto Cards,
    IReadOnlyList<DailyMovementsDto> DailyMovements,
    IReadOnlyList<TopProductDto> TopProducts,
    ItemStatusCountsDto ItemStatuses,
    IReadOnlyList<LowStockProductDto> LowStock,
    IReadOnlyList<OverdueAssignmentDto> OverdueAssignments);

/// <summary>Everything the dashboard shows, in one request; "today" and days are Europe/Istanbul.</summary>
public sealed record GetDashboardQuery : IRequest<DashboardDto>;

public sealed class GetDashboardQueryHandler(IAppDbContext db, IUserAccountService users, IDateTime dateTime)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public const int Days = 30;
    public const int TopProductCount = 10;
    public const int LowStockListSize = 20;
    public const int OverdueListSize = 10;

    // Turkey has used UTC+3 all year since 2016 (same as BusinessDay).
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = dateTime.Today();
        var firstDay = today.AddDays(-(Days - 1));
        var fromUtc = DateTime.SpecifyKind(firstDay.ToDateTime(TimeOnly.MinValue) - IstanbulOffset, DateTimeKind.Utc);

        var totals = StockTotals.Query(db, db.Products);
        var items = db.Items.AsNoTracking();
        var openAssignments = db.Assignments.AsNoTracking().Where(a => a.ReturnedAt == null);

        var cards = new DashboardCardsDto(
            await db.Products.CountAsync(cancellationToken),
            await items.CountAsync(i => i.Status != ItemStatus.Disposed, cancellationToken),
            await totals.WhereLow().CountAsync(cancellationToken),
            await openAssignments.CountAsync(cancellationToken),
            await openAssignments.CountAsync(a => a.DueDate != null && a.DueDate < today, cancellationToken),
            await items.CountAsync(i => i.Status == ItemStatus.InRepair, cancellationToken));

        var recent = db.StockMovements.AsNoTracking().Where(m => m.OccurredAt >= fromUtc);

        // Grouped in memory by Istanbul day: only (time, type) pairs of 30 days are loaded.
        var stamps = await recent.Select(m => new { m.OccurredAt, m.Type }).ToListAsync(cancellationToken);
        var byDay = stamps
            .GroupBy(s => DateOnly.FromDateTime(s.OccurredAt + IstanbulOffset))
            .ToDictionary(g => g.Key, g => g.GroupBy(s => s.Type).ToDictionary(t => t.Key, t => t.Count()));
        var daily = Enumerable.Range(0, Days).Select(i =>
        {
            var day = firstDay.AddDays(i);
            var counts = byDay.GetValueOrDefault(day) ?? [];
            int Count(MovementType type) => counts.GetValueOrDefault(type);
            return new DailyMovementsDto(
                day,
                Count(MovementType.In),
                Count(MovementType.Out),
                Count(MovementType.Transfer),
                Count(MovementType.Adjustment),
                Count(MovementType.Assign),
                Count(MovementType.Return),
                Count(MovementType.StatusChange));
        }).ToList();

        var products = db.Products.IgnoreQueryFilters();
        var top = await recent
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(TopProductCount)
            .Select(g => new
            {
                g.ProductId,
                g.Count,
                Product = products.Where(p => p.Id == g.ProductId).Select(p => new { p.Code, p.Name }).First(),
            })
            .ToListAsync(cancellationToken);

        var statuses = await items
            .Where(i => i.Status != ItemStatus.Disposed)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var lowStock = await totals.WhereLow()
            .OrderByDescending(t => t.MinStockLevel - t.Total)
            .ThenBy(t => t.Name)
            .Take(LowStockListSize)
            .ToListAsync(cancellationToken);

        var overdue = await openAssignments
            .Where(a => a.DueDate != null && a.DueDate < today)
            .OrderBy(a => a.DueDate)
            .ThenBy(a => a.AssignedAt)
            .Take(OverdueListSize)
            .Select(a => new
            {
                a.Id,
                a.ItemId,
                a.AssigneeUserId,
                DueDate = a.DueDate!.Value,
                Item = db.Items.Where(i => i.Id == a.ItemId)
                    .Select(i => new { i.Code, Product = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First() })
                    .First(),
            })
            .ToListAsync(cancellationToken);
        var names = await users.GetDisplayNamesAsync([.. overdue.Select(o => o.AssigneeUserId).Distinct()], cancellationToken);

        return new DashboardDto(
            cards,
            daily,
            [.. top.Select(t => new TopProductDto(t.ProductId, t.Product.Code, t.Product.Name, t.Count))],
            new ItemStatusCountsDto(
                statuses.GetValueOrDefault(ItemStatus.InStock),
                statuses.GetValueOrDefault(ItemStatus.Assigned),
                statuses.GetValueOrDefault(ItemStatus.InRepair)),
            [.. lowStock.Select(t => new LowStockProductDto(
                t.ProductId, t.Code, t.Name, t.Unit, t.Total, t.MinStockLevel!.Value, t.MinStockLevel!.Value - t.Total))],
            [.. overdue.Select(o => new OverdueAssignmentDto(
                o.Id,
                o.ItemId,
                o.Item.Code,
                o.Item.Product,
                names.GetValueOrDefault(o.AssigneeUserId, "?"),
                o.DueDate,
                today.DayNumber - o.DueDate.DayNumber))]);
    }
}
