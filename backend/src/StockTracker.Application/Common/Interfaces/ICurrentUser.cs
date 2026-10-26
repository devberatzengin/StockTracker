namespace StockTracker.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>Whether the user's role grants the named permission (see Permissions).</summary>
    bool HasPermission(string permission);
}
