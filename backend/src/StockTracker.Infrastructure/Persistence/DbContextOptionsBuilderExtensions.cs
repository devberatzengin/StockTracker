using Microsoft.EntityFrameworkCore;

namespace StockTracker.Infrastructure.Persistence;

internal static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseStockTrackerNpgsql(
        this DbContextOptionsBuilder builder,
        string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
