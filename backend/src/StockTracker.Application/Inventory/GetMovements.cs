using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

/// <summary>
/// Ledger entries, newest first by default. LocationId matches either source or target.
/// Sort: occurredAt or -occurredAt.
/// </summary>
public sealed record GetMovementsQuery(
    int Page = 1,
    int PageSize = 25,
    DateTime? From = null,
    DateTime? To = null,
    MovementType? Type = null,
    Guid? ProductId = null,
    Guid? ItemId = null,
    Guid? LocationId = null,
    Guid? UserId = null,
    Guid? ReferenceId = null,
    string? Sort = null) : IRequest<PagedResult<MovementDto>>, IPagedQuery;

public sealed class GetMovementsQueryValidator : AbstractValidator<GetMovementsQuery>
{
    public GetMovementsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x.Sort).Must(s => s is null or "occurredAt" or "-occurredAt")
            .WithMessage("Sıralama alanı occurredAt olmalıdır.");
    }
}

public sealed class GetMovementsQueryHandler(IAppDbContext db, IUserAccountService users)
    : IRequestHandler<GetMovementsQuery, PagedResult<MovementDto>>
{
    public async Task<PagedResult<MovementDto>> Handle(GetMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = db.StockMovements.AsNoTracking();

        if (request.From is { } from)
        {
            query = query.Where(m => m.OccurredAt >= from.ToUniversalTime());
        }

        if (request.To is { } to)
        {
            query = query.Where(m => m.OccurredAt <= to.ToUniversalTime());
        }

        if (request.Type is { } type)
        {
            query = query.Where(m => m.Type == type);
        }

        if (request.ProductId is { } productId)
        {
            query = query.Where(m => m.ProductId == productId);
        }

        if (request.ItemId is { } itemId)
        {
            query = query.Where(m => m.ItemId == itemId);
        }

        if (request.LocationId is { } locationId)
        {
            query = query.Where(m => m.FromLocationId == locationId || m.ToLocationId == locationId);
        }

        if (request.UserId is { } userId)
        {
            query = query.Where(m => m.UserId == userId);
        }

        if (request.ReferenceId is { } referenceId)
        {
            query = query.Where(m => m.ReferenceId == referenceId);
        }

        // Movements of one operation share OccurredAt. A status change there (faulty return) follows
        // the movement that caused it; ids alone do not order them (v7 ids are random within a millisecond).
        query = request.Sort == "occurredAt"
            ? query.OrderBy(m => m.OccurredAt).ThenBy(m => m.Type == MovementType.StatusChange).ThenBy(m => m.Id)
            : query.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Type == MovementType.StatusChange).ThenByDescending(m => m.Id);

        var products = db.Products.IgnoreQueryFilters();
        var items = db.Items;
        var locations = db.Locations.IgnoreQueryFilters();
        var assignments = db.Assignments;

        var page = await query
            .Select(m => new
            {
                Movement = m,
                Product = products.Where(p => p.Id == m.ProductId).Select(p => new { p.Code, p.Name, p.Unit }).First(),
                Item = items.Where(i => i.Id == m.ItemId).Select(i => new { i.Code, i.SerialNumber }).FirstOrDefault(),
                FromName = locations.Where(l => l.Id == m.FromLocationId).Select(l => l.Name).FirstOrDefault(),
                ToName = locations.Where(l => l.Id == m.ToLocationId).Select(l => l.Name).FirstOrDefault(),
                // Assign and Return movements name the assignment's holder.
                AssigneeId = m.Type == MovementType.Assign
                    ? assignments.Where(a => a.ItemId == m.ItemId && a.AssignReferenceId == m.ReferenceId)
                        .Select(a => (Guid?)a.AssigneeUserId).FirstOrDefault()
                    : m.Type == MovementType.Return
                        ? assignments.Where(a => a.ItemId == m.ItemId && a.ReturnReferenceId == m.ReferenceId)
                            .Select(a => (Guid?)a.AssigneeUserId).FirstOrDefault()
                        : null,
            })
            .ToPagedResultAsync(request, cancellationToken);

        var userNames = await users.GetDisplayNamesAsync(
            [.. page.Items.Select(x => x.Movement.UserId)
                .Concat(page.Items.Where(x => x.AssigneeId is not null).Select(x => x.AssigneeId!.Value))
                .Distinct()],
            cancellationToken);

        var dtos = page.Items.Select(x => new MovementDto(
                x.Movement.Id,
                x.Movement.Type,
                x.Movement.OccurredAt,
                x.Movement.ProductId,
                x.Product.Code,
                x.Product.Name,
                x.Product.Unit,
                x.Movement.ItemId,
                x.Item?.Code,
                x.Item?.SerialNumber,
                x.Movement.FromLocationId,
                x.FromName,
                x.Movement.ToLocationId,
                x.ToName,
                x.Movement.Quantity,
                x.Movement.Reason,
                x.Movement.Note,
                x.Movement.ReferenceId,
                x.Movement.UserId,
                userNames.GetValueOrDefault(x.Movement.UserId, "?"),
                x.Movement.FromStatus,
                x.Movement.ToStatus,
                x.AssigneeId is { } assigneeId ? userNames.GetValueOrDefault(assigneeId, "?") : null))
            .ToList();

        return new PagedResult<MovementDto>(dtos, page.TotalCount, page.Page, page.PageSize);
    }
}
