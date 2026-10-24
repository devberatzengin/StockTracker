using FluentAssertions;
using StockTracker.Application.Common.Codes;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Tests.Codes;

public class CodeGeneratorTests
{
    private sealed class FakeCheck(CodePrefix prefix, int collisions) : ICodeUniquenessCheck
    {
        public int Calls { get; private set; }

        public CodePrefix Prefix => prefix;

        public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(++Calls <= collisions);
    }

    [Theory]
    [InlineData(CodePrefix.Product, "PRD")]
    [InlineData(CodePrefix.Item, "ITM")]
    [InlineData(CodePrefix.Location, "LOC")]
    public async Task Generate_UsesPrefixAndCrockfordBody(CodePrefix prefix, string text)
    {
        var generator = new CodeGenerator([new FakeCheck(prefix, collisions: 0)]);

        var code = await generator.GenerateAsync(prefix, CancellationToken.None);

        code.Should().MatchRegex($"^{text}-[0-9A-HJKMNP-TV-Z]{{6}}$");
    }

    [Fact]
    public async Task Generate_RetriesOnCollision()
    {
        var check = new FakeCheck(CodePrefix.Product, collisions: 2);
        var generator = new CodeGenerator([check]);

        await generator.GenerateAsync(CodePrefix.Product, CancellationToken.None);

        check.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Generate_GivesUpAfterThreeCollisions()
    {
        var check = new FakeCheck(CodePrefix.Product, collisions: 3);
        var generator = new CodeGenerator([check]);

        var act = () => generator.GenerateAsync(CodePrefix.Product, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        check.Calls.Should().Be(CodeGenerator.MaxAttempts);
    }

    [Fact]
    public async Task Generate_WithoutRegisteredCheck_Throws()
    {
        var generator = new CodeGenerator([new FakeCheck(CodePrefix.Product, 0)]);

        var act = () => generator.GenerateAsync(CodePrefix.Item, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
