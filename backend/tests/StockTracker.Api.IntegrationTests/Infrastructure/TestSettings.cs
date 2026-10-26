using Microsoft.AspNetCore.Hosting;

namespace StockTracker.Api.IntegrationTests.Infrastructure;

public static class TestSettings
{
    public const string AdminEmail = "admin@stocktracker.test";
    public const string AdminPassword = "Admin12345";

    public const string StorageSigningKey = "test-storage-signing-key-at-least-32-chars";

    public static void Apply(IWebHostBuilder builder, string connectionString, string? uploadsPath = null)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Issuer", "stocktracker-tests");
        builder.UseSetting("Jwt:Audience", "stocktracker-tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-long-enough-for-hs256");
        builder.UseSetting("SEED_ADMIN_EMAIL", AdminEmail);
        builder.UseSetting("SEED_ADMIN_PASSWORD", AdminPassword);
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Storage:LocalPath", uploadsPath ?? Path.Combine(Path.GetTempPath(), "stocktracker-uploads-unused"));
        builder.UseSetting("Storage:SigningKey", StorageSigningKey);
    }
}
