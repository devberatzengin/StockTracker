using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Application.Files;

namespace StockTracker.Infrastructure.Storage;

/// <summary>Cloudflare R2 through its S3 API; files are read with presigned GET URLs.</summary>
public sealed class R2FileStorage : IFileStorage, ISignedFileStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly TimeProvider _clock;

    public R2FileStorage(IOptions<StorageOptions> options, TimeProvider clock)
    {
        var r2 = options.Value.R2;
        _bucket = r2.Bucket!;
        _clock = clock;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = $"https://{r2.AccountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AuthenticationRegion = "auto",
                // R2 does not support the SDK's default request/response checksums.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) =>
        _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                DisablePayloadSigning = true,
            },
            cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _client.DeleteObjectAsync(_bucket, key, cancellationToken);

    public string GetReadUrl(string key, TimeSpan validFor) =>
        _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = _clock.GetUtcNow().Add(validFor).UtcDateTime,
        });

    /// <summary>R2 files are read directly from R2, never through the API.</summary>
    public Task<StoredFile?> OpenSignedAsync(string key, long expires, string? signature, CancellationToken cancellationToken) =>
        Task.FromResult<StoredFile?>(null);

    public void Dispose() => _client.Dispose();
}
