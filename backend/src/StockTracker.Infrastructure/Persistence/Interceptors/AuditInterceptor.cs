using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Auditing;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Infrastructure.Identity;

namespace StockTracker.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes an AuditLog row per added, changed or deleted entity in the same SaveChanges (same transaction).
/// Skips the ledger and other high-volume or derived tables, secrets and audit bookkeeping fields.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, IDateTime dateTime) : SaveChangesInterceptor
{
    public const string UserEntityType = "User";
    public const string RoleField = "role";

    // The ledger is its own history; balances, count lines and scans are derived or high-volume.
    private static readonly HashSet<Type> ExcludedTypes =
    [
        typeof(AuditLog),
        typeof(StockMovement),
        typeof(StockBalance),
        typeof(RefreshToken),
        typeof(CountLine),
        typeof(CountScan),
        typeof(IdentityRole<Guid>),
        typeof(IdentityUserClaim<Guid>),
        typeof(IdentityUserLogin<Guid>),
        typeof(IdentityUserToken<Guid>),
        typeof(IdentityRoleClaim<Guid>),
    ];

    // Secrets, audit bookkeeping and noise (normalized copies, lockout counters, concurrency tokens).
    private static readonly HashSet<string> ExcludedProperties =
    [
        "Id",
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "TokenHash",
        nameof(IAuditable.CreatedAt),
        nameof(IAuditable.CreatedBy),
        nameof(IAuditable.UpdatedAt),
        nameof(IAuditable.UpdatedBy),
        "NormalizedName",
        "NormalizedEmail",
        "NormalizedUserName",
        "UserName",
        "AccessFailedCount",
        "LockoutEnd",
        "LockoutEnabled",
        "EmailConfirmed",
        "PhoneNumber",
        "PhoneNumberConfirmed",
        "TwoFactorEnabled",
        "xmin",
        nameof(CountSession.ScanSequence),
    ];

    // Child rows whose history belongs to a parent: type → parent key property.
    private static readonly Dictionary<Type, string> ParentKeys = new()
    {
        [typeof(ProductBarcode)] = nameof(ProductBarcode.ProductId),
        [typeof(ProductPhoto)] = nameof(ProductPhoto.ProductId),
    };

    // UserManager removes the old role and adds the new one in two SaveChanges calls of one request;
    // the removed role waits here (the interceptor is scoped) so the change is written as one row.
    private readonly Dictionary<Guid, string?> _removedRoles = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditRows(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditRows(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var now = dateTime.UtcNow;
        var userId = currentUser.UserId;
        var rows = new List<PendingRow>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || ExcludedTypes.Contains(entry.Metadata.ClrType)
                || entry.Entity is IdentityUserRole<Guid>)
            {
                continue;
            }

            if (Describe(entry) is { } row)
            {
                rows.Add(row);
            }
        }

        AddRoleChanges(context, rows);

        foreach (var row in rows.Where(r => r.Changes.Count > 0 || r.Action == AuditAction.Deleted))
        {
            context.Add(AuditLog.Create(
                row.EntityType,
                row.EntityId,
                row.ParentId,
                row.Action,
                JsonSerializer.Serialize(row.Changes, JsonOptions),
                userId,
                now));
        }
    }

    private static PendingRow? Describe(EntityEntry entry)
    {
        if (entry.Metadata.FindPrimaryKey()?.Properties is not [{ ClrType: var keyType } key] || keyType != typeof(Guid))
        {
            return null;
        }

        var id = (Guid)entry.Property(key.Name).CurrentValue!;
        var changes = new Dictionary<string, Change>();
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Updated,
        };

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (ExcludedProperties.Contains(name))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null:
                    changes[FieldName(name)] = new Change(null, property.CurrentValue);
                    break;
                case EntityState.Deleted when property.OriginalValue is not null:
                    changes[FieldName(name)] = new Change(property.OriginalValue, null);
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[FieldName(name)] = new Change(property.OriginalValue, property.CurrentValue);
                    if (name == nameof(ISoftDeletable.IsActive))
                    {
                        action = property.CurrentValue is true ? AuditAction.Reactivated : AuditAction.Deactivated;
                    }

                    break;
            }
        }

        var parentId = ParentKeys.TryGetValue(entry.Metadata.ClrType, out var parentKey)
            ? (Guid?)(entry.State == EntityState.Deleted ? entry.Property(parentKey).OriginalValue : entry.Property(parentKey).CurrentValue)
            : null;

        return new PendingRow(EntityTypeName(entry.Metadata.ClrType), id, parentId, action, changes);
    }

    /// <summary>
    /// A user's role is a user_roles row; a role change is a deleted + added pair in one save.
    /// It is recorded on the user as <c>role: { old, new }</c>.
    /// </summary>
    private void AddRoleChanges(DbContext context, List<PendingRow> rows)
    {
        var roleEntries = context.ChangeTracker.Entries<IdentityUserRole<Guid>>()
            .Where(e => e.State is EntityState.Added or EntityState.Deleted)
            .ToList();
        if (roleEntries.Count == 0)
        {
            return;
        }

        var roles = context.Set<IdentityRole<Guid>>();
        string? RoleName(Guid roleId) => roles.Find(roleId)?.Name;

        foreach (var byUser in roleEntries.GroupBy(e => e.Entity.UserId))
        {
            var removed = byUser.Where(e => e.State == EntityState.Deleted).Select(e => RoleName(e.Entity.RoleId)).FirstOrDefault();
            var added = byUser.Where(e => e.State == EntityState.Added).Select(e => RoleName(e.Entity.RoleId)).FirstOrDefault();
            if (added is null)
            {
                _removedRoles[byUser.Key] = removed;
                continue;
            }

            if (removed is null && _removedRoles.Remove(byUser.Key, out var earlier))
            {
                removed = earlier;
            }

            if (removed == added)
            {
                continue;
            }

            var row = rows.FirstOrDefault(r => r.EntityType == UserEntityType && r.EntityId == byUser.Key);
            if (row is null)
            {
                row = new PendingRow(UserEntityType, byUser.Key, null, AuditAction.Updated, []);
                rows.Add(row);
            }

            row.Changes[RoleField] = new Change(removed, added);
        }
    }

    private static string EntityTypeName(Type type) => type == typeof(AppUser) ? UserEntityType : type.Name;

    private static string FieldName(string property) => char.ToLowerInvariant(property[0]) + property[1..];

    private sealed record PendingRow(
        string EntityType,
        Guid EntityId,
        Guid? ParentId,
        AuditAction Action,
        Dictionary<string, Change> Changes);

    private sealed record Change(
        [property: JsonPropertyName("old")] object? Old,
        [property: JsonPropertyName("new")] object? New);
}
