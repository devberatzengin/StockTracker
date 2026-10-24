using Microsoft.Extensions.DependencyInjection;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Services;

namespace StockTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDateTime, SystemDateTime>();

        return services;
    }
}
