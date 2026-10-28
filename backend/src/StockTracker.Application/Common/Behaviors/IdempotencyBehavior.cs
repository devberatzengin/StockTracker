using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation.Results;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Exceptions;
using ValidationException = StockTracker.Application.Common.Exceptions.ValidationException;

namespace StockTracker.Application.Common.Behaviors;

/// <summary>
/// Runs an <see cref="IIdempotentCommand"/> at most once per user and <c>Idempotency-Key</c>. The key is saved
/// in the command's own transaction, so a failed command leaves no trace and can be retried with the same key.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyContext idempotency,
    IIdempotencyStore store,
    ICurrentUser currentUser,
    IDateTime dateTime) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int KeyMaxLength = 100;
    public const string KeyReused = "Idempotency.KeyReused";

    // A concurrent duplicate waits this long for the first request's response.
    private static readonly TimeSpan ConflictWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ConflictPoll = TimeSpan.FromMilliseconds(150);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand || string.IsNullOrWhiteSpace(idempotency.Key))
        {
            return await next();
        }

        var key = idempotency.Key.Trim();
        if (key.Length > KeyMaxLength)
        {
            throw new ValidationException([new ValidationFailure("idempotencyKey", $"Idempotency-Key en fazla {KeyMaxLength} karakter olabilir.")]);
        }

        var userId = currentUser.RequireUserId();
        var hash = Hash(request);
        if (await store.FindAsync(userId, key, cancellationToken) is { } existing)
        {
            return Replay(existing, hash);
        }

        store.Begin(userId, key, typeof(TRequest).Name, hash, dateTime.UtcNow);
        TResponse response;
        try
        {
            response = await next();
        }
        catch (IdempotencyConflictException)
        {
            return await WaitForFirstAsync(userId, key, hash, cancellationToken);
        }

        await store.CompleteAsync(userId, key, JsonSerializer.Serialize(response), cancellationToken);
        return response;
    }

    private async Task<TResponse> WaitForFirstAsync(Guid userId, string key, string hash, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + ConflictWait;
        while (true)
        {
            var existing = await store.FindAsync(userId, key, cancellationToken);
            if (existing is { ResponseJson: not null } || DateTime.UtcNow >= until)
            {
                return existing is null ? throw new ConcurrencyException() : Replay(existing, hash);
            }

            await Task.Delay(ConflictPoll, cancellationToken);
        }
    }

    private static TResponse Replay(IdempotencyEntry existing, string hash)
    {
        if (existing.RequestHash != hash)
        {
            throw new DomainException(KeyReused, "Bu işlem anahtarı başka bir istekle kullanıldı. Sayfayı yenileyip tekrar deneyin.");
        }

        // Saved, but the response was not stored (the first request is still running or stopped midway).
        return existing.ResponseJson is null
            ? throw new ConcurrencyException("Bu işlem zaten kaydedildi ya da hâlâ sürüyor. Sayfayı yenileyip kontrol edin.")
            : JsonSerializer.Deserialize<TResponse>(existing.ResponseJson)!;
    }

    private static string Hash(TRequest request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            typeof(TRequest).FullName + ":" + JsonSerializer.Serialize(request))));
}
