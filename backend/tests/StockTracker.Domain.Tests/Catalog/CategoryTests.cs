using FluentAssertions;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Tests.Catalog;

public class CategoryTests
{
    [Fact]
    public void Create_TrimsAndNormalizesName()
    {
        var category = Category.Create("  Elektronik ");

        category.Name.Should().Be("Elektronik");
        category.NormalizedName.Should().Be("ELEKTRONIK");
        category.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("", CatalogErrors.CategoryNameRequired)]
    [InlineData("   ", CatalogErrors.CategoryNameRequired)]
    public void Create_WithoutName_Throws(string name, string code)
    {
        var act = () => Category.Create(name);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Create_WithTooLongName_Throws()
    {
        var act = () => Category.Create(new string('a', Category.NameMaxLength + 1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.CategoryNameTooLong);
    }

    [Fact]
    public void Deactivate_WithActiveProducts_Throws()
    {
        var category = Category.Create("Elektronik");

        var act = () => category.Deactivate(hasActiveProducts: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.CategoryHasActiveProducts);
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WithoutActiveProducts_Succeeds()
    {
        var category = Category.Create("Elektronik");

        category.Deactivate(hasActiveProducts: false);

        category.IsActive.Should().BeFalse();
    }
}
