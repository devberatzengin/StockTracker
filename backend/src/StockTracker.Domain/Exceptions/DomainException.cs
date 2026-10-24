namespace StockTracker.Domain.Exceptions;

/// <summary>
/// Thrown when a domain rule is violated. <see cref="Code"/> is a stable,
/// machine-readable identifier such as <c>Product.TrackingTypeLocked</c>.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
