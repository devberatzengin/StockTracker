using FluentValidation;
using MediatR;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Products;

public sealed record AddProductBarcodeCommand(Guid ProductId, string Value, BarcodeFormat Format) : IRequest<Guid>;

public sealed record RemoveProductBarcodeCommand(Guid ProductId, Guid BarcodeId) : IRequest;

public sealed class AddProductBarcodeCommandValidator : AbstractValidator<AddProductBarcodeCommand>
{
    public AddProductBarcodeCommandValidator()
    {
        RuleFor(x => new BarcodeInput(x.Value, x.Format))
            .SetValidator(new BarcodeInputValidator())
            .OverridePropertyName(string.Empty);
    }
}

public sealed class AddProductBarcodeCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : IRequestHandler<AddProductBarcodeCommand, Guid>
{
    public async Task<Guid> Handle(AddProductBarcodeCommand request, CancellationToken cancellationToken)
    {
        var product = await ProductHandlerHelpers.LoadProductAsync(products, request.ProductId, cancellationToken);
        await ProductHandlerHelpers.EnsureBarcodeAvailableAsync(products, request.Value, cancellationToken);

        var barcode = product.AddBarcode(request.Value, request.Format);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return barcode.Id;
    }
}

public sealed class RemoveProductBarcodeCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveProductBarcodeCommand>
{
    public async Task Handle(RemoveProductBarcodeCommand request, CancellationToken cancellationToken)
    {
        var product = await ProductHandlerHelpers.LoadProductAsync(products, request.ProductId, cancellationToken);
        product.RemoveBarcode(request.BarcodeId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
