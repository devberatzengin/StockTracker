using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Locations;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Labels;

/// <summary>
/// Exactly one target: products, items, the items created by an In operation (ReferenceId),
/// or a location with its active sub-locations.
/// </summary>
public sealed record PrintLabelsCommand(
    LabelTemplate Template,
    int StartPosition = 1,
    IReadOnlyList<Guid>? ProductIds = null,
    IReadOnlyList<Guid>? ItemIds = null,
    Guid? ReferenceId = null,
    Guid? LocationId = null) : IRequest<LabelFile>;

public static class LabelErrors
{
    public const string BaseUrlMissing = "Labels.BaseUrlMissing";
    public const string TooMany = "Labels.TooMany";
    public const string Empty = "Labels.Empty";
    public const string NotFound = "Labels.NotFound";
}

public sealed class PrintLabelsCommandValidator : AbstractValidator<PrintLabelsCommand>
{
    public PrintLabelsCommandValidator()
    {
        RuleFor(x => x.Template).IsInEnum().WithMessage("Geçerli bir şablon seçin.");
        RuleFor(x => x)
            .Must(x => new object?[] { x.ProductIds, x.ItemIds, x.ReferenceId, x.LocationId }.Count(t => t is not null) == 1)
            .WithMessage("Tek bir hedef seçin: ürünler, adetler, bir giriş ya da bir lokasyon.")
            .OverridePropertyName("target");
        RuleFor(x => x.StartPosition)
            .Must((x, position) => position >= 1 && position <= LabelSheets.CellsPerPage(x.Template))
            .WithMessage(x => $"Başlangıç pozisyonu 1 ile {LabelSheets.CellsPerPage(x.Template)} arasında olmalıdır.");
        RuleFor(x => x.ProductIds!.Count).LessThanOrEqualTo(LabelSheets.MaxLabels).OverridePropertyName("productIds")
            .WithMessage($"Bir seferde en fazla {LabelSheets.MaxLabels} etiket basılabilir.").When(x => x.ProductIds is not null);
        RuleFor(x => x.ItemIds!.Count).LessThanOrEqualTo(LabelSheets.MaxLabels).OverridePropertyName("itemIds")
            .WithMessage($"Bir seferde en fazla {LabelSheets.MaxLabels} etiket basılabilir.").When(x => x.ItemIds is not null);
    }
}

public sealed class PrintLabelsCommandHandler(IAppDbContext db, ILabelRenderer renderer, IPublicUrls urls)
    : IRequestHandler<PrintLabelsCommand, LabelFile>
{
    public async Task<LabelFile> Handle(PrintLabelsCommand request, CancellationToken cancellationToken)
    {
        var baseUrl = urls.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new DomainException(
                LabelErrors.BaseUrlMissing,
                "Etiket basmak için PUBLIC_BASE_URL ayarlanmalı (QR kodları bu adresi açar).");
        }

        var labels = await ResolveAsync(request, cancellationToken);
        if (labels.Count == 0)
        {
            throw new DomainException(LabelErrors.Empty, "Basılacak etiket yok.", "target");
        }

        if (labels.Count > LabelSheets.MaxLabels)
        {
            throw new DomainException(
                LabelErrors.TooMany,
                $"Bir seferde en fazla {LabelSheets.MaxLabels} etiket basılabilir; seçim {labels.Count} etiket.",
                "target");
        }

        var pdf = renderer.Render(request.Template, labels, request.StartPosition, baseUrl);
        return new LabelFile(pdf, $"etiketler-{request.Template.ToString().ToLowerInvariant()}.pdf");
    }

    private async Task<IReadOnlyList<LabelData>> ResolveAsync(PrintLabelsCommand request, CancellationToken cancellationToken)
    {
        if (request.ProductIds is { } productIds)
        {
            var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Code, p.Name })
                .ToDictionaryAsync(p => p.Id, cancellationToken);
            EnsureAllFound(productIds, products.Keys, "Ürün");
            return [.. productIds.Distinct().Select(id => new LabelData(products[id].Code, products[id].Name, null))];
        }

        if (request.ItemIds is { } itemIds)
        {
            var items = await ItemsAsync(db.Items.Where(i => itemIds.Contains(i.Id)), cancellationToken);
            EnsureAllFound(itemIds, items.Keys, "Adet");
            return [.. itemIds.Distinct().Select(id => items[id])];
        }

        if (request.ReferenceId is { } referenceId)
        {
            // Items received by that operation, by serial number (as typed in the form), then code.
            var created = await db.StockMovements.AsNoTracking()
                .Where(m => m.ReferenceId == referenceId && m.Type == MovementType.In && m.ItemId != null)
                .Select(m => m.ItemId!.Value)
                .ToListAsync(cancellationToken);
            var items = await ItemsAsync(db.Items.Where(i => created.Contains(i.Id)), cancellationToken);
            return [.. items.Values.OrderBy(i => i.Detail is null).ThenBy(i => i.Detail, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal)];
        }

        return await LocationsAsync(request.LocationId!.Value, cancellationToken);
    }

    private async Task<Dictionary<Guid, LabelData>> ItemsAsync(IQueryable<Item> query, CancellationToken cancellationToken)
    {
        var products = db.Products.IgnoreQueryFilters();
        return await query.AsNoTracking()
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.SerialNumber,
                ProductName = products.Where(p => p.Id == i.ProductId).Select(p => p.Name).First(),
            })
            .ToDictionaryAsync(i => i.Id, i => new LabelData(i.Code, i.ProductName, i.SerialNumber), cancellationToken);
    }

    /// <summary>The location and its active descendants, in tree order; detail = last two levels above each.</summary>
    private async Task<IReadOnlyList<LabelData>> LocationsAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var rootPath = await db.Locations.Where(l => l.Id == locationId).Select(l => l.Path).SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(LabelErrors.NotFound, "Lokasyon bulunamadı.", "locationId");

        var locations = await db.Locations.AsNoTracking()
            .Where(l => l.Path.StartsWith(rootPath))
            .OrderBy(l => l.Path)
            .Select(l => new { l.Id, l.Code, l.Name, l.ParentId })
            .ToListAsync(cancellationToken);

        var parentPaths = await LocationPaths.BuildAsync(
            db,
            locations.Where(l => l.ParentId is not null).Select(l => l.ParentId!.Value),
            cancellationToken);

        return [.. locations.Select(l => new LabelData(
            l.Code,
            l.Name,
            l.ParentId is { } parent && parentPaths.TryGetValue(parent, out var path) ? LastTwoLevels(path) : null))];
    }

    private static string LastTwoLevels(string path) =>
        string.Join(LocationPaths.Separator, path.Split(LocationPaths.Separator).TakeLast(2));

    private static void EnsureAllFound(IEnumerable<Guid> requested, IEnumerable<Guid> found, string what)
    {
        var missing = requested.Except(found).Count();
        if (missing > 0)
        {
            throw new DomainException(LabelErrors.NotFound, $"{missing} {what.ToLowerInvariant()} bulunamadı.", "target");
        }
    }
}
