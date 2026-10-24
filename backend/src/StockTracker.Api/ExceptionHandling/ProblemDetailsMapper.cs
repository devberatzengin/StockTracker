using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Scan;
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
            Title = ProblemTitles.For(StatusCodes.Status400BadRequest),
            Detail = e.Message,
        },
        UnauthorizedException e => Create(StatusCodes.Status401Unauthorized, e.Message),
        ScanNotFoundException e => ScanNotFound(e),
        NotFoundException e => Create(StatusCodes.Status404NotFound, e.Message),
        DomainException e => WithCode(Create(StatusCodes.Status422UnprocessableEntity, e.Message), e.Code),
        ConcurrencyException e => Create(StatusCodes.Status409Conflict, e.Message),
        ForbiddenException e => Create(StatusCodes.Status403Forbidden, e.Message),
        // Never leak internal exception details to the client.
        _ => Create(StatusCodes.Status500InternalServerError, detail: null),
    };

    private static ProblemDetails Create(int status, string? detail) =>
        new() { Status = status, Title = ProblemTitles.For(status), Detail = detail };

    public const string SuggestCreateProductKey = "suggestCreateProduct";
    public const string BarcodeKey = "barcode";
    public const string BarcodeFormatKey = "barcodeFormat";

    private static ProblemDetails ScanNotFound(ScanNotFoundException exception)
    {
        var problem = Create(StatusCodes.Status404NotFound, "Bu koda ya da barkoda ait kayıt bulunamadı.");
        problem.Extensions[SuggestCreateProductKey] = exception.SuggestCreateProduct;
        if (exception.SuggestCreateProduct)
        {
            problem.Extensions[BarcodeKey] = exception.SuggestedBarcode;
            problem.Extensions[BarcodeFormatKey] = exception.SuggestedFormat?.ToString();
        }

        return problem;
    }

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions[ErrorCodeKey] = code;
        return problem;
    }
}
