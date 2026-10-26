using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Locations;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Counts;

/// <summary>Newest first. LocationId matches the counted root location.</summary>
public sealed record GetCountsQuery(
    int Page = 1,
    int PageSize = 25,
    CountStatus? Status = null,
    Guid? LocationId = null) : IRequest<PagedResult<CountListItemDto>>, IPagedQuery;

public sealed record GetCountQuery(Guid Id) : IRequest<CountDto>;

/// <summary>Differences against the stock now, with the allowed decisions per line.</summary>
public sealed record GetCountReviewQuery(Guid Id) : IRequest<CountReviewDto>;

public sealed class GetCountsQueryValidator : AbstractValidator<GetCountsQuery>
{
    public GetCountsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

public sealed class GetCountsQueryHandler(IAppDbContext db, IUserAccountService users)
    : IRequestHandler<GetCountsQuery, PagedResult<CountListItemDto>>
{
    public async Task<PagedResult<CountListItemDto>> Handle(GetCountsQuery request, CancellationToken cancellationToken)
    {
        var query = db.CountSessions.AsNoTracking();
        if (request.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        if (request.LocationId is { } locationId)
        {
            query = query.Where(c => c.LocationId == locationId);
        }

        var page = await query
            .OrderByDescending(c => c.StartedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.LocationId,
                c.IncludeChildren,
                c.Status,
                c.StartedBy,
                c.StartedAt,
                c.ApprovedAt,
                Expected = c.Lines.Count(l => l.LineType == CountLineType.Expected),
                CountedExpected = c.Lines.Count(l => l.LineType == CountLineType.Expected && l.CountedQuantity != null),
                Unexpected = c.Lines.Count(l => l.LineType == CountLineType.Unexpected),
            })
            .ToPagedResultAsync(request, cancellationToken);

        var paths = await LocationPaths.BuildAsync(db, page.Items.Select(c => c.LocationId), cancellationToken);
        var names = await users.GetDisplayNamesAsync([.. page.Items.Select(c => c.StartedBy).Distinct()], cancellationToken);

        return new PagedResult<CountListItemDto>(
            [.. page.Items.Select(c => new CountListItemDto(
                c.Id,
                c.LocationId,
                paths.GetValueOrDefault(c.LocationId, "?"),
                c.IncludeChildren,
                c.Status,
                names.GetValueOrDefault(c.StartedBy, "?"),
                c.StartedAt,
                c.ApprovedAt,
                c.Expected,
                c.CountedExpected,
                c.Unexpected))],
            page.TotalCount,
            page.Page,
            page.PageSize);
    }
}

public sealed class GetCountQueryHandler(IAppDbContext db, IUserAccountService users)
    : IRequestHandler<GetCountQuery, CountDto>
{
    private const int RecentScanCount = 20;

    public async Task<CountDto> Handle(GetCountQuery request, CancellationToken cancellationToken)
    {
        var session = await CountReadModel.LoadAsync(db, request.Id, cancellationToken);
        var lines = await CountReadModel.LinesAsync(db, session.Lines, cancellationToken);
        var lastScanOfLine = session.Scans.GroupBy(s => s.CountLineId).ToDictionary(g => g.Key, g => g.Max(s => s.Sequence));
        var recent = session.Scans.OrderByDescending(s => s.Sequence).Take(RecentScanCount).ToList();

        var names = await users.GetDisplayNamesAsync(
            [.. recent.Select(s => s.ScannedBy).Append(session.StartedBy).Concat(session.ApprovedBy is { } a ? [a] : []).Distinct()],
            cancellationToken);
        var root = await LocationPaths.BuildAsync(db, [session.LocationId], cancellationToken);
        var rootName = await db.Locations.IgnoreQueryFilters()
            .Where(l => l.Id == session.LocationId).Select(l => l.Name).SingleAsync(cancellationToken);

        return new CountDto(
            session.Id,
            session.LocationId,
            rootName,
            root.GetValueOrDefault(session.LocationId, rootName),
            session.IncludeChildren,
            session.Status,
            session.Note,
            session.StartedBy,
            names.GetValueOrDefault(session.StartedBy, "?"),
            session.StartedAt,
            session.ApprovedBy is { } approvedBy ? names.GetValueOrDefault(approvedBy, "?") : null,
            session.ApprovedAt,
            session.ApprovalReferenceId,
            CountRules.Progress(session),
            [.. session.Lines.Select(l => lines[l.Id])],
            [.. recent.Select(s =>
            {
                var line = lines[s.CountLineId];
                return new CountScanDto(
                    s.Id,
                    s.CountLineId,
                    s.Sequence,
                    s.Quantity,
                    s.ScannedAt,
                    names.GetValueOrDefault(s.ScannedBy, "?"),
                    line.ProductName,
                    line.Unit,
                    line.ItemCode,
                    line.LocationName,
                    line.LineType,
                    session.Status == CountStatus.Counting && lastScanOfLine[s.CountLineId] == s.Sequence);
            })]);
    }
}

public sealed class GetCountReviewQueryHandler(IAppDbContext db) : IRequestHandler<GetCountReviewQuery, CountReviewDto>
{
    public async Task<CountReviewDto> Handle(GetCountReviewQuery request, CancellationToken cancellationToken)
    {
        var session = await CountReadModel.LoadAsync(db, request.Id, cancellationToken);

        var quantityLines = session.Lines.Where(l => l.ItemId is null).ToList();
        var productIds = quantityLines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = quantityLines.Select(l => l.LocationId).Distinct().ToList();
        var balances = (await db.StockBalances.AsNoTracking()
                .Where(b => productIds.Contains(b.ProductId) && locationIds.Contains(b.LocationId))
                .Select(b => new { b.ProductId, b.LocationId, b.Quantity })
                .ToListAsync(cancellationToken))
            .ToDictionary(b => (b.ProductId, b.LocationId), b => b.Quantity);

        var itemIds = session.Lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        var items = (await db.Items.AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Status, i.CurrentLocationId })
                .ToListAsync(cancellationToken))
            .ToDictionary(i => i.Id, i => new ItemSnapshot(i.Status, i.CurrentLocationId));

        var review = CountReview.Build(session, balances, items);
        var lines = await CountReadModel.LinesAsync(db, session.Lines, cancellationToken);
        var itemPaths = await LocationPaths.BuildAsync(
            db,
            items.Values.Where(i => i.LocationId is not null).Select(i => i.LocationId!.Value),
            cancellationToken);

        var movements = await CountReadModel.MovementsSinceStartAsync(db, session.Id, cancellationToken);

        return new CountReviewDto(
            session.Id,
            session.Status,
            [.. review.Select(r => new CountReviewLineDto(
                lines[r.Line.Id],
                r.State,
                r.CurrentQuantity,
                r.Difference,
                r.Options,
                r.Item?.Status,
                r.Item?.LocationId is { } at ? itemPaths.GetValueOrDefault(at) : null))],
            await movements.CountAsync(cancellationToken));
    }
}

/// <summary>Read helpers shared by the count queries and the movements filter.</summary>
public static class CountReadModel
{
    public static async Task<CountSession> LoadAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.CountSessions.AsNoTracking()
            .Include(c => c.Lines)
            .Include(c => c.Scans)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new NotFoundException("Sayım", id);

    public static async Task<IReadOnlyDictionary<Guid, CountLineDto>> LinesAsync(
        IAppDbContext db,
        IReadOnlyCollection<CountLine> lines,
        CancellationToken cancellationToken)
    {
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var itemIds = lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        var locationIds = lines.Select(l => l.LocationId).Distinct().ToList();

        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Name, p.Unit })
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.SerialNumber })
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var names = await db.Locations.IgnoreQueryFilters().AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);
        var paths = await LocationPaths.BuildAsync(db, locationIds, cancellationToken);

        return lines.ToDictionary(l => l.Id, l =>
        {
            var product = products[l.ProductId];
            var item = l.ItemId is { } itemId ? items[itemId] : null;
            return new CountLineDto(
                l.Id,
                l.LineType,
                l.LocationId,
                names.GetValueOrDefault(l.LocationId, "?"),
                paths.GetValueOrDefault(l.LocationId, "?"),
                l.ProductId,
                product.Code,
                product.Name,
                product.Unit,
                l.ItemId,
                item?.Code,
                item?.SerialNumber,
                l.ExpectedQuantity,
                l.CountedQuantity,
                l.Resolution);
        });
    }

    /// <summary>
    /// Movements touching the count's scope (its current locations) after it started, except the
    /// approval's own movements.
    /// </summary>
    public static async Task<IQueryable<StockMovement>> MovementsSinceStartAsync(
        IAppDbContext db,
        Guid countId,
        CancellationToken cancellationToken)
    {
        var count = await db.CountSessions.AsNoTracking()
            .Where(c => c.Id == countId)
            .Select(c => new { c.LocationId, c.IncludeChildren, c.StartedAt, c.ApprovalReferenceId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sayım", countId);

        var locations = db.Locations.IgnoreQueryFilters();
        var rootPath = await locations.Where(l => l.Id == count.LocationId).Select(l => l.Path).SingleAsync(cancellationToken);
        var scope = count.IncludeChildren
            ? locations.Where(l => l.Path.StartsWith(rootPath)).Select(l => l.Id)
            : locations.Where(l => l.Id == count.LocationId).Select(l => l.Id);

        var query = db.StockMovements.AsNoTracking()
            .Where(m => m.OccurredAt >= count.StartedAt
                && ((m.FromLocationId != null && scope.Contains(m.FromLocationId.Value))
                    || (m.ToLocationId != null && scope.Contains(m.ToLocationId.Value))));

        if (count.ApprovalReferenceId is { } approval)
        {
            query = query.Where(m => m.ReferenceId != approval);
        }

        return query;
    }
}
