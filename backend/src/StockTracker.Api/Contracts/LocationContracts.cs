using StockTracker.Domain.Locations;

namespace StockTracker.Api.Contracts;

public sealed record UpdateLocationRequest(string Name, LocationType Type);

/// <summary>NewParentId null moves the location to the root.</summary>
public sealed record MoveLocationRequest(Guid? NewParentId);
