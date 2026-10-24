using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Catalog;
using StockTracker.Infrastructure.Identity;
using StockTracker.Infrastructure.Locations;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Infrastructure.Persistence.Interceptors;
using StockTracker.Infrastructure.Persistence.Repositories;
using StockTracker.Infrastructure.Services;

namespace StockTracker.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName}.");
        }

        services.AddSingleton<IDateTime, SystemDateTime>();

        services.AddScoped<AuditableEntityInterceptor>();
        // Migrations are never applied here; run them with the migrator container.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseStockTrackerNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductStockChecker, NoStockProductStockChecker>();
        services.AddScoped<IProductCodeGenerator, TemporaryProductCodeGenerator>();

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationContentChecker, EmptyLocationContentChecker>();
        services.AddScoped<ILocationCodeGenerator, TemporaryLocationCodeGenerator>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        AddIdentity(services);

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // Users without login permission may have no email; uniqueness is enforced by
                // a partial unique index and UserAccountService instead.
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<TurkishIdentityErrorDescriber>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IdentitySeeder>();
    }
}
