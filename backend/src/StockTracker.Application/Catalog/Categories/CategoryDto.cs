namespace StockTracker.Application.Catalog.Categories;

public sealed record CategoryDto(Guid Id, string Name, bool IsActive, int ActiveProductCount);
