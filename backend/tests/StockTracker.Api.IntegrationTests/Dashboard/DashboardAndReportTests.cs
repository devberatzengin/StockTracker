using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.IntegrationTests.Inventory;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;
using static StockTracker.Api.IntegrationTests.Inventory.InventoryApi;

namespace StockTracker.Api.IntegrationTests.Dashboard;

/// <summary>
/// Runs on its own empty database (exact totals). The data is built once:
/// Vida M4 (min 100) 50 in, 5 out → 45; Kablo 10 (no minimum); 3 laptops (min 3): one assigned
/// and overdue by 3 days, one in repair; two extra screw movements at the Istanbul midnight boundary.
/// </summary>
[Collection(DashboardCollection.Name)]
public class DashboardAndReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Seed? _seed;

    private readonly HttpClient _client = factory.CreateApiClient();

    private sealed record Seed(InventoryApi Api, Guid Screw, Guid Laptop, Guid Cable, string ScrewCode, string[] LaptopCodes, string StaffEmail);

    private static DateOnly TodayIstanbul => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    public async Task InitializeAsync()
    {
        await Gate.WaitAsync();
        try
        {
            _seed ??= await SeedAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Seed> SeedAsync()
    {
        var api = await InventoryApi.CreateAsync(factory);
        var category = await api.CategoryAsync();
        var screw = await api.QuantityProductAsync(category, "Vida M4");
        var cable = (await api.JsonAsync(HttpMethod.Post, "/api/products", new { name = "Kablo", categoryId = category, unit = "Meter", trackingType = "Quantity", isAssignable = false }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var laptop = (await api.JsonAsync(HttpMethod.Post, "/api/products", new { name = "Laptop", categoryId = category, unit = "Piece", trackingType = "Serialized", isAssignable = true, minStockLevel = 3 }, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var shelf = await api.LocationAsync("Raf A1");

        await OkAsync(await api.InAsync(shelf, new { productId = screw, quantity = 50 }, new { productId = cable, quantity = 10 }));
        var items = (await OkAsync(await api.InAsync(shelf, new { productId = laptop, quantity = 3, serialNumbers = new[] { "SN-1", "SN-2", "SN-3" } })))
            .GetProperty("createdItems").EnumerateArray().ToList();
        await OkAsync(await api.OutAsync("Consumed", null, Quantity(screw, shelf, 5)));

        var staffEmail = UniqueEmail();
        var staff = await CreateUserAsync(_client, api.Token, Roles.Staff, email: staffEmail);
        var assignment = (await OkAsync(await api.SendAsync(HttpMethod.Post, "/api/assignments", new
        {
            itemId = items[0].GetProperty("id").GetGuid(),
            assigneeUserId = staff,
            dueDate = TodayIstanbul.AddDays(1).ToString("yyyy-MM-dd"),
        }))).GetProperty("assignmentId").GetGuid();
        await api.JsonAsync(HttpMethod.Post, $"/api/items/{items[1].GetProperty("id").GetGuid()}/send-to-repair", new { note = "Ekran" });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Overdue by 3 days (the API only accepts future due dates).
            var threeDaysAgo = TodayIstanbul.AddDays(-3);
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(
                db.Database, $"UPDATE assignments SET due_date = {threeDaysAgo} WHERE id = {assignment}");

            // Istanbul midnight of today is 21:00 UTC of the previous day: one movement on each side.
            var midnightUtc = DateTime.SpecifyKind(TodayIstanbul.ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
            foreach (var at in new[] { midnightUtc, midnightUtc.AddSeconds(-1) })
            {
                db.StockMovements.Add(StockMovement.Create(
                    MovementType.Adjustment, screw, null, null, shelf, 1, null, null, new MovementContext(Guid.NewGuid(), staff, at)));
            }

            await db.SaveChangesAsync();
        }

        var screwCode = (await api.JsonAsync(HttpMethod.Get, $"/api/products/{screw}")).GetProperty("code").GetString()!;
        return new Seed(api, screw, laptop, cable, screwCode, [.. items.Select(i => i.GetProperty("code").GetString()!)], staffEmail);
    }

    private static XLWorkbook Workbook(byte[] content) => new(new MemoryStream(content));

    private static List<string> Headers(IXLWorksheet sheet) =>
        [.. sheet.Row(1).CellsUsed().Select(c => c.GetString())];

    private async Task<byte[]> DownloadAsync(string url)
    {
        var response = await _seed!.Api.SendAsync(HttpMethod.Get, url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return await response.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task Dashboard_CountsTheSeededData()
    {
        var dashboard = await _seed!.Api.JsonAsync(HttpMethod.Get, "/api/dashboard");

        var cards = dashboard.GetProperty("cards");
        cards.GetProperty("activeProducts").GetInt32().Should().Be(3);
        cards.GetProperty("totalItems").GetInt32().Should().Be(3);
        cards.GetProperty("lowStockProducts").GetInt32().Should().Be(2);
        cards.GetProperty("openAssignments").GetInt32().Should().Be(1);
        cards.GetProperty("overdueAssignments").GetInt32().Should().Be(1);
        cards.GetProperty("itemsInRepair").GetInt32().Should().Be(1);

        var statuses = dashboard.GetProperty("itemStatuses");
        (statuses.GetProperty("inStock").GetInt32(), statuses.GetProperty("assigned").GetInt32(), statuses.GetProperty("inRepair").GetInt32())
            .Should().Be((1, 1, 1));

        // 30 days ending today; the midnight-boundary movements land on the right days.
        var days = dashboard.GetProperty("dailyMovements").EnumerateArray().ToList();
        days.Should().HaveCount(30);
        var today = days[^1];
        today.GetProperty("date").GetString().Should().Be(TodayIstanbul.ToString("yyyy-MM-dd"));
        today.GetProperty("in").GetInt32().Should().Be(5);
        today.GetProperty("out").GetInt32().Should().Be(1);
        today.GetProperty("assign").GetInt32().Should().Be(1);
        today.GetProperty("statusChange").GetInt32().Should().Be(1);
        today.GetProperty("adjustment").GetInt32().Should().Be(1);
        days[^2].GetProperty("adjustment").GetInt32().Should().Be(1);
        days.Take(28).Should().OnlyContain(d => d.GetProperty("in").GetInt32() == 0);

        dashboard.GetProperty("topProducts").EnumerateArray()
            .Select(p => (p.GetProperty("name").GetString(), p.GetProperty("movementCount").GetInt32()))
            .Should().Equal(("Laptop", 5), ("Vida M4", 4), ("Kablo", 1));

        var low = dashboard.GetProperty("lowStock").EnumerateArray().ToList();
        low.Select(l => (l.GetProperty("name").GetString(), l.GetProperty("total").GetDecimal(), l.GetProperty("shortage").GetDecimal()))
            .Should().Equal(("Vida M4", 45m, 55m), ("Laptop", 1m, 2m));

        var overdue = dashboard.GetProperty("overdueAssignments").EnumerateArray().Should().ContainSingle().Subject;
        overdue.GetProperty("itemCode").GetString().Should().Be(_seed.LaptopCodes[0]);
        overdue.GetProperty("assigneeName").GetString().Should().Be("Test Staff");
        overdue.GetProperty("daysOverdue").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task StockReport_HasTheTwoPlannedSheets()
    {
        using var workbook = Workbook(await DownloadAsync("/api/reports/stock.xlsx"));

        workbook.Worksheets.Select(w => w.Name).Should().Equal("Stok durumu", "Lokasyon dağılımı");
        var summary = workbook.Worksheet("Stok durumu");
        Headers(summary).Should().Equal("Ürün kodu", "Ad", "Kategori", "Takip tipi", "Birim", "Toplam", "Minimum", "Kritik mi");
        var screw = summary.RowsUsed().Single(r => r.Cell(1).GetString() == _seed!.ScrewCode);
        screw.Cell(2).GetString().Should().Be("Vida M4");
        screw.Cell(4).GetString().Should().Be("Miktar bazlı");
        screw.Cell(6).GetValue<decimal>().Should().Be(45);
        screw.Cell(7).GetValue<decimal>().Should().Be(100);
        screw.Cell(8).GetString().Should().Be("Evet");
        summary.RowsUsed().Single(r => r.Cell(2).GetString() == "Kablo").Cell(8).GetString().Should().Be("Hayır");

        var distribution = workbook.Worksheet("Lokasyon dağılımı");
        Headers(distribution).Should().Equal("Ürün kodu", "Ürün", "Lokasyon kodu", "Lokasyon", "Miktar", "Birim");
        // Laptops: one in stock and one in repair are on the shelf; the assigned one is not.
        distribution.RowsUsed().Single(r => r.Cell(2).GetString() == "Laptop").Cell(5).GetValue<decimal>().Should().Be(2);
    }

    [Fact]
    public async Task MovementReport_UsesIstanbulTimes()
    {
        var day = TodayIstanbul.ToString("yyyy-MM-dd");
        using var workbook = Workbook(await DownloadAsync($"/api/reports/movements.xlsx?from={day}&to={day}"));

        var sheet = workbook.Worksheet("Hareketler");
        Headers(sheet).Should().Equal("Tarih", "Tip", "Ürün", "Item kodu", "Seri no", "Kaynak", "Hedef", "Miktar", "Kullanıcı", "Sebep", "Not", "Referans");
        var rows = sheet.RowsUsed().Skip(1).ToList();
        rows.Should().HaveCount(9, "today's movements only; the one before Istanbul midnight is yesterday");

        var repair = rows.Single(r => r.Cell(2).GetString() == "Durum değişikliği");
        repair.Cell(4).GetString().Should().Be(_seed!.LaptopCodes[1]);
        repair.Cell(11).GetString().Should().Be("Ekran");
        var written = repair.Cell(1).GetDateTime();
        written.Should().BeCloseTo(DateTime.UtcNow.AddHours(3), TimeSpan.FromMinutes(10));

        var consumed = rows.Single(r => r.Cell(2).GetString() == "Çıkış");
        consumed.Cell(10).GetString().Should().Be("Tüketildi");
        consumed.Cell(6).GetString().Should().StartWith("Raf A1");
    }

    [Fact]
    public async Task AssignmentReport_ShowsTheStatus()
    {
        using var workbook = Workbook(await DownloadAsync("/api/reports/assignments.xlsx"));

        var sheet = workbook.Worksheet("Zimmetler");
        Headers(sheet).Should().Equal("Item kodu", "Ürün", "Seri no", "Alan kişi", "Departman", "Veriliş", "Teslim", "İade", "Durum");
        var row = sheet.Row(2);
        row.Cell(1).GetString().Should().Be(_seed!.LaptopCodes[0]);
        row.Cell(4).GetString().Should().Be("Test Staff");
        row.Cell(5).GetString().Should().Be("Depo");
        row.Cell(7).GetDateTime().Should().Be(TodayIstanbul.AddDays(-3).ToDateTime(TimeOnly.MinValue));
        row.Cell(9).GetString().Should().Be("Gecikmiş");
    }

    [Fact]
    public async Task Reports_NeedReportPermission_AndValidDates()
    {
        var staff = (await LoginAsync(_client, _seed!.StaffEmail, "Passw0rd!")).AccessToken;
        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/dashboard", staff))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/reports/stock.xlsx", staff))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reversed = await _seed.Api.SendAsync(HttpMethod.Get, "/api/reports/movements.xlsx?from=2026-10-10&to=2026-10-01");
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonDocument.Parse(await reversed.Content.ReadAsStringAsync()).RootElement.GetProperty("errors").TryGetProperty("to", out _).Should().BeTrue();
    }
}
