using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModuleTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ModuleTypesController> _logger;

        public ModuleTypesController(ApplicationDbContext context, ILogger<ModuleTypesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all module types
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ModuleType>>> GetModuleTypes()
        {
            try
            {
                _logger.LogInformation("Retrieving all module types");
                var moduleTypes = await _context.ModuleTypes.ToListAsync();
                _logger.LogInformation($"Retrieved {moduleTypes.Count} module types");
                return Ok(moduleTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving module types");
                return StatusCode(500, new { error = "An error occurred while retrieving module types", message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific module type by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ModuleType>> GetModuleType(int id)
        {
            try
            {
                _logger.LogInformation($"Retrieving module type with ID: {id}");
                var moduleType = await _context.ModuleTypes.FindAsync(id);

                if (moduleType == null)
                {
                    _logger.LogWarning($"Module type with ID {id} not found");
                    return NotFound(new { error = "Module type not found" });
                }

                _logger.LogInformation($"Retrieved module type: {moduleType.Name}");
                return Ok(moduleType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving module type with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while retrieving the module type", message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new module type
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ModuleType>> CreateModuleType([FromBody] CreateModuleTypeRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new module type: {request.Name}");

                var moduleType = new ModuleType
                {
                    Name = request.Name
                };

                _context.ModuleTypes.Add(moduleType);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created module type with ID: {moduleType.Id}");
                return CreatedAtAction(nameof(GetModuleType), new { id = moduleType.Id }, moduleType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating module type: {request.Name}");
                return StatusCode(500, new { error = "An error occurred while creating the module type", message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing module type
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateModuleType(int id, [FromBody] UpdateModuleTypeRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating module type with ID: {id}");

                var moduleType = await _context.ModuleTypes.FindAsync(id);
                if (moduleType == null)
                {
                    _logger.LogWarning($"Module type with ID {id} not found");
                    return NotFound(new { error = "Module type not found" });
                }

                moduleType.Name = request.Name;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated module type with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating module type with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while updating the module type", message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a module type
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteModuleType(int id)
        {
            try
            {
                _logger.LogInformation($"Deleting module type with ID: {id}");

                var moduleType = await _context.ModuleTypes.FindAsync(id);
                if (moduleType == null)
                {
                    _logger.LogWarning($"Module type with ID {id} not found");
                    return NotFound(new { error = "Module type not found" });
                }

                _context.ModuleTypes.Remove(moduleType);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deleted module type with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting module type with ID: {id}");
                return StatusCode(500, new { error = "An error occurred while deleting the module type", message = ex.Message });
            }
        }
    }
}






