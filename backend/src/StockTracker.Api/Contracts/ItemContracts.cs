namespace StockTracker.Api.Contracts;

public sealed record UpdateItemRequest(string? SerialNumber);

public sealed record ItemStatusChangeRequest(string? Note);
