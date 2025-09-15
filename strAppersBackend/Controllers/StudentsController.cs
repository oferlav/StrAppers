using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(ApplicationDbContext context, ILogger<StudentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all students
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
    {
        try
        {
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.Organization)
                .Include(s => s.Project)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .ToListAsync();

            return Ok(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students");
            return StatusCode(500, "An error occurred while retrieving students");
        }
    }

    /// <summary>
    /// Get a specific student by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Student>> GetStudent(int id)
    {
        try
        {
            var student = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.Organization)
                .Include(s => s.Project)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                return NotFound($"Student with ID {id} not found");
            }

            return Ok(student);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student with ID {StudentId}", id);
            return StatusCode(500, "An error occurred while retrieving the student");
        }
    }

    /// <summary>
    /// Create a new student (for frontend registration)
    /// </summary>
    [HttpPost("use/create")]
    public async Task<ActionResult<Student>> CreateStudent(CreateStudentRequest request)
    {
        try
        {
            // Validate the request
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if email already exists
            var existingStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == request.Email);

            if (existingStudent != null)
            {
                return Conflict($"A student with email '{request.Email}' already exists");
            }

            // Check if student ID already exists (if provided)
            if (!string.IsNullOrEmpty(request.StudentId))
            {
                var existingStudentId = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId == request.StudentId);

                if (existingStudentId != null)
                {
                    return Conflict($"A student with ID '{request.StudentId}' already exists");
                }
            }

            // Validate organization exists (if provided)
            if (request.OrganizationId.HasValue)
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == request.OrganizationId.Value);

                if (organization == null)
                {
                    return BadRequest($"Organization with ID {request.OrganizationId} not found");
                }

                if (!organization.IsActive)
                {
                    return BadRequest($"Organization '{organization.Name}' is not active");
                }
            }

            // Create new student
            var student = new Student
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                StudentId = request.StudentId,
                MajorId = request.MajorId,
                YearId = request.YearId,
                LinkedInUrl = request.LinkedInUrl,
                OrganizationId = request.OrganizationId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Load the student with related data for response
            var createdStudent = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.Organization)
                .Include(s => s.Project)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == student.Id);

            _logger.LogInformation("Student created successfully with ID {StudentId} and email {Email}", 
                student.Id, student.Email);

            return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, createdStudent);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating student");
            return StatusCode(500, "An error occurred while saving the student to the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating student");
            return StatusCode(500, "An unexpected error occurred while creating the student");
        }
    }

    /// <summary>
    /// Update an existing student
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStudent(int id, UpdateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound($"Student with ID {id} not found");
            }

            // Check if email is being changed and if it already exists
            if (!string.IsNullOrEmpty(request.Email) && request.Email != student.Email)
            {
                var existingStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == request.Email && s.Id != id);

                if (existingStudent != null)
                {
                    return Conflict($"A student with email '{request.Email}' already exists");
                }
            }

            // Check if student ID is being changed and if it already exists
            if (!string.IsNullOrEmpty(request.StudentId) && request.StudentId != student.StudentId)
            {
                var existingStudentId = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId == request.StudentId && s.Id != id);

                if (existingStudentId != null)
                {
                    return Conflict($"A student with ID '{request.StudentId}' already exists");
                }
            }

            // Validate organization exists (if provided)
            if (request.OrganizationId.HasValue)
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == request.OrganizationId.Value);

                if (organization == null)
                {
                    return BadRequest($"Organization with ID {request.OrganizationId} not found");
                }

                if (!organization.IsActive)
                {
                    return BadRequest($"Organization '{organization.Name}' is not active");
                }
            }

            // Update student properties
            if (!string.IsNullOrEmpty(request.FirstName))
                student.FirstName = request.FirstName;
            if (!string.IsNullOrEmpty(request.LastName))
                student.LastName = request.LastName;
            if (!string.IsNullOrEmpty(request.Email))
                student.Email = request.Email;
            if (request.StudentId != null)
                student.StudentId = request.StudentId;
            if (request.MajorId.HasValue)
                student.MajorId = request.MajorId.Value;
            if (request.YearId.HasValue)
                student.YearId = request.YearId.Value;
            if (!string.IsNullOrEmpty(request.LinkedInUrl))
                student.LinkedInUrl = request.LinkedInUrl;
            if (request.OrganizationId.HasValue)
                student.OrganizationId = request.OrganizationId;

            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Student with ID {StudentId} updated successfully", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while updating student with ID {StudentId}", id);
            return StatusCode(500, "An error occurred while updating the student in the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating student with ID {StudentId}", id);
            return StatusCode(500, "An unexpected error occurred while updating the student");
        }
    }

    /// <summary>
    /// Delete a student
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound($"Student with ID {id} not found");
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student with ID {StudentId} deleted successfully", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while deleting student with ID {StudentId}", id);
            return StatusCode(500, "An error occurred while deleting the student from the database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting student with ID {StudentId}", id);
            return StatusCode(500, "An unexpected error occurred while deleting the student");
        }
    }

    /// <summary>
    /// Get students by organization
    /// </summary>
    [HttpGet("by-organization/{organizationId}")]
    public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByOrganization(int organizationId)
    {
        try
        {
            var students = await _context.Students
                .Where(s => s.OrganizationId == organizationId)
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.Organization)
                .Include(s => s.Project)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .ToListAsync();

            return Ok(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students for organization {OrganizationId}", organizationId);
            return StatusCode(500, "An error occurred while retrieving students for the organization");
        }
    }
}
