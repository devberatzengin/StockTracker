using StockTracker.Domain.Common;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Catalog;

public class Category : BaseEntity, ISoftDeletable
{
    public const int NameMaxLength = 100;

    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name for case-insensitive uniqueness among active categories.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public static Category Create(string name)
    {
        var category = new Category();
        category.Rename(name);
        return category;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void Rename(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException(CatalogErrors.CategoryNameRequired, "Kategori adı zorunludur.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException(
                CatalogErrors.CategoryNameTooLong,
                $"Kategori adı en fazla {NameMaxLength} karakter olabilir.");
        }

        Name = trimmed;
        NormalizedName = Normalize(trimmed);
    }

    /// <param name="hasActiveProducts">Whether any active product still uses this category.</param>
    public void Deactivate(bool hasActiveProducts)
    {
        if (hasActiveProducts)
        {
            throw new DomainException(
                CatalogErrors.CategoryHasActiveProducts,
                "İçinde aktif ürün olan kategori pasifleştirilemez.");
        }

        IsActive = false;
    }
}
