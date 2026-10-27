using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Infrastructure.Seeding;

namespace StockTracker.Api.IntegrationTests.Seeding;

/// <summary>Seed commands refuse to run outside demo mode or against a database with data.</summary>
[Collection(ApiCollection.Name)]
public class SeedCommandTests(ApiFactory factory)
{
    [Theory]
    [InlineData("seed-load", true)]
    [InlineData("reset-demo", true)]
    [InlineData("seed-demo", true)]
    [InlineData("migrate", false)]
    [InlineData("", false)]
    public void IsCommand_RecognizesOnlySeedCommands(string arg, bool expected) =>
        SeedCommands.IsCommand([arg]).Should().Be(expected);

    [Fact]
    public async Task SeedLoad_WithoutDemoMode_IsRefused()
    {
        var exitCode = await SeedCommands.RunAsync(factory.Services, [SeedCommands.SeedLoad]);

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task SeedLoad_IntoDatabaseWithData_IsRefused()
    {
        var api = await InventoryApi.CreateAsync(factory);
        await api.LocationAsync("Raf");
        await using var demo = factory.WithWebHostBuilder(b => b.UseSetting("DEMO_MODE", "true"));

        var exitCode = await SeedCommands.RunAsync(demo.Services, [SeedCommands.SeedLoad]);

        exitCode.Should().Be(1);
    }
}
