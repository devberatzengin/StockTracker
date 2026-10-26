using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Auditing;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.EntityType).HasMaxLength(AuditLog.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Changes).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.ParentEntityId).HasFilter("parent_entity_id IS NOT NULL");
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UserId);
    }
}

public sealed class ProductPhotoConfiguration : IEntityTypeConfiguration<Domain.Catalog.ProductPhoto>
{
    public void Configure(EntityTypeBuilder<Domain.Catalog.ProductPhoto> builder)
    {
        builder.ToTable("product_photos");

        builder.Property(p => p.StorageKey).HasMaxLength(Domain.Catalog.ProductPhoto.KeyMaxLength).IsRequired();
        builder.Property(p => p.ThumbnailKey).HasMaxLength(Domain.Catalog.ProductPhoto.KeyMaxLength).IsRequired();
        builder.Property(p => p.ContentType).HasMaxLength(Domain.Catalog.ProductPhoto.ContentTypeMaxLength).IsRequired();

        // Not unique: reordering and switching the primary photo update several rows in one save.
        builder.HasIndex(p => new { p.ProductId, p.SortOrder });
    }
}
