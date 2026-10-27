using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Notifications;
using StockTracker.Infrastructure.Identity;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>One open notification per user and DedupKey.</summary>
    public const string OpenDedupIndex = "ix_notifications_user_id_dedup_key_open";

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.Property(n => n.EntityType).HasMaxLength(Notification.EntityTypeMaxLength).IsRequired();
        builder.Property(n => n.DedupKey).HasMaxLength(Notification.DedupKeyMaxLength).IsRequired();
        builder.Ignore(n => n.IsOpen);

        builder.HasIndex(n => new { n.UserId, n.DedupKey })
            .HasDatabaseName(OpenDedupIndex)
            .IsUnique()
            .HasFilter("resolved_at IS NULL");
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.DedupKey).HasFilter("resolved_at IS NULL");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
