using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace StockTracker.Infrastructure.Storage;

/// <summary>HMAC-SHA256 signatures for local file URLs: sig = HMAC(key, "{fileKey}:{exp}").</summary>
public sealed class UrlSigner(string signingKey)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(signingKey);

    public string Sign(string fileKey, long expiresAtUnix) =>
        WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{fileKey}:{expiresAtUnix}")));

    /// <summary>True when the signature matches and the link has not expired.</summary>
    public bool IsValid(string fileKey, long expiresAtUnix, string? signature, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(signature) || expiresAtUnix < now.ToUnixTimeSeconds())
        {
            return false;
        }

        byte[] given;
        try
        {
            given = WebEncoders.Base64UrlDecode(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{fileKey}:{expiresAtUnix}"));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}
