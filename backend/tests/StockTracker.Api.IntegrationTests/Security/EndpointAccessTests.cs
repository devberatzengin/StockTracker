using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Common.Security;
using StockTracker.Domain.Users;
using static StockTracker.Api.IntegrationTests.Infrastructure.AuthApi;

namespace StockTracker.Api.IntegrationTests.Security;

/// <summary>
/// Every endpoint states its access explicitly (a policy, any signed-in user, or anonymous), and
/// the table below lists every endpoint: a new action without a row fails the build of trust.
/// Each row is called once as a user who should not get in.
/// </summary>
[Collection(ApiCollection.Name)]
public class EndpointAccessTests(ApiFactory factory)
{
    private const string Anonymous = nameof(Anonymous);
    private const string AnyUser = nameof(AnyUser);

    /// <summary>Endpoints that work without a token: sign-in, health ping and signed file links.</summary>
    private static readonly HashSet<string> AllowedAnonymous =
    [
        "POST api/auth/login",
        "POST api/auth/refresh",
        "POST api/auth/logout",
        "GET api/ping",
        "GET api/files/{**key}",
    ];

    public static readonly TheoryData<string, string, string> Endpoints = new()
    {
        { "POST", "api/assignments", Permissions.CanMoveStock },
        { "POST", "api/assignments/{id:guid}/return", Permissions.CanMoveStock },
        { "GET", "api/assignments", Permissions.CanMoveStock },
        { "GET", "api/assignments/mine", AnyUser },
        { "GET", "api/assignments/{id:guid}", AnyUser },
        { "GET", "api/audit", Permissions.CanViewAudit },
        { "POST", "api/auth/login", Anonymous },
        { "POST", "api/auth/refresh", Anonymous },
        { "POST", "api/auth/logout", Anonymous },
        { "GET", "api/auth/me", AnyUser },
        { "POST", "api/auth/change-password", AnyUser },
        { "GET", "api/categories", AnyUser },
        { "POST", "api/categories", Permissions.CanManageCatalog },
        { "PUT", "api/categories/{id:guid}", Permissions.CanManageCatalog },
        { "POST", "api/categories/{id:guid}/deactivate", Permissions.CanManageCatalog },
        { "POST", "api/counts", Permissions.CanApproveCount },
        { "GET", "api/counts", AnyUser },
        { "GET", "api/counts/{id:guid}", AnyUser },
        { "POST", "api/counts/{id:guid}/scan", AnyUser },
        { "DELETE", "api/counts/{id:guid}/lines/{lineId:guid}/last-scan", AnyUser },
        { "POST", "api/counts/{id:guid}/submit", AnyUser },
        { "GET", "api/counts/{id:guid}/review", Permissions.CanApproveCount },
        { "POST", "api/counts/{id:guid}/approve", Permissions.CanApproveCount },
        { "POST", "api/counts/{id:guid}/cancel", Permissions.CanApproveCount },
        { "GET", "api/dashboard", Permissions.CanViewReports },
        { "GET", "api/files/{**key}", Anonymous },
        { "GET", "api/items", AnyUser },
        { "GET", "api/items/{id:guid}", AnyUser },
        { "PUT", "api/items/{id:guid}", Permissions.CanMoveStock },
        { "POST", "api/items/{id:guid}/send-to-repair", Permissions.CanMoveStock },
        { "POST", "api/items/{id:guid}/return-from-repair", Permissions.CanMoveStock },
        { "POST", "api/labels", Permissions.CanMoveStock },
        { "GET", "api/locations/tree", AnyUser },
        { "GET", "api/locations/{id:guid}", AnyUser },
        { "GET", "api/locations/{id:guid}/contents", AnyUser },
        { "POST", "api/locations", Permissions.CanManageCatalog },
        { "PUT", "api/locations/{id:guid}", Permissions.CanManageCatalog },
        { "POST", "api/locations/{id:guid}/move", Permissions.CanManageCatalog },
        { "POST", "api/locations/{id:guid}/deactivate", Permissions.CanManageCatalog },
        { "GET", "api/movements", AnyUser },
        { "POST", "api/movements/in", Permissions.CanMoveStock },
        { "POST", "api/movements/out", Permissions.CanMoveStock },
        { "POST", "api/movements/transfer", Permissions.CanMoveStock },
        { "POST", "api/movements/{id:guid}/reverse", Permissions.CanMoveStock },
        { "GET", "api/notifications", AnyUser },
        { "GET", "api/notifications/unread-count", AnyUser },
        { "POST", "api/notifications/{id:guid}/read", AnyUser },
        { "POST", "api/notifications/read-all", AnyUser },
        { "GET", "api/ping", Anonymous },
        { "POST", "api/products/{productId:guid}/photos", Permissions.CanManageCatalog },
        { "PUT", "api/products/{productId:guid}/photos/order", Permissions.CanManageCatalog },
        { "POST", "api/products/{productId:guid}/photos/{photoId:guid}/primary", Permissions.CanManageCatalog },
        { "DELETE", "api/products/{productId:guid}/photos/{photoId:guid}", Permissions.CanManageCatalog },
        { "GET", "api/products", AnyUser },
        { "GET", "api/products/{id:guid}", AnyUser },
        { "GET", "api/products/{id:guid}/stock", AnyUser },
        { "POST", "api/products", Permissions.CanManageCatalog },
        { "PUT", "api/products/{id:guid}", Permissions.CanManageCatalog },
        { "POST", "api/products/{id:guid}/deactivate", Permissions.CanManageCatalog },
        { "POST", "api/products/{id:guid}/barcodes", Permissions.CanManageCatalog },
        { "DELETE", "api/products/{id:guid}/barcodes/{barcodeId:guid}", Permissions.CanManageCatalog },
        { "GET", "api/reports/stock.xlsx", Permissions.CanViewReports },
        { "GET", "api/reports/movements.xlsx", Permissions.CanViewReports },
        { "GET", "api/reports/assignments.xlsx", Permissions.CanViewReports },
        { "GET", "api/scan/{**input}", AnyUser },
        { "GET", "api/users/lookup", Permissions.CanMoveStock },
        { "GET", "api/users", Permissions.CanManageUsers },
        { "GET", "api/users/{id:guid}", Permissions.CanManageUsers },
        { "POST", "api/users", Permissions.CanManageUsers },
        { "PUT", "api/users/{id:guid}", Permissions.CanManageUsers },
        { "POST", "api/users/{id:guid}/deactivate", Permissions.CanManageUsers },
        { "POST", "api/users/{id:guid}/reset-password", Permissions.CanManageUsers },
    };

    private readonly HttpClient _client = factory.CreateApiClient();

    private static string? _staffToken;

    /// <summary>Every controller action, with the access its attributes give it.</summary>
    private static IEnumerable<(string Method, string Route, string Access, MethodInfo Action)> Actions()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var route = string.IsNullOrEmpty(http.Template) ? prefix : $"{prefix}/{http.Template}";
                    foreach (var method in http.HttpMethods)
                    {
                        yield return (method, route, Access(controller, action), action);
                    }
                }
            }
        }
    }

    private static string Access(Type controller, MethodInfo action)
    {
        if (action.IsDefined(typeof(AllowAnonymousAttribute)))
        {
            return Anonymous;
        }

        var policy = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p is not null);
        if (policy is not null)
        {
            return policy;
        }

        if (controller.IsDefined(typeof(AllowAnonymousAttribute)))
        {
            return Anonymous;
        }

        var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>().ToList();
        return classAuthorize.Select(a => a.Policy).FirstOrDefault(p => p is not null)
            ?? (classAuthorize.Count > 0 || action.IsDefined(typeof(AuthorizeAttribute)) ? AnyUser : "none");
    }

    [Fact]
    public void EveryAction_StatesItsAccess_WithPolicyNamesOnly()
    {
        var actions = Actions().ToList();

        actions.Should().NotContain(a => a.Access == "none", "no endpoint may rely on the fallback policy silently");
        actions.Where(a => a.Access == Anonymous).Select(a => $"{a.Method} {a.Route}")
            .Should().BeSubsetOf(AllowedAnonymous);
        actions.Where(a => a.Access is not (Anonymous or AnyUser)).Select(a => a.Access)
            .Should().OnlyContain(p => Permissions.All.Contains(p));

        var withRoles = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>().Concat(
                t.GetMethods().SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>())))
            .Where(a => !string.IsNullOrEmpty(a.Roles));
        withRoles.Should().BeEmpty("authorization uses policy names, never role names");
    }

    [Fact]
    public void TheTable_ListsEveryEndpoint()
    {
        var actual = Actions().Select(a => (a.Method, a.Route, a.Access)).ToHashSet();
        var listed = Endpoints.Select(row => ((string)row[0], (string)row[1], (string)row[2])).ToHashSet();

        actual.Except(listed).Should().BeEmpty("every endpoint needs a row in this table");
        listed.Except(actual).Should().BeEmpty("rows must match real endpoints");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task OutsidersAreTurnedAway(string method, string route, string access)
    {
        if (access == Anonymous)
        {
            return;
        }

        var request = new HttpRequestMessage(new HttpMethod(method), "/" + Sample(route));
        request.Content = route.EndsWith("/photos", StringComparison.Ordinal) && method == "POST"
            ? new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "a.jpg" } }
            : method is "POST" or "PUT" ? JsonContent("{}") : null;

        if (access == AnyUser)
        {
            (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {route} without a token");
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await StaffTokenAsync());
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} as staff");
    }

    private async Task<string> StaffTokenAsync()
    {
        if (_staffToken is null)
        {
            var admin = (await LoginAsync(_client)).AccessToken;
            var email = UniqueEmail();
            await CreateUserAsync(_client, admin, Roles.Staff, email: email);
            _staffToken = (await LoginAsync(_client, email, "Passw0rd!")).AccessToken;
        }

        return _staffToken;
    }

    private static StringContent JsonContent(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    private static string Sample(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, @"\{(\*\*)?(\w+)(:\w+)?\}", m => m.Groups[2].Value switch
        {
            "key" => "products/x/y.webp",
            "input" => "PRD-ABCDEF",
            _ => Guid.NewGuid().ToString(),
        });
}
