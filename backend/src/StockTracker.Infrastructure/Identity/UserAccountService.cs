using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Application.Users;
using StockTracker.Domain.Users;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Infrastructure.Identity;

public sealed class UserAccountService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    IDateTime dateTime) : IUserAccountService
{
    // Serializes changes that may affect the set of active admins (pg_advisory_xact_lock key).
    private const long UserAdministrationLockKey = 0x5374_6f63_6b55_7372; // "StockUsr"

    public Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        UsersWithRole(includeInactive: false)
            .Where(x => x.User.Id == userId && x.User.CanLogin)
            .Select(x => new UserProfile(x.User.Id, x.User.FullName, x.User.Email, x.User.Department, x.Role))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<UserDto>> ListAsync(UserListCriteria criteria, CancellationToken cancellationToken)
    {
        // Admins see inactive users too (status column).
        var query = UsersWithRole(includeInactive: true);

        if (!string.IsNullOrEmpty(criteria.Search))
        {
            var pattern = $"%{EscapeLike(criteria.Search)}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.User.FullName, pattern, "\\")
                || EF.Functions.ILike(x.User.Email ?? string.Empty, pattern, "\\")
                || EF.Functions.ILike(x.User.Department ?? string.Empty, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, criteria.Sort)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => new UserDto(
                x.User.Id,
                x.User.FullName,
                x.User.Department,
                x.User.Email,
                x.Role,
                x.User.CanLogin,
                x.User.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, totalCount, criteria.Page, criteria.PageSize);
    }

    public Task<UserDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        UsersWithRole(includeInactive: true)
            .Where(x => x.User.Id == userId)
            .Select(x => new UserDto(
                x.User.Id,
                x.User.FullName,
                x.User.Department,
                x.User.Email,
                x.Role,
                x.User.CanLogin,
                x.User.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserLookupDto>> LookupAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, pattern, "\\")
                || EF.Functions.ILike(u.Department ?? string.Empty, pattern, "\\"));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(limit)
            .Select(u => new UserLookupDto(u.Id, u.FullName, u.Department, u.CanLogin))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

    public async Task<Guid> CreateAsync(NewUserData data, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await EnsureEmailAvailableAsync(data.Email, excludeUserId: null, cancellationToken);

        var user = new AppUser
        {
            FullName = data.FullName,
            Department = data.Department,
            Email = data.Email,
            CanLogin = data.CanLogin,
        };
        user.UserName = UserNameFor(user);

        var created = data.Password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, data.Password);
        created.ThrowIfFailed("password");
        (await userManager.AddToRoleAsync(user, data.Role)).ThrowIfFailed("role");

        await transaction.CommitAsync(cancellationToken);
        return user.Id;
    }

    public async Task UpdateAsync(Guid actorId, Guid userId, UserUpdateData data, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAdministrationTransactionAsync(cancellationToken);

        var user = await FindAsync(userId, cancellationToken);
        var currentRole = await RoleOfAsync(user.Id, cancellationToken);

        UserAdministrationRules.EnsureChangeAllowed(
            actorId,
            user.Id,
            new UserAccessState(currentRole, user.IsActive, user.CanLogin),
            new UserAccessState(data.Role, user.IsActive, data.CanLogin),
            await CountActiveAdminsAsync(cancellationToken));

        await EnsureEmailAvailableAsync(data.Email, user.Id, cancellationToken);

        var hasPassword = await userManager.HasPasswordAsync(user);
        if (data.CanLogin && !hasPassword && data.Password is null)
        {
            throw IdentityErrors.Validation("password", "Giriş yetkisi verilen kullanıcı için şifre zorunludur.");
        }

        var revokeSessions = user.CanLogin && !data.CanLogin;

        user.FullName = data.FullName;
        user.Department = data.Department;
        user.Email = data.Email;
        user.CanLogin = data.CanLogin;
        user.UserName = UserNameFor(user);
        (await userManager.UpdateAsync(user)).ThrowIfFailed("email");

        if (data.Password is not null)
        {
            await SetPasswordAsync(user, data.Password, hasPassword);
            revokeSessions = true;
        }

        if (currentRole != data.Role)
        {
            if (await userManager.IsInRoleAsync(user, currentRole))
            {
                (await userManager.RemoveFromRoleAsync(user, currentRole)).ThrowIfFailed("role");
            }

            (await userManager.AddToRoleAsync(user, data.Role)).ThrowIfFailed("role");
        }

        if (revokeSessions)
        {
            await db.RevokeAllRefreshTokensAsync(user.Id, dateTime.UtcNow, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAdministrationTransactionAsync(cancellationToken);

        var user = await FindAsync(userId, cancellationToken);
        if (!user.IsActive)
        {
            return;
        }

        var role = await RoleOfAsync(user.Id, cancellationToken);
        UserAdministrationRules.EnsureChangeAllowed(
            actorId,
            user.Id,
            new UserAccessState(role, IsActive: true, user.CanLogin),
            new UserAccessState(role, IsActive: false, user.CanLogin),
            await CountActiveAdminsAsync(cancellationToken));

        user.IsActive = false;
        (await userManager.UpdateAsync(user)).ThrowIfFailed("id");
        await db.RevokeAllRefreshTokensAsync(user.Id, dateTime.UtcNow, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var user = await FindAsync(userId, cancellationToken);
        await SetPasswordAsync(user, newPassword, await userManager.HasPasswordAsync(user));
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await db.RevokeAllRefreshTokensAsync(user.Id, dateTime.UtcNow, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction> BeginAdministrationTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({UserAdministrationLockKey})", cancellationToken);
        return transaction;
    }

    private async Task<AppUser> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("Kullanıcı", userId);

    private async Task SetPasswordAsync(AppUser user, string password, bool hasPassword)
    {
        if (hasPassword)
        {
            (await userManager.RemovePasswordAsync(user)).ThrowIfFailed("password");
        }

        (await userManager.AddPasswordAsync(user, password)).ThrowIfFailed("password");
    }

    private async Task EnsureEmailAvailableAsync(string? email, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        if (email is null)
        {
            return;
        }

        var normalized = userManager.NormalizeEmail(email);
        var taken = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalized && u.Id != excludeUserId, cancellationToken);

        if (taken)
        {
            throw IdentityErrors.Validation("email", "Bu e-posta adresi zaten kullanılıyor.");
        }
    }

    // Active admin = active (query filter), can log in and has the Admin role.
    private Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        UsersWithRole(includeInactive: false)
            .CountAsync(x => x.User.CanLogin && x.Role == Roles.Admin, cancellationToken);

    private Task<string> RoleOfAsync(Guid userId, CancellationToken cancellationToken) =>
        UsersWithRole(includeInactive: true)
            .Where(x => x.User.Id == userId)
            .Select(x => x.Role)
            .SingleAsync(cancellationToken);

    /// <summary>Users have exactly one role; users without one are treated as Staff.</summary>
    private IQueryable<UserWithRole> UsersWithRole(bool includeInactive)
    {
        var users = includeInactive ? db.Users.IgnoreQueryFilters() : db.Users;
        return users.AsNoTracking().Select(u => new UserWithRole
        {
            User = u,
            Role = db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                .FirstOrDefault() ?? Roles.Staff,
        });
    }

    // Login users sign in with their email; others get a generated, unused user name.
    private static string UserNameFor(AppUser user) => user.Email ?? $"user-{user.Id:N}";

    private sealed class UserWithRole
    {
        public required AppUser User { get; init; }

        public required string Role { get; init; }
    }

    private static IQueryable<UserWithRole> ApplySort(IQueryable<UserWithRole> query, string? sort)
    {
        var descending = sort?.StartsWith('-') ?? false;
        IOrderedQueryable<UserWithRole> ordered = sort?.TrimStart('-') switch
        {
            "email" => descending ? query.OrderByDescending(x => x.User.Email) : query.OrderBy(x => x.User.Email),
            "department" => descending
                ? query.OrderByDescending(x => x.User.Department)
                : query.OrderBy(x => x.User.Department),
            "role" => descending ? query.OrderByDescending(x => x.Role) : query.OrderBy(x => x.Role),
            _ => descending ? query.OrderByDescending(x => x.User.FullName) : query.OrderBy(x => x.User.FullName),
        };

        // Stable paging when sort values tie.
        return ordered.ThenBy(x => x.User.Id);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
