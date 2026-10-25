using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockTracker.Domain.Inventory;

namespace StockTracker.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The stock ledger is append-only. Refuses to save any update or delete of a movement
/// (a database trigger enforces the same rule).
/// </summary>
public sealed class AppendOnlyLedgerInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnsureAppendOnly(DbContext? context)
    {
        if (context?.ChangeTracker.Entries<StockMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) == true)
        {
            throw new InvalidOperationException("Stock movements are append-only; they cannot be updated or deleted.");
        }
    }
}
