using StockTracker.Domain.Users;

namespace StockTracker.Application.Common.Security;

/// <summary>
/// Authorization policy names and the role matrix from the plan. The API registers its
/// policies from this table and /auth/me reports the same permissions.
/// </summary>
public static class Permissions
{
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanManageCatalog = nameof(CanManageCatalog);
    public const string CanMoveStock = nameof(CanMoveStock);
    public const string CanApproveCount = nameof(CanApproveCount);
    public const string CanViewReports = nameof(CanViewReports);
    public const string CanViewAudit = nameof(CanViewAudit);

    private static readonly IReadOnlyDictionary<string, string[]> RolesByPermission = new Dictionary<string, string[]>
    {
        [CanManageUsers] = [Roles.Admin],
        [CanManageCatalog] = [Roles.Admin, Roles.WarehouseManager],
        [CanMoveStock] = [Roles.Admin, Roles.WarehouseManager],
        [CanApproveCount] = [Roles.Admin, Roles.WarehouseManager],
        [CanViewReports] = [Roles.Admin, Roles.WarehouseManager],
        [CanViewAudit] = [Roles.Admin],
    };

    public static IReadOnlyCollection<string> All => (IReadOnlyCollection<string>)RolesByPermission.Keys;

    public static IReadOnlyList<string> RolesFor(string permission) => RolesByPermission[permission];

    public static IReadOnlyList<string> ForRole(string role) =>
        RolesByPermission.Where(kv => kv.Value.Contains(role)).Select(kv => kv.Key).ToList();
}
