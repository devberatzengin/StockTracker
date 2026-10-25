using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Common.Interfaces;

public interface IProductRepository
{
    /// <summary>Loads the aggregate with its barcodes; includes inactive products.</summary>
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>System-wide check across all products, active or not.</summary>
    /// <summary>Loads several products (without barcodes); includes inactive ones.</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> BarcodeExistsAsync(string normalizedValue, CancellationToken cancellationToken);

    Task<bool> HasActiveProductsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken);

    void Add(Product product);
}
