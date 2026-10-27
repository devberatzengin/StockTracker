using Microsoft.Extensions.Configuration;
using StockTracker.Application.Labels;

namespace StockTracker.Infrastructure.Labels;

/// <summary>Reads PUBLIC_BASE_URL (e.g. the cloudflared tunnel address); empty means labels cannot be printed.</summary>
public sealed class PublicUrls(IConfiguration configuration) : IPublicUrls
{
    public const string Key = "PUBLIC_BASE_URL";

    public string? BaseUrl => configuration[Key];
}
