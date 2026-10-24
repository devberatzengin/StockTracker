namespace StockTracker.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    /// <param name="resourceName">User-facing (Turkish) name, e.g. "Ürün".</param>
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} bulunamadı.")
    {
        Key = key;
    }

    /// <summary>Kept out of the message but available for logs.</summary>
    public object Key { get; }
}
