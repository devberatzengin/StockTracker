using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Api.Services;

/// <summary>Reads the <c>Idempotency-Key</c> request header.</summary>
public sealed class HttpIdempotencyContext(IHttpContextAccessor accessor) : IIdempotencyContext
{
    public const string HeaderName = "Idempotency-Key";

    public string? Key => accessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
}
