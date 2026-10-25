using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;
using static StockTracker.Application.Inventory.MovementCommandHelpers;

namespace StockTracker.Application.Inventory;

public sealed record StockTransferCommand(Guid ToLocationId, IReadOnlyList<StockLine> Lines) : IRequest<MovementResultDto>;

public sealed class StockTransferCommandValidator : AbstractValidator<StockTransferCommand>
{
    public StockTransferCommandValidator()
    {
        RuleFor(x => x.ToLocationId).NotEmpty().WithMessage("Hedef lokasyon seçin.");
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("En az bir satır girin.")
            .Must(l => l.Count <= MovementLimits.MaxLines)
            .WithMessage($"Tek işlemde en fazla {MovementLimits.MaxLines} satır olabilir.");
        RuleForEach(x => x.Lines).SetValidator(new StockLineValidator());
    }
}

public sealed class StockTransferCommandHandler(
    ILocationRepository locations,
    IProductRepository products,
    IItemRepository items,
    IStockBalanceRepository balances,
    IMovementRepository movements,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork) : IRequestHandler<StockTransferCommand, MovementResultDto>
{
    public async Task<MovementResultDto> Handle(StockTransferCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var target = await LoadTargetAsync(locations, request.ToLocationId, cancellationToken);
        if (!target.IsActive)
        {
            throw new DomainException(InventoryErrors.LocationInactive, "Pasif lokasyona transfer yapılamaz.", "toLocationId");
        }

        var lines = request.Lines;
        EnsureDistinctItems(lines);

        var loaded = await LoadedStock.LoadAsync(lines, items, products, locations, balances, cancellationToken, target.Id);
        var ledger = new List<StockMovement>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.ItemId is { } itemId)
            {
                var item = loaded.Item(itemId, i);
                var product = Product(loaded.Products, item.ProductId, i);
                ledger.Add(Line(i, () => movementService.TransferItem(item, product, target, context)));
            }
            else
            {
                var product = Product(loaded.Products, line.ProductId!.Value, i);
                var source = Source(loaded.Locations, line.FromLocationId!.Value, i);
                var sourceBalance = loaded.Balances.Find(product.Id, source.Id);
                var targetBalance = loaded.Balances.GetOrCreate(product.Id, target.Id);
                ledger.Add(Line(i, () => movementService.TransferQuantity(
                    product, source, target, sourceBalance, targetBalance, line.Quantity!.Value, context)));
            }
        }

        movements.AddRange(ledger);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MovementResultDto(context.ReferenceId, ledger.Count, []);
    }
}
