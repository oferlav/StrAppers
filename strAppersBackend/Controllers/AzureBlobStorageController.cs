using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

/// <summary>Azure Blob uploads for resource attachments. Returns a URL to store via <see cref="ResourcesController"/> add/modify.</summary>
[ApiController]
[Route("api/[controller]/use")]
public class AzureBlobStorageController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".txt", ".rtf", ".md",
        ".odt", ".ods", ".pptm", ".xlsm", ".vsdx"
    };

    private readonly ApplicationDbContext _db;
    private readonly IAzureBlobStorageService _blobs;
    private readonly AzureBlobStorageOptions _opts;
    private readonly ILogger<AzureBlobStorageController> _logger;

    public AzureBlobStorageController(
        ApplicationDbContext db,
        IAzureBlobStorageService blobs,
        Microsoft.Extensions.Options.IOptions<AzureBlobStorageOptions> opts,
        ILogger<AzureBlobStorageController> logger)
    {
        _db = db;
        _blobs = blobs;
        _opts = opts?.Value ?? new AzureBlobStorageOptions();
        _logger = logger;
    }

    /// <summary>Multipart upload: returns the blob HTTPS URL. Client then calls <c>POST /api/Resources/use/add</c> or modify with that URL.</summary>
    [HttpPost("upload-blob")]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<UploadBlobResponse>> UploadBlob(
        [FromForm] string boardId,
        [FromForm] int studentId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!_blobs.IsConfigured)
            return StatusCode(503, new { Message = "Azure Blob Storage is not configured. Set AzureStorage:ConnectionString." });

        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "File is required." });
        if (string.IsNullOrWhiteSpace(boardId))
            return BadRequest(new { Message = "boardId is required." });
        if (studentId <= 0)
            return BadRequest(new { Message = "studentId is required." });
        if (file.Length > _opts.MaxBytes)
            return BadRequest(new { Message = $"File exceeds maximum size of {_opts.MaxBytes / (1024 * 1024)} MB." });

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return BadRequest(new { Message = $"File type not allowed. Allowed: {string.Join(", ", AllowedExtensions.OrderBy(x => x))}" });

        var boardExists = await _db.ProjectBoards.AnyAsync(pb => pb.Id == boardId.Trim(), cancellationToken);
        if (!boardExists)
            return NotFound(new { Message = "Board not found." });
        var studentExists = await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            return NotFound(new { Message = "Student not found." });

        var safeName = SanitizeFileName(file.FileName);

        await using var read = file.OpenReadStream();
        string blobUrl;
        try
        {
            blobUrl = await _blobs.UploadResourceBlobAsync(
                read,
                file.ContentType ?? "application/octet-stream",
                boardId.Trim(),
                studentId,
                safeName,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob upload failed");
            return StatusCode(500, new { Message = "Upload failed." });
        }

        _logger.LogInformation("Blob uploaded BoardId={BoardId} StudentId={StudentId}", boardId, studentId);
        return Ok(new UploadBlobResponse { Url = blobUrl });
    }

    /// <summary>Streams a resource file from blob storage (for private containers or direct download). Uses the URL stored on the resource row.</summary>
    [HttpGet("file/{resourceId:int}")]
    public async Task<IActionResult> DownloadResourceFile(int resourceId, CancellationToken cancellationToken)
    {
        if (!_blobs.IsConfigured)
            return StatusCode(503, new { Message = "Azure Blob Storage is not configured." });

        var resource = await _db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resourceId && !r.IsFigma, cancellationToken);
        if (resource == null)
            return NotFound();

        if (!Uri.TryCreate(resource.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return NotFound();

        var opened = await _blobs.OpenBlobReadAsync(uri, cancellationToken);
        if (opened == null)
            return NotFound();

        var (stream, contentType, fileName) = opened.Value;
        // Same path /file/{id} after "replace file" — do not cache or browsers keep the previous blob body.
        Response.Headers.Append(HeaderNames.CacheControl, "private, no-store, no-cache, must-revalidate");
        Response.Headers.Append(HeaderNames.Pragma, "no-cache");
        return File(stream, contentType, WebUtility.UrlEncode(fileName));
    }

    private static string SanitizeFileName(string original)
    {
        var name = Path.GetFileName(original);
        var ext = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        var safe = new string(baseName.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or ' ').ToArray()).Trim();
        if (string.IsNullOrEmpty(safe))
            safe = "file";
        if (safe.Length > 80)
            safe = safe[..80];
        return safe + ext.ToLowerInvariant();
    }
}

public class UploadBlobResponse
{
    public string Url { get; set; } = string.Empty;
}
