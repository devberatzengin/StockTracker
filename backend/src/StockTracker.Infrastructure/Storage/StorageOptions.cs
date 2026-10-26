namespace StockTracker.Infrastructure.Storage;

public enum StorageProvider
{
    /// <summary>Files on disk (the Docker "uploads" volume); read through signed /api/files URLs.</summary>
    Local,

    /// <summary>Cloudflare R2 (S3 compatible); read through presigned URLs.</summary>
    R2,
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Local;

    /// <summary>Root folder of local files (relative paths resolve against the working directory).</summary>
    public string LocalPath { get; set; } = "uploads";

    /// <summary>HMAC key for local signed URLs (at least 32 characters).</summary>
    public string? SigningKey { get; set; }

    public R2Options R2 { get; set; } = new();

    /// <summary>Configuration errors for the selected provider; empty when usable.</summary>
    public IEnumerable<string> Validate()
    {
        if (Provider == StorageProvider.Local && (SigningKey?.Length ?? 0) < 32)
        {
            yield return "Storage:SigningKey must be at least 32 characters for local storage.";
        }

        if (Provider == StorageProvider.R2)
        {
            if (string.IsNullOrWhiteSpace(R2.AccountId)
                || string.IsNullOrWhiteSpace(R2.AccessKeyId)
                || string.IsNullOrWhiteSpace(R2.SecretAccessKey)
                || string.IsNullOrWhiteSpace(R2.Bucket))
            {
                yield return "Storage:R2 needs AccountId, AccessKeyId, SecretAccessKey and Bucket.";
            }
        }
    }
}

public sealed class R2Options
{
    public string? AccountId { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public string? Bucket { get; set; }
}
