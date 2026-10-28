using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using StockTracker.Infrastructure.Persistence.Configurations;

namespace StockTracker.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    // Unique indexes that back business rules. Handlers check first; these cover concurrent requests.
    public const string StockChangedMessage = "Stok bu arada değişti, tekrar deneyin.";

    private static readonly Dictionary<string, Func<Exception>> UniqueViolations = new()
    {
        [ProductBarcodeConfiguration.ValueIndex] = () =>
            new DomainException(CatalogErrors.BarcodeDuplicate, "Bu barkod başka bir ürüne kayıtlı."),
        [CategoryConfiguration.ActiveNameIndex] = () =>
            new DomainException(CatalogErrors.CategoryNameTaken, "Bu adla aktif bir kategori zaten var."),
        // Two requests creating the same (product, location) balance at once.
        [StockBalanceConfiguration.ProductLocationIndex] = () => new ConcurrencyException(StockChangedMessage),
        [ItemConfiguration.SerialNumberIndex] = () =>
            new DomainException(InventoryErrors.SerialNumberTaken, "Bu seri numarası bu üründe zaten kayıtlı.", "serialNumbers"),
        // Two checks opening the same notification at once (instant check vs. daily run).
        [NotificationConfiguration.OpenDedupIndex] = () => new ConcurrencyException(),
        // Two requests assigning the same item at once.
        [AssignmentConfiguration.OpenItemIndex] = () =>
            new ConcurrencyException("Bu adet bu arada zimmetlendi, tekrar deneyin."),
        // Two requests reversing the same movement at once.
        [StockMovementConfiguration.ReversalIndex] = () =>
            new DomainException(InventoryErrors.AlreadyReversed, "Bu hareket zaten ters çevrildi."),
        // The same idempotency key saved by a concurrent request; the behavior replays its response.
        [IdempotencyRecordConfiguration.PrimaryKey] = () => new IdempotencyConflictException(),
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
        catch (DbUpdateConcurrencyException ex)
        {
            // Balances and items carry xmin tokens; a conflict there means stock changed meanwhile.
            throw ex.Entries.Any(e => e.Entity is StockBalance or Item)
                ? new ConcurrencyException(StockChangedMessage)
                : ex.Entries.Any(e => e.Entity is CountSession)
                    ? new ConcurrencyException("Sayım bu arada değişti, tekrar okutun.")
                    : new ConcurrencyException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint,
        } && UniqueViolations.TryGetValue(constraint, out var translate))
        {
            throw translate();
        }
    }
}
