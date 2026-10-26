using FluentAssertions;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Tests.Catalog;

public class ProductTests
{
    private static readonly Category Category = Category.Create("Genel");

    internal static Product NewQuantityProduct(decimal? minStock = 100) =>
        Product.Create("PRD-ABC123", "Vida M4", null, Category, Unit.Piece, TrackingType.Quantity, false, minStock);

    private static Product NewSerializedProduct(bool isAssignable = true) =>
        Product.Create("PRD-XYZ789", "Laptop", "14 inç", Category, Unit.Piece, TrackingType.Serialized, isAssignable, 2);

    private static Action Create(
        Unit unit = Unit.Piece,
        TrackingType trackingType = TrackingType.Quantity,
        bool isAssignable = false,
        decimal? minStock = null,
        string name = "Ürün",
        string? description = null,
        Category? category = null) =>
        () => Product.Create("PRD-AAAAAA", name, description, category ?? Category, unit, trackingType, isAssignable, minStock);

    [Fact]
    public void Create_SetsFields()
    {
        var product = NewSerializedProduct();

        product.Code.Should().Be("PRD-XYZ789");
        product.Name.Should().Be("Laptop");
        product.CategoryId.Should().Be(Category.Id);
        product.TrackingType.Should().Be(TrackingType.Serialized);
        product.IsAssignable.Should().BeTrue();
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AssignableQuantityProduct_IsRejected()
    {
        Create(trackingType: TrackingType.Quantity, isAssignable: true)
            .Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductAssignableRequiresSerialized);
    }

    [Theory]
    [InlineData(Unit.Kg)]
    [InlineData(Unit.Box)]
    public void SerializedProduct_MustUsePiece(Unit unit)
    {
        Create(unit: unit, trackingType: TrackingType.Serialized)
            .Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductSerializedRequiresPiece);
    }

    [Fact]
    public void QuantityProduct_MayUseAnyUnit()
    {
        Create(unit: Unit.Kg, minStock: 2.5m).Should().NotThrow();
    }

    [Fact]
    public void NegativeMinStock_IsRejected()
    {
        Create(minStock: -1).Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductMinStockNegative);
    }

    [Fact]
    public void SerializedProduct_MinStockMustBeWhole()
    {
        Create(trackingType: TrackingType.Serialized, minStock: 1.5m)
            .Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductMinStockMustBeWhole);
    }

    [Theory]
    [InlineData("", CatalogErrors.ProductNameRequired)]
    [InlineData(" ", CatalogErrors.ProductNameRequired)]
    public void MissingName_IsRejected(string name, string code)
    {
        Create(name: name).Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void TooLongNameOrDescription_IsRejected()
    {
        Create(name: new string('a', Product.NameMaxLength + 1))
            .Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductNameTooLong);
        Create(description: new string('a', Product.DescriptionMaxLength + 1))
            .Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductDescriptionTooLong);
    }

    [Fact]
    public void InactiveCategory_IsRejected()
    {
        var inactive = Category.Create("Eski");
        inactive.Deactivate(hasActiveProducts: false);

        Create(category: inactive).Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.CategoryInactive);
    }

    [Fact]
    public void Update_KeepingItsCategoryAfterItWasDeactivated_IsAllowed()
    {
        var category = Category.Create("Eski");
        var product = Product.Create("PRD-AAAAAA", "Ürün", null, category, Unit.Piece, TrackingType.Quantity, false, null);
        category.Deactivate(hasActiveProducts: false);

        var act = () => product.Update("Yeni ad", null, category, Unit.Piece, TrackingType.Quantity, false, null, false);

        act.Should().NotThrow();
        product.Name.Should().Be("Yeni ad");
    }

    [Fact]
    public void Update_ChangingTrackingTypeWithStockHistory_IsRejected()
    {
        var product = NewQuantityProduct();

        var act = () => product.Update("Vida M4", null, Category, Unit.Piece, TrackingType.Serialized, false, 100, hasStockHistory: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductTrackingTypeLocked);
        product.TrackingType.Should().Be(TrackingType.Quantity);
    }

    [Fact]
    public void Update_ChangingTrackingTypeWithoutStockHistory_IsAllowed()
    {
        var product = NewQuantityProduct();

        product.Update("Vida M4", null, Category, Unit.Piece, TrackingType.Serialized, true, 100, hasStockHistory: false);

        product.TrackingType.Should().Be(TrackingType.Serialized);
        product.IsAssignable.Should().BeTrue();
    }

    [Fact]
    public void Update_OtherFieldsWithStockHistory_IsAllowed()
    {
        var product = NewQuantityProduct();

        var act = () => product.Update("Vida M5", "yeni", Category, Unit.Box, TrackingType.Quantity, false, 50, hasStockHistory: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_MakingQuantityProductAssignable_IsRejected()
    {
        var product = NewQuantityProduct();

        var act = () => product.Update("Vida M4", null, Category, Unit.Piece, TrackingType.Quantity, true, 100, false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductAssignableRequiresSerialized);
    }

    [Fact]
    public void Deactivate_WithStock_IsRejected()
    {
        var product = NewQuantityProduct();

        var act = () => product.Deactivate(hasStock: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductHasStock);
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WithoutStock_Succeeds()
    {
        var product = NewQuantityProduct();

        product.Deactivate(hasStock: false);

        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_TurningOffAssignableWithOpenAssignments_IsRejected()
    {
        var product = Product.Create("PRD-LAPTOP", "Laptop", null, Category, Unit.Piece, TrackingType.Serialized, true, null);

        var act = () => product.Update("Laptop", null, Category, Unit.Piece, TrackingType.Serialized, false, null, true, hasOpenAssignments: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductHasOpenAssignments);
        product.IsAssignable.Should().BeTrue();
    }

    [Fact]
    public void Update_KeepingAssignableWithOpenAssignments_IsAllowed()
    {
        var product = Product.Create("PRD-LAPTOP", "Laptop", null, Category, Unit.Piece, TrackingType.Serialized, true, null);

        product.Update("Laptop Pro", null, Category, Unit.Piece, TrackingType.Serialized, true, null, true, hasOpenAssignments: true);

        product.Name.Should().Be("Laptop Pro");
    }
}
