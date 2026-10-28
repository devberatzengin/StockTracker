using FluentValidation;
using MediatR;
using StockTracker.Application.Common;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Notifications;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Exceptions;
using StockTracker.Domain.Inventory;

namespace StockTracker.Application.Assignments;

public sealed record AssignItemCommand(Guid ItemId, Guid AssigneeUserId, DateOnly? DueDate, string? Note)
    : IRequest<AssignmentResultDto>, IIdempotentCommand;

public sealed class AssignItemCommandValidator : AbstractValidator<AssignItemCommand>
{
    public AssignItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Zimmetlenecek adedi seçin.");
        RuleFor(x => x.AssigneeUserId).NotEmpty().WithMessage("Zimmeti alacak kişiyi seçin.");
        RuleFor(x => x.Note)
            .MaximumLength(Assignment.NoteMaxLength)
            .WithMessage($"Not en fazla {Assignment.NoteMaxLength} karakter olabilir.");
    }
}

public sealed class AssignItemCommandHandler(
    IItemRepository items,
    IProductRepository products,
    IAssignmentRepository assignments,
    IMovementRepository movements,
    IUserAccountService users,
    StockMovementService movementService,
    ICurrentUser currentUser,
    IDateTime dateTime,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<AssignItemCommand, AssignmentResultDto>
{
    public async Task<AssignmentResultDto> Handle(AssignItemCommand request, CancellationToken cancellationToken)
    {
        var context = new MovementContext(Guid.CreateVersion7(), currentUser.RequireUserId(), dateTime.UtcNow);

        var item = await items.GetByIdAsync(request.ItemId, cancellationToken)
            ?? throw new DomainException(InventoryErrors.ItemNotFound, "Adet bulunamadı.", "itemId");
        var product = await products.GetByIdAsync(item.ProductId, cancellationToken)
            ?? throw new DomainException(InventoryErrors.ProductNotFound, "Ürün bulunamadı.", "itemId");
        var assignee = await users.GetAsync(request.AssigneeUserId, cancellationToken)
            ?? throw new DomainException(AssignmentErrors.AssigneeNotFound, "Kullanıcı bulunamadı.", "assigneeUserId");

        var (assignment, movement) = movementService.AssignItem(
            item,
            product,
            assignee.Id,
            assignee.IsActive,
            request.DueDate,
            dateTime.Today(),
            request.Note,
            context);

        assignments.Add(assignment);
        movements.AddRange([movement]);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new StockLevelsChanged([product.Id]), cancellationToken);

        return new AssignmentResultDto(assignment.Id, context.ReferenceId);
    }
}
