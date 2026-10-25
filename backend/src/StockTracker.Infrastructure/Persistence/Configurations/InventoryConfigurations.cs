using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Infrastructure.Persistence.Configurations;

internal static class ConcurrencyTokens
{
    /// <summary>PostgreSQL's system column xmin changes on every update; used as optimistic concurrency token.</summary>
    public static void HasXminConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("xmin").IsRowVersion();
}

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public const string SerialNumberIndex = "ix_items_product_id_serial_number";

    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");

        builder.Property(i => i.Code).HasMaxLength(Item.CodeLength).IsFixedLength().IsRequired();
        builder.HasIndex(i => i.Code).IsUnique();

        builder.Property(i => i.SerialNumber).HasMaxLength(Item.SerialNumberMaxLength);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);

        // Serial numbers are optional but unique within a product.
        builder.HasIndex(i => new { i.ProductId, i.SerialNumber })
            .HasDatabaseName(SerialNumberIndex)
            .IsUnique()
            .HasFilter("serial_number IS NOT NULL");
        builder.HasIndex(i => new { i.ProductId, i.Status });
        builder.HasIndex(i => i.CurrentLocationId);

        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(i => i.CurrentLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasXminConcurrencyToken();
    }
}

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public const string ProductLocationIndex = "ix_stock_balances_product_id_location_id";

    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances", t => t.HasCheckConstraint("ck_stock_balances_quantity", "quantity >= 0"));

        builder.HasIndex(b => new { b.ProductId, b.LocationId }).HasDatabaseName(ProductLocationIndex).IsUnique();
        builder.HasIndex(b => b.LocationId);

        builder.HasOne<Product>().WithMany().HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(b => b.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasXminConcurrencyToken();
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", t => t.HasCheckConstraint("ck_stock_movements_quantity", "quantity > 0"));

        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Reason).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.FromStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.ToStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Note).HasMaxLength(StockMovement.NoteMaxLength);

        builder.HasIndex(m => new { m.ProductId, m.OccurredAt });
        builder.HasIndex(m => m.ItemId);
        builder.HasIndex(m => m.FromLocationId);
        builder.HasIndex(m => m.ToLocationId);
        builder.HasIndex(m => m.ReferenceId);

        builder.HasOne<Product>().WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(m => m.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(m => m.ToLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
