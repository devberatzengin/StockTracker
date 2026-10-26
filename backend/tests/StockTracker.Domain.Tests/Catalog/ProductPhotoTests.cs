using FluentAssertions;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Tests.Catalog;

public class ProductPhotoTests
{
    private static Product NewProduct() =>
        Product.Create("PRD-PHOTO1", "Laptop", null, Category.Create("Genel"), Unit.Piece, TrackingType.Serialized, true, null);

    private static ProductPhoto Add(Product product) =>
        product.AddPhoto(Guid.CreateVersion7(), "products/p/a.webp", "products/p/a_thumb.webp", "image/webp", 1000);

    [Fact]
    public void FirstPhoto_IsPrimary_OthersAppendInOrder()
    {
        var product = NewProduct();

        var first = Add(product);
        var second = Add(product);

        first.IsPrimary.Should().BeTrue();
        second.IsPrimary.Should().BeFalse();
        product.Photos.Select(p => p.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public void EleventhPhoto_IsRejected()
    {
        var product = NewProduct();
        for (var i = 0; i < Product.MaxPhotos; i++)
        {
            Add(product);
        }

        var act = () => Add(product);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductPhotoLimit);
        product.Photos.Should().HaveCount(Product.MaxPhotos);
    }

    [Fact]
    public void RemovingThePrimary_PromotesTheNextOne_AndRenumbers()
    {
        var product = NewProduct();
        var first = Add(product);
        var second = Add(product);
        var third = Add(product);

        product.RemovePhoto(first.Id);

        second.IsPrimary.Should().BeTrue();
        product.Photos.Should().Equal(second, third);
        product.Photos.Select(p => p.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public void RemovingTheLastPhoto_LeavesNoPrimary()
    {
        var product = NewProduct();
        var only = Add(product);

        product.RemovePhoto(only.Id);

        product.Photos.Should().BeEmpty();
    }

    [Fact]
    public void SetPrimary_MovesThePrimaryFlag()
    {
        var product = NewProduct();
        var first = Add(product);
        var second = Add(product);

        product.SetPrimaryPhoto(second.Id);

        first.IsPrimary.Should().BeFalse();
        second.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Reorder_NeedsEveryPhotoExactlyOnce()
    {
        var product = NewProduct();
        var first = Add(product);
        var second = Add(product);
        var third = Add(product);

        product.ReorderPhotos([third.Id, first.Id, second.Id]);
        product.Photos.Should().Equal(third, first, second);
        first.IsPrimary.Should().BeTrue("reordering does not change the primary photo");

        var missing = () => product.ReorderPhotos([third.Id, first.Id]);
        var duplicate = () => product.ReorderPhotos([third.Id, third.Id, first.Id]);
        var foreign = () => product.ReorderPhotos([third.Id, first.Id, Guid.NewGuid()]);
        missing.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductPhotoOrderInvalid);
        duplicate.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductPhotoOrderInvalid);
        foreign.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductPhotoOrderInvalid);
    }

    [Fact]
    public void UnknownPhoto_IsRejected()
    {
        var product = NewProduct();

        var act = () => product.RemovePhoto(Guid.NewGuid());

        act.Should().Throw<DomainException>().Which.Code.Should().Be(CatalogErrors.ProductPhotoNotFound);
    }
}
