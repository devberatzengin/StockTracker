using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Api.Security;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Security;

/// <summary>Global per-user / per-IP ceilings and the login, refresh and heavy-endpoint policies.</summary>
[Collection(ApiCollection.Name)]
public class RateLimitingTests(ApiFactory factory)
{
    private const int Permit = 3;

    private WebApplicationFactory<Program> HostWith(string limit, int permit = Permit) =>
        factory.WithWebHostBuilder(b => b.UseSetting($"RateLimiting:{limit}PermitPerMinute", permit.ToString()));

    private static HttpClient ClientOf(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string token) =>
        client.SendAsync(Authorized(HttpMethod.Get, url, token));

    [Fact]
    public void LimitedEndpoints_ArePinned()
    {
        var expected = new Dictionary<string, string>
        {
            ["POST api/auth/login"] = ApiRateLimiting.LoginPolicy,
            ["POST api/auth/refresh"] = ApiRateLimiting.RefreshPolicy,
            ["POST api/labels"] = ApiRateLimiting.HeavyPolicy,
            ["POST api/products/{productId:guid}/photos"] = ApiRateLimiting.HeavyPolicy,
            ["GET api/reports/stock.xlsx"] = ApiRateLimiting.HeavyPolicy,
            ["GET api/reports/movements.xlsx"] = ApiRateLimiting.HeavyPolicy,
            ["GET api/reports/assignments.xlsx"] = ApiRateLimiting.HeavyPolicy,
        };

        var actual = new Dictionary<string, string>();
        foreach (var controller in typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var policy = (action.GetCustomAttribute<EnableRateLimitingAttribute>()
                    ?? controller.GetCustomAttribute<EnableRateLimitingAttribute>())?.PolicyName;
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>().Where(_ => policy is not null))
                {
                    var route = string.IsNullOrEmpty(http.Template) ? prefix : $"{prefix}/{http.Template}";
                    foreach (var method in http.HttpMethods)
                    {
                        actual[$"{method} {route}"] = policy!;
                    }
                }
            }
        }

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Refresh_IsLimitedPerClientIp()
    {
        await using var host = HostWith("Refresh");
        var client = ClientOf(host);

        for (var i = 0; i < Permit; i++)
        {
            (await RefreshRawAsync(client, "not-a-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await RefreshRawAsync(client, "not-a-token")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task HeavyEndpoints_AreLimitedPerUser()
    {
        await using var host = HostWith("Heavy");
        var client = ClientOf(host);
        var admin = (await LoginAsync(client)).AccessToken;
        var email = UniqueEmail();
        await CreateUserAsync(client, admin, Roles.WarehouseManager, email: email);
        var manager = (await LoginAsync(client, email, "Passw0rd!")).AccessToken;

        for (var i = 0; i < Permit; i++)
        {
            (await GetAsync(client, "/api/reports/stock.xlsx", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var limited = await GetAsync(client, "/api/reports/stock.xlsx", admin);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();

        // Other endpoints and other users are not affected.
        (await GetAsync(client, "/api/categories", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync(client, "/api/reports/stock.xlsx", manager)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GlobalLimit_IsPerSignedInUser()
    {
        const int permit = 5;
        await using var host = HostWith("User", permit);
        var client = ClientOf(host);
        var admin = (await LoginAsync(client)).AccessToken;
        var email = UniqueEmail();
        await CreateUserAsync(client, admin, Roles.Staff, email: email);
        var staff = (await LoginAsync(client, email, "Passw0rd!")).AccessToken;

        // Creating the user was the admin's first request.
        for (var i = 1; i < permit; i++)
        {
            (await GetAsync(client, "/api/categories", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await GetAsync(client, "/api/categories", admin)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await GetAsync(client, "/api/categories", staff)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GlobalLimit_IsPerIpForAnonymousRequests()
    {
        await using var host = HostWith("Anonymous");
        var client = ClientOf(host);

        for (var i = 0; i < Permit; i++)
        {
            (await client.GetAsync("/api/ping")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.GetAsync("/api/ping")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
