using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockTracker.Application.Common.Behaviors;
using StockTracker.Infrastructure.Idempotency;

namespace StockTracker.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public const string PrimaryKey = "pk_idempotency_requests";

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_requests");
        builder.HasKey(r => new { r.UserId, r.Key }).HasName(PrimaryKey);
        builder.Property(r => r.Key).HasMaxLength(IdempotencyBehavior<object, object>.KeyMaxLength);
        builder.Property(r => r.RequestType).HasMaxLength(200);
        builder.Property(r => r.RequestHash).HasMaxLength(64);
        builder.Property(r => r.ResponseJson).HasMaxLength(100_000);
        builder.HasIndex(r => r.CreatedAt);
    }
}
