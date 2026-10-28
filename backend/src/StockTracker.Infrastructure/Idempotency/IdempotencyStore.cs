using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Idempotency;

public sealed class IdempotencyStore(AppDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyEntry?> FindAsync(Guid userId, string key, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking()
            .Where(r => r.UserId == userId && r.Key == key)
            .Select(r => new IdempotencyEntry(r.RequestHash, r.ResponseJson))
            .SingleOrDefaultAsync(cancellationToken);

    public void Begin(Guid userId, string key, string requestType, string requestHash, DateTime now) =>
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            RequestType = requestType,
            RequestHash = requestHash,
            CreatedAt = now,
        });

    public Task CompleteAsync(Guid userId, string key, string responseJson, CancellationToken cancellationToken) =>
        db.IdempotencyRecords
            .Where(r => r.UserId == userId && r.Key == key)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ResponseJson, responseJson), cancellationToken);

    public Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.Where(r => r.CreatedAt < before).ExecuteDeleteAsync(cancellationToken);
}
