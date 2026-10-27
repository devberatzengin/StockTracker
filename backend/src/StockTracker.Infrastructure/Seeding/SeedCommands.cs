using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StockTracker.Infrastructure.Seeding;

/// <summary>
/// One-off commands run instead of the web server: <c>dotnet StockTracker.Api.dll seed-demo</c>
/// (in Docker: <c>docker compose run --rm api seed-demo</c>).
/// </summary>
public static class SeedCommands
{
    public const string SeedDemo = "seed-demo";
    public const string ResetDemo = "reset-demo";
    public const string SeedLoad = "seed-load";

    public static bool IsCommand(string[] args) => args is [SeedDemo or ResetDemo or SeedLoad, ..];

    /// <summary>Runs the command in <paramref name="args"/>; returns the process exit code.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedCommands));
        var demoMode = string.Equals(provider.GetRequiredService<IConfiguration>()["DEMO_MODE"], "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            switch (args[0])
            {
                case SeedLoad when !demoMode:
                case ResetDemo when !demoMode:
                    logger.LogError("{Command} only runs with DEMO_MODE=true.", args[0]);
                    return 2;
                case SeedLoad:
                    await provider.GetRequiredService<LoadSeeder>().RunAsync(cancellationToken);
                    break;
                case SeedDemo:
                    await provider.GetRequiredService<DemoSeeder>().RunAsync(cancellationToken);
                    break;
                case ResetDemo:
                    await provider.GetRequiredService<DemoSeeder>().ResetAsync(cancellationToken);
                    break;
            }

            return 0;
        }
        catch (InvalidOperationException e)
        {
            logger.LogError("{Command} stopped: {Reason}", args[0], e.Message);
            return 1;
        }
    }
}
