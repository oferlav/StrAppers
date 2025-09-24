using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

// DISABLED - Functionality moved to BoardsController
[ApiController]
[Route("api/[controller]")]
public class SprintPlanningController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;
    private readonly BusinessLogicConfig _businessLogicConfig;
    private readonly ILogger<SprintPlanningController> _logger;

    public SprintPlanningController(
        IAIService aiService,
        ApplicationDbContext context,
        IOptions<BusinessLogicConfig> businessLogicConfig,
        ILogger<SprintPlanningController> logger)
    {
        _aiService = aiService;
        _context = context;
        _businessLogicConfig = businessLogicConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generate sprint plan for a project based on its system design (for frontend use)
    /// </summary>
    [HttpPost("use/generate-project-sprints")]
    [Obsolete("This method is disabled. Use BoardsController instead.")]
    public async Task<ActionResult<SprintPlanningResponse>> GenerateProjectSprints(SprintPlanningRequest request)
    {
        try
        {
            _logger.LogInformation("Generating sprint plan for Project {ProjectId}", request.ProjectId);

            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate project exists and has system design
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                return NotFound($"Project with ID {request.ProjectId} not found");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                return BadRequest(new SprintPlanningResponse
                {
                    Success = false,
                    Message = "Project must have a system design document before generating sprint plans"
                });
            }

            // Build team roles from allocated students
            // var teamRoles = project.Students // DISABLED - Project.Students removed
            var teamRoles = new List<RoleInfo>(); // Empty for now
            // .SelectMany(s => s.StudentRoles)
            // .GroupBy(sr => sr.RoleId)
            // .Select(g => new RoleInfo
            // {
            //     RoleId = g.Key,
            //     RoleName = g.First().Role.Name,
            //     StudentCount = g.Count()
            // })
            // .ToList();

            if (!teamRoles.Any())
            {
                return BadRequest(new SprintPlanningResponse
                {
                    Success = false,
                    Message = "Project must have allocated students with roles before generating sprint plans"
                });
            }

            // Update request with actual team roles and project length
            request.TeamRoles = teamRoles;
            request.ProjectLengthWeeks = _businessLogicConfig.ProjectLengthInWeeks;

            // Generate sprint plan
            var result = await _aiService.GenerateSprintPlanAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("Sprint plan generated successfully for Project {ProjectId}", request.ProjectId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sprint plan for Project {ProjectId}", request.ProjectId);
            return StatusCode(500, new SprintPlanningResponse
            {
                Success = false,
                Message = "An unexpected error occurred while generating the sprint plan"
            });
        }
    }

    /// <summary>
    /// Get project team composition for sprint planning (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/team-composition")]
    [Obsolete("This method is disabled. Use BoardsController instead.")]
    public async Task<ActionResult<object>> GetProjectTeamComposition(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    ProjectId = p.Id,
                    ProjectTitle = p.Title,
                    HasSystemDesign = !string.IsNullOrEmpty(p.SystemDesign),
                    TeamRoles = new List<RoleInfo>() // DISABLED - Project.Students removed
                    // .SelectMany(s => s.StudentRoles)
                    // .GroupBy(sr => sr.RoleId)
                    // .Select(g => new
                    // {
                    //     RoleId = g.Key,
                    //     RoleName = g.First().Role.Name,
                    //     StudentCount = g.Count(),
                    //     Students = g.Select(sr => new
                    //     {
                    //         sr.Student.Id,
                    //         sr.Student.FirstName,
                    //         sr.Student.LastName,
                    //         sr.Student.Email
                    //     }).ToList()
                    // })
                        .ToList(),
                    TotalStudents = 0 // DISABLED - Project.Students removed
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
            _logger.LogError(ex, "Error retrieving team composition for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while retrieving the team composition");
        }
    }

    /// <summary>
    /// Validate project readiness for sprint planning (for frontend use)
    /// </summary>
    [HttpGet("use/project/{projectId}/sprint-readiness")]
    [Obsolete("This method is disabled. Use BoardsController instead.")]
    public async Task<ActionResult<object>> ValidateSprintReadiness(int projectId)
    {
        try
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found");
            }

            var hasSystemDesign = !string.IsNullOrEmpty(project.SystemDesign);
            var hasAllocatedStudents = false; // DISABLED - Project.Students removed
            var hasStudentRoles = false; // DISABLED - Project.Students removed

            var readiness = new
            {
                ProjectId = projectId,
                ProjectTitle = project.Title,
                IsReadyForSprintPlanning = hasSystemDesign && hasAllocatedStudents && hasStudentRoles,
                Requirements = new
                {
                    HasSystemDesign = hasSystemDesign,
                    HasAllocatedStudents = hasAllocatedStudents,
                    HasStudentRoles = hasStudentRoles,
                    StudentCount = 0, // DISABLED - Project.Students removed
                    RoleCount = 0 // DISABLED - Project.Students removed
                },
                MissingRequirements = new List<string>
                {
                    !hasSystemDesign ? "System design document" : null,
                    !hasAllocatedStudents ? "Allocated students" : null,
                    !hasStudentRoles ? "Student roles assigned" : null
                }.Where(x => x != null).ToList()
            };

            return Ok(readiness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating sprint readiness for Project {ProjectId}", projectId);
            return StatusCode(500, "An error occurred while validating sprint readiness");
        }
    }
}
