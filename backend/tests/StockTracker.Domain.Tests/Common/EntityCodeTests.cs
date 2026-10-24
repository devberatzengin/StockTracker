using FluentAssertions;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Domain.Tests.Common;

public class EntityCodeTests
{
    [Fact]
    public void Format_JoinsPrefixAndBody()
    {
        EntityCode.Format(CodePrefix.Item, "7F3K9Q").Should().Be("ITM-7F3K9Q");
    }

    [Theory]
    [InlineData("PRD-7F3K9Q", CodePrefix.Product, "PRD-7F3K9Q")]
    [InlineData("  itm-7f3k9q ", CodePrefix.Item, "ITM-7F3K9Q")]
    [InlineData("ITM7F3K9Q", CodePrefix.Item, "ITM-7F3K9Q")]
    [InlineData("loc10a0b1", CodePrefix.Location, "LOC-10A0B1")]
    [InlineData("LOC-IOAOBL", CodePrefix.Location, "LOC-10A0B1")]
    [InlineData("prdlOaObi", CodePrefix.Product, "PRD-10A0B1")]
    public void TryParsePrefixed_NormalizesCodes(string input, CodePrefix expectedPrefix, string expectedCode)
    {
        EntityCode.TryParsePrefixed(input, out var prefix, out var code, out var isWellFormed).Should().BeTrue();

        prefix.Should().Be(expectedPrefix);
        code.Should().Be(expectedCode);
        isWellFormed.Should().BeTrue();
    }

    [Theory]
    [InlineData("PRD-12")]
    [InlineData("PRD-1234567")]
    [InlineData("PRD-12345U")]
    public void TryParsePrefixed_FlagsMalformedBodies(string input)
    {
        EntityCode.TryParsePrefixed(input, out _, out _, out var isWellFormed).Should().BeTrue();

        isWellFormed.Should().BeFalse();
    }

    [Theory]
    [InlineData("4006381333931")]
    [InlineData("ABC-123")]
    [InlineData("")]
    public void TryParsePrefixed_ReturnsFalseWithoutKnownPrefix(string input)
    {
        EntityCode.TryParsePrefixed(input, out _, out _, out _).Should().BeFalse();
    }
}
