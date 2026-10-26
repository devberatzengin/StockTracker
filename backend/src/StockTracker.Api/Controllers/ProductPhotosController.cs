using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTracker.Application.Catalog.Products;
using StockTracker.Application.Common.Security;

namespace StockTracker.Api.Controllers;

public sealed record ReorderPhotosRequest(IReadOnlyList<Guid> PhotoIds);

[ApiController]
[Route("api/products/{productId:guid}/photos")]
[Authorize(Policy = Permissions.CanManageCatalog)]
public sealed class ProductPhotosController(ISender sender) : ControllerBase
{
    // 5 MB image plus multipart overhead; the handler enforces the exact limit.
    private const long RequestLimit = 6 * 1024 * 1024;

    /// <summary>Multipart upload with a single "file" field (JPEG, PNG or WebP, at most 5 MB).</summary>
    [HttpPost]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [Consumes("multipart/form-data")]
    public async Task<ProductPhotoDto> Upload(Guid productId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return await sender.Send(new UploadProductPhotoCommand(productId, content, file.Length), cancellationToken);
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(Guid productId, ReorderPhotosRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ReorderProductPhotosCommand(productId, request.PhotoIds), cancellationToken);
        return NoContent();
    }

    [HttpPost("{photoId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid productId, Guid photoId, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrimaryProductPhotoCommand(productId, photoId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> Delete(Guid productId, Guid photoId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductPhotoCommand(productId, photoId), cancellationToken);
        return NoContent();
    }
}
