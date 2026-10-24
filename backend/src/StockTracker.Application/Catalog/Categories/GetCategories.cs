using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;

namespace StockTracker.Application.Catalog.Categories;

/// <summary>Sort: name or activeProductCount; prefix with '-' for descending.</summary>
public sealed record GetCategoriesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeInactive = false,
    string? Sort = null) : IRequest<PagedResult<CategoryDto>>, IPagedQuery;

public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public static readonly IReadOnlyList<string> SortFields = ["name", "activeProductCount"];

    public GetCategoriesQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(s => s is null || SortFields.Contains(s.TrimStart('-')))
            .WithMessage($"Sıralama alanı şunlardan biri olmalıdır: {string.Join(", ", SortFields)}.");
    }
}

public sealed class GetCategoriesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetCategoriesQuery, PagedResult<CategoryDto>>
{
    public Task<PagedResult<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters applies to the whole query (including the product count subquery),
        // so both soft-delete conditions are written explicitly.
        var query = db.Categories.IgnoreQueryFilters().AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(search));
        }

        var descending = request.Sort?.StartsWith('-') ?? false;
        var ordered = request.Sort?.TrimStart('-') switch
        {
            "activeProductCount" => descending
                ? query.OrderByDescending(c => db.Products.Count(p => p.CategoryId == c.Id && p.IsActive))
                : query.OrderBy(c => db.Products.Count(p => p.CategoryId == c.Id && p.IsActive)),
            _ => descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
        };

        return ordered
            .ThenBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.IsActive, db.Products.Count(p => p.CategoryId == c.Id && p.IsActive)))
            .ToPagedResultAsync(request, cancellationToken);
    }
}
