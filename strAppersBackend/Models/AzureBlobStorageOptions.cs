namespace strAppersBackend.Models;

/// <summary>Azure Blob Storage settings (see Docs/AZURE_BLOB_STORAGE.md).</summary>
public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureStorage";

    /// <summary>Storage account connection string (or use <see cref="AccountName"/> + <see cref="AccountKey"/>).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Container for board resource attachments. Created on first upload if missing.</summary>
    public string ContainerName { get; set; } = "resources";

    /// <summary>Max upload size in bytes (default 30 MB; keep in sync with upload-blob RequestSizeLimit and IIS maxAllowedContentLength).</summary>
    public long MaxBytes { get; set; } = 30L * 1024 * 1024;

    /// <summary>When true, new containers are created with public blob read access so <see cref="Resource.Url"/> works in the browser. If false, use GET …/file/{resourceId} to stream.</summary>
    public bool PublicBlobAccess { get; set; } = true;

    /// <summary>Lifetime (minutes) for read-only user-delegation-style SAS links used by mentor resource review for PDF/images. Clamped to 5–60. Requires account-key access in the connection string.</summary>
    public int ResourceReviewSasExpiryMinutes { get; set; } = 15;
}
