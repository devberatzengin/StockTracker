using Microsoft.EntityFrameworkCore;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Common.Interfaces;

/// <summary>
/// Read-side access for query handlers: always AsNoTracking + projection to DTOs.
/// Commands use aggregate repositories and IUnitOfWork instead.
/// </summary>
public interface IAppDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductBarcode> ProductBarcodes { get; }
}
