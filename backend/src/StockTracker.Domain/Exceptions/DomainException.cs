namespace StockTracker.Domain.Exceptions;

/// <summary>
/// Thrown when a domain rule is violated. <see cref="Code"/> is a stable,
/// machine-readable identifier such as <c>Product.TrackingTypeLocked</c>.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message, string? target = null)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Target = target;
    }

    public string Code { get; }

    /// <summary>
    /// Optional path of the input field the error is about, e.g. <c>quantity</c> or <c>lines[2].quantity</c>.
    /// </summary>
    public string? Target { get; }

    /// <summary>Same error, attributed to <paramref name="target"/>.</summary>
    public DomainException WithTarget(string target) => new(Code, Message, target);
}
