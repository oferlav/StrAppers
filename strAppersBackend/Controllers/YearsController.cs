using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YearsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YearsController> _logger;

        public YearsController(ApplicationDbContext context, ILogger<YearsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active years ordered by sort order
        /// </summary>
        [HttpGet("use")]
        public async Task<ActionResult<IEnumerable<Year>>> GetYears()
        {
            try
            {
                var years = await _context.Years
                    .Where(y => y.IsActive)
                    .OrderBy(y => y.SortOrder)
                    .ToListAsync();

                return Ok(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving years");
                return StatusCode(500, "An error occurred while retrieving years");
            }
        }

        /// <summary>
        /// Get all years (including inactive) ordered by sort order
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<Year>>> GetAllYears()
        {
            try
            {
                var years = await _context.Years
                    .OrderBy(y => y.SortOrder)
                    .ToListAsync();

                return Ok(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all years");
                return StatusCode(500, "An error occurred while retrieving all years");
            }
        }

        /// <summary>
        /// Get a specific year by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Year>> GetYear(int id)
        {
            try
            {
                var year = await _context.Years.FindAsync(id);

                if (year == null)
                {
                    return NotFound($"Year with ID {id} not found");
                }

                return Ok(year);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving year with ID {YearId}", id);
                return StatusCode(500, "An error occurred while retrieving the year");
            }
        }
    }
}

