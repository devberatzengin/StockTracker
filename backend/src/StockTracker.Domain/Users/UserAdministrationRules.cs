using StockTracker.Domain.Exceptions;

namespace StockTracker.Domain.Users;

/// <summary>Role and status of a user before or after an administrative change.</summary>
public sealed record UserAccessState(string Role, bool IsActive, bool CanLogin)
{
    /// <summary>An admin who can actually sign in and manage the system.</summary>
    public bool IsActiveAdmin => Role == Roles.Admin && IsActive && CanLogin;
}

/// <summary>
/// Rules that keep the system manageable: an admin cannot lock themselves out
/// and at least one active admin always remains.
/// </summary>
public static class UserAdministrationRules
{
    public const string CannotDemoteSelf = "User.CannotDemoteSelf";
    public const string CannotDeactivateSelf = "User.CannotDeactivateSelf";
    public const string CannotDisableOwnLogin = "User.CannotDisableOwnLogin";
    public const string LastActiveAdmin = "User.LastActiveAdmin";

    /// <param name="actorId">The user performing the change.</param>
    /// <param name="targetId">The user being changed.</param>
    /// <param name="before">Target state before the change.</param>
    /// <param name="after">Target state after the change.</param>
    /// <param name="activeAdminCount">Active admins before the change, including the target.</param>
    public static void EnsureChangeAllowed(
        Guid actorId,
        Guid targetId,
        UserAccessState before,
        UserAccessState after,
        int activeAdminCount)
    {
        if (actorId == targetId)
        {
            if (before.Role == Roles.Admin && after.Role != Roles.Admin)
            {
                throw new DomainException(CannotDemoteSelf, "Kendi Admin rolünüzü kaldıramazsınız.");
            }

            if (before.IsActive && !after.IsActive)
            {
                throw new DomainException(CannotDeactivateSelf, "Kendi hesabınızı pasifleştiremezsiniz.");
            }

            if (before.CanLogin && !after.CanLogin)
            {
                throw new DomainException(CannotDisableOwnLogin, "Kendi giriş yetkinizi kaldıramazsınız.");
            }
        }

        if (before.IsActiveAdmin && !after.IsActiveAdmin && activeAdminCount <= 1)
        {
            throw new DomainException(LastActiveAdmin, "Sistemde en az bir aktif Admin kalmalıdır.");
        }
    }
}
