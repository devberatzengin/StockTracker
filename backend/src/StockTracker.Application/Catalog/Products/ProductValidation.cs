using FluentValidation;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Products;

/// <summary>Fields shared by product create and update commands.</summary>
public interface IProductFields
{
    string Name { get; }

    string? Description { get; }

    Guid CategoryId { get; }

    Unit Unit { get; }

    TrackingType TrackingType { get; }

    bool IsAssignable { get; }

    decimal? MinStockLevel { get; }
}

public sealed class ProductFieldsValidator : AbstractValidator<IProductFields>
{
    public ProductFieldsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ürün adı zorunludur.")
            .MaximumLength(Product.NameMaxLength)
            .WithMessage($"Ürün adı en fazla {Product.NameMaxLength} karakter olabilir.");
        RuleFor(x => x.Description)
            .MaximumLength(Product.DescriptionMaxLength)
            .WithMessage($"Açıklama en fazla {Product.DescriptionMaxLength} karakter olabilir.");
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Kategori seçin.");
        RuleFor(x => x.Unit).IsInEnum().WithMessage("Geçersiz birim.");
        RuleFor(x => x.TrackingType).IsInEnum().WithMessage("Geçersiz takip tipi.");
        RuleFor(x => x.MinStockLevel)
            .GreaterThanOrEqualTo(0).When(x => x.MinStockLevel is not null)
            .WithMessage("Minimum stok negatif olamaz.");
    }
}

public sealed class BarcodeInputValidator : AbstractValidator<BarcodeInput>
{
    public BarcodeInputValidator()
    {
        RuleFor(x => x.Value)
            .Must(v => ProductBarcode.Normalize(v).Length > 0).WithMessage("Barkod değeri zorunludur.")
            .Must(v => ProductBarcode.Normalize(v).Length <= ProductBarcode.ValueMaxLength)
            .WithMessage($"Barkod en fazla {ProductBarcode.ValueMaxLength} karakter olabilir.");
        RuleFor(x => x.Format).IsInEnum().WithMessage("Geçersiz barkod formatı.");
    }
}
