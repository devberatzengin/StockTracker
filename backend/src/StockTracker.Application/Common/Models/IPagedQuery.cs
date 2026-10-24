using FluentValidation;

namespace StockTracker.Application.Common.Models;

public interface IPagedQuery
{
    int Page { get; }

    int PageSize { get; }
}

public sealed class PagedQueryValidator : AbstractValidator<IPagedQuery>
{
    public const int MaxPageSize = 100;

    public PagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Sayfa 1 veya daha büyük olmalıdır.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Sayfa boyutu 1 ile {MaxPageSize} arasında olmalıdır.");
    }
}
