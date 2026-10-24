using System.Net;
using FluentAssertions;
using StockTracker.Api.IntegrationTests.Infrastructure;

namespace StockTracker.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class PingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetPing_ReturnsPong()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("pong");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetailsWithTraceId()
    {
        var client = factory.CreateApiClient();
        var session = await AuthApi.LoginAsync(client);

        var response = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, "/api/does-not-exist", session.AccessToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("traceId");
    }

    [Fact]
    public async Task UnknownRoute_WithoutToken_Returns401()
    {
        // The fallback policy also covers unmatched routes, so anonymous callers cannot probe them.
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task SwaggerDocument_IsServedInDevelopment()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("/api/ping");
    }
}
