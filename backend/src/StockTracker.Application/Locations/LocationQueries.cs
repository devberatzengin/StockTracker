using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Locations;

/// <summary>The whole tree in one response (not paged), roots and children sorted by name.</summary>
public sealed record GetLocationTreeQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<LocationTreeNodeDto>>;

public sealed record GetLocationQuery(Guid Id) : IRequest<LocationDto>;

public sealed class GetLocationTreeQueryHandler(IAppDbContext db)
    : IRequestHandler<GetLocationTreeQuery, IReadOnlyList<LocationTreeNodeDto>>
{
    public async Task<IReadOnlyList<LocationTreeNodeDto>> Handle(
        GetLocationTreeQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.Locations.IgnoreQueryFilters().AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(l => l.IsActive);
        }

        var rows = await query
            .Select(l => new { l.Id, l.Code, l.Name, l.Type, l.ParentId, l.IsActive })
            .ToListAsync(cancellationToken);

        var byParent = rows.ToLookup(r => r.ParentId);
        var comparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

        List<LocationTreeNodeDto> Build(Guid? parentId) =>
            byParent[parentId]
                .OrderBy(r => r.Name, comparer)
                .Select(r => new LocationTreeNodeDto(r.Id, r.Code, r.Name, r.Type, r.IsActive, Build(r.Id)))
                .ToList();

        // Inactive nodes are hidden with their subtree, so every visible node's parent is visible too.
        return Build(null);
    }
}

public sealed class GetLocationQueryHandler(IAppDbContext db) : IRequestHandler<GetLocationQuery, LocationDto>
{
    public async Task<LocationDto> Handle(GetLocationQuery request, CancellationToken cancellationToken)
    {
        var locations = db.Locations.IgnoreQueryFilters().AsNoTracking();

        var location = await locations.SingleOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Lokasyon", request.Id);

        var ancestorIds = location.Path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .Where(id => id != location.Id)
            .ToList();

        var ancestors = await locations
            .Where(l => ancestorIds.Contains(l.Id))
            .OrderBy(l => l.Depth)
            .Select(l => new LocationRefDto(l.Id, l.Code, l.Name))
            .ToListAsync(cancellationToken);

        var children = await locations
            .Where(l => l.ParentId == location.Id)
            .OrderBy(l => l.Name)
            .Select(l => new LocationChildDto(l.Id, l.Code, l.Name, l.Type, l.IsActive))
            .ToListAsync(cancellationToken);

        return new LocationDto(
            location.Id,
            location.Code,
            location.Name,
            location.Type,
            location.ParentId,
            location.Depth,
            location.IsActive,
            ancestors,
            children,
            location.CreatedAt);
    }
}
