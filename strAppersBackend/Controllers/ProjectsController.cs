using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(ApplicationDbContext context, ILogger<ProjectsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects()
    {
        try
        {
            var projects = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .Include(p => p.Students)
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
    public async Task<ActionResult<Project>> GetProject(int id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .Include(p => p.Students)
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
    /// Get all projects with 'New' status
    /// </summary>
    [HttpGet("new")]
    public async Task<ActionResult<IEnumerable<Project>>> GetNewProjects()
    {
        try
        {
            var newProjects = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .Include(p => p.Students)
                .Where(p => p.Status.Name == "New")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return Ok(newProjects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving new projects");
            return StatusCode(500, "An error occurred while retrieving new projects");
        }
    }

    /// <summary>
    /// Get projects by status
    /// </summary>
    [HttpGet("by-status/{statusName}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByStatus(string statusName)
    {
        try
        {
            var projects = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .Include(p => p.Students)
                .Where(p => p.Status.Name.ToLower() == statusName.ToLower())
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects with status {StatusName}", statusName);
            return StatusCode(500, "An error occurred while retrieving projects by status");
        }
    }

    /// <summary>
    /// Get projects by organization
    /// </summary>
    [HttpGet("by-organization/{organizationId}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByOrganization(int organizationId)
    {
        try
        {
            var projects = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .Include(p => p.Students)
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for organization {OrganizationId}", organizationId);
            return StatusCode(500, "An error occurred while retrieving projects for the organization");
        }
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [HttpPost("use/create")]
    public async Task<ActionResult<Project>> CreateProject(CreateProjectRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate organization exists (if provided)
            if (request.OrganizationId.HasValue)
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == request.OrganizationId.Value);

                if (organization == null)
                {
                    return BadRequest($"Organization with ID {request.OrganizationId} not found");
                }

                if (!organization.IsActive)
                {
                    return BadRequest($"Organization '{organization.Name}' is not active");
                }
            }

            // Validate status exists
            var status = await _context.ProjectStatuses
                .FirstOrDefaultAsync(ps => ps.Id == request.StatusId);

            if (status == null)
            {
                return BadRequest($"Project status with ID {request.StatusId} not found");
            }

            if (!status.IsActive)
            {
                return BadRequest($"Project status '{status.Name}' is not active");
            }

            // Create new project
            var project = new Project
            {
                Title = request.Title,
                Description = request.Description,
                StatusId = request.StatusId,
                Priority = request.Priority,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                DueDate = request.DueDate,
                OrganizationId = request.OrganizationId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Load the project with related data for response
            var createdProject = await _context.Projects
                .Include(p => p.Organization)
                .Include(p => p.Status)
                .FirstOrDefaultAsync(p => p.Id == project.Id);

            _logger.LogInformation("Project created successfully with ID {ProjectId} and title {Title}", 
                project.Id, project.Title);

            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, createdProject);
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
    /// Update an existing project
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, UpdateProjectRequest request)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }

            // Validate organization exists (if provided)
            if (request.OrganizationId.HasValue)
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == request.OrganizationId.Value);

                if (organization == null)
                {
                    return BadRequest($"Organization with ID {request.OrganizationId} not found");
                }

                if (!organization.IsActive)
                {
                    return BadRequest($"Organization '{organization.Name}' is not active");
                }
            }

            // Validate status exists (if provided)
            if (request.StatusId.HasValue)
            {
                var status = await _context.ProjectStatuses
                    .FirstOrDefaultAsync(ps => ps.Id == request.StatusId.Value);

                if (status == null)
                {
                    return BadRequest($"Project status with ID {request.StatusId} not found");
                }

                if (!status.IsActive)
                {
                    return BadRequest($"Project status '{status.Name}' is not active");
                }
            }

            // Update project properties
            if (!string.IsNullOrEmpty(request.Title))
                project.Title = request.Title;
            if (request.Description != null)
                project.Description = request.Description;
            if (request.StatusId.HasValue)
                project.StatusId = request.StatusId.Value;
            if (!string.IsNullOrEmpty(request.Priority))
                project.Priority = request.Priority;
            if (request.StartDate.HasValue)
                project.StartDate = request.StartDate;
            if (request.EndDate.HasValue)
                project.EndDate = request.EndDate;
            if (request.DueDate.HasValue)
                project.DueDate = request.DueDate;
            if (request.OrganizationId.HasValue)
                project.OrganizationId = request.OrganizationId;

            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project with ID {ProjectId} updated successfully", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while updating project with ID {ProjectId}", id);
            return StatusCode(500, "An error occurred while updating the project in the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating project with ID {ProjectId}", id);
            return StatusCode(500, "An unexpected error occurred while updating the project");
        }
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [HttpDelete("{id}")]
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
    /// Get all project statuses
    /// </summary>
    [HttpGet("statuses")]
    public async Task<ActionResult<IEnumerable<ProjectStatus>>> GetProjectStatuses()
    {
        try
        {
            var statuses = await _context.ProjectStatuses
                .Where(ps => ps.IsActive)
                .OrderBy(ps => ps.SortOrder)
                .ToListAsync();

            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project statuses");
            return StatusCode(500, "An error occurred while retrieving project statuses");
        }
    }

    /// <summary>
    /// Get a specific project status by ID (for frontend use)
    /// </summary>
    [HttpGet("use/status/{statusId}")]
    public async Task<ActionResult<ProjectStatus>> GetProjectStatusById(int statusId)
    {
        try
        {
            var status = await _context.ProjectStatuses
                .FirstOrDefaultAsync(ps => ps.Id == statusId && ps.IsActive);

            if (status == null)
            {
                return NotFound($"Project status with ID {statusId} not found");
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project status with ID {StatusId}", statusId);
            return StatusCode(500, "An error occurred while retrieving the project status");
        }
    }
}

