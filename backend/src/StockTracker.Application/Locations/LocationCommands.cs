using FluentValidation;
using FluentValidation.Results;
using MediatR;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Common.Codes;
using StockTracker.Domain.Locations;
using ValidationException = StockTracker.Application.Common.Exceptions.ValidationException;

namespace StockTracker.Application.Locations;

public sealed record CreateLocationCommand(string Name, LocationType Type, Guid? ParentId) : IRequest<Guid>;

public sealed record UpdateLocationCommand(Guid Id, string Name, LocationType Type) : IRequest;

/// <summary>NewParentId null moves the location to the root.</summary>
public sealed record MoveLocationCommand(Guid Id, Guid? NewParentId) : IRequest;

public sealed record DeactivateLocationCommand(Guid Id) : IRequest;

internal static class LocationRules
{
    public static IRuleBuilderOptions<T, string> LocationName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Lokasyon adı zorunludur.")
            .MaximumLength(Location.NameMaxLength)
            .WithMessage($"Lokasyon adı en fazla {Location.NameMaxLength} karakter olabilir.");

    public static async Task EnsureNameAvailableAsync(
        ILocationRepository locations,
        Guid? parentId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (await locations.IsNameTakenAmongSiblingsAsync(parentId, Location.Normalize(name), excludeId, cancellationToken))
        {
            throw new ValidationException(
            [
                new ValidationFailure("Name", "Aynı üst lokasyon altında bu adla bir lokasyon zaten var."),
            ]);
        }
    }

    public static async Task<Location> LoadAsync(ILocationRepository locations, Guid id, CancellationToken cancellationToken) =>
        await locations.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Lokasyon", id);

    public static async Task<Location> LoadParentAsync(
        ILocationRepository locations,
        Guid parentId,
        string propertyName,
        CancellationToken cancellationToken) =>
        await locations.GetByIdAsync(parentId, cancellationToken)
        ?? throw new ValidationException([new ValidationFailure(propertyName, "Üst lokasyon bulunamadı.")]);
}

public sealed class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.Name).LocationName();
        RuleFor(x => x.Type).IsInEnum().WithMessage("Geçersiz lokasyon tipi.");
    }
}

public sealed class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationCommandValidator()
    {
        RuleFor(x => x.Name).LocationName();
        RuleFor(x => x.Type).IsInEnum().WithMessage("Geçersiz lokasyon tipi.");
    }
}

public sealed class CreateLocationCommandHandler(
    ILocationRepository locations,
    ICodeGenerator codeGenerator,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLocationCommand, Guid>
{
    public async Task<Guid> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var parent = request.ParentId is { } parentId
            ? await LocationRules.LoadParentAsync(locations, parentId, nameof(request.ParentId), cancellationToken)
            : null;

        await LocationRules.EnsureNameAvailableAsync(locations, parent?.Id, request.Name, excludeId: null, cancellationToken);

        var location = Location.Create(
            await codeGenerator.GenerateAsync(CodePrefix.Location, cancellationToken),
            request.Name,
            request.Type,
            parent);

        locations.Add(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return location.Id;
    }
}

public sealed class UpdateLocationCommandHandler(ILocationRepository locations, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateLocationCommand>
{
    public async Task Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await LocationRules.LoadAsync(locations, request.Id, cancellationToken);

        if (location.IsActive)
        {
            await LocationRules.EnsureNameAvailableAsync(
                locations, location.ParentId, request.Name, location.Id, cancellationToken);
        }

        location.Update(request.Name, request.Type);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class MoveLocationCommandHandler(
    ILocationRepository locations,
    IUnitOfWork unitOfWork,
    IDateTime dateTime,
    ICurrentUser currentUser) : IRequestHandler<MoveLocationCommand>
{
    public Task Handle(MoveLocationCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // Lock first, then read: the subtree checks must see the latest tree.
                await locations.LockTreeAsync(ct);

                var location = await LocationRules.LoadAsync(locations, request.Id, ct);
                if (location.ParentId == request.NewParentId)
                {
                    return;
                }

                var newParent = request.NewParentId is { } parentId
                    ? await LocationRules.LoadParentAsync(locations, parentId, nameof(request.NewParentId), ct)
                    : null;

                var oldPath = location.Path;
                var oldDepth = location.Depth;
                location.MoveTo(newParent, await locations.GetSubtreeHeightAsync(location, ct));

                await LocationRules.EnsureNameAvailableAsync(locations, newParent?.Id, location.Name, location.Id, ct);

                await unitOfWork.SaveChangesAsync(ct);
                await locations.MoveDescendantsAsync(
                    location.Id,
                    oldPath,
                    location.Path,
                    location.Depth - oldDepth,
                    dateTime.UtcNow,
                    currentUser.UserId,
                    ct);
            },
            cancellationToken);
}

public sealed class DeactivateLocationCommandHandler(
    ILocationRepository locations,
    ILocationContentChecker contentChecker,
    IUnitOfWork unitOfWork,
    IDateTime dateTime,
    ICurrentUser currentUser) : IRequestHandler<DeactivateLocationCommand>
{
    public Task Handle(DeactivateLocationCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await locations.LockTreeAsync(ct);

                var location = await LocationRules.LoadAsync(locations, request.Id, ct);
                if (!location.IsActive)
                {
                    return;
                }

                location.Deactivate(await contentChecker.HasContentAsync(location.Path, ct));

                await unitOfWork.SaveChangesAsync(ct);
                await locations.DeactivateDescendantsAsync(
                    location.Id, location.Path, dateTime.UtcNow, currentUser.UserId, ct);
            },
            cancellationToken);
}
