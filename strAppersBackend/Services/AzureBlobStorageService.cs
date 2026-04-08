using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public class AzureBlobStorageService : IAzureBlobStorageService
{
    private readonly AzureBlobStorageOptions _options;
    private readonly BlobServiceClient? _client;
    private readonly string? _containerName;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options, ILogger<AzureBlobStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value ?? new AzureBlobStorageOptions();
        _logger = logger;
        var cs = _options.ConnectionString?.Trim();
        if (string.IsNullOrEmpty(cs))
        {
            _client = null;
            _containerName = null;
            return;
        }

        _client = new BlobServiceClient(cs);
        _containerName = string.IsNullOrWhiteSpace(_options.ContainerName) ? "resources" : _options.ContainerName.Trim();
    }

    public bool IsConfigured => _client != null && !string.IsNullOrEmpty(_containerName);

    public async Task<string> UploadResourceBlobAsync(
        Stream content,
        string contentType,
        string boardId,
        int studentId,
        string safeFileName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _client == null || string.IsNullOrEmpty(_containerName))
            throw new InvalidOperationException("Azure Blob Storage is not configured.");

        var container = _client.GetBlobContainerClient(_containerName);
        var publicAccess = _options.PublicBlobAccess ? PublicAccessType.Blob : PublicAccessType.None;
        await container.CreateIfNotExistsAsync(publicAccess, cancellationToken: cancellationToken);

        var prefix = $"{SanitizePathSegment(boardId)}/{studentId}";
        var blobName = $"{prefix}/{Guid.NewGuid():N}-{safeFileName}";
        var blob = container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType };
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        return blob.Uri.ToString();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> OpenBlobReadAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default)
    {
        var r = await OpenBlobReadDetailedAsync(blobUri, cancellationToken);
        return r.Success ? (r.Stream!, r.ContentType!, r.FileName!) : null;
    }

    public async Task<BlobOpenReadResult> OpenBlobReadDetailedAsync(Uri blobUri, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _client == null || string.IsNullOrEmpty(_containerName))
        {
            _logger.LogDebug("OpenBlobReadAsync: storage not configured.");
            return BlobOpenReadResult.Fail("NotConfigured",
                "Azure Blob Storage is not configured on this server.");
        }

        if (!IsAzureBlobStorageHost(blobUri.Host))
        {
            _logger.LogWarning("OpenBlobReadAsync: host {Host} is not an Azure Blob host.", blobUri.Host);
            return BlobOpenReadResult.Fail("BadHost",
                "The saved URL is not an Azure Blob Storage address.");
        }

        if (!string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("OpenBlobReadAsync: scheme {Scheme} is not https.", blobUri.Scheme);
            return BlobOpenReadResult.Fail("NotHttps", "The saved URL must use HTTPS.");
        }

        if (TryGetAccountNameFromBlobHost(blobUri.Host, out var urlAccount) &&
            !string.Equals(urlAccount, _client.AccountName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "OpenBlobReadAsync: URL storage account {UrlAccount} does not match configured account {CfgAccount}.",
                urlAccount,
                _client.AccountName);
            return BlobOpenReadResult.Fail("AccountMismatch",
                $"This server’s storage account is \"{_client.AccountName}\" but the file URL points to account \"{urlAccount}\". Re-upload from Resources on this environment, or fix the AzureStorage connection string / saved URL.");
        }

        if (!TryResolveBlobContainerAndName(blobUri, out var containerFromUrl, out var blobPath))
        {
            _logger.LogWarning("OpenBlobReadAsync: could not parse container/blob from {Uri}.", blobUri.GetLeftPart(UriPartial.Path));
            return BlobOpenReadResult.Fail("InvalidBlobPath",
                "The blob URL path must look like /container-name/blob/path (could not parse container and blob name).");
        }

        if (!string.Equals(containerFromUrl, _containerName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "OpenBlobReadAsync: URL container {UrlContainer} does not match configured container {CfgContainer}.",
                containerFromUrl,
                _containerName);
            return BlobOpenReadResult.Fail("ContainerMismatch",
                $"The URL uses container \"{containerFromUrl}\" but this app is configured for container \"{_containerName}\". Fix AzureStorage:ContainerName or re-upload so the saved URL matches.");
        }

        var container = _client.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            _logger.LogWarning(
                "OpenBlobReadAsync: blob not found (account {Account}, container {Container}, name prefix {Prefix}…).",
                _client.AccountName,
                _containerName,
                blobPath.Length > 48 ? blobPath[..48] : blobPath);
            return BlobOpenReadResult.Fail("BlobNotFound",
                "That file is no longer in team storage (deleted or never uploaded here), or the saved link is wrong. Re-upload from Resources.");
        }

        var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        var ct = props.Value.ContentType ?? "application/octet-stream";
        var fileName = Path.GetFileName(blobPath);
        return BlobOpenReadResult.Ok(stream, ct, fileName);
    }

    /// <inheritdoc />
    public async Task<string?> GetBlobReadSasUriAsync(Uri blobUri, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _client == null || string.IsNullOrEmpty(_containerName))
            return null;

        if (!IsAzureBlobStorageHost(blobUri.Host))
            return null;

        if (!string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        if (TryGetAccountNameFromBlobHost(blobUri.Host, out var urlAccountSas) &&
            !string.Equals(urlAccountSas, _client.AccountName, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!TryResolveBlobContainerAndName(blobUri, out var containerFromUrl, out var blobPath))
            return null;

        if (!string.Equals(containerFromUrl, _containerName, StringComparison.OrdinalIgnoreCase))
            return null;

        var container = _client.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(cancellationToken))
            return null;

        var expiryMinutes = _options.ResourceReviewSasExpiryMinutes > 0
            ? _options.ResourceReviewSasExpiryMinutes
            : 15;
        if (expiryMinutes < 5) expiryMinutes = 5;
        if (expiryMinutes > 60) expiryMinutes = 60;

        if (!blob.CanGenerateSasUri)
            return null;

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobPath,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blob.GenerateSasUri(sasBuilder).AbsoluteUri;
    }

    /// <summary>
    /// Resolves container and blob name from a blob HTTPS URL. Uses <see cref="BlobUriBuilder"/> so
    /// percent-encoded paths (e.g. Hebrew filenames) decode to the same logical name Azure stores — unlike
    /// using <see cref="Uri.AbsolutePath"/> raw segments with <see cref="BlobContainerClient.GetBlobClient(string)"/>.
    /// </summary>
    private static bool TryResolveBlobContainerAndName(Uri blobUri, out string containerName, out string blobName)
    {
        containerName = "";
        blobName = "";
        try
        {
            var b = new BlobUriBuilder(blobUri);
            if (string.IsNullOrEmpty(b.BlobContainerName) || string.IsNullOrEmpty(b.BlobName))
                return false;
            containerName = b.BlobContainerName;
            blobName = b.BlobName;
            return true;
        }
        catch (Exception)
        {
            return TryResolveBlobContainerAndNameFromRawPath(blobUri, out containerName, out blobName);
        }
    }

    private static bool TryResolveBlobContainerAndNameFromRawPath(Uri blobUri, out string containerName, out string blobName)
    {
        containerName = "";
        blobName = "";
        var path = blobUri.AbsolutePath.TrimStart('/');
        var slash = path.IndexOf('/');
        if (slash <= 0 || slash >= path.Length - 1)
            return false;
        try
        {
            containerName = Uri.UnescapeDataString(path[..slash]);
            blobName = Uri.UnescapeDataString(path[(slash + 1)..]);
        }
        catch (UriFormatException)
        {
            containerName = path[..slash];
            blobName = path[(slash + 1)..];
        }

        return !string.IsNullOrEmpty(blobName);
    }

    private static bool IsAzureBlobStorageHost(string host) =>
        host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".blob.storage.azure.net", StringComparison.OrdinalIgnoreCase);

    /// <summary>First label of <c>*.blob.core.windows.net</c> or <c>*.blob.storage.azure.net</c> is the storage account name.</summary>
    private static bool TryGetAccountNameFromBlobHost(string host, out string accountName)
    {
        accountName = "";
        if (host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".blob.storage.azure.net", StringComparison.OrdinalIgnoreCase))
        {
            var i = host.IndexOf('.');
            if (i <= 0)
                return false;
            accountName = host[..i];
            return accountName.Length > 0;
        }

        return false;
    }

    private static string SanitizePathSegment(string boardId)
    {
        var s = boardId.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Length > 0 ? s : "board";
    }
}
