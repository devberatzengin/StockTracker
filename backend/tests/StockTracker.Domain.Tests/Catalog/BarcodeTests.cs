using FluentAssertions;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Tests.Catalog;

public class BarcodeTests
{
    private static Product NewProduct() => ProductTests.NewQuantityProduct();

    [Theory]
    [InlineData("4006381333931", BarcodeFormat.Ean13)]
    [InlineData("73513537", BarcodeFormat.Ean8)]
    [InlineData("036000291452", BarcodeFormat.UpcA)]
    [InlineData("ABC-123/xyz", BarcodeFormat.Code128)]
    [InlineData("any value", BarcodeFormat.Other)]
    public void AddBarcode_AcceptsValidValues(string value, BarcodeFormat format)
    {
        var barcode = NewProduct().AddBarcode(value, format);

        barcode.Format.Should().Be(format);
    }

    [Fact]
    public void AddBarcode_RemovesWhitespace()
    {
        var barcode = NewProduct().AddBarcode(" 4006381 333931\t", BarcodeFormat.Ean13);

        barcode.Value.Should().Be("4006381333931");
    }

    [Theory]
    [InlineData("4006381333932", BarcodeFormat.Ean13)]
    [InlineData("73513538", BarcodeFormat.Ean8)]
    [InlineData("036000291453", BarcodeFormat.UpcA)]
    public void AddBarcode_WithWrongCheckDigit_Throws(string value, BarcodeFormat format)
    {
        var act = () => NewProduct().AddBarcode(value, format);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.BarcodeInvalidCheckDigit);
    }

    [Theory]
    [InlineData("400638133393", BarcodeFormat.Ean13)]
    [InlineData("40063813339311", BarcodeFormat.Ean13)]
    [InlineData("4006381A33931", BarcodeFormat.Ean13)]
    [InlineData("7351353", BarcodeFormat.Ean8)]
    [InlineData("03600029145", BarcodeFormat.UpcA)]
    public void AddBarcode_WithWrongLengthOrNonDigits_Throws(string value, BarcodeFormat format)
    {
        var act = () => NewProduct().AddBarcode(value, format);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.BarcodeInvalidFormat);
    }

    [Fact]
    public void AddBarcode_Empty_Throws()
    {
        var act = () => NewProduct().AddBarcode("   ", BarcodeFormat.Code128);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.BarcodeRequired);
    }

    [Fact]
    public void AddBarcode_TooLong_Throws()
    {
        var act = () => NewProduct().AddBarcode(new string('X', ProductBarcode.ValueMaxLength + 1), BarcodeFormat.Code128);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.BarcodeTooLong);
    }

    [Fact]
    public void AddBarcode_SameValueTwiceOnProduct_Throws()
    {
        var product = NewProduct();
        product.AddBarcode("4006381333931", BarcodeFormat.Ean13);

        var act = () => product.AddBarcode("4006381 333931", BarcodeFormat.Ean13);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductDuplicateBarcode);
    }

    [Fact]
    public void RemoveBarcode_RemovesIt_AndUnknownIdThrows()
    {
        var product = NewProduct();
        var barcode = product.AddBarcode("4006381333931", BarcodeFormat.Ean13);

        product.RemoveBarcode(barcode.Id);
        var act = () => product.RemoveBarcode(barcode.Id);

        product.Barcodes.Should().BeEmpty();
        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductBarcodeNotFound);
    }

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333930", false)]
    [InlineData("", false)]
    [InlineData("12a", false)]
    public void IsCheckDigitValid_FollowsGs1Mod10(string digits, bool expected)
    {
        BarcodeRules.IsCheckDigitValid(digits).Should().Be(expected);
    }
}
