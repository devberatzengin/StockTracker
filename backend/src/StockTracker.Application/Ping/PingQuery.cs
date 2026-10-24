using MediatR;

namespace StockTracker.Application.Ping;

public sealed record PingQuery : IRequest<string>;

public sealed class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken) =>
        Task.FromResult("pong");
}
