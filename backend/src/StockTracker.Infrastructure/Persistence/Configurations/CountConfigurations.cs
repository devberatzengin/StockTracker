using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class CountSessionConfiguration : IEntityTypeConfiguration<CountSession>
{
    public void Configure(EntityTypeBuilder<CountSession> builder)
    {
        builder.ToTable("count_sessions");

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.Note).HasMaxLength(CountSession.NoteMaxLength);
        builder.Ignore(c => c.IsOpen);

        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CountSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(c => c.Scans).WithOne().HasForeignKey(s => s.CountSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Scans).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => new { c.LocationId, c.StartedAt });
        builder.HasOne<Location>().WithMany().HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Every scan bumps ScanSequence, so concurrent scans of one count conflict instead of racing.
        builder.HasXminConcurrencyToken();
    }
}

public sealed class CountLineConfiguration : IEntityTypeConfiguration<CountLine>
{
    public void Configure(EntityTypeBuilder<CountLine> builder)
    {
        builder.ToTable("count_lines");

        builder.Property(l => l.LineType).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Resolution).HasConversion<string>().HasMaxLength(24);
        builder.Ignore(l => l.IsItem);
        builder.Ignore(l => l.IsCounted);

        builder.HasIndex(l => new { l.CountSessionId, l.LocationId });
        builder.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CountScanConfiguration : IEntityTypeConfiguration<CountScan>
{
    public void Configure(EntityTypeBuilder<CountScan> builder)
    {
        builder.ToTable("count_scans");

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasIndex(s => new { s.CountSessionId, s.Sequence }).IsUnique();
        builder.HasIndex(s => s.CountLineId);
        builder.HasOne<CountLine>().WithMany().HasForeignKey(s => s.CountLineId).OnDelete(DeleteBehavior.Cascade);
    }
}
