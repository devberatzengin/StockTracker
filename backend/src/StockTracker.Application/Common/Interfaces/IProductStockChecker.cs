namespace StockTracker.Application.Common.Interfaces;

/// <summary>Answers stock questions that product rules depend on.</summary>
public interface IProductStockChecker
{
    /// <summary>Positive balance, or items that are in stock, assigned or in repair.</summary>
    Task<bool> HasStockAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Any stock or any recorded movement; locks the tracking type.</summary>
    Task<bool> HasStockOrMovementsAsync(Guid productId, CancellationToken cancellationToken);
}
