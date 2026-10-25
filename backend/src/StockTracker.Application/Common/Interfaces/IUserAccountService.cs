using StockTracker.Application.Common.Models;
using StockTracker.Application.Users;

namespace StockTracker.Application.Common.Interfaces;

public sealed record UserProfile(Guid Id, string FullName, string? Email, string? Department, string Role);

public sealed record UserListCriteria(int Page, int PageSize, string? Search, string? Sort);

public sealed record NewUserData(
    string FullName,
    string? Department,
    string? Email,
    string? Password,
    string Role,
    bool CanLogin);

public sealed record UserUpdateData(
    string FullName,
    string? Department,
    string? Email,
    string? Password,
    string Role,
    bool CanLogin);

/// <summary>
/// User administration backed by ASP.NET Core Identity. Users live in Infrastructure,
/// so reads go through this service instead of IAppDbContext.
/// </summary>
public interface IUserAccountService
{
    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> ListAsync(UserListCriteria criteria, CancellationToken cancellationToken);

    /// <summary>Full names of the given users (including inactive ones), for history lists.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<Guid> CreateAsync(NewUserData data, CancellationToken cancellationToken);

    Task UpdateAsync(Guid actorId, Guid userId, UserUpdateData data, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid actorId, Guid userId, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);
}
