using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Common.Security;
using StockTracker.Application.Locations;
using StockTracker.Domain.Assignments;

namespace StockTracker.Application.Assignments;

/// <summary>
/// Search matches the item code, serial number or product name.
/// Sort: assignedAt, dueDate or returnedAt ('-' for descending; default -assignedAt).
/// </summary>
public sealed record GetAssignmentsQuery(
    int Page = 1,
    int PageSize = 25,
    AssignmentStatus? Status = null,
    Guid? UserId = null,
    Guid? ProductId = null,
    Guid? ItemId = null,
    string? Search = null,
    string? Sort = null) : IRequest<PagedResult<AssignmentDto>>, IPagedQuery;

/// <summary>The current user's open assignments, earliest due date first.</summary>
public sealed record GetMyAssignmentsQuery : IRequest<IReadOnlyList<AssignmentDto>>;

/// <summary>Visible to users who can move stock and to the assignee.</summary>
public sealed record GetAssignmentQuery(Guid Id) : IRequest<AssignmentDto>;

public sealed class GetAssignmentsQueryValidator : AbstractValidator<GetAssignmentsQuery>
{
    public static readonly IReadOnlyList<string> SortFields = ["assignedAt", "dueDate", "returnedAt"];

    public GetAssignmentsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(s => s is null || SortFields.Contains(s.TrimStart('-')))
            .WithMessage($"Sıralama alanı şunlardan biri olmalıdır: {string.Join(", ", SortFields)}.");
    }
}

public sealed class GetAssignmentsQueryHandler(IAppDbContext db, IUserAccountService users, IDateTime dateTime)
    : IRequestHandler<GetAssignmentsQuery, PagedResult<AssignmentDto>>
{
    public async Task<PagedResult<AssignmentDto>> Handle(GetAssignmentsQuery request, CancellationToken cancellationToken)
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

        if (request.UserId is { } userId)
        {
            query = query.Where(a => a.AssigneeUserId == userId);
        }

        if (request.ItemId is { } itemId)
        {
            query = query.Where(a => a.ItemId == itemId);
        }

        var items = db.Items;
        var products = db.Products.IgnoreQueryFilters();

        if (request.ProductId is { } productId)
        {
            query = query.Where(a => items.Any(i => i.Id == a.ItemId && i.ProductId == productId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(a => items.Any(i => i.Id == a.ItemId
                && (i.Code.ToLower().Contains(search)
                    || (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(search))
                    || products.Any(p => p.Id == i.ProductId && p.Name.ToLower().Contains(search)))));
        }

        var descending = request.Sort?.StartsWith('-') ?? request.Sort is null;
        var ordered = request.Sort?.TrimStart('-') switch
        {
            "dueDate" => descending ? query.OrderByDescending(a => a.DueDate) : query.OrderBy(a => a.DueDate),
            "returnedAt" => descending ? query.OrderByDescending(a => a.ReturnedAt) : query.OrderBy(a => a.ReturnedAt),
            _ => descending ? query.OrderByDescending(a => a.AssignedAt) : query.OrderBy(a => a.AssignedAt),
        };

        var page = await AssignmentProjection.Project(db, ordered.ThenByDescending(a => a.Id))
            .ToPagedResultAsync(request, cancellationToken);
        var dtos = await AssignmentProjection.ToDtosAsync(db, users, page.Items, today, cancellationToken);

        return new PagedResult<AssignmentDto>(dtos, page.TotalCount, page.Page, page.PageSize);
    }
}

public sealed class GetMyAssignmentsQueryHandler(
    IAppDbContext db,
    IUserAccountService users,
    ICurrentUser currentUser,
    IDateTime dateTime) : IRequestHandler<GetMyAssignmentsQuery, IReadOnlyList<AssignmentDto>>
{
    public async Task<IReadOnlyList<AssignmentDto>> Handle(GetMyAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = db.Assignments.AsNoTracking()
            .Where(a => a.AssigneeUserId == userId && a.ReturnedAt == null)
            .OrderBy(a => a.DueDate == null)
            .ThenBy(a => a.DueDate)
            .ThenByDescending(a => a.AssignedAt);

        var rows = await AssignmentProjection.Project(db, query).ToListAsync(cancellationToken);
        return await AssignmentProjection.ToDtosAsync(db, users, rows, dateTime.Today(), cancellationToken);
    }
}

public sealed class GetAssignmentQueryHandler(
    IAppDbContext db,
    IUserAccountService users,
    ICurrentUser currentUser,
    IDateTime dateTime) : IRequestHandler<GetAssignmentQuery, AssignmentDto>
{
    public async Task<AssignmentDto> Handle(GetAssignmentQuery request, CancellationToken cancellationToken)
    {
        var query = db.Assignments.AsNoTracking().Where(a => a.Id == request.Id);
        var row = await AssignmentProjection.Project(db, query).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Zimmet", request.Id);

        if (row.Assignment.AssigneeUserId != currentUser.RequireUserId()
            && !currentUser.HasPermission(Permissions.CanMoveStock))
        {
            throw new ForbiddenException();
        }

        return (await AssignmentProjection.ToDtosAsync(db, users, [row], dateTime.Today(), cancellationToken))[0];
    }
}

internal static class AssignmentProjection
{
    public sealed record Row(Assignment Assignment, string ItemCode, string? SerialNumber, Guid ProductId, string ProductCode, string ProductName);

    public static IQueryable<Row> Project(IAppDbContext db, IQueryable<Assignment> query)
    {
        var items = db.Items;
        var products = db.Products.IgnoreQueryFilters();
        return query.Select(a => new
            {
                Assignment = a,
                Item = items.Where(i => i.Id == a.ItemId).Select(i => new { i.Code, i.SerialNumber, i.ProductId }).First(),
            })
            .Select(x => new Row(
                x.Assignment,
                x.Item.Code,
                x.Item.SerialNumber,
                x.Item.ProductId,
                products.Where(p => p.Id == x.Item.ProductId).Select(p => p.Code).First(),
                products.Where(p => p.Id == x.Item.ProductId).Select(p => p.Name).First()));
    }

    public static async Task<IReadOnlyList<AssignmentDto>> ToDtosAsync(
        IAppDbContext db,
        IUserAccountService users,
        IReadOnlyList<Row> rows,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var userIds = rows
            .SelectMany(r => new[] { r.Assignment.AssigneeUserId, r.Assignment.AssignedByUserId, r.Assignment.ReturnedByUserId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var names = await users.GetDisplayNamesAsync(userIds, cancellationToken);
        var paths = await LocationPaths.BuildAsync(
            db,
            rows.Where(r => r.Assignment.ReturnLocationId is not null).Select(r => r.Assignment.ReturnLocationId!.Value),
            cancellationToken);

        return [.. rows.Select(r =>
        {
            var a = r.Assignment;
            return new AssignmentDto(
                a.Id,
                !a.IsOpen ? AssignmentStatus.Returned : a.IsOverdue(today) ? AssignmentStatus.Overdue : AssignmentStatus.Open,
                a.ItemId,
                r.ItemCode,
                r.SerialNumber,
                r.ProductId,
                r.ProductCode,
                r.ProductName,
                a.AssigneeUserId,
                names.GetValueOrDefault(a.AssigneeUserId, "?"),
                a.AssignedByUserId,
                names.GetValueOrDefault(a.AssignedByUserId, "?"),
                a.AssignedAt,
                a.DueDate,
                a.Note,
                a.AssignReferenceId,
                a.ReturnedAt,
                a.ReturnedByUserId,
                a.ReturnedByUserId is { } by ? names.GetValueOrDefault(by, "?") : null,
                a.ReturnLocationId,
                a.ReturnLocationId is { } l ? paths.GetValueOrDefault(l) : null,
                a.ReturnCondition,
                a.ReturnNote,
                a.ReturnReferenceId);
        })];
    }
}
