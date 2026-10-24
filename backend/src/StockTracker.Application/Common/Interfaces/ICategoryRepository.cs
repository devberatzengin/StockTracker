using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Common.Interfaces;

public interface ICategoryRepository
{
    /// <summary>Includes inactive categories.</summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether another active category already uses the (normalized) name.</summary>
    Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludeId, CancellationToken cancellationToken);

    void Add(Category category);
}
