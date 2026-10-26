using StockTracker.Application.Common.Interfaces;

namespace StockTracker.Application.Common;

/// <summary>Calendar days are Europe/Istanbul days (due dates, overdue checks).</summary>
public static class BusinessDay
{
    // Turkey has used UTC+3 all year since 2016; the frontend uses the same fixed offset.
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    public static DateOnly Today(this IDateTime clock) => DateOnly.FromDateTime(clock.UtcNow + IstanbulOffset);
}
