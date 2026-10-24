namespace StockTracker.Application.Common.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message = "The resource was modified by another request.")
        : base(message)
    {
    }
}
