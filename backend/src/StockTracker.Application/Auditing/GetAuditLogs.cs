using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Common.Models;
using StockTracker.Domain.Auditing;

namespace StockTracker.Application.Auditing;

/// <param name="Old">Previous value as text (strings as-is, other JSON values as written); null when none.</param>
public sealed record AuditChangeDto(string Field, string? Old, string? New);

/// <param name="EntityLabel">Readable name of the entity (product name, item code, …) when it still exists.</param>
public sealed record AuditLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string? EntityLabel,
    Guid? ParentEntityId,
    AuditAction Action,
    IReadOnlyList<AuditChangeDto> Changes,
    Guid? UserId,
    string? UserName,
    DateTime CreatedAt);

/// <summary>
/// Newest first. With an entity id, rows of its children (a product's barcodes and photos) are included.
/// </summary>
public sealed record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 25,
    string? EntityType = null,
    Guid? EntityId = null,
    Guid? UserId = null,
    DateTime? From = null,
    DateTime? To = null) : IRequest<PagedResult<AuditLogDto>>, IPagedQuery;

public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        Include(new PagedQueryValidator());
        RuleFor(x => x.EntityType).MaximumLength(AuditLog.EntityTypeMaxLength);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}

public sealed class GetAuditLogsQueryHandler(IAppDbContext db, IUserAccountService users)
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking();

        if (request.EntityId is { } entityId)
        {
            query = request.EntityType is { } type
                ? query.Where(a => (a.EntityType == type && a.EntityId == entityId) || a.ParentEntityId == entityId)
                : query.Where(a => a.EntityId == entityId || a.ParentEntityId == entityId);
        }
        else if (request.EntityType is { } type)
        {
            query = query.Where(a => a.EntityType == type);
        }

        if (request.UserId is { } userId)
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (request.From is { } from)
        {
            query = query.Where(a => a.CreatedAt >= from.ToUniversalTime());
        }

        if (request.To is { } to)
        {
            query = query.Where(a => a.CreatedAt <= to.ToUniversalTime());
        }

        var page = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToPagedResultAsync(request, cancellationToken);

        var names = await users.GetDisplayNamesAsync(
            [.. page.Items.Where(a => a.UserId is not null).Select(a => a.UserId!.Value)
                .Concat(page.Items.Where(a => a.EntityType == "User").Select(a => a.EntityId))
                .Distinct()],
            cancellationToken);
        var labels = await LabelsAsync(page.Items, names, cancellationToken);

        return new PagedResult<AuditLogDto>(
            [.. page.Items.Select(a => new AuditLogDto(
                a.Id,
                a.EntityType,
                a.EntityId,
                labels.GetValueOrDefault(a.EntityId),
                a.ParentEntityId,
                a.Action,
                ParseChanges(a.Changes),
                a.UserId,
                a.UserId is { } by ? names.GetValueOrDefault(by) : null,
                a.CreatedAt))],
            page.TotalCount,
            page.Page,
            page.PageSize);
    }

    private async Task<Dictionary<Guid, string>> LabelsAsync(
        IReadOnlyList<AuditLog> rows,
        IReadOnlyDictionary<Guid, string> userNames,
        CancellationToken cancellationToken)
    {
        List<Guid> Ids(string type) => [.. rows.Where(r => r.EntityType == type).Select(r => r.EntityId).Distinct()];

        var productIds = Ids("Product");
        var itemIds = Ids("Item");
        var locationIds = Ids("Location");
        var categoryIds = Ids("Category");

        var labels = new Dictionary<Guid, string>();
        foreach (var x in await db.Products.IgnoreQueryFilters().Where(p => productIds.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken))
        {
            labels[x.Id] = x.Name;
        }

        foreach (var x in await db.Items.Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code }).ToListAsync(cancellationToken))
        {
            labels[x.Id] = x.Code;
        }

        foreach (var x in await db.Locations.IgnoreQueryFilters().Where(l => locationIds.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToListAsync(cancellationToken))
        {
            labels[x.Id] = x.Name;
        }

        foreach (var x in await db.Categories.IgnoreQueryFilters().Where(c => categoryIds.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken))
        {
            labels[x.Id] = x.Name;
        }

        foreach (var row in rows.Where(r => r.EntityType == "User"))
        {
            if (userNames.TryGetValue(row.EntityId, out var name))
            {
                labels[row.EntityId] = name;
            }
        }

        return labels;
    }

    private static IReadOnlyList<AuditChangeDto> ParseChanges(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(p => new AuditChangeDto(
            p.Name,
            Text(p.Value, "old"),
            Text(p.Value, "new")))];
    }

    private static string? Text(JsonElement change, string name) =>
        change.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => value.GetString(),
                _ => value.GetRawText(),
            }
            : null;
}
