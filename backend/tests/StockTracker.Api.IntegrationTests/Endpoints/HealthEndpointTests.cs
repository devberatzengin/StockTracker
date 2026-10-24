using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using StockTracker.Api.IntegrationTests.Infrastructure;

namespace StockTracker.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetHealth_WithReachableDatabase_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task GetHealth_WithUnreachableDatabase_ReturnsUnhealthy()
    {
        await using var unreachable = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            TestSettings.Apply(
                builder,
                "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2"));
        var client = unreachable.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
    }
}
