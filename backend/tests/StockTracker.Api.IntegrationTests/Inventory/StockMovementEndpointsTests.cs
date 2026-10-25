using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Inventory;

[Collection(ApiCollection.Name)]
public class StockMovementEndpointsTests(ApiFactory factory)
{
    private sealed record Setup(InventoryApi Api, Guid Laptop, Guid Screw, Guid Building, Guid ShelfA1, Guid ShelfB2);

    /// <summary>Laptop (serialized) + Vida M4 (quantity); Bina › Raf A1 / Raf B2.</summary>
    private async Task<Setup> SetupAsync()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var building = await api.LocationAsync("Bina", type: "Building");
        return new Setup(
            api,
            await api.SerializedProductAsync(category),
            await api.QuantityProductAsync(category),
            building,
            await api.LocationAsync("Raf A1", building),
            await api.LocationAsync("Raf B2", building));
    }

    private static object LaptopLine(Guid productId, int count, params string[] serials) =>
        new { productId, quantity = count, serialNumbers = serials };

    private static object ScrewLine(Guid productId, decimal quantity) => new { productId, quantity };

    [Fact]
    public async Task InTransferOut_UpdateBalancesItemsAndHistory()
    {
        var (api, laptop, screw, building, a1, b2) = await SetupAsync();

        // In: 3 serialized laptops + 500 screws in one operation.
        var received = await OkAsync(await api.InAsync(a1, LaptopLine(laptop, 3, "SN-1", "SN-2", "SN-3"), ScrewLine(screw, 500)));
        received.GetProperty("movementCount").GetInt32().Should().Be(4);
        var created = received.GetProperty("createdItems").EnumerateArray().ToList();
        created.Should().HaveCount(3);
        created.Select(i => i.GetProperty("code").GetString()).Should().AllSatisfy(c => c.Should().MatchRegex("^ITM-[0-9A-HJKMNP-TV-Z]{6}$"));
        created.Select(i => i.GetProperty("serialNumber").GetString()).Should().BeEquivalentTo("SN-1", "SN-2", "SN-3");
        var laptop1 = created[0].GetProperty("id").GetGuid();
        var laptop2 = created[1].GetProperty("id").GetGuid();
        (await api.QuantityAtAsync(screw, a1)).Should().Be(500);

        // Transfer: 200 screws and one laptop to Raf B2.
        var transfer = await OkAsync(await api.TransferAsync(b2, Quantity(screw, a1, 200), Item(laptop1)));
        var transferRef = transfer.GetProperty("referenceId").GetGuid();
        (await api.QuantityAtAsync(screw, a1)).Should().Be(300);
        (await api.QuantityAtAsync(screw, b2)).Should().Be(200);
        (await api.ItemAsync(laptop1)).GetProperty("currentLocationId").GetGuid().Should().Be(b2);
        (await api.ItemAsync(laptop1)).GetProperty("locationPath").GetString().Should().EndWith("› Raf B2");

        // Out: 50 screws used, one laptop damaged.
        await OkAsync(await api.OutAsync("Damaged", "Düştü", Quantity(screw, b2, 50), Item(laptop2)));
        (await api.QuantityAtAsync(screw, b2)).Should().Be(150);
        var disposed = await api.ItemAsync(laptop2);
        disposed.GetProperty("status").GetString().Should().Be("Disposed");
        disposed.TryGetProperty("currentLocationId", out var location).Should().BeTrue();
        location.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        // History: grouped by reference, newest first, with names.
        var transferMovements = await api.JsonAsync(HttpMethod.Get, $"/api/movements?referenceId={transferRef}");
        transferMovements.GetProperty("totalCount").GetInt32().Should().Be(2);
        var screwHistory = (await api.JsonAsync(HttpMethod.Get, $"/api/movements?productId={screw}")).GetProperty("items").EnumerateArray().ToList();
        screwHistory.Select(m => m.GetProperty("type").GetString()).Should().Equal("Out", "Transfer", "In");
        screwHistory[0].GetProperty("reason").GetString().Should().Be("Damaged");
        screwHistory[0].GetProperty("note").GetString().Should().Be("Düştü");
        screwHistory[0].GetProperty("fromLocationName").GetString().Should().Be("Raf B2");
        screwHistory[1].GetProperty("toLocationName").GetString().Should().Be("Raf B2");
        screwHistory[1].GetProperty("userName").GetString().Should().Be("Sistem Yöneticisi");
        (await api.JsonAsync(HttpMethod.Get, $"/api/movements?itemId={laptop1}")).GetProperty("totalCount").GetInt32().Should().Be(2);

        // Stock views.
        var laptopStock = await api.JsonAsync(HttpMethod.Get, $"/api/products/{laptop}/stock");
        laptopStock.GetProperty("totalQuantity").GetDecimal().Should().Be(2);
        laptopStock.GetProperty("statusCounts").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s.GetProperty("count").GetInt32())
            .Should().Contain(new Dictionary<string, int> { ["InStock"] = 2, ["Disposed"] = 1 });

        var contents = await api.JsonAsync(HttpMethod.Get, $"/api/locations/{building}/contents?includeChildren=true");
        contents.GetProperty("balances").EnumerateArray().Select(b => b.GetProperty("quantity").GetDecimal()).Should().BeEquivalentTo([300m, 150m]);
        contents.GetProperty("items").GetArrayLength().Should().Be(2);
        (await api.JsonAsync(HttpMethod.Get, $"/api/locations/{building}/contents")).GetProperty("balances").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task InvalidThirdLine_WritesNothing_AndNamesTheLine()
    {
        var (api, laptop, screw, _, a1, b2) = await SetupAsync();
        var received = await OkAsync(await api.InAsync(a1, LaptopLine(laptop, 2), ScrewLine(screw, 100)));
        var items = received.GetProperty("createdItems").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var movementsBefore = await api.MovementCountAsync(screw);

        // Line 2 asks for 80 while only 100 - 30 = 70 screws remain.
        var response = await api.TransferAsync(
            b2,
            Quantity(screw, a1, 30),
            Item(items[0]),
            Quantity(screw, a1, 80),
            Quantity(screw, a1, 5),
            Item(items[1]));

        await ShouldBeLineErrorAsync(response, HttpStatusCode.UnprocessableEntity, "lines[2].quantity", InventoryErrors.InsufficientStock);
        (await api.QuantityAtAsync(screw, a1)).Should().Be(100);
        (await api.QuantityAtAsync(screw, b2)).Should().Be(0);
        (await api.ItemAsync(items[0])).GetProperty("currentLocationId").GetGuid().Should().Be(a1);
        (await api.MovementCountAsync(screw)).Should().Be(movementsBefore);
    }

    [Fact]
    public async Task FieldErrors_UseLineKeys()
    {
        var (api, laptop, screw, _, a1, _) = await SetupAsync();

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, ScrewLine(screw, 5), ScrewLine(screw, 0)),
            HttpStatusCode.BadRequest,
            "lines[1].quantity");

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, LaptopLine(laptop, 101)),
            HttpStatusCode.UnprocessableEntity,
            "lines[0].quantity",
            InventoryErrors.TooManyItems);

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, LaptopLine(laptop, 1, "A", "B")),
            HttpStatusCode.UnprocessableEntity,
            "lines[0].serialNumbers");

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, new { productId = screw, quantity = 1, serialNumbers = new[] { "X" } }),
            HttpStatusCode.UnprocessableEntity,
            "lines[0].serialNumbers");

        var tooMany = Enumerable.Range(0, 101).Select(_ => ScrewLine(screw, 1)).ToArray();
        var response = await api.InAsync(a1, tooMany);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SerialNumbers_AreUniqueWithinProduct()
    {
        var (api, laptop, _, _, a1, _) = await SetupAsync();
        var serial = $"SN-{Guid.NewGuid():N}"[..12];
        await OkAsync(await api.InAsync(a1, LaptopLine(laptop, 1, serial)));

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, LaptopLine(laptop, 1, "OTHER"), LaptopLine(laptop, 1, serial)),
            HttpStatusCode.UnprocessableEntity,
            "lines[1].serialNumbers",
            InventoryErrors.SerialNumberTaken);

        await ShouldBeLineErrorAsync(
            await api.InAsync(a1, LaptopLine(laptop, 2, "DUP", "DUP")),
            HttpStatusCode.UnprocessableEntity,
            "lines[0].serialNumbers",
            InventoryErrors.SerialNumberTaken);
    }

    [Fact]
    public async Task OutOfAssignedOrDisposedItem_AndTransferToSameLocation_AreRejected()
    {
        var (api, laptop, screw, _, a1, b2) = await SetupAsync();
        var received = await OkAsync(await api.InAsync(a1, LaptopLine(laptop, 1), ScrewLine(screw, 10)));
        var item = received.GetProperty("createdItems")[0].GetProperty("id").GetGuid();
        await OkAsync(await api.OutAsync("Lost", null, Item(item)));

        await ShouldBeLineErrorAsync(await api.TransferAsync(b2, Item(item)), HttpStatusCode.UnprocessableEntity, "lines[0].itemId", InventoryErrors.InvalidItemTransition);
        await ShouldBeLineErrorAsync(await api.OutAsync("Lost", null, Item(item)), HttpStatusCode.UnprocessableEntity, "lines[0].itemId", InventoryErrors.InvalidItemTransition);
        await ShouldBeLineErrorAsync(await api.TransferAsync(a1, Quantity(screw, a1, 1)), HttpStatusCode.UnprocessableEntity, "lines[0].toLocationId", InventoryErrors.SameSourceAndTarget);
        await ShouldBeLineErrorAsync(await api.TransferAsync(b2, Item(item), Item(item)), HttpStatusCode.UnprocessableEntity, "lines[1].itemId");

        var noteMissing = await api.OutAsync("Other", null, Quantity(screw, a1, 1));
        noteMissing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentUpdatesOfTheSameBalance_SecondGetsConcurrencyConflict()
    {
        var (api, _, screw, _, a1, _) = await SetupAsync();
        await OkAsync(await api.InAsync(a1, ScrewLine(screw, 10)));

        // Two operations read the same balance before either saves.
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var (firstBalance, firstSave) = await LoadAndConsumeAsync(first, screw, a1, 6);
        var (secondBalance, secondSave) = await LoadAndConsumeAsync(second, screw, a1, 3);

        await firstSave();
        var act = () => secondSave();

        (await act.Should().ThrowAsync<ConcurrencyException>()).Which.Message.Should().Be("Stok bu arada değişti, tekrar deneyin.");
        firstBalance.Quantity.Should().Be(4);
        secondBalance.Quantity.Should().Be(7);
        (await api.QuantityAtAsync(screw, a1)).Should().Be(4);
    }

    [Fact]
    public async Task ParallelOutRequests_NeverOversellStock()
    {
        var (api, _, screw, _, a1, _) = await SetupAsync();
        await OkAsync(await api.InAsync(a1, ScrewLine(screw, 10)));

        var responses = await Task.WhenAll(
            api.OutAsync("Consumed", null, Quantity(screw, a1, 6)),
            api.OutAsync("Consumed", null, Quantity(screw, a1, 6)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Select(r => r.StatusCode).Should().Contain(s => s == HttpStatusCode.Conflict || s == HttpStatusCode.UnprocessableEntity);
        (await api.QuantityAtAsync(screw, a1)).Should().Be(4);
    }

    [Fact]
    public async Task StockedProductAndLocation_CannotBeDeactivated_AndTrackingTypeIsLocked()
    {
        var (api, laptop, screw, building, a1, _) = await SetupAsync();
        await OkAsync(await api.InAsync(a1, ScrewLine(screw, 5)));

        var deactivateProduct = await api.SendAsync(HttpMethod.Post, $"/api/products/{screw}/deactivate");
        await ShouldBeDomainErrorAsync(deactivateProduct, CatalogErrors.ProductHasStock);

        var deactivateBuilding = await api.SendAsync(HttpMethod.Post, $"/api/locations/{building}/deactivate");
        await ShouldBeDomainErrorAsync(deactivateBuilding, Domain.Locations.LocationErrors.HasContent);

        (await api.JsonAsync(HttpMethod.Get, $"/api/products/{screw}")).GetProperty("trackingTypeLocked").GetBoolean().Should().BeTrue();
        var changeType = await api.SendAsync(HttpMethod.Put, $"/api/products/{screw}", new
        {
            name = "Vida M4",
            categoryId = (await api.JsonAsync(HttpMethod.Get, $"/api/products/{screw}")).GetProperty("categoryId").GetGuid(),
            unit = "Piece",
            trackingType = "Serialized",
            isAssignable = false,
        });
        await ShouldBeDomainErrorAsync(changeType, CatalogErrors.ProductTrackingTypeLocked);

        // Empty again: the location can go, but the product's history still locks its tracking type.
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, a1, 5)));
        (await api.SendAsync(HttpMethod.Post, $"/api/locations/{a1}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await api.SendAsync(HttpMethod.Post, $"/api/products/{screw}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await api.JsonAsync(HttpMethod.Get, $"/api/products/{laptop}")).GetProperty("trackingTypeLocked").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task InactiveTargetOrProduct_IsRejected()
    {
        var (api, _, screw, building, a1, _) = await SetupAsync();
        var empty = await api.LocationAsync("Boş Raf", building);
        (await api.SendAsync(HttpMethod.Post, $"/api/locations/{empty}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldBeLineErrorAsync(await api.InAsync(empty, ScrewLine(screw, 1)), HttpStatusCode.UnprocessableEntity, "toLocationId", InventoryErrors.LocationInactive);

        (await api.SendAsync(HttpMethod.Post, $"/api/products/{screw}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeLineErrorAsync(await api.InAsync(a1, ScrewLine(screw, 1)), HttpStatusCode.UnprocessableEntity, "lines[0].productId", InventoryErrors.ProductInactive);
    }

    [Fact]
    public async Task Items_CanBeListedEditedAndScanned()
    {
        var (api, laptop, _, _, a1, _) = await SetupAsync();
        var received = await OkAsync(await api.InAsync(a1, LaptopLine(laptop, 2, "SN-A")));
        var created = received.GetProperty("createdItems").EnumerateArray().ToList();
        var withoutSerial = created.Single(i => i.GetProperty("serialNumber").ValueKind == System.Text.Json.JsonValueKind.Null);
        var itemId = withoutSerial.GetProperty("id").GetGuid();
        var code = withoutSerial.GetProperty("code").GetString()!;

        (await api.JsonAsync(HttpMethod.Get, $"/api/items?productId={laptop}")).GetProperty("totalCount").GetInt32().Should().Be(2);
        (await api.JsonAsync(HttpMethod.Get, $"/api/items?productId={laptop}&search=sn-a")).GetProperty("totalCount").GetInt32().Should().Be(1);

        await api.JsonAsync(HttpMethod.Put, $"/api/items/{itemId}", new { serialNumber = " SN-B " }, HttpStatusCode.NoContent);
        (await api.ItemAsync(itemId)).GetProperty("serialNumber").GetString().Should().Be("SN-B");
        await ShouldBeDomainErrorAsync(await api.SendAsync(HttpMethod.Put, $"/api/items/{itemId}", new { serialNumber = "SN-A" }), InventoryErrors.SerialNumberTaken);

        var scan = await api.JsonAsync(HttpMethod.Get, $"/api/scan/{code.Replace("-", string.Empty).ToLowerInvariant()}");
        scan.GetProperty("type").GetString().Should().Be("item");
        scan.GetProperty("id").GetGuid().Should().Be(itemId);
        scan.GetProperty("title").GetString().Should().Be($"{code} · SN-B");
        scan.GetProperty("subtitle").GetString().Should().StartWith("Laptop · Bina").And.EndWith("› Raf A1");
        scan.GetProperty("status").GetString().Should().Be("InStock");
    }

    [Fact]
    public async Task Ledger_RejectsUpdatesAndDeletesInTheDatabase()
    {
        var (api, _, screw, _, a1, _) = await SetupAsync();
        await OkAsync(await api.InAsync(a1, ScrewLine(screw, 1)));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = () => db.Database.ExecuteSqlAsync($"UPDATE stock_movements SET quantity = 99 WHERE product_id = {screw}");
        var delete = () => db.Database.ExecuteSqlAsync($"DELETE FROM stock_movements WHERE product_id = {screw}");

        (await update.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
        (await delete.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
    }

    [Fact]
    public async Task Staff_CanViewButNotMoveStock()
    {
        var (api, _, screw, _, a1, _) = await SetupAsync();
        var client = factory.CreateApiClient();
        var email = AuthApi.UniqueEmail();
        await AuthApi.CreateUserAsync(client, api.Token, Roles.Staff, email: email);
        var staff = new InventoryApi(client, (await AuthApi.LoginAsync(client, email, "Passw0rd!")).AccessToken);

        (await staff.InAsync(a1, ScrewLine(screw, 1))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.SendAsync(HttpMethod.Get, "/api/movements")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.SendAsync(HttpMethod.Get, $"/api/products/{screw}/stock")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<(StockBalance Balance, Func<Task> Save)> LoadAndConsumeAsync(
        IServiceScope scope,
        Guid productId,
        Guid locationId,
        decimal quantity)
    {
        var services = scope.ServiceProvider;
        var balance = (await services.GetRequiredService<IStockBalanceRepository>()
            .GetAsync([(productId, locationId)], CancellationToken.None)).Single();
        var product = (await services.GetRequiredService<IProductRepository>().GetByIdsAsync([productId], CancellationToken.None)).Single();
        var location = (await services.GetRequiredService<ILocationRepository>().GetByIdsAsync([locationId], CancellationToken.None)).Single();
        var context = new MovementContext(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var movement = new StockMovementService().ConsumeQuantity(product, location, balance, quantity, MovementReason.Consumed, null, context);
        services.GetRequiredService<IMovementRepository>().AddRange([movement]);

        return (balance, () => services.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
        (await AuthApi.ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be(code);
    }
}
