using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;

namespace StockTracker.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters()
            .Include(p => p.Barcodes)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters().AnyAsync(p => p.Code == code, cancellationToken);

    public Task<bool> BarcodeExistsAsync(string normalizedValue, CancellationToken cancellationToken) =>
        db.ProductBarcodes.IgnoreQueryFilters().AnyAsync(b => b.Value == normalizedValue, cancellationToken);

    public Task<bool> HasActiveProductsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.CategoryId == categoryId, cancellationToken);

    public void Add(Product product) => db.Products.Add(product);
}
