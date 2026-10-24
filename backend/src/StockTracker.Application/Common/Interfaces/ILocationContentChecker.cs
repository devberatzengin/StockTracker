namespace StockTracker.Application.Common.Interfaces;

public interface ILocationContentChecker
{
    /// <summary>Whether any location in the subtree (path prefix) holds stock or items.</summary>
    Task<bool> HasContentAsync(string subtreePath, CancellationToken cancellationToken);
}
