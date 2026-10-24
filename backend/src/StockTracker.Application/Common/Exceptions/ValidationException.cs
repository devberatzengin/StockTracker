using FluentValidation.Results;

namespace StockTracker.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("Bazı alanlar hatalı. Lütfen kontrol edin.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
