using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RolesController> _logger;

        public RolesController(ApplicationDbContext context, ILogger<RolesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active roles
        /// </summary>
        [HttpGet("use")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                return StatusCode(500, "An error occurred while retrieving roles");
            }
        }

        /// <summary>
        /// Get all roles (including inactive)
        /// </summary>
        [HttpGet("use/all")]
        public async Task<ActionResult<IEnumerable<Role>>> GetAllRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all roles");
                return StatusCode(500, "An error occurred while retrieving all roles");
            }
        }

        /// <summary>
        /// Get a specific role by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Role>> GetRole(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);

                if (role == null)
                {
                    return NotFound($"Role with ID {id} not found");
                }

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role with ID {RoleId}", id);
                return StatusCode(500, "An error occurred while retrieving the role");
            }
        }

        /// <summary>
        /// Get roles by category
        /// </summary>
        [HttpGet("by-category/{category}")]
        public async Task<ActionResult<IEnumerable<Role>>> GetRolesByCategory(string category)
        {
            try
            {
                var roles = await _context.Roles
                    .Where(r => r.Category == category && r.IsActive)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for category {Category}", category);
                return StatusCode(500, "An error occurred while retrieving roles for the category");
            }
        }

        /// <summary>
        /// Get students with a specific role
        /// </summary>
        [HttpGet("{id}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetRoleStudents(int id)
        {
            try
            {
                var role = await _context.Roles.FindAsync(id);
                if (role == null)
                {
                    return NotFound($"Role with ID {id} not found");
                }

                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
                    .Include(s => s.StudentRoles)
                    .Where(s => s.StudentRoles.Any(sr => sr.RoleId == id))
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for role {RoleId}", id);
                return StatusCode(500, "An error occurred while retrieving students for the role");
            }
        }
    }
}

