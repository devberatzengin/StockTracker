namespace StockTracker.Application.Common.Interfaces;

/// <summary>
/// Read-side access for query handlers (AsNoTracking + projection).
/// DbSets are added here as aggregates are introduced.
/// </summary>
public interface IAppDbContext;
