using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Catalog;

namespace StockTracker.Application.Catalog.Products;

public sealed record ProductPhotoDto(Guid Id, string Url, string ThumbnailUrl, bool IsPrimary, int SortOrder, long SizeBytes);

/// <summary>Photo URLs are signed for one hour.</summary>
public static class PhotoUrls
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(1);

    public static string? Read(this IFileStorage storage, string? key) =>
        key is null ? null : storage.GetReadUrl(key, Validity);

    public static ProductPhotoDto ToDto(this IFileStorage storage, ProductPhoto photo) =>
        new(
            photo.Id,
            storage.GetReadUrl(photo.StorageKey, Validity),
            storage.GetReadUrl(photo.ThumbnailKey, Validity),
            photo.IsPrimary,
            photo.SortOrder,
            photo.SizeBytes);
}

/// <summary>Accepted uploads are recognized by their first bytes, never by the file name.</summary>
public static class ImageSignature
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static bool IsSupported(ReadOnlySpan<byte> header) =>
        IsJpeg(header) || IsPng(header) || IsWebP(header);

    private static bool IsJpeg(ReadOnlySpan<byte> h) => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> h) => h.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    // "RIFF" <size> "WEBP"
    private static bool IsWebP(ReadOnlySpan<byte> h) =>
        h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8);
}

public sealed record UploadProductPhotoCommand(Guid ProductId, Stream Content, long Length) : IRequest<ProductPhotoDto>;

public sealed record ReorderProductPhotosCommand(Guid ProductId, IReadOnlyList<Guid> PhotoIds) : IRequest;

public sealed record SetPrimaryProductPhotoCommand(Guid ProductId, Guid PhotoId) : IRequest;

public sealed record DeleteProductPhotoCommand(Guid ProductId, Guid PhotoId) : IRequest;

public sealed class ReorderProductPhotosCommandValidator : AbstractValidator<ReorderProductPhotosCommand>
{
    public ReorderProductPhotosCommandValidator() =>
        RuleFor(x => x.PhotoIds).NotNull().WithMessage("Fotoğraf sırası gönderilmedi.");
}

public sealed class UploadProductPhotoCommandHandler(
    IProductRepository products,
    IFileStorage storage,
    IImageProcessor images,
    IUnitOfWork unitOfWork,
    ILogger<UploadProductPhotoCommandHandler> logger) : IRequestHandler<UploadProductPhotoCommand, ProductPhotoDto>
{
    private const string BadFormat = "Sadece JPEG, PNG ya da WebP fotoğraf yükleyebilirsiniz.";

    public async Task<ProductPhotoDto> Handle(UploadProductPhotoCommand request, CancellationToken cancellationToken)
    {
        if (request.Length <= 0)
        {
            throw CatalogValidation.FieldError("file", "Bir fotoğraf seçin.");
        }

        if (request.Length > ImageSignature.MaxBytes)
        {
            throw CatalogValidation.FieldError("file", "Fotoğraf en fazla 5 MB olabilir.");
        }

        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > ImageSignature.MaxBytes)
        {
            throw CatalogValidation.FieldError("file", "Fotoğraf en fazla 5 MB olabilir.");
        }

        if (!ImageSignature.IsSupported(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16))))
        {
            throw CatalogValidation.FieldError("file", BadFormat);
        }

        buffer.Position = 0;
        ProcessedImage processed;
        try
        {
            processed = await images.ProcessAsync(buffer, cancellationToken);
        }
        catch (InvalidImageException e)
        {
            throw CatalogValidation.FieldError("file", e.Message);
        }

        ProductPhoto? photo = null;
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // Serializes uploads of one product so the 10-photo limit holds.
                await products.LockAsync(request.ProductId, ct);
                var product = await ProductHandlerHelpers.LoadProductAsync(products, request.ProductId, ct);

                var photoId = Guid.CreateVersion7();
                var key = $"products/{product.Id}/{photoId}.webp";
                var thumbnailKey = $"products/{product.Id}/{photoId}_thumb.webp";
                photo = product.AddPhoto(photoId, key, thumbnailKey, processed.ContentType, processed.Main.Length);

                await storage.SaveAsync(key, new MemoryStream(processed.Main), processed.ContentType, ct);
                await storage.SaveAsync(thumbnailKey, new MemoryStream(processed.Thumbnail), processed.ContentType, ct);
                try
                {
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch
                {
                    await DeleteQuietlyAsync(storage, logger, [key, thumbnailKey]);
                    throw;
                }
            },
            cancellationToken);

        return storage.ToDto(photo!);
    }

    internal static async Task DeleteQuietlyAsync(IFileStorage storage, ILogger logger, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Could not delete file {Key}; it is no longer referenced.", key);
            }
        }
    }
}

public sealed class ProductPhotoCommandHandlers(
    IProductRepository products,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<ProductPhotoCommandHandlers> logger)
    : IRequestHandler<ReorderProductPhotosCommand>,
      IRequestHandler<SetPrimaryProductPhotoCommand>,
      IRequestHandler<DeleteProductPhotoCommand>
{
    public Task Handle(ReorderProductPhotosCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.ProductId, p => p.ReorderPhotos(request.PhotoIds), cancellationToken);

    public Task Handle(SetPrimaryProductPhotoCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.ProductId, p => p.SetPrimaryPhoto(request.PhotoId), cancellationToken);

    /// <summary>The row goes first; the files are removed after the commit (a leftover file is only logged).</summary>
    public async Task Handle(DeleteProductPhotoCommand request, CancellationToken cancellationToken)
    {
        ProductPhoto? removed = null;
        await ChangeAsync(request.ProductId, p => removed = p.RemovePhoto(request.PhotoId), cancellationToken);
        await UploadProductPhotoCommandHandler.DeleteQuietlyAsync(storage, logger, [removed!.StorageKey, removed.ThumbnailKey]);
    }

    private Task ChangeAsync(Guid productId, Action<Product> change, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                await products.LockAsync(productId, ct);
                var product = await ProductHandlerHelpers.LoadProductAsync(products, productId, ct);
                change(product);
                await unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);
}
