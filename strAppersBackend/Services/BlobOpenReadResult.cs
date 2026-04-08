namespace strAppersBackend.Services;

/// <summary>Outcome of opening a blob by stored HTTPS URL (same patterns as <see cref="AzureBlobStorageService"/>).</summary>
public sealed class BlobOpenReadResult
{
    public Stream? Stream { get; init; }
    public string? ContentType { get; init; }
    public string? FileName { get; init; }

    /// <summary>Machine-readable: BlobNotFound, AccountMismatch, ContainerMismatch, InvalidBlobPath, NotConfigured, BadHost, NotHttps.</summary>
    public string? ErrorCode { get; init; }

    public string? UserHint { get; init; }

    public bool Success => Stream != null;

    public static BlobOpenReadResult Ok(Stream stream, string contentType, string fileName) => new()
    {
        Stream = stream,
        ContentType = contentType,
        FileName = fileName,
    };

    public static BlobOpenReadResult Fail(string errorCode, string userHint) => new()
    {
        ErrorCode = errorCode,
        UserHint = userHint,
    };
}
