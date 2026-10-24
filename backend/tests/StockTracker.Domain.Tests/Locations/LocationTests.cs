using FluentAssertions;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Locations;

namespace StockTracker.Domain.Tests.Locations;

public class LocationTests
{
    private static Location Root(string name = "Bina") => Location.Create("LOC-AAAAAA", name, LocationType.Building, null);

    private static Location Child(Location parent, string name, LocationType type = LocationType.Room) =>
        Location.Create("LOC-BBBBBB", name, type, parent);

    /// <summary>Builds root → … with <paramref name="levels"/> nodes; returns the deepest.</summary>
    private static Location Chain(int levels)
    {
        var node = Root();
        for (var i = 1; i < levels; i++)
        {
            node = Child(node, $"Seviye {i}");
        }

        return node;
    }

    [Fact]
    public void Root_HasDepthZeroAndOwnPath()
    {
        var root = Root();

        root.ParentId.Should().BeNull();
        root.Depth.Should().Be(0);
        root.Path.Should().Be($"/{root.Id}/");
    }

    [Fact]
    public void Child_ExtendsParentPath()
    {
        var root = Root();
        var room = Child(root, "Oda");
        var cabinet = Child(room, "Dolap", LocationType.Cabinet);

        cabinet.ParentId.Should().Be(room.Id);
        cabinet.Depth.Should().Be(2);
        cabinet.Path.Should().Be($"/{root.Id}/{room.Id}/{cabinet.Id}/");
    }

    [Fact]
    public void Create_BeyondMaxDepth_IsRejected()
    {
        var deepest = Chain(Location.MaxDepth + 1);
        deepest.Depth.Should().Be(Location.MaxDepth);

        var act = () => Child(deepest, "Fazla");

        act.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.MaxDepthExceeded);
    }

    [Fact]
    public void Create_UnderInactiveParent_IsRejected()
    {
        var root = Root();
        root.Deactivate(hasContent: false);

        var act = () => Child(root, "Oda");

        act.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.ParentInactive);
    }

    [Theory]
    [InlineData("", LocationErrors.NameRequired)]
    [InlineData("  ", LocationErrors.NameRequired)]
    public void Create_WithoutName_IsRejected(string name, string code)
    {
        var act = () => Root(name);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Create_WithTooLongName_IsRejected()
    {
        var act = () => Root(new string('a', Location.NameMaxLength + 1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.NameTooLong);
    }

    [Fact]
    public void Update_RenamesAndChangesType()
    {
        var location = Root();

        location.Update("  Ana Depo ", LocationType.Other);

        location.Name.Should().Be("Ana Depo");
        location.NormalizedName.Should().Be("ANA DEPO");
        location.Type.Should().Be(LocationType.Other);
    }

    [Fact]
    public void MoveTo_ItselfOrDescendant_IsRejected()
    {
        var root = Root();
        var room = Child(root, "Oda");
        var cabinet = Child(room, "Dolap");

        var intoSelf = () => room.MoveTo(room, subtreeHeight: 1);
        var intoChild = () => room.MoveTo(cabinet, subtreeHeight: 1);

        intoSelf.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.MoveIntoOwnSubtree);
        intoChild.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.MoveIntoOwnSubtree);
        room.ParentId.Should().Be(root.Id);
    }

    [Fact]
    public void MoveTo_NewParent_UpdatesParentPathAndDepth()
    {
        var building = Root("Bina 1");
        var room = Child(building, "Oda");
        var otherBuilding = Root("Bina 2");
        var floor = Child(otherBuilding, "Kat 2");

        room.MoveTo(floor, subtreeHeight: 2);

        room.ParentId.Should().Be(floor.Id);
        room.Depth.Should().Be(2);
        room.Path.Should().Be($"/{otherBuilding.Id}/{floor.Id}/{room.Id}/");
    }

    [Fact]
    public void MoveTo_Root_ClearsParent()
    {
        var room = Child(Root(), "Oda");

        room.MoveTo(null, subtreeHeight: 0);

        room.ParentId.Should().BeNull();
        room.Depth.Should().Be(0);
        room.Path.Should().Be($"/{room.Id}/");
    }

    [Fact]
    public void MoveTo_WhereSubtreeWouldExceedMaxDepth_IsRejected()
    {
        var target = Chain(Location.MaxDepth); // depth 7
        var room = Child(Root("Diğer"), "Oda");

        // room would be at depth 8 and its children at 9.
        var withChildren = () => room.MoveTo(target, subtreeHeight: 1);
        withChildren.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.MaxDepthExceeded);

        // A leaf fits exactly at the limit.
        room.MoveTo(target, subtreeHeight: 0);
        room.Depth.Should().Be(Location.MaxDepth);
    }

    [Fact]
    public void MoveTo_InactiveParentOrInactiveLocation_IsRejected()
    {
        var inactiveTarget = Root("Eski");
        inactiveTarget.Deactivate(hasContent: false);
        var room = Child(Root(), "Oda");

        var intoInactive = () => room.MoveTo(inactiveTarget, subtreeHeight: 0);
        intoInactive.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.ParentInactive);

        room.Deactivate(hasContent: false);
        var moveInactive = () => room.MoveTo(null, subtreeHeight: 0);
        moveInactive.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.Inactive);
    }

    [Fact]
    public void Deactivate_WithContent_IsRejected()
    {
        var location = Root();

        var act = () => location.Deactivate(hasContent: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(LocationErrors.HasContent);
        location.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WhenEmpty_Succeeds()
    {
        var location = Root();

        location.Deactivate(hasContent: false);

        location.IsActive.Should().BeFalse();
    }
}
