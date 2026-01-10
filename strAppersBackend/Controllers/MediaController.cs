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
                    YoutubeUrl = b.YoutubeUrl
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
                YoutubeUrl = board.YoutubeUrl
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








