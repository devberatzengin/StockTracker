using System.Text.Json;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockTracker.Api.ExceptionHandling;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Api.IntegrationTests.ExceptionHandling;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int> StatusCases => new()
    {
        { new ValidationException([new ValidationFailure("Name", "Name is required.")]), 400 },
        { new NotFoundException("Product", Guid.Empty), 404 },
        { new DomainException("Product.TrackingTypeLocked", "Locked."), 422 },
        { new ConcurrencyException(), 409 },
        { new ForbiddenException(), 403 },
        { new InvalidOperationException("boom"), 500 },
    };

    [Theory]
    [MemberData(nameof(StatusCases))]
    public async Task TryHandleAsync_MapsExceptionToStatusCodeWithTraceId(Exception exception, int expectedStatus)
    {
        var (handled, context, body) = await HandleAsync(exception);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        body.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_IncludesFieldErrors()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Quantity", "Quantity must be positive."),
        ]);

        var (_, _, body) = await HandleAsync(exception);

        var errors = body.GetProperty("errors");
        errors.GetProperty("Name")[0].GetString().Should().Be("Name is required.");
        errors.GetProperty("Quantity")[0].GetString().Should().Be("Quantity must be positive.");
    }

    [Fact]
    public async Task TryHandleAsync_DomainException_IncludesErrorCode()
    {
        var (_, _, body) = await HandleAsync(new DomainException("Product.TrackingTypeLocked", "Locked."));

        body.GetProperty("code").GetString().Should().Be("Product.TrackingTypeLocked");
        body.GetProperty("detail").GetString().Should().Be("Locked.");
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_DoesNotLeakDetails()
    {
        var (_, _, body) = await HandleAsync(new InvalidOperationException("connection string secret"));

        body.GetRawText().Should().NotContain("connection string secret");
    }

    private static async Task<(bool Handled, HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApiProblemDetails()
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (handled, context, document.RootElement.Clone());
    }
}
