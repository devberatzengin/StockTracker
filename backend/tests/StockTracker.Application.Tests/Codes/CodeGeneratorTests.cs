using FluentAssertions;
using StockTracker.Application.Common.Codes;
using StockTracker.Domain.Common.Codes;

namespace StockTracker.Application.Tests.Codes;

public class CodeGeneratorTests
{
    /// <summary>Reports the first <paramref name="collisions"/> checked codes as taken.</summary>
    private sealed class FakeCheck(CodePrefix prefix, int collisions) : ICodeUniquenessCheck
    {
        private int _checked;

        public int Rounds { get; private set; }

        public CodePrefix Prefix => prefix;

        public Task<IReadOnlySet<string>> ExistingAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
        {
            Rounds++;
            var taken = new HashSet<string>();
            foreach (var code in codes)
            {
                if (_checked++ < collisions)
                {
                    taken.Add(code);
                }
            }

            return Task.FromResult<IReadOnlySet<string>>(taken);
        }
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

        check.Rounds.Should().Be(3);
    }

    [Fact]
    public async Task Generate_GivesUpAfterThreeRounds()
    {
        var check = new FakeCheck(CodePrefix.Product, collisions: 3);
        var generator = new CodeGenerator([check]);

        var act = () => generator.GenerateAsync(CodePrefix.Product, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        check.Rounds.Should().Be(CodeGenerator.MaxAttempts);
    }

    [Fact]
    public async Task GenerateMany_ReturnsDistinctCodes_RegeneratingOnlyCollisions()
    {
        var check = new FakeCheck(CodePrefix.Item, collisions: 10);
        var generator = new CodeGenerator([check]);

        var codes = await generator.GenerateManyAsync(CodePrefix.Item, 100, CancellationToken.None);

        codes.Should().HaveCount(100).And.OnlyHaveUniqueItems();
        check.Rounds.Should().Be(2);
    }

    [Fact]
    public async Task Generate_WithoutRegisteredCheck_Throws()
    {
        var generator = new CodeGenerator([new FakeCheck(CodePrefix.Product, 0)]);

        var act = () => generator.GenerateAsync(CodePrefix.Item, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
