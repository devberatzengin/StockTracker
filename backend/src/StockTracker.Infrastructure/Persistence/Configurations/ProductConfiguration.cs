using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Catalog;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public const string CodeIndex = "ix_products_code";
    private const int EnumMaxLength = 16;

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.Property(p => p.Code).HasMaxLength(Product.CodeLength).IsFixedLength().IsRequired();
        builder.HasIndex(p => p.Code).HasDatabaseName(CodeIndex).IsUnique();

        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(p => p.Unit).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(p => p.TrackingType).HasConversion<string>().HasMaxLength(EnumMaxLength);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Barcodes)
            .WithOne()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Barcodes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Photos)
            .WithOne()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
