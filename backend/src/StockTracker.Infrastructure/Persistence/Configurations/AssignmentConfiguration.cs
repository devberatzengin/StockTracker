using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using StockTracker.Infrastructure.Identity;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    /// <summary>An item has at most one open assignment.</summary>
    public const string OpenItemIndex = "ix_assignments_item_id_open";

    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.Property(a => a.Note).HasMaxLength(Assignment.NoteMaxLength);
        builder.Property(a => a.ReturnNote).HasMaxLength(Assignment.NoteMaxLength);
        builder.Property(a => a.ReturnCondition).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(a => a.IsOpen);

        builder.HasIndex(a => a.ItemId)
            .HasDatabaseName(OpenItemIndex)
            .IsUnique()
            .HasFilter("returned_at IS NULL");
        builder.HasIndex(a => new { a.ItemId, a.AssignedAt });
        builder.HasIndex(a => new { a.AssigneeUserId, a.ReturnedAt });
        builder.HasIndex(a => a.DueDate).HasFilter("returned_at IS NULL");

        builder.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(a => a.ReturnLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
