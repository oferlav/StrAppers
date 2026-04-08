namespace strAppersBackend.Services;

public interface IAzureBlobStorageService
{
    bool IsConfigured { get; }

    /// <summary>Uploads a blob; returns public or canonical HTTPS URI (depending on container ACL).</summary>
    Task<string> UploadResourceBlobAsync(
        Stream content,
        string contentType,
        string boardId,
        int studentId,
        string safeFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a read stream for a blob referenced by its stored HTTPS URL (same account).</summary>
    Task<(Stream Stream, string ContentType, string FileName)?> OpenBlobReadAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="OpenBlobReadAsync"/> but returns a specific failure reason for API messages.</summary>
    Task<BlobOpenReadResult> OpenBlobReadDetailedAsync(Uri blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a read-only SAS URL for a blob in this account/container. Used for short-lived mentor/LLM access to PDFs and images.
    /// Returns null if blob missing, host invalid, or SAS cannot be generated (e.g. no account key).
    /// </summary>
    Task<string?> GetBlobReadSasUriAsync(Uri blobUri, CancellationToken cancellationToken = default);
}
