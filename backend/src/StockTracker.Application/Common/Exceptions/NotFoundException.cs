namespace StockTracker.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} '{key}' was not found.")
    {
    }
}
