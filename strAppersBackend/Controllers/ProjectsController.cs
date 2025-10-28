using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProjectsController> _logger;
    private readonly IDesignDocumentService _designDocumentService;
    private readonly BusinessLogicConfig _businessLogicConfig;
    private readonly KickoffConfig _kickoffConfig;
    private readonly EngagementRulesConfig _engagementRulesConfig;

    public ProjectsController(ApplicationDbContext context, ILogger<ProjectsController> logger, IDesignDocumentService designDocumentService, IOptions<BusinessLogicConfig> businessLogicConfig, IOptions<KickoffConfig> kickoffConfig, IOptions<EngagementRulesConfig> engagementRulesConfig)
    {
        _context = context;
        _logger = logger;
        _designDocumentService = designDocumentService;
        _businessLogicConfig = businessLogicConfig.Value;
        _kickoffConfig = kickoffConfig.Value;
        _engagementRulesConfig = engagementRulesConfig.Value;
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    [HttpGet]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects()
    {
        try
        {
            var projects = await _context.Projects
                .Include(p => p.Organization)
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            return StatusCode(500, "An error occurred while retrieving projects");
        }
    }

    /// <summary>
    /// Get a specific project by ID
    /// </summary>
    [HttpGet("{id}")]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<Project>> GetProject(int id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the project");
        }
    }


    /// <summary>
    /// Get projects by organization
    /// </summary>
    [HttpGet("use/by-organization/{organizationId}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByOrganization(int organizationId)
    {
        try
        {
            _logger.LogInformation("Getting projects for organization {OrganizationId}", organizationId);
            
            // Test database connection first
            var totalProjects = await _context.Projects.CountAsync();
            _logger.LogInformation("Database connection successful. Total projects in database: {Count}", totalProjects);
            
            // First check if organization exists
            var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == organizationId);
            _logger.LogInformation("Organization {OrganizationId} exists: {Exists}", organizationId, organizationExists);
            
            if (!organizationExists)
            {
                _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
                return NotFound($"Organization with ID {organizationId} not found");
            }
            
            // Get all projects first to see what we have
            var allProjects = await _context.Projects.ToListAsync();
            _logger.LogInformation("All projects in database: {Projects}", 
                string.Join(", ", allProjects.Select(p => $"ID:{p.Id}, OrgId:{p.OrganizationId}")));
            
            var projects = await _context.Projects
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} projects for organization {OrganizationId}", projects.Count, organizationId);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for organization {OrganizationId}: {Message}", organizationId, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving projects for the organization: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [HttpPost("use/create/{organizationId}")]
    public async Task<ActionResult<Project>> CreateProject(int organizationId, CreateProjectRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate organization exists
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (organization == null)
            {
                return BadRequest($"Organization with ID {organizationId} not found");
            }

            if (!organization.IsActive)
            {
                return BadRequest($"Organization '{organization.Name}' is not active");
            }


            // Create new project
            var project = new Project
            {
                Title = request.Title,
                Description = request.Description,
                ExtendedDescription = request.ExtendedDescription,
                Priority = request.Priority,
                OrganizationId = organizationId,
                IsAvailable = request.IsAvailable,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Load the project with related data for response (excluding problematic fields)
            var createdProject = await _context.Projects
                .Where(p => p.Id == project.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Project created successfully with ID {ProjectId} and title {Title}", 
                project.Id, project.Title);

            // Generate system design for the project
            try
            {
                _logger.LogInformation("Generating system design for project {ProjectId}", project.Id);
                
                var systemDesignRequest = new SystemDesignRequest
                {
                    ProjectId = project.Id,
                    ExtendedDescription = project.ExtendedDescription ?? project.Description ?? "",
                    CreatedBy = null,
                    TeamRoles = new List<RoleInfo>() // Empty for now since students aren't allocated yet
                };

                var systemDesignResponse = await _designDocumentService.CreateSystemDesignAsync(systemDesignRequest);
                
                if (systemDesignResponse != null && systemDesignResponse.Success)
                {
                    // Update the project with the generated system design
                    project.SystemDesign = systemDesignResponse.DesignDocument;
                    project.SystemDesignDoc = systemDesignResponse.DesignDocumentPdf;
                    project.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("System design generated and saved for project {ProjectId}", project.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to generate system design for project {ProjectId}: {Message}", 
                        project.Id, systemDesignResponse?.Message ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system design for project {ProjectId}: {Message}", 
                    project.Id, ex.Message);
                // Don't fail the project creation if system design generation fails
            }

            return Ok(createdProject);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating project");
            return StatusCode(500, "An error occurred while saving the project to the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating project");
            return StatusCode(500, "An unexpected error occurred while creating the project");
        }
    }

    /// <summary>
    /// Create a project without generating system design
    /// </summary>
    [HttpPost("use/create")]
    public async Task<ActionResult<Project>> CreateProjectSimple(CreateProjectSimpleRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate organization exists
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == request.OrganizationId);

            if (organization == null)
            {
                return BadRequest($"Organization with ID {request.OrganizationId} not found");
            }

            if (!organization.IsActive)
            {
                return BadRequest($"Organization '{organization.Name}' is not active");
            }

            // Create new project with default values
            var project = new Project
            {
                Title = request.Title,
                Description = request.Description,
                ExtendedDescription = request.ExtendedDescription,
                Priority = "high", // Default priority
                OrganizationId = request.OrganizationId,
                IsAvailable = true, // Default isAvailable
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Load the project with related data for response (excluding problematic fields)
            var createdProject = await _context.Projects
                .Where(p => p.Id == project.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Project created successfully (simple) with ID {ProjectId} and title {Title}", 
                project.Id, project.Title);

            return Ok(createdProject);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating project (simple)");
            return StatusCode(500, "An error occurred while saving the project to the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating project (simple)");
            return StatusCode(500, "An unexpected error occurred while creating the project");
        }
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [HttpDelete("{id}")]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Project with ID {ProjectId} deleted successfully", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while deleting project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while deleting the project from the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting project with ID {ProjectId}", id);
            return StatusCode(500, "An unexpected error occurred while deleting the project");
        }
    }

    /// <summary>
    /// Get all available projects
    /// </summary>
    [HttpGet("use/available")]
    public async Task<ActionResult<IEnumerable<Project>>> GetAvailableProjects()
    {
        try
        {
            var projects = await _context.Projects
                .Where(p => p.IsAvailable)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    Kickoff = p.Kickoff,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available projects");
            return StatusCode(500, "An error occurred while retrieving available projects");
        }
    }

    /// <summary>
    /// Get all projects for an organization by email address
    /// </summary>
    [HttpGet("use/by-email/{email}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByEmail(string email)
    {
        try
        {
            _logger.LogInformation("Getting projects for organization with email {Email}", email);

            // First, find the organization by email
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.ContactEmail == email);

            if (organization == null)
            {
                _logger.LogWarning("Organization not found for email {Email}", email);
                return NotFound($"Organization with email {email} not found.");
            }

            _logger.LogInformation("Found organization {OrganizationId} for email {Email}", organization.Id, email);

            // Get all projects for this organization
            var projects = await _context.Projects
                .Where(p => p.OrganizationId == organization.Id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            _logger.LogInformation("Found {ProjectCount} projects for organization {OrganizationId}", projects.Count, organization.Id);

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for email {Email}", email);
            return StatusCode(500, "An error occurred while retrieving projects for the organization");
        }
    }

    /// <summary>
    /// Get Project Engagement Rules - informative rules for project participation
    /// </summary>
    [HttpGet("use/engagement-rules")]
    public ActionResult<object> GetProjectEngagementRules()
    {
        try
        {
            _logger.LogInformation("Retrieving Project Engagement Rules");

            // Build sections from configuration
            var sections = new List<object>();

            // Project Structure section with dynamic values
            if (_engagementRulesConfig.Sections.ContainsKey("ProjectStructure"))
            {
                var projectStructure = _engagementRulesConfig.Sections["ProjectStructure"];
                var projectStructureRules = new List<string>(projectStructure.Rules);
                projectStructureRules.Insert(0, $"Project Duration: {_businessLogicConfig.ProjectLengthInWeeks} weeks");
                projectStructureRules.Insert(1, $"Sprint Length: {_businessLogicConfig.SprintLengthInWeeks} week per sprint");
                projectStructureRules.Insert(2, $"Total Sprints: {_businessLogicConfig.ProjectLengthInWeeks / _businessLogicConfig.SprintLengthInWeeks} sprints per project");

                sections.Add(new
                {
                    title = projectStructure.Title,
                    description = projectStructure.Description,
                    rules = projectStructureRules
                });
            }

            // Kickoff Requirements section with dynamic values
            if (_engagementRulesConfig.Sections.ContainsKey("KickoffRequirements"))
            {
                var kickoffRequirements = _engagementRulesConfig.Sections["KickoffRequirements"];
                var kickoffRules = new List<string>(kickoffRequirements.Rules);
                kickoffRules.Insert(0, $"Minimum Juniors: {_kickoffConfig.MinimumStudents} juniors must be allocated");
                kickoffRules.Insert(1, _kickoffConfig.RequireAdmin ? "Admin Required: At least one junior must be an admin" : "Admin Required: Not required");
                kickoffRules.Insert(2, _kickoffConfig.RequireUIUXDesigner ? "UI/UX Designer Required: At least one junior must have UI/UX Designer role" : "UI/UX Designer Required: Not required");
                kickoffRules.Insert(3, _kickoffConfig.RequireDeveloperRule ? "Developer Rule: At least 1 Full-stack Developer OR at least one Backend and one Frontend developers" : "Developer Rule: Not required");

                sections.Add(new
                {
                    title = kickoffRequirements.Title,
                    description = kickoffRequirements.Description,
                    rules = kickoffRules
                });
            }

            // Add other sections from configuration
            foreach (var sectionKey in new[] { "RoleTypes", "ProjectStatusFlow", "TechnicalRequirements", "JuniorAllocationProcess" })
            {
                if (_engagementRulesConfig.Sections.ContainsKey(sectionKey))
                {
                    var section = _engagementRulesConfig.Sections[sectionKey];
                    sections.Add(new
                    {
                        title = section.Title,
                        description = section.Description,
                        rules = section.Rules
                    });
                }
            }

            var rules = new
            {
                title = _engagementRulesConfig.Title,
                description = _engagementRulesConfig.Description,
                lastUpdated = DateTime.UtcNow,
                sections = sections,
                configuration = new
                {
                    businessLogic = new
                    {
                        projectLengthInWeeks = _businessLogicConfig.ProjectLengthInWeeks,
                        sprintLengthInWeeks = _businessLogicConfig.SprintLengthInWeeks
                    },
                    kickoff = new
                    {
                        minimumStudents = _kickoffConfig.MinimumStudents,
                        requireAdmin = _kickoffConfig.RequireAdmin,
                        requireUIUXDesigner = _kickoffConfig.RequireUIUXDesigner,
                        requireDeveloperRule = _kickoffConfig.RequireDeveloperRule
                    }
                },
                summary = _engagementRulesConfig.Summary
            };

            _logger.LogInformation("Project Engagement Rules retrieved successfully");
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Project Engagement Rules");
            return StatusCode(500, new
            {
                error = "An error occurred while retrieving the Project Engagement Rules",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get a single project by ID
    /// </summary>
    [HttpGet("use/{id}")]
    public async Task<ActionResult<Project>> GetProjectById(int id)
    {
        try
        {
            _logger.LogInformation("Getting project with ID {ProjectId}", id);

            var project = await _context.Projects
                .Where(p => p.Id == id)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    Kickoff = p.Kickoff,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                _logger.LogWarning("Project with ID {ProjectId} not found", id);
                return NotFound($"Project with ID {id} not found.");
            }

            _logger.LogInformation("Found project {ProjectTitle} with ID {ProjectId}", project.Title, project.Id);
            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the project");
        }
    }

    /// <summary>
    /// Suspend a project (set IsAvailable to false)
    /// </summary>
    [HttpPost("use/suspend/{id}")]
    public async Task<ActionResult> SuspendProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            project.IsAvailable = false;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} suspended successfully", id);
            return Ok(new { Success = true, Message = "Project suspended successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending project {ProjectId}", id);
            return StatusCode(500, "An error occurred while suspending the project");
        }
    }

    /// <summary>
    /// Activate a project (set IsAvailable to true)
    /// </summary>
    [HttpPost("use/activate/{id}")]
    public async Task<ActionResult> ActivateProject(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            project.IsAvailable = true;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} activated successfully", id);
            return Ok(new { Success = true, Message = "Project activated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while activating the project");
        }
    }

    /// <summary>
    /// Update a project
    /// </summary>
    [HttpPost("use/update/{id}")]
    public async Task<ActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(request.Title))
                project.Title = request.Title;

            if (request.Description != null)
                project.Description = request.Description;

            if (request.ExtendedDescription != null)
                project.ExtendedDescription = request.ExtendedDescription;

            if (!string.IsNullOrEmpty(request.Priority))
                project.Priority = request.Priority;

            if (request.IsAvailable.HasValue)
                project.IsAvailable = request.IsAvailable.Value;

            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectId} updated successfully", id);

            // Generate system design for the updated project
            try
            {
                _logger.LogInformation("Generating system design for updated project {ProjectId}", id);
                
                var systemDesignRequest = new SystemDesignRequest
                {
                    ProjectId = id,
                    ExtendedDescription = project.ExtendedDescription ?? project.Description ?? "",
                    CreatedBy = null,
                    TeamRoles = new List<RoleInfo>() // Empty for now since students aren't allocated yet
                };

                var systemDesignResponse = await _designDocumentService.CreateSystemDesignAsync(systemDesignRequest);
                
                if (systemDesignResponse != null && systemDesignResponse.Success)
                {
                    // Update the project with the generated system design
                    project.SystemDesign = systemDesignResponse.DesignDocument;
                    project.SystemDesignDoc = systemDesignResponse.DesignDocumentPdf;
                    project.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("System design generated and saved for updated project {ProjectId}", id);
                }
                else
                {
                    _logger.LogWarning("Failed to generate system design for updated project {ProjectId}: {Message}", 
                        id, systemDesignResponse?.Message ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system design for updated project {ProjectId}: {Message}", 
                    id, ex.Message);
                // Don't fail the project update if system design generation fails
            }

            return Ok(new { Success = true, Message = "Project updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500, "An error occurred while updating the project");
        }
    }

    /// <summary>
    /// Get system design JSON content for a specific project
    /// </summary>
    [HttpGet("use/DesignContent/{id}")]
    public async Task<ActionResult<string>> GetProjectDesignContent(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                return NotFound($"No system design content found for project {id}.");
            }

            _logger.LogInformation("Retrieved system design content for project {ProjectId}", id);
            return Ok(project.SystemDesign);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system design content for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the system design content");
        }
    }

    /// <summary>
    /// Get system design document (PDF) for a specific project
    /// </summary>
    [HttpGet("use/DesignDocument/{id}")]
    public async Task<ActionResult> GetProjectDesignDocument(int id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found.");
            }

            if (project.SystemDesignDoc == null || project.SystemDesignDoc.Length == 0)
            {
                return NotFound($"No system design document found for project {id}.");
            }

            _logger.LogInformation("Retrieved system design document for project {ProjectId}", id);
            
            // Return the PDF document
            return File(project.SystemDesignDoc, "application/pdf", $"project_{id}_system_design.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system design document for project {ProjectId}", id);
            return StatusCode(500, "An error occurred while retrieving the system design document");
        }
    }

}

