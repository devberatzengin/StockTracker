using Microsoft.AspNetCore.Identity;
using StockTracker.Domain.Common;

namespace StockTracker.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>, ISoftDeletable
{
    public const int FullNameMaxLength = 100;
    public const int DepartmentMaxLength = 100;

    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = string.Empty;

    public string? Department { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Users without login permission can still receive assignments.
    /// </summary>
    public bool CanLogin { get; set; }
}
