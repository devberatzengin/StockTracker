namespace StockTracker.Application.Users;

public sealed record UserDto(
    Guid Id,
    string FullName,
    string? Department,
    string? Email,
    string Role,
    bool CanLogin,
    bool IsActive);
