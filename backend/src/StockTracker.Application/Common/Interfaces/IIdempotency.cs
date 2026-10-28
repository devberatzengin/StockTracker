namespace StockTracker.Application.Common.Interfaces;

/// <summary>
/// A command that changes stock. Sent with an <c>Idempotency-Key</c>, it runs at most once per user and key:
/// a repeat (double click, network retry) gets the first response back.
/// </summary>
public interface IIdempotentCommand;

/// <summary>The idempotency key of the current request, if the client sent one.</summary>
public interface IIdempotencyContext
{
    string? Key { get; }
}

/// <summary>What was stored for a key: the request it was used with and, once done, the response.</summary>
public sealed record IdempotencyEntry(string RequestHash, string? ResponseJson);

/// <summary>Stored responses of idempotent commands (one row per user and key).</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> FindAsync(Guid userId, string key, CancellationToken cancellationToken);

    /// <summary>Adds the key to the unit of work, so it is saved in the command's own transaction.</summary>
    void Begin(Guid userId, string key, string requestType, string requestHash, DateTime now);

    Task CompleteAsync(Guid userId, string key, string responseJson, CancellationToken cancellationToken);

    Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken cancellationToken);
}
