using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Locations;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public const string SiblingNameIndex = "ix_locations_parent_id_normalized_name";

    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.Property(l => l.Code).HasMaxLength(Location.CodeLength).IsFixedLength().IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();

        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.NormalizedName).HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Path).HasMaxLength(Location.PathMaxLength).IsRequired();

        // Subtree queries use "path LIKE '/…/{id}/%'"; text_pattern_ops makes them index scans.
        builder.HasIndex(l => l.Path).HasOperators("text_pattern_ops");

        // Names are unique among active siblings; NULLS NOT DISTINCT covers root locations too.
        builder.HasIndex(l => new { l.ParentId, l.NormalizedName })
            .HasDatabaseName(SiblingNameIndex)
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("is_active");

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(l => l.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
