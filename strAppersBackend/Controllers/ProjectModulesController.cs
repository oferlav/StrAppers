using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectModulesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectModulesController> _logger;

        public ProjectModulesController(ApplicationDbContext context, ILogger<ProjectModulesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all project modules
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectModule>>> GetProjectModules()
        {
            try
            {
                _logger.LogInformation("Retrieving all project modules");
                var projectModules = await _context.ProjectModules
                    .Include(pm => pm.Project)
                    .Include(pm => pm.ModuleTypeNavigation)
                    .ToListAsync();
                _logger.LogInformation($"Retrieved {projectModules.Count} project modules");
                return Ok(projectModules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project modules");
                return StatusCode(500, new { error = "An error occurred while retrieving project modules", message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific project module by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectModule>> GetProjectModule(int id)
        {
            try
            {
                _logger.LogInformation($"Retrieving project module with ID: {id}");
                var projectModule = await _context.ProjectModules
                    .Include(pm => pm.Project)
                    .Include(pm => pm.ModuleTypeNavigation)
                    .FirstOrDefaultAsync(pm => pm.Id == id);

                if (projectModule == null)
                {
                    _logger.LogWarning($"Project module with ID {id} not found");
                    return NotFound(new { error = "Project module not found" });
                }

                _logger.LogInformation($"Retrieved project module: {projectModule.Title}");
                return Ok(projectModule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving project module with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while retrieving the project module", message = ex.Message });
            }
        }

        /// <summary>
        /// Get all modules for a specific project
        /// </summary>
        [HttpGet("by-project/{projectId}")]
        public async Task<ActionResult<IEnumerable<ProjectModule>>> GetProjectModulesByProject(int projectId)
        {
            try
            {
                _logger.LogInformation($"Retrieving modules for project ID: {projectId}");
                var projectModules = await _context.ProjectModules
                    .Include(pm => pm.ModuleTypeNavigation)
                    .Where(pm => pm.ProjectId == projectId)
                    .ToListAsync();
                _logger.LogInformation($"Retrieved {projectModules.Count} modules for project {projectId}");
                return Ok(projectModules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving modules for project ID: {projectId}");
                return StatusCode(500, new { error = "An error occurred while retrieving project modules", message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new project module
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProjectModule>> CreateProjectModule([FromBody] CreateProjectModuleRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new project module: {request.Title} for project {request.ProjectId}");

                // Verify project exists
                var project = await _context.Projects.FindAsync(request.ProjectId);
                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {request.ProjectId} not found");
                    return BadRequest(new { error = "Project not found" });
                }

                // Verify module type exists
                var moduleType = await _context.ModuleTypes.FindAsync(request.ModuleType);
                if (moduleType == null)
                {
                    _logger.LogWarning($"Module type with ID {request.ModuleType} not found");
                    return BadRequest(new { error = "Module type not found" });
                }

                var projectModule = new ProjectModule
                {
                    ProjectId = request.ProjectId,
                    ModuleType = request.ModuleType,
                    Title = request.Title,
                    Description = request.Description
                };

                _context.ProjectModules.Add(projectModule);
                await _context.SaveChangesAsync();

                // Load the created module with navigation properties
                var createdModule = await _context.ProjectModules
                    .Include(pm => pm.Project)
                    .Include(pm => pm.ModuleTypeNavigation)
                    .FirstOrDefaultAsync(pm => pm.Id == projectModule.Id);

                _logger.LogInformation($"Created project module with ID: {projectModule.Id}");
                return CreatedAtAction(nameof(GetProjectModule), new { id = projectModule.Id }, createdModule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating project module: {request.Title}");
                return StatusCode(500, new { error = "An error occurred while creating the project module", message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing project module
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProjectModule(int id, [FromBody] UpdateProjectModuleRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating project module with ID: {id}");

                var projectModule = await _context.ProjectModules.FindAsync(id);
                if (projectModule == null)
                {
                    _logger.LogWarning($"Project module with ID {id} not found");
                    return NotFound(new { error = "Project module not found" });
                }

                // Verify project exists if provided
                if (request.ProjectId.HasValue)
                {
                    var project = await _context.Projects.FindAsync(request.ProjectId.Value);
                    if (project == null)
                    {
                        _logger.LogWarning($"Project with ID {request.ProjectId} not found");
                        return BadRequest(new { error = "Project not found" });
                    }
                    projectModule.ProjectId = request.ProjectId.Value;
                }

                // Verify module type exists if provided
                if (request.ModuleType.HasValue)
                {
                    var moduleType = await _context.ModuleTypes.FindAsync(request.ModuleType.Value);
                    if (moduleType == null)
                    {
                        _logger.LogWarning($"Module type with ID {request.ModuleType} not found");
                        return BadRequest(new { error = "Module type not found" });
                    }
                    projectModule.ModuleType = request.ModuleType.Value;
                }

                if (!string.IsNullOrEmpty(request.Title))
                    projectModule.Title = request.Title;

                if (request.Description != null)
                    projectModule.Description = request.Description;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated project module with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating project module with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while updating the project module", message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a project module
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProjectModule(int id)
        {
            try
            {
                _logger.LogInformation($"Deleting project module with ID: {id}");

                var projectModule = await _context.ProjectModules.FindAsync(id);
                if (projectModule == null)
                {
                    _logger.LogWarning($"Project module with ID {id} not found");
                    return NotFound(new { error = "Project module not found" });
                }

                _context.ProjectModules.Remove(projectModule);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deleted project module with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting project module with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while deleting the project module", message = ex.Message });
            }
        }
    }
}






