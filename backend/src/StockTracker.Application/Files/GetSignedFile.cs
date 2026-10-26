using MediatR;
using StockTracker.Application.Common.Exceptions;

namespace StockTracker.Application.Files;

/// <param name="MaxAge">How long the browser may cache it (until the link expires).</param>
public sealed record StoredFile(Stream Content, string ContentType, TimeSpan MaxAge);

/// <summary>Opens a file through a signed link (local storage; R2 links go to R2 directly).</summary>
public interface ISignedFileStore
{
    /// <summary>Throws <see cref="ForbiddenException"/> for an invalid or expired signature; null when missing.</summary>
    Task<StoredFile?> OpenSignedAsync(string key, long expires, string? signature, CancellationToken cancellationToken);
}

public sealed record GetSignedFileQuery(string Key, long Expires, string? Signature) : IRequest<StoredFile>;

public sealed class GetSignedFileQueryHandler(ISignedFileStore files) : IRequestHandler<GetSignedFileQuery, StoredFile>
{
    public async Task<StoredFile> Handle(GetSignedFileQuery request, CancellationToken cancellationToken) =>
        await files.OpenSignedAsync(request.Key, request.Expires, request.Signature, cancellationToken)
        ?? throw new NotFoundException("Dosya", request.Key);
}
