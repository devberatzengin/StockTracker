using FluentAssertions;
using FluentValidation.TestHelper;
using StockTracker.Application.Catalog.Categories;
using StockTracker.Application.Catalog.Products;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Tests.Catalog;

public class CatalogValidatorTests
{
    private static CreateProductCommand ValidProduct() =>
        new("Laptop", null, Guid.NewGuid(), Unit.Piece, TrackingType.Serialized, true, 1,
            [new BarcodeInput("4006381333931", BarcodeFormat.Ean13)]);

    [Fact]
    public void CreateProduct_Valid_Passes()
    {
        new CreateProductCommandValidator().TestValidate(ValidProduct()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateProduct_InvalidFields_Fail()
    {
        var command = ValidProduct() with
        {
            Name = "",
            CategoryId = Guid.Empty,
            Unit = (Unit)99,
            MinStockLevel = -1,
            Barcodes = [new BarcodeInput("  ", BarcodeFormat.Code128)],
        };

        var result = new CreateProductCommandValidator().TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
        result.ShouldHaveValidationErrorFor(x => x.Unit);
        result.ShouldHaveValidationErrorFor(x => x.MinStockLevel);
        result.ShouldHaveValidationErrorFor("Barcodes[0].Value");
    }

    [Fact]
    public void CreateCategory_RequiresNameWithinLimit()
    {
        var validator = new CreateCategoryCommandValidator();

        validator.TestValidate(new CreateCategoryCommand("")).ShouldHaveValidationErrorFor(x => x.Name);
        validator.TestValidate(new CreateCategoryCommand(new string('a', 101))).ShouldHaveValidationErrorFor(x => x.Name);
        validator.TestValidate(new CreateCategoryCommand("Elektronik")).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1, 25, null, true)]
    [InlineData(1, 101, null, false)]
    [InlineData(1, 25, "-category", true)]
    [InlineData(1, 25, "price", false)]
    public void GetProducts_ValidatesPagingAndSort(int page, int pageSize, string? sort, bool valid)
    {
        new GetProductsQueryValidator()
            .TestValidate(new GetProductsQuery(page, pageSize, Sort: sort))
            .IsValid.Should().Be(valid);
    }
}
