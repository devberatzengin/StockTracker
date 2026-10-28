using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

/// <summary>Cancels out a wrong movement with an opposite one; the original stays in the ledger.</summary>
public sealed record ReverseMovementCommand(Guid MovementId, string? Note) : IRequest<MovementResultDto>, IIdempotentCommand;

public sealed class ReverseMovementCommandValidator : AbstractValidator<ReverseMovementCommand>
{
    public ReverseMovementCommandValidator()
    {
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Ters kayıt için not zorunludur.")
            .MaximumLength(StockMovement.NoteMaxLength)
            .WithMessage($"Not en fazla {StockMovement.NoteMaxLength} karakter olabilir.");
    }
}

public sealed class ReverseMovementCommandHandler(
    IMovementRepository movements,
    IProductRepository products,
    IItemRepository items,
    ILocationRepository locations,
    IStockBalanceRepository balances,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<ReverseMovementCommand, MovementResultDto>
{
    public async Task<MovementResultDto> Handle(ReverseMovementCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var original = await movements.GetByIdAsync(request.MovementId, cancellationToken)
            ?? throw new NotFoundException("Hareket", request.MovementId);
        var alreadyReversed = await movements.IsReversedAsync(original.Id, cancellationToken);
        var product = await products.GetByIdAsync(original.ProductId, cancellationToken)
            ?? throw new NotFoundException("Ürün", original.ProductId);
        var item = original.ItemId is { } itemId
            ? await items.GetByIdAsync(itemId, cancellationToken) ?? throw new NotFoundException("Adet", itemId)
            : null;

        var locationIds = new[] { original.FromLocationId, original.ToLocationId }.OfType<Guid>().ToList();
        var loaded = (await locations.GetByIdsAsync(locationIds, cancellationToken)).ToDictionary(l => l.Id);
        var from = original.FromLocationId is { } fromId ? loaded.GetValueOrDefault(fromId) : null;
        var to = original.ToLocationId is { } toId ? loaded.GetValueOrDefault(toId) : null;

        StockBalance? fromBalance = null;
        StockBalance? toBalance = null;
        if (item is null)
        {
            var found = await balances.GetAsync([.. locationIds.Select(l => (product.Id, l))], cancellationToken);
            toBalance = found.SingleOrDefault(b => b.LocationId == original.ToLocationId);
            fromBalance = found.SingleOrDefault(b => b.LocationId == original.FromLocationId);
            if (fromBalance is null && original.FromLocationId is { } sourceId)
            {
                // Stock goes back to a shelf whose balance row does not exist (yet).
                fromBalance = StockBalance.Empty(product.Id, sourceId);
                balances.Add(fromBalance);
            }
        }

        var reversal = movementService.Reverse(
            original, alreadyReversed, product, item, from, to, fromBalance, toBalance, request.Note, context);

        movements.AddRange([reversal]);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new StockLevelsChanged([product.Id]), cancellationToken);

        return new MovementResultDto(context.ReferenceId, 1, []);
    }
}
