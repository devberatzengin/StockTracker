using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Inventory;
using StockTracker.Application.Locations;
using StockTracker.Application.Notifications;
using StockTracker.Application.Scan;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using Unit = StockTracker.Domain.Catalog.Unit;

namespace StockTracker.Application.Counts;

public sealed record StartCountCommand(Guid LocationId, bool IncludeChildren, string? Note) : IRequest<Guid>;

/// <summary>
/// One scan while counting. <paramref name="LocationId"/> is the context location (the shelf the counter
/// is at); a quantity product also needs <paramref name="Quantity"/>.
/// </summary>
public sealed record ScanCountCommand(Guid Id, Guid? LocationId, string Code, decimal? Quantity) : IRequest<CountScanResultDto>;

public sealed record UndoCountScanCommand(Guid Id, Guid LineId) : IRequest;

public sealed record SubmitCountCommand(Guid Id) : IRequest;

public sealed record CancelCountCommand(Guid Id) : IRequest;

public sealed record ApproveCountCommand(Guid Id, IReadOnlyList<CountDecision> Decisions) : IRequest<CountApprovalResultDto>;

public sealed class StartCountCommandValidator : AbstractValidator<StartCountCommand>
{
    public StartCountCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Sayılacak lokasyonu seçin.");
        RuleFor(x => x.Note)
            .MaximumLength(CountSession.NoteMaxLength)
            .WithMessage($"Not en fazla {CountSession.NoteMaxLength} karakter olabilir.");
    }
}

public sealed class ScanCountCommandValidator : AbstractValidator<ScanCountCommand>
{
    public ScanCountCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Okutulan kod boş olamaz.")
            .MaximumLength(512).WithMessage("Okutulan değer çok uzun.");
        RuleFor(x => x.Quantity!.Value)
            .GreaterThan(0).WithMessage("Miktar sıfırdan büyük olmalıdır.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true).WithMessage("Miktar en fazla 3 ondalık basamak içerebilir.")
            .OverridePropertyName("quantity")
            .When(x => x.Quantity is not null);
    }
}

public sealed class ApproveCountCommandValidator : AbstractValidator<ApproveCountCommand>
{
    public ApproveCountCommandValidator()
    {
        RuleFor(x => x.Decisions).NotNull().WithMessage("Kararlar gönderilmedi.");
        RuleFor(x => x.Decisions)
            .Must(d => d.Select(x => x.LineId).Distinct().Count() == d.Count)
            .WithMessage("Bir satır için birden fazla karar gönderildi.")
            .When(x => x.Decisions is not null);
        RuleForEach(x => x.Decisions).ChildRules(d => d.RuleFor(x => x.Resolution).IsInEnum());
    }
}

internal static class CountRules
{
    public static async Task<CountSession> LoadAsync(ICountRepository counts, Guid id, CancellationToken cancellationToken) =>
        await counts.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Sayım", id);

    public static async Task<Location> LocationAsync(ILocationRepository locations, Guid id, CancellationToken cancellationToken) =>
        await locations.GetByIdAsync(id, cancellationToken)
        ?? throw new DomainException(CountErrors.LocationNotFound, "Lokasyon bulunamadı.", "locationId");

    public static CountProgressDto Progress(CountSession session) =>
        new(
            session.Lines.Count(l => l.LineType == CountLineType.Expected),
            session.Lines.Count(l => l.LineType == CountLineType.Expected && l.IsCounted),
            session.Lines.Count(l => l.LineType == CountLineType.Unexpected));
}

public sealed class StartCountCommandHandler(
    ICountRepository counts,
    ILocationRepository locations,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork) : IRequestHandler<StartCountCommand, Guid>
{
    public async Task<Guid> Handle(StartCountCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var id = Guid.Empty;

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // Lock first, then read the open counts: two overlapping starts cannot both pass.
                await counts.LockStartsAsync(ct);

                var location = await CountRules.LocationAsync(locations, request.LocationId, ct);
                var open = await counts.GetOpenScopesAsync(ct);
                if (open.Any(o => CountSession.ScopesOverlap(o.LocationPath, o.IncludeChildren, location.Path, request.IncludeChildren)))
                {
                    throw new DomainException(
                        CountErrors.Overlapping,
                        "Bu lokasyonu (ya da üst/alt lokasyonunu) kapsayan açık bir sayım var.",
                        "locationId");
                }

                var scope = await counts.GetScopeLocationIdsAsync(location.Id, location.Path, request.IncludeChildren, ct);
                var expected = await counts.GetExpectedStockAsync(scope, ct);
                var session = CountSession.Start(location, request.IncludeChildren, request.Note, expected, userId, dateTime.UtcNow);

                counts.Add(session);
                await unitOfWork.SaveChangesAsync(ct);
                id = session.Id;
            },
            cancellationToken);

        return id;
    }
}

public sealed class ScanCountCommandHandler(
    ICountRepository counts,
    ILocationRepository locations,
    IItemRepository items,
    IProductRepository products,
    IAppDbContext db,
    ISender sender,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork) : IRequestHandler<ScanCountCommand, CountScanResultDto>
{
    public async Task<CountScanResultDto> Handle(ScanCountCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var session = await CountRules.LoadAsync(counts, request.Id, cancellationToken);
        var root = await CountRules.LocationAsync(locations, session.LocationId, cancellationToken);
        var scanned = await ResolveAsync(request.Code, cancellationToken);

        if (scanned.Type == ScanResultTypes.Location)
        {
            var location = await CountRules.LocationAsync(locations, scanned.Id, cancellationToken);
            session.EnsureCanScan(root, location);
            return Result(CountScanKind.Location, location.Name, session, location: location, locationPath: await PathAsync(location.Id, cancellationToken));
        }

        if (request.LocationId is not { } contextId)
        {
            throw new DomainException(CountErrors.ContextRequired, "Önce bulunduğunuz rafın etiketini okutun.", "locationId");
        }

        var context = await CountRules.LocationAsync(locations, contextId, cancellationToken);
        session.EnsureCanScan(root, context);

        if (scanned.Type == ScanResultTypes.Item)
        {
            var item = await items.GetByIdAsync(scanned.Id, cancellationToken) ?? throw new NotFoundException("Adet", scanned.Id);
            var result = session.ScanItem(root, context, item, userId, dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result(
                Kind(result.Outcome),
                scanned.Title,
                session,
                line: result.Line,
                productId: item.ProductId,
                itemStatus: item.Status,
                itemLocationPath: item.CurrentLocationId is { } at ? await PathAsync(at, cancellationToken) : null);
        }

        var product = await products.GetByIdAsync(scanned.Id, cancellationToken) ?? throw new NotFoundException("Ürün", scanned.Id);
        if (product.TrackingType == TrackingType.Quantity && request.Quantity is null)
        {
            return Result(CountScanKind.QuantityRequired, product.Name, session, productId: product.Id, unit: product.Unit);
        }

        var counted = session.ScanQuantity(root, context, product, request.Quantity ?? 1, userId, dateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result(Kind(counted.Outcome), product.Name, session, line: counted.Line, productId: product.Id, unit: product.Unit);
    }

    private async Task<ScanResultDto> ResolveAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(new ScanQuery(code), cancellationToken);
        }
        catch (ScanNotFoundException)
        {
            throw new DomainException(CountErrors.CodeNotFound, "Bu koda ait kayıt bulunamadı.", "code");
        }
    }

    private async Task<string?> PathAsync(Guid locationId, CancellationToken cancellationToken) =>
        (await LocationPaths.BuildAsync(db, [locationId], cancellationToken)).GetValueOrDefault(locationId);

    private static CountScanKind Kind(CountScanOutcome outcome) => outcome switch
    {
        CountScanOutcome.Counted => CountScanKind.Counted,
        CountScanOutcome.Unexpected => CountScanKind.Unexpected,
        _ => CountScanKind.AlreadyCounted,
    };

    private static CountScanResultDto Result(
        CountScanKind kind,
        string title,
        CountSession session,
        Location? location = null,
        string? locationPath = null,
        CountLine? line = null,
        Guid? productId = null,
        Unit? unit = null,
        ItemStatus? itemStatus = null,
        string? itemLocationPath = null) =>
        new(
            kind,
            title,
            location?.Id,
            locationPath,
            productId,
            unit,
            line?.Id,
            line?.CountedQuantity,
            itemStatus,
            itemLocationPath,
            CountRules.Progress(session));
}

public sealed class UndoCountScanCommandHandler(ICountRepository counts, IUnitOfWork unitOfWork)
    : IRequestHandler<UndoCountScanCommand>
{
    public async Task Handle(UndoCountScanCommand request, CancellationToken cancellationToken)
    {
        var session = await CountRules.LoadAsync(counts, request.Id, cancellationToken);
        session.UndoLastScan(request.LineId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CountStatusCommandHandlers(ICountRepository counts, IUnitOfWork unitOfWork)
    : IRequestHandler<SubmitCountCommand>, IRequestHandler<CancelCountCommand>
{
    public async Task Handle(SubmitCountCommand request, CancellationToken cancellationToken)
    {
        var session = await CountRules.LoadAsync(counts, request.Id, cancellationToken);
        session.Submit();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(CancelCountCommand request, CancellationToken cancellationToken)
    {
        var session = await CountRules.LoadAsync(counts, request.Id, cancellationToken);
        session.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ApproveCountCommandHandler(
    ICountRepository counts,
    IItemRepository items,
    IProductRepository products,
    ILocationRepository locations,
    IStockBalanceRepository balances,
    IMovementRepository movements,
    CountApprovalService approval,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<ApproveCountCommand, CountApprovalResultDto>
{
    public async Task<CountApprovalResultDto> Handle(ApproveCountCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var session = await CountRules.LoadAsync(counts, request.Id, cancellationToken);
        var stock = await CountStock.LoadAsync(session, items, products, locations, balances, cancellationToken);

        // Differences against the stock now (tracked: approval changes it and xmin guards it).
        var review = CountReview.Build(session, stock.Quantities(), stock.ItemSnapshots());
        var ledger = approval.Approve(
            session,
            review,
            request.Decisions.ToDictionary(d => d.LineId, d => d.Resolution),
            stock,
            context);

        movements.AddRange(ledger);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new StockLevelsChanged([.. ledger.Select(m => m.ProductId).Distinct()]), cancellationToken);

        return new CountApprovalResultDto(context.ReferenceId, ledger.Count);
    }
}

/// <summary>The products, locations, items and balances a count's lines refer to, tracked for the approval.</summary>
internal sealed class CountStock : ICountApprovalStock
{
    private readonly Dictionary<Guid, Product> _products;
    private readonly Dictionary<Guid, Location> _locations;
    private readonly Dictionary<Guid, Item> _items;
    private readonly BalanceSet _balances;
    private readonly IReadOnlyList<(Guid ProductId, Guid LocationId)> _quantityKeys;

    private CountStock(
        Dictionary<Guid, Product> products,
        Dictionary<Guid, Location> locations,
        Dictionary<Guid, Item> items,
        BalanceSet balances,
        IReadOnlyList<(Guid, Guid)> quantityKeys)
    {
        _products = products;
        _locations = locations;
        _items = items;
        _balances = balances;
        _quantityKeys = quantityKeys;
    }

    public static async Task<CountStock> LoadAsync(
        CountSession session,
        IItemRepository items,
        IProductRepository products,
        ILocationRepository locations,
        IStockBalanceRepository balances,
        CancellationToken cancellationToken)
    {
        var quantityKeys = session.Lines.Where(l => l.ItemId is null).Select(l => (l.ProductId, l.LocationId)).Distinct().ToList();
        var itemIds = session.Lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();

        return new CountStock(
            await MovementCommandHelpers.LoadProductsAsync(products, session.Lines.Select(l => l.ProductId), cancellationToken),
            await MovementCommandHelpers.LoadLocationsAsync(locations, session.Lines.Select(l => l.LocationId), cancellationToken),
            (await items.GetByIdsAsync(itemIds, cancellationToken)).ToDictionary(i => i.Id),
            await MovementCommandHelpers.LoadBalancesAsync(balances, quantityKeys, cancellationToken),
            quantityKeys);
    }

    public IReadOnlyDictionary<(Guid ProductId, Guid LocationId), decimal> Quantities() =>
        _quantityKeys.ToDictionary(k => k, k => _balances.Find(k.Item1, k.Item2)?.Quantity ?? 0);

    public IReadOnlyDictionary<Guid, ItemSnapshot> ItemSnapshots() =>
        _items.Values.ToDictionary(i => i.Id, i => new ItemSnapshot(i.Status, i.CurrentLocationId));

    public Product Product(Guid id) => _products[id];

    public Location Location(Guid id) => _locations[id];

    public Item Item(Guid id) => _items[id];

    public StockBalance Balance(Guid productId, Guid locationId) => _balances.GetOrCreate(productId, locationId);
}
