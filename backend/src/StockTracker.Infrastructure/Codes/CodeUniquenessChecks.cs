using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Codes;
using StockTracker.Domain.Common.Codes;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Codes;

public sealed class ProductCodeUniquenessCheck(AppDbContext db) : ICodeUniquenessCheck
{
    public CodePrefix Prefix => CodePrefix.Product;

    public async Task<IReadOnlySet<string>> ExistingAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken) =>
        (await db.Products.IgnoreQueryFilters().Where(p => codes.Contains(p.Code)).Select(p => p.Code).ToListAsync(cancellationToken))
        .ToHashSet();
}

public sealed class LocationCodeUniquenessCheck(AppDbContext db) : ICodeUniquenessCheck
{
    public CodePrefix Prefix => CodePrefix.Location;

    public async Task<IReadOnlySet<string>> ExistingAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken) =>
        (await db.Locations.IgnoreQueryFilters().Where(l => codes.Contains(l.Code)).Select(l => l.Code).ToListAsync(cancellationToken))
        .ToHashSet();
}

public sealed class ItemCodeUniquenessCheck(AppDbContext db) : ICodeUniquenessCheck
{
    public CodePrefix Prefix => CodePrefix.Item;

    public async Task<IReadOnlySet<string>> ExistingAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken) =>
        (await db.Items.Where(i => codes.Contains(i.Code)).Select(i => i.Code).ToListAsync(cancellationToken))
        .ToHashSet();
}
