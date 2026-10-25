using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Domain.Locations;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Locations;

[Collection(ApiCollection.Name)]
public class LocationEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    // Root names are unique in the shared database, so each test uses its own.
    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    private async Task<string> AdminTokenAsync() => (await LoginAsync(_client)).AccessToken;

    private Task<HttpResponseMessage> CreateRawAsync(string token, string name, string type, Guid? parentId) =>
        _client.SendAsync(Authorized(HttpMethod.Post, "/api/locations", token, new { name, type, parentId }));

    private async Task<Guid> CreateAsync(string token, string name, string type, Guid? parentId = null)
    {
        var response = await CreateRawAsync(token, name, type, parentId);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> MoveAsync(string token, Guid id, Guid? newParentId) =>
        _client.SendAsync(Authorized(HttpMethod.Post, $"/api/locations/{id}/move", token, new { newParentId }));

    private async Task<Dictionary<Guid, Location>> LoadAsync(params Guid[] ids)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Locations
            .IgnoreQueryFilters().AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id);
    }

    /// <summary>Bina › Oda › Dolap › Raf A1 / Raf B2.</summary>
    private async Task<(Guid Building, Guid Room, Guid Cabinet, Guid ShelfA1, Guid ShelfB2)> BuildAcceptanceTreeAsync(string token)
    {
        var building = await CreateAsync(token, Unique("Bina"), "Building");
        var room = await CreateAsync(token, "Oda 101", "Room", building);
        var cabinet = await CreateAsync(token, "Dolap 3", "Cabinet", room);
        var shelfA1 = await CreateAsync(token, "Raf A1", "Shelf", cabinet);
        var shelfB2 = await CreateAsync(token, "Raf B2", "Shelf", cabinet);
        return (building, room, cabinet, shelfA1, shelfB2);
    }

    [Fact]
    public async Task AcceptanceTree_IsBuiltWithCorrectPaths_AndAppearsInTree()
    {
        var token = await AdminTokenAsync();
        var (building, room, cabinet, shelfA1, shelfB2) = await BuildAcceptanceTreeAsync(token);

        var rows = await LoadAsync(building, room, cabinet, shelfA1, shelfB2);
        rows[shelfA1].Path.Should().Be($"/{building}/{room}/{cabinet}/{shelfA1}/");
        rows[shelfA1].Depth.Should().Be(3);
        rows[shelfA1].Code.Should().MatchRegex("^LOC-[0-9A-HJKMNP-TV-Z]{6}$");

        var tree = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/locations/tree", token)));
        var buildingNode = tree.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == building);
        var cabinetNode = buildingNode.GetProperty("children")[0].GetProperty("children")[0];
        cabinetNode.GetProperty("children").EnumerateArray().Select(n => n.GetProperty("name").GetString())
            .Should().Equal("Raf A1", "Raf B2");

        var detail = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/locations/{shelfA1}", token)));
        detail.GetProperty("ancestors").EnumerateArray().Select(a => a.GetProperty("id").GetGuid())
            .Should().Equal(building, room, cabinet);
        detail.GetProperty("type").GetString().Should().Be("Shelf");
    }

    [Fact]
    public async Task MovingAThreeLevelSubtree_RewritesAllPathsAndDepths()
    {
        var token = await AdminTokenAsync();
        var (building, room, cabinet, shelfA1, shelfB2) = await BuildAcceptanceTreeAsync(token);
        var otherBuilding = await CreateAsync(token, Unique("Bina"), "Building");
        var floor = await CreateAsync(token, "Kat 2", "Other", otherBuilding);
        // A sibling whose path shares no prefix must stay untouched.
        var otherRoom = await CreateAsync(token, "Oda 102", "Room", building);

        var move = await MoveAsync(token, room, floor);
        move.StatusCode.Should().Be(HttpStatusCode.NoContent, await move.Content.ReadAsStringAsync());

        var rows = await LoadAsync(room, cabinet, shelfA1, shelfB2, otherRoom);
        var prefix = $"/{otherBuilding}/{floor}/";
        rows[room].Path.Should().Be($"{prefix}{room}/");
        rows[room].ParentId.Should().Be(floor);
        rows[room].Depth.Should().Be(2);
        rows[cabinet].Path.Should().Be($"{prefix}{room}/{cabinet}/");
        rows[cabinet].Depth.Should().Be(3);
        rows[shelfA1].Path.Should().Be($"{prefix}{room}/{cabinet}/{shelfA1}/");
        rows[shelfA1].Depth.Should().Be(4);
        rows[shelfB2].Path.Should().Be($"{prefix}{room}/{cabinet}/{shelfB2}/");
        rows[shelfB2].Depth.Should().Be(4);
        rows[otherRoom].Path.Should().Be($"/{building}/{otherRoom}/");

        // ...and back to the root.
        (await MoveAsync(token, room, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        rows = await LoadAsync(room, shelfB2);
        rows[room].Path.Should().Be($"/{room}/");
        rows[room].Depth.Should().Be(0);
        rows[shelfB2].Path.Should().Be($"/{room}/{cabinet}/{shelfB2}/");
        rows[shelfB2].Depth.Should().Be(2);
    }

    [Fact]
    public async Task MovingIntoOwnSubtree_IsRejected()
    {
        var token = await AdminTokenAsync();
        var (_, room, _, shelfA1, _) = await BuildAcceptanceTreeAsync(token);

        await ShouldBeDomainErrorAsync(await MoveAsync(token, room, shelfA1), LocationErrors.MoveIntoOwnSubtree);
        await ShouldBeDomainErrorAsync(await MoveAsync(token, room, room), LocationErrors.MoveIntoOwnSubtree);
    }

    [Fact]
    public async Task SiblingNames_AreUnique_ButMayRepeatUnderOtherParents()
    {
        var token = await AdminTokenAsync();
        var (building, room, cabinet, _, _) = await BuildAcceptanceTreeAsync(token);

        var duplicate = await CreateRawAsync(token, "raf a1", "Shelf", cabinet);
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(duplicate)).GetProperty("errors").TryGetProperty("name", out _).Should().BeTrue();

        // Same name under another parent is fine.
        (await CreateRawAsync(token, "Raf A1", "Shelf", room)).StatusCode.Should().Be(HttpStatusCode.Created);

        // Moving into a parent that already has a child with the same name is rejected.
        var otherCabinet = await CreateAsync(token, "Dolap 4", "Cabinet", room);
        var shelf = await CreateAsync(token, "Raf B2", "Shelf", otherCabinet);
        (await MoveAsync(token, shelf, cabinet)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Root names are unique too.
        var rootName = (await LoadAsync(building))[building].Name;
        (await CreateRawAsync(token, rootName, "Building", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivation_IncludesTheSubtree()
    {
        var token = await AdminTokenAsync();
        var (building, room, cabinet, shelfA1, shelfB2) = await BuildAcceptanceTreeAsync(token);

        var response = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/locations/{room}/deactivate", token));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rows = await LoadAsync(building, room, cabinet, shelfA1, shelfB2);
        rows[building].IsActive.Should().BeTrue();
        rows[room].IsActive.Should().BeFalse();
        rows[cabinet].IsActive.Should().BeFalse();
        rows[shelfA1].IsActive.Should().BeFalse();
        rows[shelfB2].IsActive.Should().BeFalse();

        var tree = await ReadJsonAsync(await _client.SendAsync(Authorized(HttpMethod.Get, "/api/locations/tree", token)));
        tree.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == building)
            .GetProperty("children").GetArrayLength().Should().Be(0);

        await ShouldBeDomainErrorAsync(await CreateRawAsync(token, "Yeni Raf", "Shelf", cabinet), LocationErrors.ParentInactive);
    }

    [Fact]
    public async Task DepthLimit_IsEnforced()
    {
        var token = await AdminTokenAsync();
        Guid? parent = null;
        for (var depth = 0; depth <= Location.MaxDepth; depth++)
        {
            parent = await CreateAsync(token, depth == 0 ? Unique("Kök") : $"Seviye {depth}", "Other", parent);
        }

        await ShouldBeDomainErrorAsync(await CreateRawAsync(token, "Fazla", "Box", parent), LocationErrors.MaxDepthExceeded);
    }

    [Fact]
    public async Task Staff_CanViewButNotManageLocations()
    {
        var admin = await AdminTokenAsync();
        var email = UniqueEmail();
        await CreateUserAsync(_client, admin, Roles.Staff, email: email);
        var staff = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;

        (await _client.SendAsync(Authorized(HttpMethod.Get, "/api/locations/tree", staff))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CreateRawAsync(staff, Unique("Bina"), "Building", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task ShouldBeDomainErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be(code);
    }
}
