using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Files;
using StockTracker.Infrastructure.Codes;
using StockTracker.Infrastructure.Identity;
using StockTracker.Infrastructure.Inventory;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Infrastructure.Persistence.Interceptors;
using StockTracker.Infrastructure.Persistence.Repositories;
using StockTracker.Infrastructure.Services;
using StockTracker.Infrastructure.Storage;

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
        AddStorage(services, configuration);

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddSingleton<AppendOnlyLedgerInterceptor>();
        // Migrations are never applied here; run them with the migrator container.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseStockTrackerNpgsql(connectionString)
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<AppendOnlyLedgerInterceptor>(),
                sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductStockChecker, ProductStockChecker>();

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationContentChecker, LocationContentChecker>();

        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IStockBalanceRepository, StockBalanceRepository>();
        services.AddScoped<IMovementRepository, MovementRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IAssignmentChecker, AssignmentChecker>();
        services.AddScoped<ICountRepository, CountRepository>();

        services.AddScoped<ICodeUniquenessCheck, ProductCodeUniquenessCheck>();
        services.AddScoped<ICodeUniquenessCheck, LocationCodeUniquenessCheck>();
        services.AddScoped<ICodeUniquenessCheck, ItemCodeUniquenessCheck>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        AddIdentity(services);

        return services;
    }

    /// <summary>Photo storage: local disk or Cloudflare R2 (Storage:Provider); misconfiguration fails at startup.</summary>
    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);
        var options = section.Get<StorageOptions>() ?? new StorageOptions();
        var errors = options.Validate().ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        services.AddOptions<StorageOptions>().Bind(section);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        if (options.Provider == StorageProvider.R2)
        {
            services.AddSingleton<R2FileStorage>();
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<R2FileStorage>());
            services.AddSingleton<ISignedFileStore>(sp => sp.GetRequiredService<R2FileStorage>());
        }
        else
        {
            services.AddSingleton<LocalFileStorage>();
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
            services.AddSingleton<ISignedFileStore>(sp => sp.GetRequiredService<LocalFileStorage>());
        }
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
