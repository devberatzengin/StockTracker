using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Codes;
using StockTracker.Domain.Common.Codes;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Codes;

public sealed class ProductCodeUniquenessCheck(AppDbContext db) : ICodeUniquenessCheck
{
    public CodePrefix Prefix => CodePrefix.Product;

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Products.IgnoreQueryFilters().AnyAsync(p => p.Code == code, cancellationToken);
}

public sealed class LocationCodeUniquenessCheck(AppDbContext db) : ICodeUniquenessCheck
{
    public CodePrefix Prefix => CodePrefix.Location;

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Locations.IgnoreQueryFilters().AnyAsync(l => l.Code == code, cancellationToken);
}
