using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MajorsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MajorsController> _logger;

        public MajorsController(ApplicationDbContext context, ILogger<MajorsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active majors
        /// </summary>
        [HttpGet("use")]
        public async Task<ActionResult<IEnumerable<Major>>> GetMajors()
        {
            try
            {
                var majors = await _context.Majors
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return Ok(majors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving majors");
                return StatusCode(500, "An error occurred while retrieving majors");
            }
        }

        /// <summary>
        /// Get all majors (including inactive)
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<Major>>> GetAllMajors()
        {
            try
            {
                var majors = await _context.Majors
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return Ok(majors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all majors");
                return StatusCode(500, "An error occurred while retrieving all majors");
            }
        }

        /// <summary>
        /// Get a specific major by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Major>> GetMajor(int id)
        {
            try
            {
                var major = await _context.Majors.FindAsync(id);

                if (major == null)
                {
                    return NotFound($"Major with ID {id} not found");
                }

                return Ok(major);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving major with ID {MajorId}", id);
                return StatusCode(500, "An error occurred while retrieving the major");
            }
        }

        /// <summary>
        /// Get majors by department
        /// </summary>
        [HttpGet("by-department/{department}")]
        public async Task<ActionResult<IEnumerable<Major>>> GetMajorsByDepartment(string department)
        {
            try
            {
                var majors = await _context.Majors
                    .Where(m => m.Department == department && m.IsActive)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

                return Ok(majors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving majors for department {Department}", department);
                return StatusCode(500, "An error occurred while retrieving majors for the department");
            }
        }
    }
}

