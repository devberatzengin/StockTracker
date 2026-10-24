using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Categories.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.NormalizedName == normalizedName && c.Id != excludeId, cancellationToken);

    public void Add(Category category) => db.Categories.Add(category);
}
