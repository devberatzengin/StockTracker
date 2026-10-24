using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Identity;

/// <summary>
/// Creates the roles and the first admin (from SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD) at startup.
/// Never applies migrations; skips seeding when the database is not migrated yet.
/// </summary>
public sealed class IdentitySeeder(
    AppDbContext db,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public const string AdminEmailKey = "SEED_ADMIN_EMAIL";
    public const string AdminPasswordKey = "SEED_ADMIN_PASSWORD";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogWarning("Database has pending migrations; identity seed skipped. Run the migrator first");
            return;
        }

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }

        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var email = configuration[AdminEmailKey];
        var password = configuration[AdminPasswordKey];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("{EmailKey}/{PasswordKey} not set; admin seed skipped", AdminEmailKey, AdminPasswordKey);
            return;
        }

        var normalizedEmail = userManager.NormalizeEmail(email);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return;
        }

        var admin = new AppUser
        {
            FullName = "Sistem Yöneticisi",
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            CanLogin = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!result.Succeeded)
        {
            logger.LogError(
                "Admin seed failed: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Seeded admin user {Email}", email);
    }
}

public static class IdentitySeederExtensions
{
    public static async Task SeedIdentityAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentitySeeder>>();

        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The API should still start (e.g. DB temporarily unreachable); /health reports the problem.
            logger.LogError(ex, "Identity seed failed");
        }
    }
}
