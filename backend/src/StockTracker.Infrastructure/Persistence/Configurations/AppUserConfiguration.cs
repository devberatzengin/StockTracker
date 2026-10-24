using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Infrastructure.Identity;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");

        // Identity hard-codes these index names; rename them to match the snake_case convention.
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
        // Unique only when set: users without login permission may have no email.
        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("ix_users_normalized_email")
            .IsUnique()
            .HasFilter("normalized_email IS NOT NULL");

        builder.Property(u => u.FullName).HasMaxLength(AppUser.FullNameMaxLength).IsRequired();
        builder.Property(u => u.Department).HasMaxLength(AppUser.DepartmentMaxLength);

        // Identity leaves these as unbounded text by default.
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64);
        builder.Property(u => u.ConcurrencyStamp).HasMaxLength(64);
        builder.Property(u => u.PhoneNumber).HasMaxLength(32);
    }
}
