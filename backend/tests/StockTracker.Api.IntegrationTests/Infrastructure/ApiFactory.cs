using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using StockTracker.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StockTracker.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API against a throwaway PostgreSQL container with all migrations applied
/// and the admin seeded from <see cref="TestSettings"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _database.GetConnectionString();

    /// <summary>Local file storage root for this test host; removed on dispose.</summary>
    public string UploadsPath { get; } = Path.Combine(Path.GetTempPath(), $"stocktracker-uploads-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Migrate before the host starts: the identity seed runs at startup and needs the schema.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(UploadsPath))
        {
            Directory.Delete(UploadsPath, recursive: true);
        }
    }

    /// <summary>HTTPS base address so Secure cookies are issued; cookies are handled manually by tests.</summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestSettings.Apply(builder, ConnectionString, UploadsPath);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

/// <summary>
/// Separate database for tests that depend on the exact set of admins.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AdminRulesCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "AdminRules";
}

/// <summary>
/// Separate database for dashboard and report tests, which check exact totals.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DashboardCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Dashboard";
}
