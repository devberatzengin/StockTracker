using FluentAssertions;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_SetsCodeAndMessage()
    {
        var exception = new DomainException("Product.TrackingTypeLocked", "Tracking type cannot be changed.");

        exception.Code.Should().Be("Product.TrackingTypeLocked");
        exception.Message.Should().Be("Tracking type cannot be changed.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankCode_Throws(string code)
    {
        var act = () => new DomainException(code, "message");

        act.Should().Throw<ArgumentException>();
    }
}
