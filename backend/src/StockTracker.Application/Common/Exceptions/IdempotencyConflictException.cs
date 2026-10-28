namespace StockTracker.Application.Common.Exceptions;

/// <summary>Another request with the same idempotency key was saved first.</summary>
public sealed class IdempotencyConflictException() : Exception("The idempotency key was used by a concurrent request.");
