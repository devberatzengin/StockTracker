using FluentValidation.Results;
using ValidationException = StockTracker.Application.Common.Exceptions.ValidationException;

namespace StockTracker.Application.Catalog;

internal static class CatalogValidation
{
    public static ValidationException FieldError(string propertyName, string message) =>
        new([new ValidationFailure(propertyName, message)]);
}
