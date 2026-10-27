using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Ping;

namespace StockTracker.Api.IntegrationTests.Security;

/// <summary>PLAN 4.3: login rate limit per client IP, and what Production hides.</summary>
[Collection(ApiCollection.Name)]
public class HardeningTests(ApiFactory factory)
{
    private const int Permit = 3;

    [Fact]
    public async Task Login_IsLimitedPerForwardedClientIp()
    {
        await using var host = WithProxy(factory.WithWebHostBuilder(b =>
            b.UseSetting("RateLimiting:LoginPermitPerMinute", Permit.ToString())));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        for (var i = 0; i < Permit; i++)
        {
            (await LoginAsync(client, "203.0.113.10")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await LoginAsync(client, "203.0.113.10");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        var problem = await limited.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        problem!["title"].ToString().Should().Be("Çok fazla deneme yapıldı. Lütfen biraz sonra tekrar deneyin.");

        // Another client behind the same proxy has its own window.
        (await LoginAsync(client, "203.0.113.20")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Production_UnhandledError_HasNoExceptionDetails()
    {
        await using var host = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.ConfigureTestServices(s => s.AddTransient<IRequestHandler<PingQuery, string>, FailingPingHandler>());
        });
        var client = host.CreateClient();

        var response = await client.GetAsync("/api/ping");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Beklenmeyen bir hata oluştu.")
            .And.NotContain(FailingPingHandler.Secret)
            .And.NotContain(nameof(FailingPingHandler))
            .And.NotContain("stackTrace")
            .And.NotContain("exception")
            .And.NotContain(" at ");
    }

    [Fact]
    public async Task Production_HasNoSwagger()
    {
        (await factory.CreateClient().GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var host = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var client = host.CreateClient();

        // Unknown paths fall through to the authenticated fallback policy, so anonymous callers get 401.
        foreach (var path in new[] { "/swagger/v1/swagger.json", "/swagger/index.html" })
        {
            var response = await client.GetAsync(path);
            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("openapi");
        }
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "nobody@stocktracker.test", password = "Wrong12345" }),
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return client.SendAsync(request);
    }

    /// <summary>Requests arrive from a private-network address, like nginx inside Docker.</summary>
    private static WebApplicationFactory<Program> WithProxy(WebApplicationFactory<Program> host) =>
        host.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddTransient<IStartupFilter, ProxyAddressStartupFilter>()));

    private sealed class ProxyAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.5");
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class FailingPingHandler : IRequestHandler<PingQuery, string>
    {
        public const string Secret = "connection-string-password-leak";

        public Task<string> Handle(PingQuery request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Secret);
    }
}
