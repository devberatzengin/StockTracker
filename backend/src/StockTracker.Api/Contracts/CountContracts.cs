using StockTracker.Application.Counts;

namespace StockTracker.Api.Contracts;

/// <param name="LocationId">The context location (the shelf being counted).</param>
public sealed record CountScanRequest(Guid? LocationId, string Code, decimal? Quantity);

public sealed record ApproveCountRequest(IReadOnlyList<CountDecision> Decisions);
