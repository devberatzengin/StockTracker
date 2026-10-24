using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Locations;
using StockTracker.Infrastructure.Persistence.Configurations;

namespace StockTracker.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    // Unique indexes that back business rules. Handlers check first; these cover concurrent requests.
    private static readonly Dictionary<string, Func<DomainException>> UniqueViolations = new()
    {
        [ProductBarcodeConfiguration.ValueIndex] = () =>
            new DomainException(CatalogErrors.BarcodeDuplicate, "Bu barkod başka bir ürüne kayıtlı."),
        [CategoryConfiguration.ActiveNameIndex] = () =>
            new DomainException(CatalogErrors.CategoryNameTaken, "Bu adla aktif bir kategori zaten var."),
        [LocationConfiguration.SiblingNameIndex] = () =>
            new DomainException(LocationErrors.NameTaken, "Aynı üst lokasyon altında bu adla bir lokasyon zaten var."),
    };

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint,
        } && UniqueViolations.TryGetValue(constraint, out var toDomainException))
        {
            throw toDomainException();
        }
    }
}
