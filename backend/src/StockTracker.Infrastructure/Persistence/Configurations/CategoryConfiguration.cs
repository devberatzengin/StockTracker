using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Catalog;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string ActiveNameIndex = "ix_categories_normalized_name";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(Category.NameMaxLength).IsRequired();

        // Names are unique (case-insensitive) among active categories only.
        builder.HasIndex(c => c.NormalizedName)
            .HasDatabaseName(ActiveNameIndex)
            .IsUnique()
            .HasFilter("is_active");
    }
}
