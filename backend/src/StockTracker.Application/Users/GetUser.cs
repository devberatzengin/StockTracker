using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Users;

public sealed record GetUserQuery(Guid Id) : IRequest<UserDto>;

/// <summary>Active users for pickers; searches name and department.</summary>
public sealed record LookupUsersQuery(string? Search = null, int Limit = 20) : IRequest<IReadOnlyList<UserLookupDto>>;

public sealed class LookupUsersQueryValidator : AbstractValidator<LookupUsersQuery>
{
    public LookupUsersQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class GetUserQueryHandler(IUserAccountService users) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken) =>
        await users.GetAsync(request.Id, cancellationToken) ?? throw new NotFoundException("Kullanıcı", request.Id);
}

public sealed class LookupUsersQueryHandler(IUserAccountService users)
    : IRequestHandler<LookupUsersQuery, IReadOnlyList<UserLookupDto>>
{
    public Task<IReadOnlyList<UserLookupDto>> Handle(LookupUsersQuery request, CancellationToken cancellationToken) =>
        users.LookupAsync(request.Search?.Trim(), request.Limit, cancellationToken);
}
