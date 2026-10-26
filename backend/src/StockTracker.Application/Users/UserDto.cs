namespace StockTracker.Application.Users;

public sealed record UserDto(
    Guid Id,
    string FullName,
    string? Department,
    string? Email,
    string Role,
    bool CanLogin,
    bool IsActive);

/// <summary>Minimal active user info for pickers; does not require user administration rights.</summary>
public sealed record UserLookupDto(Guid Id, string FullName, string? Department, bool CanLogin);
