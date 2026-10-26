using Microsoft.Extensions.Options;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Files;

namespace StockTracker.Infrastructure.Storage;

/// <summary>Files under <see cref="StorageOptions.LocalPath"/>, read through HMAC-signed /api/files URLs.</summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options, TimeProvider clock) : IFileStorage, ISignedFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalPath);
    private readonly UrlSigner _signer = new(options.Value.SigningKey!);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temporary file first so readers never see a half-written file.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var file = File.Create(temporary))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    public string GetReadUrl(string key, TimeSpan validFor)
    {
        var expires = clock.GetUtcNow().Add(validFor).ToUnixTimeSeconds();
        return $"/api/files/{key}?exp={expires}&sig={_signer.Sign(key, expires)}";
    }

    public Task<StoredFile?> OpenSignedAsync(string key, long expires, string? signature, CancellationToken cancellationToken)
    {
        if (!FileKeys.IsValid(key) || !_signer.IsValid(key, expires, signature, clock.GetUtcNow()))
        {
            throw new ForbiddenException("Bağlantının süresi dolmuş ya da geçersiz.");
        }

        var path = PathOf(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var maxAge = TimeSpan.FromSeconds(Math.Max(0, expires - clock.GetUtcNow().ToUnixTimeSeconds()));
        return Task.FromResult<StoredFile?>(new StoredFile(File.OpenRead(path), FileKeys.ContentTypeOf(key), maxAge));
    }

    private string PathOf(string key)
    {
        if (!FileKeys.IsValid(key))
        {
            throw new ArgumentException($"Invalid file key '{key}'.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException($"Invalid file key '{key}'.", nameof(key));
    }
}
