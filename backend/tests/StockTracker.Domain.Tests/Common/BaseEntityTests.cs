using FluentAssertions;
using StockTracker.Domain.Common;

namespace StockTracker.Domain.Tests.Common;

public class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity;

    [Fact]
    public void NewEntity_HasNonEmptyUniqueId()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        first.Id.Should().NotBeEmpty();
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void NewEntity_HasNoAuditValues()
    {
        var entity = new TestEntity();

        entity.CreatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }
}
