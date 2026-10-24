using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Infrastructure.Persistence.Interceptors;
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

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
