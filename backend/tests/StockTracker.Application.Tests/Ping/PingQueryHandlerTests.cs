using FluentAssertions;
using StockTracker.Application.Ping;

namespace StockTracker.Application.Tests.Ping;

public class PingQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPong()
    {
        var result = await new PingQueryHandler().Handle(new PingQuery(), CancellationToken.None);

        result.Should().Be("pong");
    }
}
