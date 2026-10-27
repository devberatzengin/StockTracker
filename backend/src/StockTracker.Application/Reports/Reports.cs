using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Assignments;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Inventory;
using StockTracker.Application.Locations;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Application.Reports.ReportLabels;

namespace StockTracker.Application.Reports;

/// <summary>Active products with totals; second sheet: where the stock is.</summary>
public sealed record GetStockReportQuery : IRequest<ReportFile>;

/// <summary>Movements between two Istanbul days (both inclusive), oldest first.</summary>
public sealed record GetMovementReportQuery(DateOnly From, DateOnly To) : IRequest<ReportFile>;

/// <summary>All assignments (optionally one status), newest first.</summary>
public sealed record GetAssignmentReportQuery(AssignmentStatus? Status = null) : IRequest<ReportFile>;

public sealed class GetMovementReportQueryValidator : AbstractValidator<GetMovementReportQuery>
{
    public GetMovementReportQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("Başlangıç tarihini seçin.");
        RuleFor(x => x.To).NotEmpty().WithMessage("Bitiş tarihini seçin.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}

public sealed class GetAssignmentReportQueryValidator : AbstractValidator<GetAssignmentReportQuery>
{
    public GetAssignmentReportQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public static class ReportGuard
{
    /// <summary>Checked before loading rows, so a huge report fails fast.</summary>
    public static void EnsureRowLimit(int rows, string hint)
    {
        if (rows > ReportLimits.MaxRows)
        {
            throw new DomainException(
                ReportLimits.TooManyRows,
                $"Rapor {rows:N0} satır içeriyor; en fazla {ReportLimits.MaxRows:N0} satır indirilebilir. {hint}");
        }
    }

    public static string FileName(string name, IDateTime clock) => $"{name}-{clock.Today():yyyy-MM-dd}.xlsx";
}

public sealed class ReportQueryHandlers(
    IAppDbContext db,
    IUserAccountService users,
    IReportWriter writer,
    IDateTime dateTime)
    : IRequestHandler<GetStockReportQuery, ReportFile>,
      IRequestHandler<GetMovementReportQuery, ReportFile>,
      IRequestHandler<GetAssignmentReportQuery, ReportFile>
{
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    public async Task<ReportFile> Handle(GetStockReportQuery request, CancellationToken cancellationToken)
    {
        var totals = StockTotals.Query(db, db.Products);
        var balances = db.StockBalances.AsNoTracking().Where(b => b.Quantity > 0);
        var placedItems = db.Items.AsNoTracking()
            .Where(i => i.CurrentLocationId != null && (i.Status == ItemStatus.InStock || i.Status == ItemStatus.InRepair));

        ReportGuard.EnsureRowLimit(await totals.CountAsync(cancellationToken), string.Empty);
        ReportGuard.EnsureRowLimit(
            await balances.CountAsync(cancellationToken)
                + await placedItems.Select(i => new { i.ProductId, i.CurrentLocationId }).Distinct().CountAsync(cancellationToken),
            string.Empty);

        var categories = await db.Categories.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var products = await totals.OrderBy(t => t.Code).ToListAsync(cancellationToken);

        var quantityRows = await balances
            .Select(b => new { b.ProductId, b.LocationId, b.Quantity })
            .ToListAsync(cancellationToken);
        var itemRows = await placedItems
            .GroupBy(i => new { i.ProductId, LocationId = i.CurrentLocationId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Quantity = (decimal)g.Count() })
            .ToListAsync(cancellationToken);
        var placements = quantityRows.Concat(itemRows).ToList();

        var locationIds = placements.Select(p => p.LocationId).Distinct().ToList();
        var paths = await LocationPaths.BuildAsync(db, locationIds, cancellationToken);
        var locationCodes = await db.Locations.IgnoreQueryFilters().AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var allProducts = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Select(p => new { p.Id, p.Code, p.Name, p.Unit })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var summary = new ReportSheet(
            "Stok durumu",
            [
                new("Ürün kodu", ReportColumnType.Text, 14),
                new("Ad", ReportColumnType.Text, 32),
                new("Kategori", ReportColumnType.Text, 20),
                new("Takip tipi", ReportColumnType.Text, 14),
                new("Birim", ReportColumnType.Text, 10),
                new("Toplam", ReportColumnType.Number, 12),
                new("Minimum", ReportColumnType.Number, 12),
                new("Kritik mi", ReportColumnType.Text, 10),
            ],
            [.. products.Select(p => new object?[]
            {
                p.Code,
                p.Name,
                categories.GetValueOrDefault(p.CategoryId),
                Tracking(p.TrackingType),
                Unit(p.Unit),
                p.Total,
                p.MinStockLevel,
                YesNo(StockTotals.IsLow(p)),
            })]);

        var distribution = new ReportSheet(
            "Lokasyon dağılımı",
            [
                new("Ürün kodu", ReportColumnType.Text, 14),
                new("Ürün", ReportColumnType.Text, 32),
                new("Lokasyon kodu", ReportColumnType.Text, 14),
                new("Lokasyon", ReportColumnType.Text, 44),
                new("Miktar", ReportColumnType.Number, 12),
                new("Birim", ReportColumnType.Text, 10),
            ],
            [.. placements
                .Select(p => new { Row = p, Product = allProducts[p.ProductId], Path = paths.GetValueOrDefault(p.LocationId, "?") })
                .OrderBy(x => x.Product.Code, StringComparer.Ordinal)
                .ThenBy(x => x.Path, StringComparer.Ordinal)
                .Select(x => new object?[]
                {
                    x.Product.Code,
                    x.Product.Name,
                    locationCodes.GetValueOrDefault(x.Row.LocationId),
                    x.Path,
                    x.Row.Quantity,
                    Unit(x.Product.Unit),
                })]);

        return new ReportFile(writer.Write([summary, distribution]), ReportGuard.FileName("stok-durumu", dateTime));
    }

    public async Task<ReportFile> Handle(GetMovementReportQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = DateTime.SpecifyKind(request.From.ToDateTime(TimeOnly.MinValue) - IstanbulOffset, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue) - IstanbulOffset, DateTimeKind.Utc);
        var movements = db.StockMovements.AsNoTracking().Where(m => m.OccurredAt >= fromUtc && m.OccurredAt < toUtc);

        ReportGuard.EnsureRowLimit(await movements.CountAsync(cancellationToken), "Tarih aralığını daraltın.");

        var products = db.Products.IgnoreQueryFilters();
        var items = db.Items;
        var rows = await movements
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.ReferenceId)
            .Select(m => new
            {
                Movement = m,
                Product = products.Where(p => p.Id == m.ProductId).Select(p => p.Name).First(),
                Item = items.Where(i => i.Id == m.ItemId).Select(i => new { i.Code, i.SerialNumber }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var paths = await LocationPaths.BuildAsync(
            db,
            rows.SelectMany(r => new[] { r.Movement.FromLocationId, r.Movement.ToLocationId }).OfType<Guid>().Distinct(),
            cancellationToken);
        var names = await users.GetDisplayNamesAsync([.. rows.Select(r => r.Movement.UserId).Distinct()], cancellationToken);
        string? Path(Guid? id) => id is { } value ? paths.GetValueOrDefault(value) : null;

        var sheet = new ReportSheet(
            "Hareketler",
            [
                new("Tarih", ReportColumnType.DateTime, 17),
                new("Tip", ReportColumnType.Text, 16),
                new("Ürün", ReportColumnType.Text, 28),
                new("Item kodu", ReportColumnType.Text, 13),
                new("Seri no", ReportColumnType.Text, 16),
                new("Kaynak", ReportColumnType.Text, 36),
                new("Hedef", ReportColumnType.Text, 36),
                new("Miktar", ReportColumnType.Number, 10),
                new("Kullanıcı", ReportColumnType.Text, 20),
                new("Sebep", ReportColumnType.Text, 12),
                new("Not", ReportColumnType.Text, 30),
                new("Referans", ReportColumnType.Text, 38),
            ],
            [.. rows.Select(r => new object?[]
            {
                Istanbul(r.Movement.OccurredAt),
                Movement(r.Movement.Type),
                r.Product,
                r.Item?.Code,
                r.Item?.SerialNumber,
                Path(r.Movement.FromLocationId),
                Path(r.Movement.ToLocationId),
                r.Movement.Quantity,
                names.GetValueOrDefault(r.Movement.UserId, "?"),
                Reason(r.Movement.Reason),
                r.Movement.Note,
                r.Movement.ReferenceId.ToString(),
            })]);

        return new ReportFile(
            writer.Write([sheet]),
            $"hareketler-{request.From:yyyy-MM-dd}_{request.To:yyyy-MM-dd}.xlsx");
    }

    public async Task<ReportFile> Handle(GetAssignmentReportQuery request, CancellationToken cancellationToken)
    {
        var today = dateTime.Today();
        var query = db.Assignments.AsNoTracking();
        query = request.Status switch
        {
            AssignmentStatus.Open => query.Where(a => a.ReturnedAt == null),
            AssignmentStatus.Overdue => query.Where(a => a.ReturnedAt == null && a.DueDate != null && a.DueDate < today),
            AssignmentStatus.Returned => query.Where(a => a.ReturnedAt != null),
            _ => query,
        };

        ReportGuard.EnsureRowLimit(await query.CountAsync(cancellationToken), string.Empty);

        var products = db.Products.IgnoreQueryFilters();
        var rows = await query
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new
            {
                Assignment = a,
                Item = db.Items.Where(i => i.Id == a.ItemId)
                    .Select(i => new { i.Code, i.SerialNumber, Product = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First() })
                    .First(),
            })
            .ToListAsync(cancellationToken);
        var people = await users.GetSummariesAsync([.. rows.Select(r => r.Assignment.AssigneeUserId).Distinct()], cancellationToken);

        static string Status(Domain.Assignments.Assignment a, DateOnly today) =>
            !a.IsOpen ? "İade edildi" : a.IsOverdue(today) ? "Gecikmiş" : "Açık";

        var sheet = new ReportSheet(
            "Zimmetler",
            [
                new("Item kodu", ReportColumnType.Text, 13),
                new("Ürün", ReportColumnType.Text, 28),
                new("Seri no", ReportColumnType.Text, 16),
                new("Alan kişi", ReportColumnType.Text, 22),
                new("Departman", ReportColumnType.Text, 18),
                new("Veriliş", ReportColumnType.DateTime, 17),
                new("Teslim", ReportColumnType.Date, 12),
                new("İade", ReportColumnType.DateTime, 17),
                new("Durum", ReportColumnType.Text, 12),
            ],
            [.. rows.Select(r =>
            {
                var person = people.GetValueOrDefault(r.Assignment.AssigneeUserId);
                return new object?[]
                {
                    r.Item.Code,
                    r.Item.Product,
                    r.Item.SerialNumber,
                    person?.FullName ?? "?",
                    person?.Department,
                    Istanbul(r.Assignment.AssignedAt),
                    r.Assignment.DueDate,
                    Istanbul(r.Assignment.ReturnedAt),
                    Status(r.Assignment, today),
                };
            })]);

        return new ReportFile(writer.Write([sheet]), ReportGuard.FileName("zimmetler", dateTime));
    }
}
