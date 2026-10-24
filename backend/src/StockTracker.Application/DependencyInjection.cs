using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Application.Common.Behaviors;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Scan;

namespace StockTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Order matters: logging wraps validation so rejected requests are logged too.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<ICodeGenerator, CodeGenerator>();

        // One resolver per code prefix; the item resolver arrives with the Item entity.
        services.AddScoped<IScanCodeResolver, ProductCodeScanResolver>();
        services.AddScoped<IScanCodeResolver, LocationCodeScanResolver>();
        services.AddScoped<ProductBarcodeScanResolver>();

        return services;
    }
}
