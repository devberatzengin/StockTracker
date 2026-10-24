using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Users;

namespace StockTracker.Domain.Tests.Users;

public class UserAdministrationRulesTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static readonly UserAccessState ActiveAdmin = new(Roles.Admin, IsActive: true, CanLogin: true);

    private static Action Change(Guid target, UserAccessState before, UserAccessState after, int activeAdmins) =>
        () => UserAdministrationRules.EnsureChangeAllowed(Actor, target, before, after, activeAdmins);

    [Fact]
    public void AdminDemotingSelf_IsRejected()
    {
        var act = Change(Actor, ActiveAdmin, ActiveAdmin with { Role = Roles.Staff }, activeAdmins: 3);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(UserAdministrationRules.CannotDemoteSelf);
    }

    [Fact]
    public void UserDeactivatingSelf_IsRejected()
    {
        var act = Change(Actor, ActiveAdmin, ActiveAdmin with { IsActive = false }, activeAdmins: 3);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(UserAdministrationRules.CannotDeactivateSelf);
    }

    [Fact]
    public void UserDisablingOwnLogin_IsRejected()
    {
        var act = Change(Actor, ActiveAdmin, ActiveAdmin with { CanLogin = false }, activeAdmins: 3);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(UserAdministrationRules.CannotDisableOwnLogin);
    }

    [Theory]
    [InlineData(Roles.Staff, true, true)]
    [InlineData(Roles.Admin, false, true)]
    [InlineData(Roles.Admin, true, false)]
    public void RemovingTheLastActiveAdmin_IsRejected(string role, bool isActive, bool canLogin)
    {
        var act = Change(Other, ActiveAdmin, new UserAccessState(role, isActive, canLogin), activeAdmins: 1);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(UserAdministrationRules.LastActiveAdmin);
    }

    [Fact]
    public void RemovingAnAdmin_WhenAnotherActiveAdminRemains_IsAllowed()
    {
        var act = Change(Other, ActiveAdmin, ActiveAdmin with { IsActive = false }, activeAdmins: 2);

        act.Should().NotThrow();
    }

    [Fact]
    public void ChangingAnInactiveAdmin_IsAllowedEvenWithOneActiveAdmin()
    {
        var inactiveAdmin = ActiveAdmin with { IsActive = false };

        var act = Change(Other, inactiveAdmin, inactiveAdmin with { Role = Roles.Staff }, activeAdmins: 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void ChangingOwnNonCriticalFields_IsAllowed()
    {
        var act = Change(Actor, ActiveAdmin, ActiveAdmin, activeAdmins: 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void PromotingStaffToAdmin_IsAllowed()
    {
        var staff = new UserAccessState(Roles.Staff, IsActive: true, CanLogin: true);

        var act = Change(Other, staff, staff with { Role = Roles.Admin }, activeAdmins: 1);

        act.Should().NotThrow();
    }
}
