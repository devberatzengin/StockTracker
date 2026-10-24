using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using ValidationException = StockTracker.Application.Common.Exceptions.ValidationException;

namespace StockTracker.Infrastructure.Identity;

internal static class IdentityErrors
{
    public static void ThrowIfFailed(this IdentityResult result, string propertyName)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(propertyName, e.Description)));
        }
    }

    public static ValidationException Validation(string propertyName, string message) =>
        new([new ValidationFailure(propertyName, message)]);
}
