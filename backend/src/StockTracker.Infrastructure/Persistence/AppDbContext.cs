using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Common;
using StockTracker.Infrastructure.Identity;

namespace StockTracker.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplySoftDeleteFilters(builder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 3);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        var softDeletableRoots = builder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && typeof(ISoftDeletable).IsAssignableFrom(t.ClrType));

        foreach (var entityType in softDeletableRoots)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Property(parameter, nameof(ISoftDeletable.IsActive)),
                parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
