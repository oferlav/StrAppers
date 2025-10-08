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
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
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
        [HttpGet("use/all")]
        public async Task<ActionResult<IEnumerable<Organization>>> GetAllOrganizations()
        {
            try
            {
                _logger.LogInformation("Starting GetAllOrganizations method");
                
                // Test basic database connection first
                var count = await _context.Organizations.CountAsync();
                _logger.LogInformation("Database connection successful. Found {Count} organizations", count);
                
                var organizations = await _context.Organizations
                    .OrderBy(o => o.Name)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} organizations", organizations.Count);
                return Ok(organizations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all organizations: {Message}", ex.Message);
                return StatusCode(500, $"An error occurred while retrieving all organizations: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a specific organization by ID
        /// </summary>
        [HttpGet("{id}")]
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
        public async Task<ActionResult<Organization>> GetOrganization(int id)
        {
            try
            {
                var organization = await _context.Organizations
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
                _logger.LogInformation("Starting CreateOrganization method with request: {Request}", 
                    System.Text.Json.JsonSerializer.Serialize(request));
                
                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid: {ModelState}", ModelState);
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
                    Logo = request.Logo, // Base64 encoded image or URL
                    CreatedAt = DateTime.UtcNow
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                // Load the organization with related data for response
                var createdOrganization = await _context.Organizations
                    .Include(o => o.Projects)
                    .FirstOrDefaultAsync(o => o.Id == organization.Id);

                _logger.LogInformation("Organization created successfully with ID {OrganizationId} and name {Name}", 
                    organization.Id, organization.Name);

                return Ok(createdOrganization);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating organization: {Message}", ex.Message);
                return StatusCode(500, $"An error occurred while saving the organization to the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating organization: {Message}", ex.Message);
                return StatusCode(500, $"An unexpected error occurred while creating the organization: {ex.Message}");
            }
        }

        /// <summary>
        /// Get organizations by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
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
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
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
                    .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
                    // OrganizationId removed from Student model
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
        [Obsolete("This method is disabled. Use /use/ routing instead.")]
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

        /// <summary>
        /// Update an existing organization
        /// </summary>
        [HttpPost("use/update/{id}")]
        public async Task<ActionResult<Organization>> UpdateOrganization(int id, CreateOrganizationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var organization = await _context.Organizations.FindAsync(id);
                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found");
                }

                // Check if name is being changed and if it conflicts with existing
                if (organization.Name.ToLower() != request.Name.ToLower())
                {
                    var existingOrganization = await _context.Organizations
                        .FirstOrDefaultAsync(o => o.Name.ToLower() == request.Name.ToLower() && o.Id != id);

                    if (existingOrganization != null)
                    {
                        return Conflict($"An organization with name '{request.Name}' already exists");
                    }
                }

                // Update organization properties
                organization.Name = request.Name;
                organization.Description = request.Description;
                organization.Website = request.Website;
                organization.ContactEmail = request.ContactEmail;
                organization.Phone = request.Phone;
                organization.Address = request.Address;
                organization.Type = request.Type;
                organization.IsActive = request.IsActive;
                organization.Logo = request.Logo; // Base64 encoded image or URL
                organization.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Organization with ID {OrganizationId} updated successfully", id);

                return Ok(organization);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while updating organization with ID {OrganizationId}", id);
                return StatusCode(500, "An error occurred while updating the organization in the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating organization with ID {OrganizationId}", id);
                return StatusCode(500, "An unexpected error occurred while updating the organization");
            }
        }

        /// <summary>
        /// Suspend an organization (set IsActive to false)
        /// </summary>
        [HttpPost("use/suspend/{id}")]
        public async Task<ActionResult> SuspendOrganization(int id)
        {
            try
            {
                var organization = await _context.Organizations.FindAsync(id);
                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found");
                }

                organization.IsActive = false;
                organization.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Organization with ID {OrganizationId} suspended successfully", id);

                return Ok(new { message = $"Organization '{organization.Name}' has been suspended successfully" });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while suspending organization with ID {OrganizationId}", id);
                return StatusCode(500, "An error occurred while suspending the organization in the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while suspending organization with ID {OrganizationId}", id);
                return StatusCode(500, "An unexpected error occurred while suspending the organization");
            }
        }

        /// <summary>
        /// Activate an organization (set IsActive to true)
        /// </summary>
        [HttpPost("use/activate/{id}")]
        public async Task<ActionResult> ActivateOrganization(int id)
        {
            try
            {
                var organization = await _context.Organizations.FindAsync(id);
                if (organization == null)
                {
                    return NotFound($"Organization with ID {id} not found.");
                }

                organization.IsActive = true;
                organization.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Organization {OrganizationId} activated successfully", id);
                return Ok(new { Success = true, Message = "Organization activated successfully." });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while activating organization with ID {OrganizationId}", id);
                return StatusCode(500, "An error occurred while activating the organization in the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while activating organization with ID {OrganizationId}", id);
                return StatusCode(500, "An unexpected error occurred while activating the organization");
            }
        }

        /// <summary>
        /// Get a specific organization by ID (for frontend use)
        /// </summary>
        [HttpGet("use/{id}")]
        public async Task<ActionResult<object>> GetOrganizationById(int id)
        {
            try
            {
                _logger.LogInformation("Getting organization by ID {OrganizationId}", id);

                var organization = await _context.Organizations
                    .Include(o => o.Projects)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (organization == null)
                {
                    _logger.LogWarning("Organization with ID {OrganizationId} not found", id);
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"Organization with ID {id} not found"
                    });
                }

                _logger.LogInformation("Found organization {OrganizationId}: {Name}", id, organization.Name);

                // Return a simplified response to avoid serialization issues
                return Ok(new
                {
                    Success = true,
                    Id = organization.Id,
                    Name = organization.Name,
                    Description = organization.Description,
                    Website = organization.Website,
                    ContactEmail = organization.ContactEmail,
                    Phone = organization.Phone,
                    Address = organization.Address,
                    Type = organization.Type,
                    IsActive = organization.IsActive,
                    Logo = organization.Logo,
                    CreatedAt = organization.CreatedAt,
                    UpdatedAt = organization.UpdatedAt,
                    ProjectCount = organization.Projects?.Count ?? 0
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while retrieving organization {OrganizationId}: {Message}", id, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Database error occurred while retrieving the organization"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organization {OrganizationId}: {Message}", id, ex.Message);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"An error occurred while retrieving the organization: {ex.Message}"
                });
            }
        }
    }
}

