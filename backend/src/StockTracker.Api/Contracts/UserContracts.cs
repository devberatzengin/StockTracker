namespace StockTracker.Api.Contracts;

public sealed record UpdateUserRequest(
    string FullName,
    string? Department,
    string? Email,
    string? Password,
    string Role,
    bool CanLogin);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record CreatedResponse(Guid Id);
