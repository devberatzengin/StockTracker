using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Locations;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

/// <summary>Search matches the item code or serial number. Sort: code, serialNumber or createdAt ('-' for descending).</summary>
public sealed record GetItemsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? ProductId = null,
    ItemStatus? Status = null,
    Guid? LocationId = null,
    bool IncludeChildren = false,
    string? Sort = null) : IRequest<PagedResult<ItemListItemDto>>, IPagedQuery;

public sealed record GetItemQuery(Guid Id) : IRequest<ItemDto>;

public sealed record UpdateItemCommand(Guid Id, string? SerialNumber) : IRequest;

public sealed class GetItemsQueryValidator : AbstractValidator<GetItemsQuery>
{
    public static readonly IReadOnlyList<string> SortFields = ["code", "serialNumber", "createdAt"];

    public GetItemsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Sort)
            .Must(s => s is null || SortFields.Contains(s.TrimStart('-')))
            .WithMessage($"Sıralama alanı şunlardan biri olmalıdır: {string.Join(", ", SortFields)}.");
    }
}

public sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.SerialNumber)
            .MaximumLength(Item.SerialNumberMaxLength)
            .WithMessage($"Seri numarası en fazla {Item.SerialNumberMaxLength} karakter olabilir.");
    }
}

public sealed class GetItemsQueryHandler(IAppDbContext db) : IRequestHandler<GetItemsQuery, PagedResult<ItemListItemDto>>
{
    public async Task<PagedResult<ItemListItemDto>> Handle(GetItemsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Items.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(i => i.Code.ToLower().Contains(search)
                || (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(search)));
        }

        if (request.ProductId is { } productId)
        {
            query = query.Where(i => i.ProductId == productId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(i => i.Status == status);
        }

        if (request.LocationId is { } locationId)
        {
            if (request.IncludeChildren)
            {
                var path = await db.Locations.IgnoreQueryFilters()
                    .Where(l => l.Id == locationId).Select(l => l.Path).SingleOrDefaultAsync(cancellationToken) ?? "/none/";
                var subtree = db.Locations.IgnoreQueryFilters().Where(l => l.Path.StartsWith(path)).Select(l => l.Id);
                query = query.Where(i => i.CurrentLocationId != null && subtree.Contains(i.CurrentLocationId.Value));
            }
            else
            {
                query = query.Where(i => i.CurrentLocationId == locationId);
            }
        }

        var descending = request.Sort?.StartsWith('-') ?? false;
        query = request.Sort?.TrimStart('-') switch
        {
            "serialNumber" => descending ? query.OrderByDescending(i => i.SerialNumber) : query.OrderBy(i => i.SerialNumber),
            "createdAt" => descending ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            _ => descending ? query.OrderByDescending(i => i.Code) : query.OrderBy(i => i.Code),
        };

        var products = db.Products.IgnoreQueryFilters();
        var page = await query
            .Select(i => new
            {
                Item = i,
                Product = products.Where(p => p.Id == i.ProductId).Select(p => new { p.Code, p.Name }).First(),
            })
            .ToPagedResultAsync(request, cancellationToken);

        var paths = await LocationPaths.BuildAsync(
            db,
            page.Items.Where(x => x.Item.CurrentLocationId is not null).Select(x => x.Item.CurrentLocationId!.Value),
            cancellationToken);

        var dtos = page.Items.Select(x => new ItemListItemDto(
                x.Item.Id,
                x.Item.Code,
                x.Item.ProductId,
                x.Product.Code,
                x.Product.Name,
                x.Item.SerialNumber,
                x.Item.Status,
                x.Item.CurrentLocationId,
                x.Item.CurrentLocationId is { } l ? paths.GetValueOrDefault(l) : null))
            .ToList();

        return new PagedResult<ItemListItemDto>(dtos, page.TotalCount, page.Page, page.PageSize);
    }
}

public sealed class GetItemQueryHandler(IAppDbContext db, IUserAccountService users, IDateTime dateTime)
    : IRequestHandler<GetItemQuery, ItemDto>
{
    public async Task<ItemDto> Handle(GetItemQuery request, CancellationToken cancellationToken)
    {
        var products = db.Products.IgnoreQueryFilters();
        var row = await db.Items.AsNoTracking()
            .Where(i => i.Id == request.Id)
            .Select(i => new
            {
                Item = i,
                Product = products.Where(p => p.Id == i.ProductId).Select(p => new { p.Code, p.Name, p.IsAssignable }).First(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Adet", request.Id);

        var item = row.Item;
        var openAssignment = await db.Assignments.AsNoTracking()
            .Where(a => a.ItemId == item.Id && a.ReturnedAt == null)
            .SingleOrDefaultAsync(cancellationToken);
        var assigneeName = openAssignment is null
            ? null
            : (await users.GetDisplayNamesAsync([openAssignment.AssigneeUserId], cancellationToken))
                .GetValueOrDefault(openAssignment.AssigneeUserId, "?");

        // Valid operations come from the item state; assigning also needs an assignable product.
        var actions = item.AvailableActions
            .Where(a => a != ItemAction.Assign || row.Product.IsAssignable)
            .ToList();

        var paths = item.CurrentLocationId is { } locationId
            ? await LocationPaths.BuildAsync(db, [locationId], cancellationToken)
            : new Dictionary<Guid, string>();

        return new ItemDto(
            item.Id,
            item.Code,
            item.ProductId,
            row.Product.Code,
            row.Product.Name,
            row.Product.IsAssignable,
            item.SerialNumber,
            item.Status,
            item.CurrentLocationId,
            item.CurrentLocationId is { } l ? paths.GetValueOrDefault(l) : null,
            item.CreatedAt,
            actions,
            openAssignment is null
                ? null
                : new ItemOpenAssignmentDto(
                    openAssignment.Id,
                    openAssignment.AssigneeUserId,
                    assigneeName!,
                    openAssignment.AssignedAt,
                    openAssignment.DueDate,
                    openAssignment.IsOverdue(dateTime.Today())));
    }
}

public sealed class UpdateItemCommandHandler(IItemRepository items, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateItemCommand>
{
    public async Task Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Adet", request.Id);

        if (Item.NormalizeSerialNumber(request.SerialNumber) is { } serial)
        {
            var used = await items.GetUsedSerialNumbersAsync(item.ProductId, [serial], item.Id, cancellationToken);
            if (used.Count > 0)
            {
                throw new DomainException(
                    InventoryErrors.SerialNumberTaken,
                    $"\"{serial}\" seri numarası bu üründe zaten kayıtlı.",
                    "serialNumber");
            }
        }

        item.ChangeSerialNumber(request.SerialNumber);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
