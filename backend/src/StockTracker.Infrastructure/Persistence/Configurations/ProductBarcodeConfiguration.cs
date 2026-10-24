using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Catalog;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public const string ValueIndex = "ix_product_barcodes_value";

    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.ToTable("product_barcodes");

        builder.Property(b => b.Value).HasMaxLength(ProductBarcode.ValueMaxLength).IsRequired();
        builder.Property(b => b.Format).HasConversion<string>().HasMaxLength(16);

        // System-wide uniqueness, across active and inactive products.
        builder.HasIndex(b => b.Value).HasDatabaseName(ValueIndex).IsUnique();
    }
}
