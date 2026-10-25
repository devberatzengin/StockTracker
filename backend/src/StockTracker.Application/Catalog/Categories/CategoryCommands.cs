using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Categories;

public sealed record CreateCategoryCommand(string Name) : IRequest<Guid>;

public sealed record UpdateCategoryCommand(Guid Id, string Name) : IRequest;

public sealed record DeactivateCategoryCommand(Guid Id) : IRequest;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator() => RuleFor(x => x.Name).CategoryName();
}

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator() => RuleFor(x => x.Name).CategoryName();
}

internal static class CategoryRules
{
    public static IRuleBuilderOptions<T, string> CategoryName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Kategori adı zorunludur.")
            .MaximumLength(Category.NameMaxLength)
            .WithMessage($"Kategori adı en fazla {Category.NameMaxLength} karakter olabilir.");

    public static async Task EnsureNameAvailableAsync(
        ICategoryRepository categories,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (await categories.IsNameTakenAsync(Category.Normalize(name), excludeId, cancellationToken))
        {
            throw CatalogValidation.FieldError("name", "Bu adla aktif bir kategori zaten var.");
        }
    }
}

public sealed class CreateCategoryCommandHandler(ICategoryRepository categories, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        await CategoryRules.EnsureNameAvailableAsync(categories, request.Name, excludeId: null, cancellationToken);

        var category = Category.Create(request.Name);
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}

public sealed class UpdateCategoryCommandHandler(ICategoryRepository categories, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCategoryCommand>
{
    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kategori", request.Id);

        if (category.IsActive)
        {
            await CategoryRules.EnsureNameAvailableAsync(categories, request.Name, category.Id, cancellationToken);
        }

        category.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeactivateCategoryCommandHandler(
    ICategoryRepository categories,
    IProductRepository products,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateCategoryCommand>
{
    public async Task Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kategori", request.Id);

        if (!category.IsActive)
        {
            return;
        }

        category.Deactivate(await products.HasActiveProductsInCategoryAsync(category.Id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
