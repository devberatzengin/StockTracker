using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Application.Common.Behaviors;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Inventory;
using StockTracker.Application.Notifications;
using StockTracker.Application.Scan;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Error keys match the JSON payload: "email", "lines[2].quantity" instead of "Email", "Lines[2].Quantity".
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Order matters: logging wraps validation so rejected requests are logged too.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddSingleton<StockMovementService>();
        services.AddSingleton<CountApprovalService>();
        services.AddScoped<LowStockMonitor>();
        services.AddScoped<AssignmentReminderMonitor>();
        services.AddScoped<LedgerReconciliation>();
        services.AddScoped<DailyNotificationRun>();

        // One resolver per code prefix.
        services.AddScoped<IScanCodeResolver, ProductCodeScanResolver>();
        services.AddScoped<IScanCodeResolver, LocationCodeScanResolver>();
        services.AddScoped<IScanCodeResolver, ItemCodeScanResolver>();
        services.AddScoped<ProductBarcodeScanResolver>();

        return services;
    }
}
