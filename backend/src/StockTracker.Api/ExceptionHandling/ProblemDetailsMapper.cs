using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Api.ExceptionHandling;

public static class ProblemDetailsMapper
{
    public const string ErrorCodeKey = "code";

    public static ProblemDetails Map(Exception exception) => exception switch
    {
        ValidationException e => new ValidationProblemDetails(e.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed.",
            Detail = e.Message,
        },
        NotFoundException e => Create(StatusCodes.Status404NotFound, "Resource not found.", e.Message),
        DomainException e => WithCode(
            Create(StatusCodes.Status422UnprocessableEntity, "Business rule violated.", e.Message),
            e.Code),
        ConcurrencyException e => Create(StatusCodes.Status409Conflict, "Concurrency conflict.", e.Message),
        ForbiddenException e => Create(StatusCodes.Status403Forbidden, "Forbidden.", e.Message),
        // Never leak internal exception details to the client.
        _ => Create(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
    };

    private static ProblemDetails Create(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions[ErrorCodeKey] = code;
        return problem;
    }
}
