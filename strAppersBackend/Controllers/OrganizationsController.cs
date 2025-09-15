using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrganizationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrganizationsController> _logger;

        public OrganizationsController(ApplicationDbContext context, ILogger<OrganizationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active organizations
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Organization>>> GetOrganizations()
        {
            try
            {
                var organizations = await _context.Organizations
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync();

                return Ok(organizations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organizations");
                return StatusCode(500, "An error occurred while retrieving organizations");
            }
        }

        /// <summary>
        /// Get all organizations (including inactive)
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<Organization>>> GetAllOrganizations()
        {
            try
            {
                var organizations = await _context.Organizations
                    .OrderBy(o => o.Name)
                    .ToListAsync();

                return Ok(organizations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all organizations");
                return StatusCode(500, "An error occurred while retrieving all organizations");
            }
        }

        /// <summary>
        /// Get a specific organization by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Organization>> GetOrganization(int id)
        {
            try
            {
                var organization = await _context.Organizations
                    .Include(o => o.Students)
                    .Include(o => o.Projects)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found");
                }

                return Ok(organization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organization with ID {OrganizationId}", id);
                return StatusCode(500, "An error occurred while retrieving the organization");
            }
        }

        /// <summary>
        /// Create a new organization (for frontend use)
        /// </summary>
        [HttpPost("use/create")]
        public async Task<ActionResult<Organization>> CreateOrganization(CreateOrganizationRequest request)
        {
            try
            {
                // Validate the request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if organization name already exists
                var existingOrganization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Name.ToLower() == request.Name.ToLower());

                if (existingOrganization != null)
                {
                    return Conflict($"An organization with name '{request.Name}' already exists");
                }

                // Create new organization
                var organization = new Organization
                {
                    Name = request.Name,
                    Description = request.Description,
                    Website = request.Website,
                    ContactEmail = request.ContactEmail,
                    Phone = request.Phone,
                    Address = request.Address,
                    Type = request.Type,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                // Load the organization with related data for response
                var createdOrganization = await _context.Organizations
                    .Include(o => o.Students)
                    .Include(o => o.Projects)
                    .FirstOrDefaultAsync(o => o.Id == organization.Id);

                _logger.LogInformation("Organization created successfully with ID {OrganizationId} and name {Name}", 
                    organization.Id, organization.Name);

                return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, createdOrganization);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating organization");
                return StatusCode(500, "An error occurred while saving the organization to the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating organization");
                return StatusCode(500, "An unexpected error occurred while creating the organization");
            }
        }

        /// <summary>
        /// Get organizations by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        public async Task<ActionResult<IEnumerable<Organization>>> GetOrganizationsByType(string type)
        {
            try
            {
                var organizations = await _context.Organizations
                    .Where(o => o.Type == type && o.IsActive)
                    .OrderBy(o => o.Name)
                    .ToListAsync();

                return Ok(organizations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organizations for type {Type}", type);
                return StatusCode(500, "An error occurred while retrieving organizations for the type");
            }
        }

        /// <summary>
        /// Get students in an organization
        /// </summary>
        [HttpGet("{id}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetOrganizationStudents(int id)
        {
            try
            {
                var organization = await _context.Organizations.FindAsync(id);
                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found");
                }

                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.Project)
                    .Where(s => s.OrganizationId == id)
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for organization {OrganizationId}", id);
                return StatusCode(500, "An error occurred while retrieving students for the organization");
            }
        }

        /// <summary>
        /// Get projects in an organization
        /// </summary>
        [HttpGet("{id}/projects")]
        public async Task<ActionResult<IEnumerable<Project>>> GetOrganizationProjects(int id)
        {
            try
            {
                var organization = await _context.Organizations.FindAsync(id);
                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found");
                }

                var projects = await _context.Projects
                    .Include(p => p.Status)
                    .Include(p => p.Students)
                    .Where(p => p.OrganizationId == id)
                    .OrderBy(p => p.Title)
                    .ToListAsync();

                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects for organization {OrganizationId}", id);
                return StatusCode(500, "An error occurred while retrieving projects for the organization");
            }
        }
    }
}

