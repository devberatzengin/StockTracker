using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using static StockTracker.Application.Inventory.MovementCommandHelpers;

namespace StockTracker.Application.Inventory;

/// <summary>Reason and note apply to every line of the operation.</summary>
public sealed record StockOutCommand(MovementReason Reason, string? Note, IReadOnlyList<StockLine> Lines)
    : IRequest<MovementResultDto>;

public sealed class StockOutCommandValidator : AbstractValidator<StockOutCommand>
{
    public StockOutCommandValidator()
    {
        RuleFor(x => x.Reason).IsInEnum().WithMessage("Geçersiz çıkış sebebi.");
        RuleFor(x => x.Note)
            .MaximumLength(StockMovement.NoteMaxLength)
            .WithMessage($"Not en fazla {StockMovement.NoteMaxLength} karakter olabilir.");
        RuleFor(x => x.Note)
            .NotEmpty().When(x => x.Reason == MovementReason.Other)
            .WithMessage("Sebep \"Diğer\" ise not zorunludur.");
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("En az bir satır girin.")
            .Must(l => l.Count <= MovementLimits.MaxLines)
            .WithMessage($"Tek işlemde en fazla {MovementLimits.MaxLines} satır olabilir.");
        RuleForEach(x => x.Lines).SetValidator(new StockLineValidator());
    }
}

public sealed class StockOutCommandHandler(
    ILocationRepository locations,
    IProductRepository products,
    IItemRepository items,
    IStockBalanceRepository balances,
    IMovementRepository movements,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<StockOutCommand, MovementResultDto>
{
    public async Task<MovementResultDto> Handle(StockOutCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var lines = request.Lines;
        EnsureDistinctItems(lines);

        var loaded = await LoadedStock.LoadAsync(lines, items, products, locations, balances, cancellationToken);
        var ledger = new List<StockMovement>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.ItemId is { } itemId)
            {
                var item = loaded.Item(itemId, i);
                var product = Product(loaded.Products, item.ProductId, i);
                Location? source = item.CurrentLocationId is { } locationId ? loaded.Locations.GetValueOrDefault(locationId) : null;
                ledger.Add(Line(i, () => movementService.DisposeItem(item, product, source, request.Reason, request.Note, context)));
            }
            else
            {
                var product = Product(loaded.Products, line.ProductId!.Value, i);
                var source = Source(loaded.Locations, line.FromLocationId!.Value, i);
                var balance = loaded.Balances.Find(product.Id, source.Id);
                ledger.Add(Line(i, () => movementService.ConsumeQuantity(
                    product, source, balance, line.Quantity!.Value, request.Reason, request.Note, context)));
            }
        }

        movements.AddRange(ledger);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new StockLevelsChanged([.. ledger.Select(m => m.ProductId).Distinct()]), cancellationToken);

        return new MovementResultDto(context.ReferenceId, ledger.Count, []);
    }
}

/// <summary>Everything the lines of an out or transfer command refer to, loaded in a few queries.</summary>
internal sealed class LoadedStock
{
    private LoadedStock(
        Dictionary<Guid, Item> items,
        Dictionary<Guid, Domain.Catalog.Product> products,
        Dictionary<Guid, Location> locations,
        BalanceSet balances)
    {
        Items = items;
        Products = products;
        Locations = locations;
        Balances = balances;
    }

    public Dictionary<Guid, Item> Items { get; }

    public Dictionary<Guid, Domain.Catalog.Product> Products { get; }

    public Dictionary<Guid, Location> Locations { get; }

    public BalanceSet Balances { get; }

    public static async Task<LoadedStock> LoadAsync(
        IReadOnlyList<StockLine> lines,
        IItemRepository itemRepository,
        IProductRepository productRepository,
        ILocationRepository locationRepository,
        IStockBalanceRepository balanceRepository,
        CancellationToken cancellationToken,
        Guid? targetLocationId = null)
    {
        var items = (await itemRepository.GetByIdsAsync(
                [.. lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value)],
                cancellationToken))
            .ToDictionary(i => i.Id);

        var quantityLines = lines.Where(l => l.ItemId is null).ToList();
        var products = await LoadProductsAsync(
            productRepository,
            items.Values.Select(i => i.ProductId).Concat(quantityLines.Select(l => l.ProductId!.Value)),
            cancellationToken);

        var locationIds = quantityLines.Select(l => l.FromLocationId!.Value)
            .Concat(items.Values.Where(i => i.CurrentLocationId is not null).Select(i => i.CurrentLocationId!.Value));
        var locations = await LoadLocationsAsync(locationRepository, locationIds, cancellationToken);

        var balanceKeys = quantityLines.Select(l => (l.ProductId!.Value, l.FromLocationId!.Value));
        if (targetLocationId is { } target)
        {
            balanceKeys = balanceKeys.Concat(quantityLines.Select(l => (l.ProductId!.Value, target)));
        }

        var balances = await LoadBalancesAsync(balanceRepository, [.. balanceKeys.Distinct()], cancellationToken);
        return new LoadedStock(items, products, locations, balances);
    }

    public Item Item(Guid id, int index) =>
        Items.GetValueOrDefault(id)
        ?? throw LineError(index, "itemId", InventoryErrors.ItemNotFound, "Adet bulunamadı.");
}
