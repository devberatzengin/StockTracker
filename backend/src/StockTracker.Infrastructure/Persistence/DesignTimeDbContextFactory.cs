using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockTracker.Infrastructure.Persistence;

/// <summary>
/// Used by dotnet-ef (migrations add, bundle) so design-time tooling never needs a live database
/// or the full API host. The bundle receives the real connection string via --connection.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string PlaceholderConnectionString = "Host=localhost;Database=stocktracker";

    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseStockTrackerNpgsql(PlaceholderConnectionString);

        return new AppDbContext(builder.Options);
    }
}
