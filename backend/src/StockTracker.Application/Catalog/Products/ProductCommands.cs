using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Codes;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Common.Codes;
using Unit = StockTracker.Domain.Catalog.Unit;
using StockTracker.Domain.Exceptions;

namespace StockTracker.Application.Catalog.Products;

public sealed record CreateProductCommand(
    string Name,
    string? Description,
    Guid CategoryId,
    Unit Unit,
    TrackingType TrackingType,
    bool IsAssignable,
    decimal? MinStockLevel,
    IReadOnlyList<BarcodeInput>? Barcodes) : IRequest<Guid>, IProductFields;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    Unit Unit,
    TrackingType TrackingType,
    bool IsAssignable,
    decimal? MinStockLevel) : IRequest, IProductFields;

public sealed record DeactivateProductCommand(Guid Id) : IRequest;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        Include(new ProductFieldsValidator());
        RuleForEach(x => x.Barcodes).SetValidator(new BarcodeInputValidator());
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator() => Include(new ProductFieldsValidator());
}

internal static class ProductHandlerHelpers
{
    public static async Task<Category> LoadCategoryAsync(
        ICategoryRepository categories,
        Guid categoryId,
        CancellationToken cancellationToken) =>
        await categories.GetByIdAsync(categoryId, cancellationToken)
        ?? throw CatalogValidation.FieldError("categoryId", "Kategori bulunamadı.");

    public static async Task<Product> LoadProductAsync(
        IProductRepository products,
        Guid productId,
        CancellationToken cancellationToken) =>
        await products.GetByIdAsync(productId, cancellationToken)
        ?? throw new NotFoundException("Ürün", productId);

    /// <summary>Barcode values are unique across all products; the unique index backs this up.</summary>
    public static async Task EnsureBarcodeAvailableAsync(
        IProductRepository products,
        string value,
        CancellationToken cancellationToken)
    {
        if (await products.BarcodeExistsAsync(ProductBarcode.Normalize(value), cancellationToken))
        {
            throw new DomainException(CatalogErrors.BarcodeDuplicate, "Bu barkod başka bir ürüne kayıtlı.");
        }
    }
}

public sealed class CreateProductCommandHandler(
    ICategoryRepository categories,
    IProductRepository products,
    ICodeGenerator codeGenerator,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var category = await ProductHandlerHelpers.LoadCategoryAsync(categories, request.CategoryId, cancellationToken);

        var product = Product.Create(
            await codeGenerator.GenerateAsync(CodePrefix.Product, cancellationToken),
            request.Name,
            request.Description,
            category,
            request.Unit,
            request.TrackingType,
            request.IsAssignable,
            request.MinStockLevel);

        foreach (var barcode in request.Barcodes ?? [])
        {
            await ProductHandlerHelpers.EnsureBarcodeAvailableAsync(products, barcode.Value, cancellationToken);
            product.AddBarcode(barcode.Value, barcode.Format);
        }

        products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}

public sealed class UpdateProductCommandHandler(
    ICategoryRepository categories,
    IProductRepository products,
    IProductStockChecker stockChecker,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await ProductHandlerHelpers.LoadProductAsync(products, request.Id, cancellationToken);
        var category = await ProductHandlerHelpers.LoadCategoryAsync(categories, request.CategoryId, cancellationToken);

        var trackingTypeChanges = request.TrackingType != product.TrackingType;
        var hasStockHistory = trackingTypeChanges
            && await stockChecker.HasStockOrMovementsAsync(product.Id, cancellationToken);

        product.Update(
            request.Name,
            request.Description,
            category,
            request.Unit,
            request.TrackingType,
            request.IsAssignable,
            request.MinStockLevel,
            hasStockHistory);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeactivateProductCommandHandler(
    IProductRepository products,
    IProductStockChecker stockChecker,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateProductCommand>
{
    public async Task Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await ProductHandlerHelpers.LoadProductAsync(products, request.Id, cancellationToken);
        if (!product.IsActive)
        {
            return;
        }

        product.Deactivate(await stockChecker.HasStockAsync(product.Id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
