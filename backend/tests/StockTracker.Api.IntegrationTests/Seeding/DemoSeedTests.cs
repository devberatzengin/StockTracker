using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Inventory;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Infrastructure.Seeding;

namespace StockTracker.Api.IntegrationTests.Seeding;

/// <summary>The "Demo Atölye" scenario of PLAN 4.4, seeded and then reset.</summary>
[Collection(DemoSeedCollection.Name)]
public class DemoSeedTests(ApiFactory factory)
{
    [Fact]
    public async Task SeedDemo_ThenResetDemo_BuildsTheScenario()
    {
        await using var demo = factory.WithWebHostBuilder(b => b.UseSetting("DEMO_MODE", "true"));

        (await SeedCommands.RunAsync(demo.Services, [SeedCommands.SeedDemo])).Should().Be(0);
        await AssertScenarioAsync(demo.Services);

        // A second seed is refused; a reset replaces everything with the same scenario.
        (await SeedCommands.RunAsync(demo.Services, [SeedCommands.SeedDemo])).Should().Be(1);
        (await SeedCommands.RunAsync(demo.Services, [SeedCommands.ResetDemo])).Should().Be(0);
        await AssertScenarioAsync(demo.Services);

        var client = demo.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var email in new[] { DemoSeeder.AdminEmail, DemoSeeder.WarehouseEmail, DemoSeeder.StaffEmail })
        {
            (await AuthApi.LoginRawAsync(client, email, DemoSeeder.Password)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // The environment's admin is seeded again after the reset.
        (await AuthApi.LoginRawAsync(client, TestSettings.AdminEmail, TestSettings.AdminPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task AssertScenarioAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

        (await db.Products.CountAsync()).Should().Be(40);
        (await db.Categories.CountAsync()).Should().Be(8);
        (await db.Items.CountAsync()).Should().Be(150);
        (await db.StockMovements.MinAsync(m => m.OccurredAt)).Should().BeBefore(DateTime.UtcNow.AddDays(-170));

        var open = await db.Assignments.Where(a => a.ReturnedAt == null).ToListAsync();
        open.Should().HaveCount(10);
        open.Count(a => a.DueDate < today).Should().Be(2);

        (await db.CountSessions.CountAsync(c => c.Status == CountStatus.Approved)).Should().Be(1);
        (await db.Items.CountAsync(i => i.Status == ItemStatus.InRepair)).Should().Be(2);
        (await db.Notifications.Where(n => n.ResolvedAt == null && n.EntityType == "Product").Select(n => n.EntityId).Distinct().CountAsync())
            .Should().Be(2);

        // Every balance and item of the scenario matches its movements.
        var report = await scope.ServiceProvider.GetRequiredService<LedgerReconciliation>().CheckAsync(CancellationToken.None);
        report.Problems.Should().BeEmpty();
        report.ItemsChecked.Should().Be(150);
    }
}
