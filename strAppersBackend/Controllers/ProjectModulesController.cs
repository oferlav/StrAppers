using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        /// Get all modules for a specific project (only ModuleType = 2).
        /// </summary>
        [HttpGet("by-project/{projectId}")]
        public async Task<ActionResult<IEnumerable<ProjectModule>>> GetProjectModulesByProject(int projectId)
        {
            try
            {
                _logger.LogInformation($"Retrieving modules for project ID: {projectId}");
                var projectModules = await _context.ProjectModules
                    .Include(pm => pm.ModuleTypeNavigation)
                    .Where(pm => pm.ProjectId == projectId && pm.ModuleType == 2)
                    .OrderBy(pm => pm.Sequence ?? int.MaxValue)
                    .ThenBy(pm => pm.Id)
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

                int? nextSequence = request.Sequence;
                if (!nextSequence.HasValue)
                {
                    var maxSeq = await _context.ProjectModules
                        .Where(pm => pm.ProjectId == request.ProjectId)
                        .Select(pm => (int?)(pm.Sequence ?? 0))
                        .MaxAsync() ?? 0;
                    nextSequence = maxSeq + 1;
                }

                var projectModule = new ProjectModule
                {
                    ProjectId = request.ProjectId,
                    ModuleType = request.ModuleType,
                    Title = request.Title,
                    Description = request.Description,
                    Sequence = nextSequence
                };

                _context.ProjectModules.Add(projectModule);
                await _context.SaveChangesAsync();
                await SanitizeProjectTrelloBoardModuleIdsAsync(projectModule.ProjectId);

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

                if (request.Sequence.HasValue)
                    projectModule.Sequence = request.Sequence;

                await _context.SaveChangesAsync();
                await SanitizeProjectTrelloBoardModuleIdsAsync(projectModule.ProjectId);

                _logger.LogInformation($"Updated project module with ID: {id}");
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Update concurrency miss for project module {ModuleId}; row no longer exists.", id);
                return NotFound(new { error = "Project module not found" });
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
                await SanitizeProjectTrelloBoardModuleIdsAsync(projectModule.ProjectId);

                _logger.LogInformation($"Deleted project module with ID: {id}");
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Delete concurrency miss for project module {ModuleId}; treating as already deleted.", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting project module with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while deleting the project module", message = ex.Message });
            }
        }

        /// <summary>
        /// Removes stale ModuleId references from Projects.TrelloBoardJson when modules changed.
        /// </summary>
        private async Task SanitizeProjectTrelloBoardModuleIdsAsync(int? projectId)
        {
            if (!projectId.HasValue || projectId.Value <= 0)
            {
                return;
            }

            var pid = projectId.Value;
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == pid);
            if (project == null || string.IsNullOrWhiteSpace(project.TrelloBoardJson))
            {
                return;
            }

            var validModuleIds = await _context.ProjectModules
                .AsNoTracking()
                .Where(pm => pm.ProjectId == pid)
                .Select(pm => pm.Id)
                .ToListAsync();
            var validSet = validModuleIds.ToHashSet();

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(project.TrelloBoardJson);
            }
            catch (JsonException)
            {
                return;
            }

            if (root == null)
            {
                return;
            }

            var changed = false;

            void Walk(JsonNode? node)
            {
                if (node is JsonObject obj)
                {
                    foreach (var key in obj.Select(kv => kv.Key).ToList())
                    {
                        var child = obj[key];
                        if (string.Equals(key, "ModuleId", StringComparison.OrdinalIgnoreCase)
                            && child is JsonValue jv)
                        {
                            int? idVal = null;
                            if (jv.TryGetValue<int>(out var i))
                            {
                                idVal = i;
                            }
                            else if (jv.TryGetValue<string>(out var s) && int.TryParse(s?.Trim(), out var parsed))
                            {
                                idVal = parsed;
                            }

                            if (idVal.HasValue && !validSet.Contains(idVal.Value))
                            {
                                obj[key] = JsonValue.Create(string.Empty);
                                changed = true;
                                continue;
                            }
                        }

                        Walk(child);
                    }
                }
                else if (node is JsonArray arr)
                {
                    for (var i = 0; i < arr.Count; i++)
                    {
                        Walk(arr[i]);
                    }
                }
            }

            Walk(root);
            if (!changed)
            {
                return;
            }

            project.TrelloBoardJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}






