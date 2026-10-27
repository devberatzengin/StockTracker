using Microsoft.EntityFrameworkCore;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Seeding;

internal static class SeedGuard
{
    /// <summary>Seeds never write into a database that already has catalog data.</summary>
    public static async Task EnsureEmptyAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Products.IgnoreQueryFilters().AnyAsync(cancellationToken)
            || await db.Locations.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The database already has products or locations. Seed data is only written into an empty database (use reset-demo with DEMO_MODE=true).");
        }
    }

    public static async Task<Guid> EnsureEmptyAndGetAdminAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await EnsureEmptyAsync(db, cancellationToken);
        var adminRole = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).SingleAsync(cancellationToken);
        return await db.UserRoles.Where(ur => ur.RoleId == adminRole).Select(ur => ur.UserId).FirstOrDefaultAsync(cancellationToken) is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("No admin user exists; set SEED_ADMIN_EMAIL and SEED_ADMIN_PASSWORD.");
    }
}
