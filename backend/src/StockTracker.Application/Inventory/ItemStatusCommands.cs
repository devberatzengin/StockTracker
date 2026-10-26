using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Inventory;

/// <summary>InStock → InRepair with a StatusChange movement; the note is required.</summary>
public sealed record SendItemToRepairCommand(Guid Id, string? Note) : IRequest<MovementResultDto>;

/// <summary>InRepair → InStock with a StatusChange movement; the note is required.</summary>
public sealed record ReturnItemFromRepairCommand(Guid Id, string? Note) : IRequest<MovementResultDto>;

internal static class ItemStatusNoteRules
{
    public static void Apply<T>(AbstractValidator<T> validator, Func<T, string?> note) =>
        validator.RuleFor(x => note(x))
            .NotEmpty().WithMessage("Durum değişikliğinde not zorunludur.")
            .MaximumLength(StockMovement.NoteMaxLength)
            .WithMessage($"Not en fazla {StockMovement.NoteMaxLength} karakter olabilir.")
            .OverridePropertyName("note");
}

public sealed class SendItemToRepairCommandValidator : AbstractValidator<SendItemToRepairCommand>
{
    public SendItemToRepairCommandValidator() => ItemStatusNoteRules.Apply(this, x => x.Note);
}

public sealed class ReturnItemFromRepairCommandValidator : AbstractValidator<ReturnItemFromRepairCommand>
{
    public ReturnItemFromRepairCommandValidator() => ItemStatusNoteRules.Apply(this, x => x.Note);
}

public sealed class ItemStatusCommandHandlers(
    IItemRepository items,
    IProductRepository products,
    IMovementRepository movements,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SendItemToRepairCommand, MovementResultDto>,
      IRequestHandler<ReturnItemFromRepairCommand, MovementResultDto>
{
    public Task<MovementResultDto> Handle(SendItemToRepairCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, (item, product, context) =>
            movementService.SendToRepair(item, product, request.Note, context), cancellationToken);

    public Task<MovementResultDto> Handle(ReturnItemFromRepairCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, (item, product, context) =>
            movementService.CompleteRepair(item, product, request.Note, context), cancellationToken);

    private async Task<MovementResultDto> ChangeAsync(
        Guid itemId,
        Func<Item, Domain.Catalog.Product, MovementContext, StockMovement> change,
        CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);
        var item = await items.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Adet", itemId);
        var product = await products.GetByIdAsync(item.ProductId, cancellationToken)
            ?? throw new NotFoundException("Ürün", item.ProductId);

        movements.AddRange([change(item, product, context)]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MovementResultDto(context.ReferenceId, 1, []);
    }
}
