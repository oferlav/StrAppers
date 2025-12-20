using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/Affinda")]
public class AffindaController : ControllerBase
{
    private readonly ILogger<AffindaController> _logger;
    private readonly IAffindaService _affindaService;

    public AffindaController(
        ILogger<AffindaController> logger,
        IAffindaService affindaService)
    {
        _logger = logger;
        _affindaService = affindaService;
    }

    /// <summary>
    /// Get list of available workspaces from Affinda API
    /// </summary>
    /// <returns>List of workspaces</returns>
    [HttpGet("workspaces")]
    public async Task<ActionResult<List<AffindaWorkspace>>> GetWorkspaces()
    {
        try
        {
            _logger.LogInformation("GetWorkspaces endpoint called");
            var workspaces = await _affindaService.GetWorkspacesAsync();
            return Ok(workspaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workspaces: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to fetch workspaces: {ex.Message}" });
        }
    }

    /// <summary>
    /// Parse a resume/CV file using Affinda Resume Parser API
    /// </summary>
    /// <param name="request">Request containing the base64-encoded file</param>
    /// <returns>Parsed resume data with candidate metadata, professional data, and raw Affinda response</returns>
    [HttpPost("use/parse")]
    public async Task<ActionResult<ParseResumeResponse>> ParseResume([FromBody] ParseResumeRequest request)
    {
        try
        {
            _logger.LogInformation("ParseResume endpoint called");

            if (request == null)
            {
                _logger.LogWarning("ParseResume called with null request");
                return BadRequest(new { error = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.FileBase64))
            {
                _logger.LogWarning("ParseResume called with empty FileBase64");
                return BadRequest(new { error = "FileBase64 is required and cannot be empty" });
            }

            _logger.LogInformation("Parsing resume via Affinda API. FileBase64 length: {Length}", request.FileBase64.Length);

            var result = await _affindaService.ParseResumeAsync(request.FileBase64);

            _logger.LogInformation("Resume parsed successfully. Candidate: {FirstName} {LastName}, Skills: {SkillCount}", 
                result.CandidateMetaData?.FirstName, 
                result.CandidateMetaData?.LastName,
                result.ProfessionalData?.Skills?.Count ?? 0);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in ParseResume: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Affinda API: {Message}", ex.Message);
            return StatusCode(502, new { error = $"Affinda API error: {ex.Message}" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation in ParseResume: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ParseResume: {Message}", ex.Message);
            return StatusCode(500, new { error = "An error occurred while parsing the resume. Please try again." });
        }
    }
}

