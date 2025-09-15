using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemDesignController : ControllerBase
{
    private readonly IDesignDocumentService _designDocumentService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SystemDesignController> _logger;

    public SystemDesignController(
        IDesignDocumentService designDocumentService,
        ApplicationDbContext context,
        ILogger<SystemDesignController> logger)
    {
        _designDocumentService = designDocumentService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Generate a system design document for a project (for frontend use)
    /// </summary>
    [HttpPost("use/generate-design-document")]
    public async Task<ActionResult<SystemDesignResponse>> GenerateDesignDocument([FromForm] int projectId, [FromForm] string extendedDescription, [FromForm] string? createdBy = null)
    {
        try
        {
            _logger.LogInformation("Generating design document for Project {ProjectId}", projectId);

            // Clean the extended description
            var cleanedDescription = extendedDescription
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Trim();

            // Create the request object
            var request = new SystemDesignRequest
            {
                ProjectId = projectId,
                ExtendedDescription = cleanedDescription,
                CreatedBy = createdBy,
                TeamRoles = new List<RoleInfo>() // Will be populated from database
            };

            _logger.LogInformation("Processing request for Project {ProjectId}", request.ProjectId);

            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate project exists and get team roles
            var project = await _context.Projects
                .Include(p => p.Students)
                .ThenInclude(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found");
            }

            // Build team roles from allocated students
            var teamRoles = project.Students
                .SelectMany(s => s.StudentRoles)
                .GroupBy(sr => sr.RoleId)
                .Select(g => new RoleInfo
                {
                    RoleId = g.Key,
                    RoleName = g.First().Role.Name,
                    StudentCount = g.Count()
                })
                .ToList();

            // Update request with actual team roles
            request.TeamRoles = teamRoles;

            // Generate system design
            var result = await _designDocumentService.CreateSystemDesignAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("Design document generated successfully for Project {ProjectId}, Version {VersionId}", 
                request.ProjectId, result.DesignVersionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating design document");
            return StatusCode(500, new SystemDesignResponse
            {
                Success = false,
                Message = "An unexpected error occurred while generating the design document"
            });
        }
    }

    /// <summary>
    /// Get the latest design version for a project (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/latest-design")]
    public async Task<ActionResult<DesignVersion>> GetLatestDesignVersion(int projectId)
    {
        try
        {
            var designVersion = await _designDocumentService.GetLatestDesignVersionAsync(projectId);

            if (designVersion == null)
            {
                return NotFound($"No design version found for project {projectId}");
            }

            return Ok(designVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest design version for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the design version");
        }
    }

    /// <summary>
    /// Get all design versions for a project (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/design-versions")]
    public async Task<ActionResult<List<DesignVersion>>> GetDesignVersions(int projectId)
    {
        try
        {
            var designVersions = await _designDocumentService.GetDesignVersionsAsync(projectId);
            return Ok(designVersions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design versions for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the design versions");
        }
    }

    /// <summary>
    /// Get a specific design version by ID (for frontend use)
    /// </summary>
    [HttpGet("use/design-version/{designVersionId}")]
    public async Task<ActionResult<DesignVersion>> GetDesignVersionById(int designVersionId)
    {
        try
        {
            var designVersion = await _designDocumentService.GetDesignVersionByIdAsync(designVersionId);

            if (designVersion == null)
            {
                return NotFound($"Design version with ID {designVersionId} not found");
            }

            return Ok(designVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving design version {DesignVersionId}", designVersionId);
            return StatusCode(500, "An error occurred while retrieving the design version");
        }
    }

    /// <summary>
    /// Get project's current system design (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/current-design")]
    public async Task<ActionResult<object>> GetCurrentSystemDesign(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.SystemDesign,
                    HasSystemDesign = !string.IsNullOrEmpty(p.SystemDesign),
                    HasSystemDesignPdf = p.SystemDesignDoc != null
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current system design for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the system design");
        }
    }

    /// <summary>
    /// Generate a system design document for a project (alternative endpoint that accepts form data)
    /// </summary>
    [HttpPost("use/generate-design-document-form")]
    public async Task<ActionResult<SystemDesignResponse>> GenerateDesignDocumentForm([FromForm] int projectId, [FromForm] string extendedDescription, [FromForm] string? createdBy = null)
    {
        try
        {
            _logger.LogInformation("Generating design document with form data for Project {ProjectId}", projectId);

            // Clean the extended description manually
            var cleanedDescription = extendedDescription
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Trim();

            // Create the request object
            var request = new SystemDesignRequest
            {
                ProjectId = projectId,
                ExtendedDescription = cleanedDescription,
                CreatedBy = createdBy,
                TeamRoles = new List<RoleInfo>() // Will be populated from database
            };

            _logger.LogInformation("Processing request for Project {ProjectId}", request.ProjectId);

            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate project exists and get team roles
            var project = await _context.Projects
                .Include(p => p.Students)
                .ThenInclude(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found");
            }

            // Build team roles from allocated students
            var teamRoles = project.Students
                .SelectMany(s => s.StudentRoles)
                .GroupBy(sr => sr.RoleId)
                .Select(g => new RoleInfo
                {
                    RoleId = g.Key,
                    RoleName = g.First().Role.Name,
                    StudentCount = g.Count()
                })
                .ToList();

            // Update request with actual team roles
            request.TeamRoles = teamRoles;

            // Generate system design
            var result = await _designDocumentService.CreateSystemDesignAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("Design document generated successfully for Project {ProjectId}, Version {VersionId}", 
                request.ProjectId, result.DesignVersionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating design document with raw JSON handling");
            return StatusCode(500, new SystemDesignResponse
            {
                Success = false,
                Message = "An unexpected error occurred while generating the design document"
            });
        }
    }
}
