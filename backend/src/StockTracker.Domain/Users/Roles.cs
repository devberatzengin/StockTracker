namespace StockTracker.Domain.Users;

public static class Roles
{
    public const string Admin = "Admin";
    public const string WarehouseManager = "WarehouseManager";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = [Admin, WarehouseManager, Staff];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);
}
