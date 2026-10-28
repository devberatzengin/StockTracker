using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Application.Inventory;
using StockTracker.Domain.Notifications;
using StockTracker.Infrastructure.Notifications;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Ledger;

/// <summary>Balances and items are checked against the ledger; differences notify the admins.</summary>
[Collection(LedgerCollection.Name)]
public class LedgerReconciliationTests(ApiFactory factory)
{
    private async Task<LedgerReport> CheckAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LedgerReconciliation>().CheckAsync(CancellationToken.None);
    }

    private async Task<int> OpenMismatchNotificationsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.CountAsync(n => n.DedupKey == Notification.LedgerMismatchKey && n.ResolvedAt == null);
    }

    private async Task SqlAsync(string sql, params object[] parameters)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql, parameters);
    }

    [Fact]
    public async Task ConsistentLedger_HasNoProblems_AndDifferencesAreReportedUntilFixed()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var screw = await api.QuantityProductAsync(category);
        var laptop = await api.SerializedProductAsync(category);
        var a = await api.LocationAsync("Raf A");
        var b = await api.LocationAsync("Raf B");
        var received = await OkAsync(await api.InAsync(a,
            new { productId = screw, quantity = 50 },
            new { productId = laptop, quantity = 2, serialNumbers = Array.Empty<string>() }));
        var item = received.GetProperty("createdItems")[0].GetProperty("id").GetGuid();
        await OkAsync(await api.TransferAsync(b, Quantity(screw, a, 20), Item(item)));
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, b, 5)));

        (await CheckAsync()).Problems.Should().BeEmpty();
        (await OpenMismatchNotificationsAsync()).Should().Be(0);

        // A balance and an item changed behind the ledger's back.
        await SqlAsync("UPDATE stock_balances SET quantity = quantity + 3 WHERE product_id = {0} AND location_id = {1}", screw, a);
        await SqlAsync("UPDATE items SET current_location_id = {0} WHERE id = {1}", a, item);

        var report = await CheckAsync();
        report.Problems.Should().HaveCount(2);
        report.Problems.Should().Contain(p => p.StartsWith("Bakiye:") && p.Contains("kayıtlı 33") && p.Contains("hareketlere göre 30"));
        report.Problems.Should().Contain(p => p.StartsWith("Adet "));
        (await OpenMismatchNotificationsAsync()).Should().Be(1);

        // A second run keeps the single notification; fixing the data resolves it.
        await CheckAsync();
        (await OpenMismatchNotificationsAsync()).Should().Be(1);
        await SqlAsync("UPDATE stock_balances SET quantity = quantity - 3 WHERE product_id = {0} AND location_id = {1}", screw, a);
        await SqlAsync("UPDATE items SET current_location_id = {0} WHERE id = {1}", b, item);

        (await CheckAsync()).Problems.Should().BeEmpty();
        (await OpenMismatchNotificationsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DailyJob_RunsOnlyWhereItGetsTheLock()
    {
        await using var other = new NpgsqlConnection(factory.ConnectionString);
        await other.OpenAsync();
        await using (var hold = new NpgsqlCommand($"SELECT pg_advisory_lock({DailyNotificationJob.LockKey})", other))
        {
            await hold.ExecuteNonQueryAsync();
        }

        var ran = 0;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await DailyNotificationJob.TryRunExclusiveAsync(db, () => Task.FromResult(ran++), CancellationToken.None))
                .Should().BeFalse();
        }

        await using (var release = new NpgsqlCommand($"SELECT pg_advisory_unlock({DailyNotificationJob.LockKey})", other))
        {
            await release.ExecuteNonQueryAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await DailyNotificationJob.TryRunExclusiveAsync(db, () => Task.FromResult(ran++), CancellationToken.None))
                .Should().BeTrue();
        }

        ran.Should().Be(1);
    }
}
