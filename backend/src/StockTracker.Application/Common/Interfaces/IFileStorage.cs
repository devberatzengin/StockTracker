namespace StockTracker.Application.Common.Interfaces;

/// <summary>Stores files by key (local uploads volume or Cloudflare R2, see StorageOptions.Provider).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Deleting a missing file is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// A URL that reads the file without an access token (img tags cannot send one), valid for
    /// <paramref name="validFor"/>: a signed /api/files URL locally, a presigned URL on R2.
    /// </summary>
    string GetReadUrl(string key, TimeSpan validFor);
}

public sealed record ProcessedImage(byte[] Main, byte[] Thumbnail, string ContentType);

/// <summary>Thrown when an upload is not a usable image (corrupt, too large in pixels).</summary>
public sealed class InvalidImageException(string message) : Exception(message);

public interface IImageProcessor
{
    /// <summary>
    /// Applies the EXIF orientation, strips metadata and returns a main image (long side ≤ 1600 px)
    /// and a thumbnail (≤ 400 px), both WebP.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken cancellationToken);
}
