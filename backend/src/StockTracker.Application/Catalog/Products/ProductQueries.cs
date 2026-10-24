using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Products;

/// <summary>
/// Search matches name, code or barcode. Sort: name, code, category or createdAt; prefix '-' for descending.
/// </summary>
public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? CategoryId = null,
    TrackingType? TrackingType = null,
    bool IncludeInactive = false,
    string? Sort = null) : IRequest<PagedResult<ProductListItemDto>>, IPagedQuery;

public sealed record GetProductQuery(Guid Id) : IRequest<ProductDto>;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public static readonly IReadOnlyList<string> SortFields = ["name", "code", "category", "createdAt"];

    public GetProductsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.TrackingType).IsInEnum().When(x => x.TrackingType is not null);
        RuleFor(x => x.Sort)
            .Must(s => s is null || SortFields.Contains(s.TrimStart('-')))
            .WithMessage($"Sıralama alanı şunlardan biri olmalıdır: {string.Join(", ", SortFields)}.");
    }
}

public sealed class GetProductsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductListItemDto>>
{
    public Task<PagedResult<ProductListItemDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters applies to the whole query (including the category subquery below),
        // so the soft-delete filter is applied explicitly instead.
        var query = db.Products.IgnoreQueryFilters().AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            var barcode = ProductBarcode.Normalize(request.Search);
            query = query.Where(p =>
                p.Name.ToLower().Contains(search)
                || p.Code.ToLower().Contains(search)
                || p.Barcodes.Any(b => b.Value.Contains(barcode)));
        }

        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (request.TrackingType is { } trackingType)
        {
            query = query.Where(p => p.TrackingType == trackingType);
        }

        var categories = db.Categories;
        var descending = request.Sort?.StartsWith('-') ?? false;
        var ordered = request.Sort?.TrimStart('-') switch
        {
            "code" => descending ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code),
            "createdAt" => descending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            "category" => descending
                ? query.OrderByDescending(p => categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First())
                : query.OrderBy(p => categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First()),
            _ => descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
        };

        return ordered
            .ThenBy(p => p.Id)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.CategoryId,
                categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First(),
                p.Unit,
                p.TrackingType,
                p.IsAssignable,
                p.MinStockLevel,
                p.IsActive,
                p.Barcodes.OrderBy(b => b.CreatedAt).Select(b => b.Value).ToList()))
            .ToPagedResultAsync(request, cancellationToken);
    }
}

public sealed class GetProductQueryHandler(IAppDbContext db, IProductStockChecker stockChecker)
    : IRequestHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var categories = db.Categories;
        var product = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new
            {
                Product = p,
                Category = categories.Where(c => c.Id == p.CategoryId).Select(c => new { c.Name, c.IsActive }).First(),
                Barcodes = p.Barcodes
                    .OrderBy(b => b.CreatedAt)
                    .Select(b => new ProductBarcodeDto(b.Id, b.Value, b.Format))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ürün", request.Id);

        var p = product.Product;
        return new ProductDto(
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.CategoryId,
            product.Category.Name,
            product.Category.IsActive,
            p.Unit,
            p.TrackingType,
            p.IsAssignable,
            p.MinStockLevel,
            p.IsActive,
            await stockChecker.HasStockOrMovementsAsync(p.Id, cancellationToken),
            product.Barcodes,
            p.CreatedAt,
            p.UpdatedAt);
    }
}
