using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/Employers/Adds")]
    public class EmployersAddsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployersAddsController> _logger;

        public EmployersAddsController(ApplicationDbContext context, ILogger<EmployersAddsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new employer add (job posting) (for frontend use)
        /// </summary>
        [HttpPost("use/create")]
        public async Task<ActionResult<EmployerAdd>> CreateEmployerAdd([FromBody] CreateEmployerAddRequest request)
        {
            try
            {
                _logger.LogInformation("Starting CreateEmployerAdd method with request: {Request}",
                    System.Text.Json.JsonSerializer.Serialize(request));

                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid. Validation errors:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("Field: {Field}, Errors: {Errors}",
                            error.Key,
                            string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                // Validate that EmployerId exists
                var employerExists = await _context.Employers
                    .AnyAsync(e => e.Id == request.EmployerId);

                if (!employerExists)
                {
                    _logger.LogWarning("EmployerId {EmployerId} does not exist", request.EmployerId);
                    return BadRequest($"Employer with ID {request.EmployerId} does not exist");
                }

                // Validate that RoleId exists
                var roleExists = await _context.Roles
                    .AnyAsync(r => r.Id == request.RoleId);

                if (!roleExists)
                {
                    _logger.LogWarning("RoleId {RoleId} does not exist", request.RoleId);
                    return BadRequest($"Role with ID {request.RoleId} does not exist");
                }

                // Create new employer add
                var employerAdd = new EmployerAdd
                {
                    EmployerId = request.EmployerId,
                    RoleId = request.RoleId,
                    Tags = request.Tags,
                    JobDescription = request.JobDescription,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmployerAdds.Add(employerAdd);
                await _context.SaveChangesAsync();

                // Load the employer add with related data for response
                var createdEmployerAdd = await _context.EmployerAdds
                    .Include(ea => ea.Employer)
                    .Include(ea => ea.Role)
                    .FirstOrDefaultAsync(ea => ea.Id == employerAdd.Id);

                _logger.LogInformation("EmployerAdd created successfully with ID {EmployerAddId} for EmployerId {EmployerId} and RoleId {RoleId}",
                    employerAdd.Id, request.EmployerId, request.RoleId);

                return Ok(createdEmployerAdd);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating employer add: {Message}", ex.Message);
                return StatusCode(500, $"An error occurred while saving the employer add to the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating employer add: {Message}", ex.Message);
                return StatusCode(500, $"An unexpected error occurred while creating the employer add: {ex.Message}");
            }
        }

        /// <summary>
        /// Edit/Update an existing employer add (for frontend use)
        /// </summary>
        [HttpPost("use/edit")]
        public async Task<ActionResult<EmployerAdd>> EditEmployerAdd([FromBody] EditEmployerAddRequest request)
        {
            try
            {
                _logger.LogInformation("Starting EditEmployerAdd method for employer add ID {EmployerAddId} with request: {Request}",
                    request.Id, System.Text.Json.JsonSerializer.Serialize(request));

                // Validate the request
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid. Validation errors:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("Field: {Field}, Errors: {Errors}",
                            error.Key,
                            string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                // Find the employer add
                var employerAdd = await _context.EmployerAdds.FindAsync(request.Id);
                if (employerAdd == null)
                {
                    _logger.LogWarning("EmployerAdd with ID {EmployerAddId} not found", request.Id);
                    return NotFound($"EmployerAdd with ID {request.Id} not found");
                }

                // Validate that EmployerId exists (if being changed)
                if (employerAdd.EmployerId != request.EmployerId)
                {
                    var employerExists = await _context.Employers
                        .AnyAsync(e => e.Id == request.EmployerId);

                    if (!employerExists)
                    {
                        _logger.LogWarning("EmployerId {EmployerId} does not exist", request.EmployerId);
                        return BadRequest($"Employer with ID {request.EmployerId} does not exist");
                    }
                }

                // Validate that RoleId exists (if being changed)
                if (employerAdd.RoleId != request.RoleId)
                {
                    var roleExists = await _context.Roles
                        .AnyAsync(r => r.Id == request.RoleId);

                    if (!roleExists)
                    {
                        _logger.LogWarning("RoleId {RoleId} does not exist", request.RoleId);
                        return BadRequest($"Role with ID {request.RoleId} does not exist");
                    }
                }

                // Update employer add properties
                employerAdd.EmployerId = request.EmployerId;
                employerAdd.RoleId = request.RoleId;
                employerAdd.Tags = request.Tags;
                employerAdd.JobDescription = request.JobDescription;
                employerAdd.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Load the employer add with related data for response
                var updatedEmployerAdd = await _context.EmployerAdds
                    .Include(ea => ea.Employer)
                    .Include(ea => ea.Role)
                    .FirstOrDefaultAsync(ea => ea.Id == employerAdd.Id);

                _logger.LogInformation("EmployerAdd with ID {EmployerAddId} updated successfully", request.Id);

                return Ok(updatedEmployerAdd);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while updating employer add with ID {EmployerAddId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An error occurred while updating the employer add in the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating employer add with ID {EmployerAddId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An unexpected error occurred while updating the employer add: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete an employer add (for frontend use)
        /// </summary>
        [HttpPost("use/delete")]
        public async Task<ActionResult> DeleteEmployerAdd([FromBody] DeleteEmployerAddRequest request)
        {
            try
            {
                _logger.LogInformation("Starting DeleteEmployerAdd method for employer add ID {EmployerAddId}", request.Id);

                // Find the employer add
                var employerAdd = await _context.EmployerAdds.FindAsync(request.Id);
                if (employerAdd == null)
                {
                    _logger.LogWarning("EmployerAdd with ID {EmployerAddId} not found", request.Id);
                    return NotFound($"EmployerAdd with ID {request.Id} not found");
                }

                // Store ID for logging before deletion
                var employerAddId = employerAdd.Id;
                var employerId = employerAdd.EmployerId;
                var roleId = employerAdd.RoleId;

                // Delete the employer add
                _context.EmployerAdds.Remove(employerAdd);
                await _context.SaveChangesAsync();

                _logger.LogInformation("EmployerAdd with ID {EmployerAddId} deleted successfully (EmployerId: {EmployerId}, RoleId: {RoleId})",
                    employerAddId, employerId, roleId);

                return Ok(new
                {
                    Success = true,
                    Message = $"EmployerAdd with ID {employerAddId} deleted successfully",
                    DeletedId = employerAddId
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while deleting employer add with ID {EmployerAddId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An error occurred while deleting the employer add from the database: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting employer add with ID {EmployerAddId}: {Message}", request.Id, ex.Message);
                return StatusCode(500, $"An unexpected error occurred while deleting the employer add: {ex.Message}");
            }
        }
    }
}

// Request DTOs for EmployersAddsController
public class CreateEmployerAddRequest
{
    [Required]
    public int EmployerId { get; set; }

    [Required]
    public int RoleId { get; set; }

    public string? Tags { get; set; }

    public string? JobDescription { get; set; }
}

public class EditEmployerAddRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public int EmployerId { get; set; }

    [Required]
    public int RoleId { get; set; }

    public string? Tags { get; set; }

    public string? JobDescription { get; set; }
}

public class DeleteEmployerAddRequest
{
    [Required]
    public int Id { get; set; }
}




