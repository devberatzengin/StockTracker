using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Assignments;

public sealed record ReturnAssignmentCommand(Guid Id, Guid ReturnLocationId, ReturnCondition Condition, string? Note)
    : IRequest<AssignmentResultDto>;

public sealed class ReturnAssignmentCommandValidator : AbstractValidator<ReturnAssignmentCommand>
{
    public ReturnAssignmentCommandValidator()
    {
        RuleFor(x => x.ReturnLocationId).NotEmpty().WithMessage("İade lokasyonunu seçin.");
        RuleFor(x => x.Condition).IsInEnum().WithMessage("Durum Sağlam ya da Arızalı olmalıdır.");
        RuleFor(x => x.Note)
            .MaximumLength(Assignment.NoteMaxLength)
            .WithMessage($"Not en fazla {Assignment.NoteMaxLength} karakter olabilir.");
        RuleFor(x => x.Note)
            .NotEmpty()
            .WithMessage("Arızalı iadede arızayı açıklayan not zorunludur.")
            .When(x => x.Condition == ReturnCondition.Faulty);
    }
}

public sealed class ReturnAssignmentCommandHandler(
    IAssignmentRepository assignments,
    IItemRepository items,
    IProductRepository products,
    ILocationRepository locations,
    IMovementRepository movements,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<ReturnAssignmentCommand, AssignmentResultDto>
{
    public async Task<AssignmentResultDto> Handle(ReturnAssignmentCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);

        var assignment = await assignments.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Zimmet", request.Id);
        var item = await items.GetByIdAsync(assignment.ItemId, cancellationToken)
            ?? throw new NotFoundException("Adet", assignment.ItemId);
        var product = await products.GetByIdAsync(item.ProductId, cancellationToken)
            ?? throw new NotFoundException("Ürün", item.ProductId);
        var target = await locations.GetByIdAsync(request.ReturnLocationId, cancellationToken)
            ?? throw new DomainException(InventoryErrors.LocationNotFound, "İade lokasyonu bulunamadı.", "returnLocationId");

        var ledger = movementService.ReturnItem(
            assignment, item, product, target, request.Condition, request.Note, context);

        movements.AddRange(ledger);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new AssignmentReturned(assignment.Id), cancellationToken);
        await publisher.Publish(new StockLevelsChanged([product.Id]), cancellationToken);

        return new AssignmentResultDto(assignment.Id, context.ReferenceId);
    }
}
