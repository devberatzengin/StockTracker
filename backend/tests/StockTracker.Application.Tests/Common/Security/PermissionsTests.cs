using FluentAssertions;
using StockTracker.Application.Common.Security;
using StockTracker.Domain.Users;

namespace StockTracker.Application.Tests.Common.Security;

/// <summary>Guards the role matrix in docs/PLAN.md (Phase 1.1).</summary>
public class PermissionsTests
{
    [Fact]
    public void Admin_HasEveryPermission()
    {
        Permissions.ForRole(Roles.Admin).Should().BeEquivalentTo(Permissions.All);
    }

    [Fact]
    public void WarehouseManager_HasCatalogStockCountAndReports()
    {
        Permissions.ForRole(Roles.WarehouseManager).Should().BeEquivalentTo(
            Permissions.CanManageCatalog,
            Permissions.CanMoveStock,
            Permissions.CanApproveCount,
            Permissions.CanViewReports);
    }

    [Fact]
    public void Staff_HasNoPolicyPermissions()
    {
        Permissions.ForRole(Roles.Staff).Should().BeEmpty();
    }

    [Theory]
    [InlineData(Permissions.CanManageUsers, new[] { Roles.Admin })]
    [InlineData(Permissions.CanViewAudit, new[] { Roles.Admin })]
    [InlineData(Permissions.CanMoveStock, new[] { Roles.Admin, Roles.WarehouseManager })]
    public void RolesFor_ReturnsAllowedRoles(string permission, string[] roles)
    {
        Permissions.RolesFor(permission).Should().BeEquivalentTo(roles);
    }
}
