using Microsoft.EntityFrameworkCore;

namespace StockTracker.Application.Common.Models;

public static class PagingExtensions
{
    /// <summary>Counts and pages an ordered query into the standard list response.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        IPagedQuery paging,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, paging.Page, paging.PageSize);
    }
}
