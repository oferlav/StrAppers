using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/skill-in-website")]
public class SkillInWebsiteController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SkillInWebsiteController> _logger;

    public SkillInWebsiteController(ApplicationDbContext context, ILogger<SkillInWebsiteController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get marketing image by Id and return as base64 data URL
    /// </summary>
    /// <param name="id">The image Id</param>
    /// <returns>Image as base64 data URL or 404 if not found</returns>
    [HttpGet("render-image")]
    public async Task<IActionResult> RenderImage([FromQuery] int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { Success = false, Message = "Id must be greater than 0" });
            }

            _logger.LogInformation("🖼️ [RENDER-IMAGE] Fetching image with Id: {Id}", id);

            var image = await _context.MarketingImages
                .FirstOrDefaultAsync(m => m.Id == id);

            if (image == null)
            {
                _logger.LogWarning("⚠️ [RENDER-IMAGE] Image with Id {Id} not found", id);
                return NotFound(new { Success = false, Message = $"Image with Id {id} not found" });
            }

            if (string.IsNullOrWhiteSpace(image.Base64))
            {
                _logger.LogWarning("⚠️ [RENDER-IMAGE] Image with Id {Id} has empty Base64 data", id);
                return NotFound(new { Success = false, Message = $"Image with Id {id} has no data" });
            }

            // Determine content type and handle different formats
            // Base64 image data typically starts with "data:image/..." or just the base64 string
            string contentType = "image/png"; // Default
            string base64Data = image.Base64;
            bool isSvg = false;

            if (image.Base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // Extract content type from data URL
                var dataUrlParts = image.Base64.Split(',');
                if (dataUrlParts.Length == 2)
                {
                    var header = dataUrlParts[0];
                    if (header.Contains("image/png", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/png";
                    else if (header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) || header.Contains("image/jpg", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/jpeg";
                    else if (header.Contains("image/gif", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/gif";
                    else if (header.Contains("image/webp", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/webp";
                    else if (header.Contains("image/svg", StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = "image/svg+xml";
                        isSvg = true;
                    }
                    
                    base64Data = dataUrlParts[1];
                }
            }
            else
            {
                // Check if it's plain SVG text (starts with <svg or <?xml)
                var trimmedData = image.Base64.TrimStart();
                if (trimmedData.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || 
                    trimmedData.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                {
                    // Plain SVG XML text - return as-is
                    contentType = "image/svg+xml";
                    _logger.LogInformation("✅ [RENDER-IMAGE] Detected plain SVG text for image Id {Id}", id);
                    return Content(image.Base64, contentType);
                }
                
                // Try to detect image type from base64 content
                // PNG starts with iVBORw0KGgo
                // JPEG starts with /9j/
                // GIF starts with R0lGODlh
                // WebP starts with UklGR
                if (base64Data.StartsWith("iVBORw0KGgo", StringComparison.OrdinalIgnoreCase))
                    contentType = "image/png";
                else if (base64Data.StartsWith("/9j/", StringComparison.OrdinalIgnoreCase))
                    contentType = "image/jpeg";
                else if (base64Data.StartsWith("R0lGODlh", StringComparison.OrdinalIgnoreCase))
                    contentType = "image/gif";
                else if (base64Data.StartsWith("UklGR", StringComparison.OrdinalIgnoreCase))
                    contentType = "image/webp";
            }

            // Try to convert base64 to bytes
            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                // If base64 decode fails, check if it's SVG text (fallback)
                var trimmedData = image.Base64.TrimStart();
                if (trimmedData.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || 
                    trimmedData.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                {
                    // Plain SVG XML text - return as-is
                    contentType = "image/svg+xml";
                    _logger.LogInformation("✅ [RENDER-IMAGE] Base64 decode failed, treating as plain SVG text for image Id {Id}", id);
                    return Content(image.Base64, contentType);
                }
                
                // Not base64 and not SVG - invalid format
                _logger.LogError("❌ [RENDER-IMAGE] Invalid base64 format and not SVG text for image Id {Id}", id);
                return BadRequest(new { Success = false, Message = "Invalid base64 image data or SVG format" });
            }

            // If it's SVG from data URL, decode the base64 to get the SVG text
            if (isSvg && imageBytes != null)
            {
                var svgText = System.Text.Encoding.UTF8.GetString(imageBytes);
                _logger.LogInformation("✅ [RENDER-IMAGE] Successfully decoded base64 SVG for image Id {Id}, Size: {Size} bytes", 
                    id, imageBytes.Length);
                return Content(svgText, contentType);
            }

            _logger.LogInformation("✅ [RENDER-IMAGE] Successfully retrieved image Id {Id}, ContentType: {ContentType}, Size: {Size} bytes", 
                id, contentType, imageBytes.Length);

            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [RENDER-IMAGE] Error rendering image with Id {Id}: {Message}", id, ex.Message);
            return StatusCode(500, new { Success = false, Message = $"Error rendering image: {ex.Message}" });
        }
    }

    /// <summary>
    /// Register an early bird user (Junior or Employer)
    /// </summary>
    /// <param name="request">Registration request data</param>
    /// <returns>Success response with registration ID</returns>
    [HttpPost("register")]
    public async Task<ActionResult<object>> Register([FromBody] EarlyBirdRegistrationRequest request)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Type))
            {
                return BadRequest(new { Success = false, Message = "Type is required (must be 'Junior' or 'Employer')" });
            }

            // Validate Type value
            var typeUpper = request.Type.Trim().ToUpperInvariant();
            if (typeUpper != "JUNIOR" && typeUpper != "EMPLOYER")
            {
                return BadRequest(new { Success = false, Message = "Type must be either 'Junior' or 'Employer'" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { Success = false, Message = "Name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { Success = false, Message = "Email is required" });
            }

            // Validate email format
            try
            {
                var emailAddress = new System.Net.Mail.MailAddress(request.Email);
            }
            catch (FormatException)
            {
                return BadRequest(new { Success = false, Message = "Invalid email format" });
            }

            _logger.LogInformation("📝 [EARLY-BIRD-REGISTER] Registering new early bird: Type={Type}, Name={Name}, Email={Email}", 
                request.Type, request.Name, request.Email);

            // Check if email already exists
            var existingRegistration = await _context.EarlyBirds
                .FirstOrDefaultAsync(e => e.Email.ToLower() == request.Email.Trim().ToLower());

            if (existingRegistration != null)
            {
                _logger.LogWarning("⚠️ [EARLY-BIRD-REGISTER] Email {Email} already registered", request.Email);
                return Conflict(new { Success = false, Message = "This email is already registered" });
            }

            // Create new early bird registration
            var earlyBird = new EarlyBirds
            {
                Type = typeUpper == "JUNIOR" ? "Junior" : "Employer", // Normalize to proper case
                Name = request.Name.Trim(),
                Email = request.Email.Trim().ToLower(),
                OrgName = string.IsNullOrWhiteSpace(request.OrgName) ? null : request.OrgName.Trim(),
                FutureRole = string.IsNullOrWhiteSpace(request.FutureRole) ? null : request.FutureRole.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.EarlyBirds.Add(earlyBird);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ [EARLY-BIRD-REGISTER] Successfully registered early bird with Id: {Id}, Type: {Type}, Email: {Email}", 
                earlyBird.Id, earlyBird.Type, earlyBird.Email);

            return Ok(new
            {
                Success = true,
                Id = earlyBird.Id,
                Type = earlyBird.Type,
                Name = earlyBird.Name,
                Email = earlyBird.Email,
                OrgName = earlyBird.OrgName,
                FutureRole = earlyBird.FutureRole,
                CreatedAt = earlyBird.CreatedAt,
                Message = "Registration successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [EARLY-BIRD-REGISTER] Error registering early bird: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = $"Error during registration: {ex.Message}" });
        }
    }

    /// <summary>
    /// Request model for early bird registration
    /// </summary>
    public class EarlyBirdRegistrationRequest
    {
        public string Type { get; set; } = string.Empty; // "Junior" or "Employer"
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? OrgName { get; set; }
        public string? FutureRole { get; set; }
    }
}
