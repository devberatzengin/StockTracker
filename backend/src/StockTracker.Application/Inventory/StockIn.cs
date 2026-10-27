using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common.Codes;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Application.Inventory.MovementCommandHelpers;

namespace StockTracker.Application.Inventory;

/// <summary>
/// Quantity line: product + quantity. Serialized line: product + number of items (Quantity) and
/// optional serial numbers (at most one per item).
/// </summary>
public sealed record StockInLine(Guid ProductId, decimal Quantity, IReadOnlyList<string>? SerialNumbers);

public sealed record StockInCommand(Guid ToLocationId, IReadOnlyList<StockInLine> Lines) : IRequest<MovementResultDto>;

public sealed class StockInCommandValidator : AbstractValidator<StockInCommand>
{
    public StockInCommandValidator()
    {
        RuleFor(x => x.ToLocationId).NotEmpty().WithMessage("Hedef lokasyon seçin.");
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("En az bir satır girin.")
            .Must(l => l.Count <= MovementLimits.MaxLines)
            .WithMessage($"Tek işlemde en fazla {MovementLimits.MaxLines} satır olabilir.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Ürün seçin.");
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Miktar sıfırdan büyük olmalıdır.")
                .PrecisionScale(18, 3, ignoreTrailingZeros: true).WithMessage("Miktar en fazla 3 ondalık basamak içerebilir.");
            line.RuleForEach(l => l.SerialNumbers)
                .MaximumLength(Item.SerialNumberMaxLength)
                .WithMessage($"Seri numarası en fazla {Item.SerialNumberMaxLength} karakter olabilir.");
        });
    }
}

public sealed class StockInCommandHandler(
    ILocationRepository locations,
    IProductRepository products,
    IItemRepository items,
    IStockBalanceRepository balances,
    IMovementRepository movements,
    ICodeGenerator codeGenerator,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<StockInCommand, MovementResultDto>
{
    public async Task<MovementResultDto> Handle(StockInCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var target = await LoadTargetAsync(locations, request.ToLocationId, cancellationToken);
        if (!target.IsActive)
        {
            throw new DomainException(InventoryErrors.LocationInactive, "Pasif lokasyona giriş yapılamaz.", "toLocationId");
        }

        var productsById = await LoadProductsAsync(products, request.Lines.Select(l => l.ProductId), cancellationToken);
        var serialsByLine = await PrepareSerializedLinesAsync(request.Lines, productsById, cancellationToken);

        var itemCount = serialsByLine.Values.Sum(s => s.Count);
        var codes = itemCount > 0
            ? new Queue<string>(await codeGenerator.GenerateManyAsync(CodePrefix.Item, itemCount, cancellationToken))
            : new Queue<string>();

        var balanceSet = await LoadBalancesAsync(
            balances,
            [.. request.Lines
                .Where(l => productsById.GetValueOrDefault(l.ProductId)?.TrackingType == TrackingType.Quantity)
                .Select(l => (l.ProductId, target.Id))
                .Distinct()],
            cancellationToken);

        var created = new List<CreatedItemDto>();
        var ledger = new List<StockMovement>();

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var product = Product(productsById, line.ProductId, i);

            if (serialsByLine.TryGetValue(i, out var serials))
            {
                foreach (var serial in serials)
                {
                    var (item, movement) = Line(i, () => movementService.ReceiveItem(codes.Dequeue(), product, target, serial, context));
                    items.Add(item);
                    ledger.Add(movement);
                    created.Add(new CreatedItemDto(item.Id, item.Code, item.ProductId, item.SerialNumber));
                }
            }
            else
            {
                if (line.SerialNumbers is { Count: > 0 })
                {
                    throw LineError(i, "serialNumbers", InventoryErrors.ProductNotSerialized, "Miktar bazlı ürünlerde seri numarası girilmez.");
                }

                var balance = balanceSet.GetOrCreate(product.Id, target.Id);
                ledger.Add(Line(i, () => movementService.ReceiveQuantity(product, target, balance, line.Quantity, context)));
            }
        }

        movements.AddRange(ledger);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new StockLevelsChanged([.. ledger.Select(m => m.ProductId).Distinct()]), cancellationToken);

        return new MovementResultDto(context.ReferenceId, ledger.Count, created);
    }

    /// <summary>
    /// For serialized lines: checks the item count and serial numbers and returns one serial
    /// (or null) per item to create, keyed by line index.
    /// </summary>
    private async Task<Dictionary<int, List<string?>>> PrepareSerializedLinesAsync(
        IReadOnlyList<StockInLine> lines,
        Dictionary<Guid, Product> productsById,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, List<string?>>();
        var serialOwners = new Dictionary<(Guid ProductId, string Serial), int>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (productsById.GetValueOrDefault(line.ProductId) is not { TrackingType: TrackingType.Serialized })
            {
                continue;
            }

            if (line.Quantity != decimal.Truncate(line.Quantity) || line.Quantity > StockMovementService.MaxItemsPerLine)
            {
                throw LineError(
                    i,
                    "quantity",
                    InventoryErrors.TooManyItems,
                    $"Tekil takipli üründe adet 1 ile {StockMovementService.MaxItemsPerLine} arasında tam sayı olmalıdır.");
            }

            var count = (int)line.Quantity;
            var serials = (line.SerialNumbers ?? []).Select(Item.NormalizeSerialNumber).Where(s => s is not null).ToList();
            if (serials.Count > count)
            {
                throw LineError(i, "serialNumbers", InventoryErrors.InvalidMovement, "Seri numarası sayısı adet sayısından fazla olamaz.");
            }

            foreach (var serial in serials)
            {
                if (!serialOwners.TryAdd((line.ProductId, serial!), i))
                {
                    throw LineError(i, "serialNumbers", InventoryErrors.SerialNumberTaken, $"\"{serial}\" seri numarası işlemde birden fazla kez geçiyor.");
                }
            }

            result[i] = [.. serials, .. Enumerable.Repeat<string?>(null, count - serials.Count)];
        }

        foreach (var group in serialOwners.GroupBy(o => o.Key.ProductId))
        {
            var used = await items.GetUsedSerialNumbersAsync(
                group.Key,
                [.. group.Select(o => o.Key.Serial)],
                excludeItemId: null,
                cancellationToken);
            var conflict = group.FirstOrDefault(o => used.Contains(o.Key.Serial));
            if (conflict.Key.Serial is not null)
            {
                throw LineError(conflict.Value, "serialNumbers", InventoryErrors.SerialNumberTaken, $"\"{conflict.Key.Serial}\" seri numarası bu üründe zaten kayıtlı.");
            }
        }

        return result;
    }
}
