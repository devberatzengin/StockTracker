using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;

namespace StockTracker.Application.Users;

/// <summary>Sort: fullName, email, department or role; prefix with '-' for descending.</summary>
public sealed record GetUsersQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null)
    : IRequest<PagedResult<UserDto>>, IPagedQuery;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public static readonly IReadOnlyList<string> SortFields = ["fullName", "email", "department", "role"];

    public GetUsersQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(s => s is null || SortFields.Contains(s.TrimStart('-')))
            .WithMessage($"Sıralama alanı şunlardan biri olmalıdır: {string.Join(", ", SortFields)}.");
    }
}

public sealed class GetUsersQueryHandler(IUserAccountService users)
    : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken) =>
        users.ListAsync(
            new UserListCriteria(request.Page, request.PageSize, request.Search?.Trim(), request.Sort),
            cancellationToken);
}
