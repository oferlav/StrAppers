using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class MediaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MediaController> _logger;

    public MediaController(ApplicationDbContext context, ILogger<MediaController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Request model for updating media URLs
    /// </summary>
    public class UpdateMediaRequest
    {
        [Required]
        public string BoardId { get; set; } = string.Empty;

        public string? FacebookUrl { get; set; }
        public string? PresentationUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? CollectionJourneyUrl { get; set; }
        public string? DatabaseSchemaUrl { get; set; }
        public string? Document1Url { get; set; }
        public string? Document2Url { get; set; }
        public string? Document3Url { get; set; }
        public string? Document4Url { get; set; }
        public string? Document1Name { get; set; }
        public string? Document2Name { get; set; }
        public string? Document3Name { get; set; }
        public string? Document4Name { get; set; }
    }

    /// <summary>
    /// Response model for media URLs
    /// </summary>
    public class MediaResponse
    {
        public string BoardId { get; set; } = string.Empty;
        public string? FacebookUrl { get; set; }
        public string? PresentationUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? CollectionJourneyUrl { get; set; }
        public string? DatabaseSchemaUrl { get; set; }
        public string? Document1Url { get; set; }
        public string? Document2Url { get; set; }
        public string? Document3Url { get; set; }
        public string? Document4Url { get; set; }
        public string? Document1Name { get; set; }
        public string? Document2Name { get; set; }
        public string? Document3Name { get; set; }
        public string? Document4Name { get; set; }
    }

    /// <summary>
    /// Get all media URLs for a project board
    /// </summary>
    [HttpGet("get-media/{boardId}")]
    public async Task<ActionResult<MediaResponse>> GetMedia(string boardId)
    {
        try
        {
            _logger.LogInformation("Getting media URLs for board: {BoardId}", boardId);

            var board = await _context.ProjectBoards
                .Where(b => b.Id == boardId)
                .Select(b => new MediaResponse
                {
                    BoardId = b.Id,
                    FacebookUrl = b.FacebookUrl,
                    PresentationUrl = b.PresentationUrl,
                    LinkedInUrl = b.LinkedInUrl,
                    InstagramUrl = b.InstagramUrl,
                    YoutubeUrl = b.YoutubeUrl,
                    CollectionJourneyUrl = b.CollectionJourneyUrl,
                    DatabaseSchemaUrl = b.DatabaseSchemaUrl,
                    Document1Url = b.Document1Url,
                    Document2Url = b.Document2Url,
                    Document3Url = b.Document3Url,
                    Document4Url = b.Document4Url,
                    Document1Name = b.Document1Name,
                    Document2Name = b.Document2Name,
                    Document3Name = b.Document3Name,
                    Document4Name = b.Document4Name
                })
                .FirstOrDefaultAsync();

            if (board == null)
            {
                return NotFound(new { success = false, message = $"Board with ID '{boardId}' not found" });
            }

            return Ok(new { success = true, data = board });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media URLs for board: {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }

    /// <summary>
    /// Update media URLs for a project board
    /// </summary>
    [HttpPost("update-media")]
    public async Task<ActionResult<MediaResponse>> UpdateMedia([FromBody] UpdateMediaRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BoardId))
            {
                return BadRequest(new { success = false, message = "BoardId is required" });
            }

            _logger.LogInformation("Updating media URLs for board: {BoardId}", request.BoardId);

            var board = await _context.ProjectBoards
                .FirstOrDefaultAsync(b => b.Id == request.BoardId);

            if (board == null)
            {
                return NotFound(new { success = false, message = $"Board with ID '{request.BoardId}' not found" });
            }

            // Update only provided fields (null values are ignored)
            if (request.FacebookUrl != null)
            {
                board.FacebookUrl = request.FacebookUrl.Length > 1000 ? request.FacebookUrl.Substring(0, 1000) : request.FacebookUrl;
            }
            
            if (request.PresentationUrl != null)
            {
                board.PresentationUrl = request.PresentationUrl.Length > 1000 ? request.PresentationUrl.Substring(0, 1000) : request.PresentationUrl;
            }
            
            if (request.LinkedInUrl != null)
            {
                board.LinkedInUrl = request.LinkedInUrl.Length > 1000 ? request.LinkedInUrl.Substring(0, 1000) : request.LinkedInUrl;
            }
            
            if (request.InstagramUrl != null)
            {
                board.InstagramUrl = request.InstagramUrl.Length > 1000 ? request.InstagramUrl.Substring(0, 1000) : request.InstagramUrl;
            }
            
            if (request.YoutubeUrl != null)
            {
                board.YoutubeUrl = request.YoutubeUrl.Length > 1000 ? request.YoutubeUrl.Substring(0, 1000) : request.YoutubeUrl;
            }
            if (request.CollectionJourneyUrl != null)
            {
                board.CollectionJourneyUrl = request.CollectionJourneyUrl.Length > 1000 ? request.CollectionJourneyUrl.Substring(0, 1000) : request.CollectionJourneyUrl;
            }
            if (request.DatabaseSchemaUrl != null)
            {
                board.DatabaseSchemaUrl = request.DatabaseSchemaUrl.Length > 1000 ? request.DatabaseSchemaUrl.Substring(0, 1000) : request.DatabaseSchemaUrl;
            }
            if (request.Document1Url != null)
            {
                board.Document1Url = request.Document1Url.Length > 1000 ? request.Document1Url.Substring(0, 1000) : request.Document1Url;
            }
            if (request.Document2Url != null)
            {
                board.Document2Url = request.Document2Url.Length > 1000 ? request.Document2Url.Substring(0, 1000) : request.Document2Url;
            }
            if (request.Document3Url != null)
            {
                board.Document3Url = request.Document3Url.Length > 1000 ? request.Document3Url.Substring(0, 1000) : request.Document3Url;
            }
            if (request.Document4Url != null)
            {
                board.Document4Url = request.Document4Url.Length > 1000 ? request.Document4Url.Substring(0, 1000) : request.Document4Url;
            }
            if (request.Document1Name != null)
            {
                board.Document1Name = request.Document1Name.Length > 50 ? request.Document1Name.Substring(0, 50) : request.Document1Name;
            }
            if (request.Document2Name != null)
            {
                board.Document2Name = request.Document2Name.Length > 50 ? request.Document2Name.Substring(0, 50) : request.Document2Name;
            }
            if (request.Document3Name != null)
            {
                board.Document3Name = request.Document3Name.Length > 50 ? request.Document3Name.Substring(0, 50) : request.Document3Name;
            }
            if (request.Document4Name != null)
            {
                board.Document4Name = request.Document4Name.Length > 50 ? request.Document4Name.Substring(0, 50) : request.Document4Name;
            }

            board.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated media URLs for board: {BoardId}", request.BoardId);

            var response = new MediaResponse
            {
                BoardId = board.Id,
                FacebookUrl = board.FacebookUrl,
                PresentationUrl = board.PresentationUrl,
                LinkedInUrl = board.LinkedInUrl,
                InstagramUrl = board.InstagramUrl,
                YoutubeUrl = board.YoutubeUrl,
                CollectionJourneyUrl = board.CollectionJourneyUrl,
                DatabaseSchemaUrl = board.DatabaseSchemaUrl,
                Document1Url = board.Document1Url,
                Document2Url = board.Document2Url,
                Document3Url = board.Document3Url,
                Document4Url = board.Document4Url,
                Document1Name = board.Document1Name,
                Document2Name = board.Document2Name,
                Document3Name = board.Document3Name,
                Document4Name = board.Document4Name
            };

            return Ok(new { success = true, data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media URLs for board: {BoardId}", request.BoardId);
            return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }
}








