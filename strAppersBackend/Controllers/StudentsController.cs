using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StudentsController> _logger;
    private readonly IGitHubService _githubService;
    private readonly IKickoffService _kickoffService;

    public StudentsController(ApplicationDbContext context, ILogger<StudentsController> logger, IGitHubService githubService, IKickoffService kickoffService)
    {
        _context = context;
        _logger = logger;
        _githubService = githubService;
        _kickoffService = kickoffService;
    }

    /// <summary>
    /// Get all students
    /// </summary>
    [HttpGet]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
    {
        try
        {
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
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
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
    public async Task<ActionResult<Student>> GetStudent(int id)
    {
        try
        {
            var student = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
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
            _logger.LogInformation("Starting CreateStudent method with request: {Request}", 
                System.Text.Json.JsonSerializer.Serialize(request));
            
            // Validate the request
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid: {ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            // Check if email already exists
            _logger.LogInformation("Checking if email {Email} already exists", request.Email);
            var existingStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == request.Email);

            if (existingStudent != null)
            {
                _logger.LogWarning("Student with email {Email} already exists", request.Email);
                return Conflict($"A student with email '{request.Email}' already exists");
            }

            // Validate major exists
            _logger.LogInformation("Validating major with ID {MajorId}", request.MajorId);
            var major = await _context.Majors
                .FirstOrDefaultAsync(m => m.Id == request.MajorId);

            if (major == null)
            {
                _logger.LogWarning("Major with ID {MajorId} not found", request.MajorId);
                return BadRequest($"Major with ID {request.MajorId} not found");
            }

            // Validate year exists
            _logger.LogInformation("Validating year with ID {YearId}", request.YearId);
            var year = await _context.Years
                .FirstOrDefaultAsync(y => y.Id == request.YearId);

            if (year == null)
            {
                _logger.LogWarning("Year with ID {YearId} not found", request.YearId);
                return BadRequest($"Year with ID {request.YearId} not found");
            }

            // Validate role exists
            _logger.LogInformation("Validating role with ID {RoleId}", request.RoleId);
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == request.RoleId);

            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found", request.RoleId);
                return BadRequest($"Role with ID {request.RoleId} not found");
            }

            // Validate GitHub username
            if (string.IsNullOrWhiteSpace(request.GithubUser))
            {
                _logger.LogWarning("GitHub username is required but was not provided");
                return BadRequest("GitHub username is required");
            }

            _logger.LogInformation("Validating GitHub user: {GithubUser}", request.GithubUser);
            var isValidGitHubUser = await _githubService.ValidateGitHubUserAsync(request.GithubUser);

            if (!isValidGitHubUser)
            {
                _logger.LogWarning("GitHub user {GithubUser} does not exist", request.GithubUser);
                return BadRequest($"GitHub user '{request.GithubUser}' does not exist. Please provide a valid GitHub username.");
            }

            _logger.LogInformation("GitHub user {GithubUser} validated successfully", request.GithubUser);

            // Create new student
            _logger.LogInformation("Creating new student with email {Email}", request.Email);
            var student = new Student
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                StudentId = request.Email, // Use email as student ID
                MajorId = request.MajorId,
                YearId = request.YearId,
                LinkedInUrl = request.LinkedInUrl,
                GithubUser = request.GithubUser, // GitHub username
                Photo = request.Photo, // Base64 encoded image or URL
                ProjectId = null, // Default to null
                IsAdmin = false, // Default to false
                BoardId = null, // Default to null
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Adding student to context");
            _context.Students.Add(student);
            
            _logger.LogInformation("Saving changes to database");
            await _context.SaveChangesAsync();
            _logger.LogInformation("Student saved successfully with ID {StudentId}", student.Id);

            // Create StudentRole record
            _logger.LogInformation("Creating StudentRole record for student {StudentId} with role {RoleId}", student.Id, request.RoleId);
            var studentRole = new StudentRole
            {
                StudentId = student.Id,
                RoleId = request.RoleId,
                AssignedDate = DateTime.UtcNow,
                IsActive = true
            };

            _context.StudentRoles.Add(studentRole);
            await _context.SaveChangesAsync();
            _logger.LogInformation("StudentRole created successfully");

            _logger.LogInformation("Student created successfully with ID {StudentId} and email {Email}", 
                student.Id, student.Email);

            // Return a simple response without complex navigation properties
            return Ok(new
            {
                Id = student.Id,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email,
                StudentId = student.StudentId,
                MajorId = student.MajorId,
                YearId = student.YearId,
                LinkedInUrl = student.LinkedInUrl,
                GithubUser = student.GithubUser,
                IsAdmin = student.IsAdmin,
                IsAvailable = student.IsAvailable,
                CreatedAt = student.CreatedAt
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating student: {Message}", ex.Message);
            return StatusCode(500, $"An error occurred while saving the student to the database: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating student: {Message}", ex.Message);
            return StatusCode(500, $"An unexpected error occurred while creating the student: {ex.Message}");
        }
    }


    /// <summary>
    /// Delete a student
    /// </summary>
    [HttpDelete("{id}")]
    [Obsolete("This method is disabled. Use /use/ routing instead.")]
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
    [Obsolete("This method is disabled.")]
    public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByOrganization(int organizationId)
    {
        try
        {
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.ProjectBoard)
                .ThenInclude(pb => pb.Project)
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

    /// <summary>
    /// Allocate a student to a project
    /// </summary>
    /// <param name="projectId">The project ID to allocate the student to</param>
    /// <param name="studentId">The student ID to allocate</param>
    /// <param name="request">Request body containing IsAdmin flag</param>
    [HttpPost("use/allocate/{projectId}/{studentId}")]
    public async Task<ActionResult> AllocateStudentToProject(int projectId, int studentId, [FromBody] AllocateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound($"Student with ID {studentId} not found.");
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            if (!project.IsAvailable)
            {
                return BadRequest("Project is not available for allocation.");
            }

            student.ProjectId = projectId;
            student.IsAdmin = request.IsAdmin; // Set IsAdmin flag based on request
            student.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectId set to {ProjectId}, IsAdmin set to {IsAdmin}", 
                studentId, projectId, request.IsAdmin);

            // IMPORTANT: Save changes first so the database reflects the allocation
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("ALLOCATE: Changes saved to database");

            // Now get all students in the project (including the one just allocated)
            var allProjectStudentIds = await _context.Students
                .Where(s => s.ProjectId == projectId)
                .Select(s => s.Id)
                .ToListAsync();

            _logger.LogInformation("ALLOCATE: All students in project {ProjectId} (after allocation): [{StudentIds}]", 
                projectId, string.Join(", ", allProjectStudentIds));
            _logger.LogInformation("ALLOCATE: Total students in project: {Count}", allProjectStudentIds.Count);

            // Use centralized service to determine Kickoff status
            project.Kickoff = await _kickoffService.ShouldKickoffBeTrue(projectId, allProjectStudentIds);
            
            _logger.LogInformation("ALLOCATE: Kickoff flag set to {Kickoff} for project {ProjectId}", project.Kickoff, projectId);

            // Save the Kickoff status
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} allocated to project {ProjectId}", studentId, projectId);
            return Ok(new 
            { 
                Success = true, 
                Message = "Student allocated to project successfully.",
                KickoffStatus = project.Kickoff
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating student {StudentId} to project {ProjectId}", studentId, projectId);
            return StatusCode(500, "An error occurred while allocating the student to the project");
        }
    }

    /// <summary>
    /// Deallocate a student from a project
    /// </summary>
    [HttpPost("use/deallocate/{projectId}/{studentId}")]
    public async Task<ActionResult> DeallocateStudentFromProject(int projectId, int studentId, [FromBody] DeallocateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound($"Student with ID {studentId} not found.");
            }

            if (student.ProjectId != projectId)
            {
                return BadRequest("Student is not allocated to this project.");
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            student.ProjectId = null;
            student.IsAdmin = false; // Set IsAdmin to false when deallocating
            student.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectId set to NULL", studentId);

            // Get remaining students allocated to the project (AFTER deallocation - exclude the deallocated student)
            var remainingProjectStudentIds = await _context.Students
                .Where(s => s.ProjectId == projectId && s.Id != studentId)
                .Select(s => s.Id)
                .ToListAsync();

            _logger.LogInformation("DEALLOCATE: Remaining students in project {ProjectId}: [{StudentIds}]", 
                projectId, string.Join(", ", remainingProjectStudentIds));
            _logger.LogInformation("DEALLOCATE: Total remaining students: {Count}", remainingProjectStudentIds.Count);

            // Use centralized service to determine Kickoff status
            project.Kickoff = await _kickoffService.ShouldKickoffBeTrue(projectId, remainingProjectStudentIds);
            
            _logger.LogInformation("DEALLOCATE: Kickoff flag set to {Kickoff} for project {ProjectId}", project.Kickoff, projectId);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} deallocated from project {ProjectId}", studentId, projectId);
            return Ok(new 
            { 
                Success = true, 
                Message = "Student deallocated from project successfully.",
                KickoffStatus = project.Kickoff
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deallocating student {StudentId} from project {ProjectId}", studentId, projectId);
            return StatusCode(500, "An error occurred while deallocating the student from the project");
        }
    }

    /// <summary>
    /// Suspend a student (set IsAvailable to false)
    /// </summary>
    [HttpPost("use/suspend/{id}")]
    public async Task<ActionResult> SuspendStudent(int id)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound($"Student with ID {id} not found.");
            }

            student.IsAvailable = false;
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} suspended successfully", id);
            return Ok(new { Success = true, Message = "Student suspended successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending student {StudentId}", id);
            return StatusCode(500, "An error occurred while suspending the student");
        }
    }

    /// <summary>
    /// Activate a student (set IsAvailable to true)
    /// </summary>
    [HttpPost("use/activate/{id}")]
    public async Task<ActionResult> ActivateStudent(int id)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound($"Student with ID {id} not found.");
            }

            student.IsAvailable = true;
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} activated successfully", id);
            return Ok(new { Success = true, Message = "Student activated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating student {StudentId}", id);
            return StatusCode(500, "An error occurred while activating the student");
        }
    }

    /// <summary>
    /// Update a student
    /// </summary>
    [HttpPost("use/update/{id}")]
    public async Task<ActionResult> UpdateStudent(int id, [FromBody] UpdateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound($"Student with ID {id} not found.");
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(request.FirstName))
                student.FirstName = request.FirstName;

            if (!string.IsNullOrEmpty(request.LastName))
                student.LastName = request.LastName;

            if (!string.IsNullOrEmpty(request.Email))
                student.Email = request.Email;

            if (request.MajorId.HasValue)
                student.MajorId = request.MajorId.Value;

            if (request.YearId.HasValue)
                student.YearId = request.YearId.Value;

            if (!string.IsNullOrEmpty(request.LinkedInUrl))
                student.LinkedInUrl = request.LinkedInUrl;

            // Update photo if provided (can be base64 or URL)
            if (request.Photo != null)
                student.Photo = request.Photo;

            // Validate and update GitHub username if provided
            if (request.GithubUser != null)
            {
                // Check if GitHub user is not empty
                if (string.IsNullOrWhiteSpace(request.GithubUser))
                {
                    return BadRequest("GitHub username cannot be empty. Provide a valid username or omit the field.");
                }

                // Validate GitHub user exists
                _logger.LogInformation("Validating GitHub user: {GithubUser}", request.GithubUser);
                var isValidGitHubUser = await _githubService.ValidateGitHubUserAsync(request.GithubUser);

                if (!isValidGitHubUser)
                {
                    _logger.LogWarning("GitHub user {GithubUser} does not exist", request.GithubUser);
                    return BadRequest($"GitHub user '{request.GithubUser}' does not exist. Please provide a valid GitHub username.");
                }

                student.GithubUser = request.GithubUser;
                _logger.LogInformation("GitHub user {GithubUser} validated and updated successfully", request.GithubUser);
            }

            // Handle role update if provided
            if (request.RoleId.HasValue)
            {
                // Validate role exists
                var role = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Id == request.RoleId.Value);

                if (role == null)
                {
                    return BadRequest($"Role with ID {request.RoleId.Value} not found");
                }

                // Find existing StudentRole record
                var existingStudentRole = await _context.StudentRoles
                    .FirstOrDefaultAsync(sr => sr.StudentId == id);

                if (existingStudentRole != null)
                {
                    // Update existing role
                    existingStudentRole.RoleId = request.RoleId.Value;
                    existingStudentRole.AssignedDate = DateTime.UtcNow;
                }
                else
                {
                    // Create new StudentRole record
                    var studentRole = new StudentRole
                    {
                        StudentId = id,
                        RoleId = request.RoleId.Value,
                        AssignedDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.StudentRoles.Add(studentRole);
                }
            }

            // StudentId, OrganizationId, ProjectId, IsAdmin, IsAvailable, BoardId are not updatable

            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} updated successfully", id);
            return Ok(new { Success = true, Message = "Student updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student {StudentId}", id);
            return StatusCode(500, "An error occurred while updating the student");
        }
    }

    /// <summary>
    /// Get student by email
    /// </summary>
    [HttpGet("use/by-email/{email}")]
    public async Task<ActionResult<Student>> GetStudentByEmail(string email)
    {
        try
        {
            _logger.LogInformation("Starting GetStudentByEmail method with email: {Email}", email);
            
            // Test database connection first
            var totalStudents = await _context.Students.CountAsync();
            _logger.LogInformation("Database connection successful. Total students in database: {Count}", totalStudents);
            
            // Get all students first to see what we have
            var allStudents = await _context.Students.Select(s => new { s.Id, s.Email }).ToListAsync();
            _logger.LogInformation("All students in database: {Students}", 
                string.Join(", ", allStudents.Select(s => $"ID:{s.Id}, Email:{s.Email}")));
            
            _logger.LogInformation("Searching for student with email: {Email}", email);
            
            var student = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Email == email);

            if (student == null)
            {
                _logger.LogWarning("Student with email {Email} not found", email);
                return NotFound($"Student with email {email} not found.");
            }

            _logger.LogInformation("Student found with ID {StudentId} and email {Email}", student.Id, student.Email);
            
            // Get role information
            var roleInfo = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
            
            // Return a simplified response to avoid serialization issues
            return Ok(new
            {
                Id = student.Id,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email,
                StudentId = student.StudentId,
                MajorId = student.MajorId,
                MajorName = student.Major?.Name,
                YearId = student.YearId,
                YearName = student.Year?.Name,
                LinkedInUrl = student.LinkedInUrl,
                GithubUser = student.GithubUser,
                Photo = student.Photo,
                ProjectId = student.ProjectId,
                IsAdmin = student.IsAdmin,
                BoardId = student.BoardId,
                IsAvailable = student.IsAvailable,
                RoleId = roleInfo?.RoleId,
                RoleName = roleInfo?.Role?.Name,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student with email {Email}: {Message}", email, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving the student: {ex.Message}");
        }
    }

    /// <summary>
    /// Get students by board ID
    /// </summary>
    [HttpGet("use/by-board/{boardId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetStudentsByBoard(string boardId)
    {
        try
        {
            _logger.LogInformation("Starting GetStudentsByBoard method with boardId: {BoardId}", boardId);
            
            // Validate boardId parameter
            if (string.IsNullOrEmpty(boardId))
            {
                _logger.LogWarning("BoardId parameter is null or empty");
                return BadRequest("BoardId parameter is required");
            }
            
            // Test database connection first
            var totalStudents = await _context.Students.CountAsync();
            _logger.LogInformation("Database connection successful. Total students in database: {Count}", totalStudents);
            
            // Get all students first to see what we have
            var allStudents = await _context.Students.Select(s => new { s.Id, s.BoardId, s.IsAvailable }).ToListAsync();
            _logger.LogInformation("All students in database: {Students}", 
                string.Join(", ", allStudents.Select(s => $"ID:{s.Id}, BoardId:{s.BoardId}, IsAvailable:{s.IsAvailable}")));
            
            _logger.LogInformation("Searching for students with boardId: {BoardId} and IsAvailable: true", boardId);
            
            // Get students with role information
            var students = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId == boardId && s.IsAvailable)
                .Select(s => new
                {
                    Id = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Email = s.Email,
                    StudentId = s.StudentId,
                    MajorId = s.MajorId,
                    YearId = s.YearId,
                    LinkedInUrl = s.LinkedInUrl,
                    GithubUser = s.GithubUser,
                    Photo = s.Photo,
                    ProjectId = s.ProjectId,
                    IsAdmin = s.IsAdmin,
                    BoardId = s.BoardId,
                    IsAvailable = s.IsAvailable,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).RoleId,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).Role.Name,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} students for board {BoardId}", students.Count, boardId);
            return Ok(students);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while retrieving students for board {BoardId}: {Message}", boardId, ex.Message);
            return StatusCode(500, "Database error occurred while retrieving students for the board");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students for board {BoardId}: {Message}", boardId, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving students for the board: {ex.Message}");
        }
    }

    /// <summary>
    /// Get students allocated to a specific project but not to any board
    /// </summary>
    [HttpGet("use/project-allocated-no-board/{projectId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetStudentsAllocatedToProjectButNotBoard(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting students allocated to project {ProjectId} but not to any board", projectId);

            // Get students who have this ProjectId but no BoardId
            var students = await _context.Students
                .Include(s => s.Major)
                .Include(s => s.Year)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.ProjectId == projectId && (s.BoardId == null || s.BoardId == "") && s.IsAvailable)
                .Select(s => new
                {
                    Id = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Email = s.Email,
                    StudentId = s.StudentId,
                    MajorId = s.MajorId,
                    MajorName = s.Major != null ? s.Major.Name : null,
                    YearId = s.YearId,
                    YearName = s.Year != null ? s.Year.Name : null,
                    LinkedInUrl = s.LinkedInUrl,
                    GithubUser = s.GithubUser,
                    Photo = s.Photo,
                    ProjectId = s.ProjectId,
                    IsAdmin = s.IsAdmin,
                    BoardId = s.BoardId,
                    IsAvailable = s.IsAvailable,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.IsActive) != null ? s.StudentRoles.FirstOrDefault(sr => sr.IsActive).RoleId : (int?)null,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.IsActive) != null ? s.StudentRoles.FirstOrDefault(sr => sr.IsActive).Role.Name : null,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} students allocated to project {ProjectId} but not to any board", students.Count, projectId);
            return Ok(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students for project {ProjectId} without board: {Message}", projectId, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving students: {ex.Message}");
        }
    }

    /// <summary>
    /// Get students who are allocated to projects but not to boards (all projects)
    /// </summary>
    [HttpGet("use/project-allocated-no-board")]
    public async Task<ActionResult<IEnumerable<object>>> GetAllStudentsAllocatedToProjectButNotBoard()
    {
        try
        {
            _logger.LogInformation("Getting students allocated to projects but not to boards");

            // Get students who have ProjectId (allocated to project) but no BoardId (not allocated to board)
            var students = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.ProjectId.HasValue && s.ProjectId > 0 && (s.BoardId == null || s.BoardId == ""))
                .Select(s => new
                {
                    StudentId = s.Id,
                    ProjectId = s.ProjectId,
                    IsAdmin = s.IsAdmin,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Email = s.Email,
                    LinkedInUrl = s.LinkedInUrl,
                    GithubUser = s.GithubUser,
                    IsAvailable = s.IsAvailable,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).RoleId,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).Role.Name
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} students allocated to projects but not to boards", students.Count);

            return Ok(new
            {
                Success = true,
                Count = students.Count,
                Students = students
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while retrieving students allocated to projects but not to boards: {Message}", ex.Message);
            return StatusCode(500, "Database error occurred while retrieving students");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students allocated to projects but not to boards: {Message}", ex.Message);
            return StatusCode(500, $"An error occurred while retrieving students: {ex.Message}");
        }
    }

    /// <summary>
    /// Set a student as admin for a project and set all other students in the same project as non-admin
    /// </summary>
    /// <param name="studentId">The student ID to set as admin</param>
    /// <param name="projectId">The project ID</param>
    /// <returns>Success response</returns>
    [HttpPost("use/set-project-admin/{studentId}/{projectId}")]
    public async Task<ActionResult> SetProjectAdmin(int studentId, int projectId)
    {
        try
        {
            _logger.LogInformation("Setting student {StudentId} as admin for project {ProjectId}", studentId, projectId);

            // Validate that the student exists and is allocated to the specified project
            var targetStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && s.ProjectId == projectId);

            if (targetStudent == null)
            {
                _logger.LogWarning("Student {StudentId} not found or not allocated to project {ProjectId}", studentId, projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Student with ID {studentId} not found or not allocated to project {projectId}"
                });
            }

            // Get all students in the project
            var studentsInProject = await _context.Students
                .Where(s => s.ProjectId == projectId)
                .ToListAsync();

            if (!studentsInProject.Any())
            {
                _logger.LogWarning("No students found for project {ProjectId}", projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"No students found for project {projectId}"
                });
            }

            // Set all students in the project to non-admin first
            foreach (var student in studentsInProject)
            {
                student.IsAdmin = false;
                student.UpdatedAt = DateTime.UtcNow;
            }

            // Set the target student as admin
            targetStudent.IsAdmin = true;
            targetStudent.UpdatedAt = DateTime.UtcNow;

            // Save changes to database
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully set student {StudentId} as admin for project {ProjectId}. Updated {TotalStudents} students total", 
                studentId, projectId, studentsInProject.Count);

            return Ok(new
            {
                Success = true,
                Message = $"Student {targetStudent.FirstName} {targetStudent.LastName} has been set as admin for project {projectId}",
                StudentId = studentId,
                ProjectId = projectId,
                StudentName = $"{targetStudent.FirstName} {targetStudent.LastName}",
                TotalStudentsUpdated = studentsInProject.Count,
                PreviousAdminCount = studentsInProject.Count(s => s.IsAdmin) - 1 // Subtract 1 since we just set one as admin
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while setting student {StudentId} as admin for project {ProjectId}: {Message}", studentId, projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "Database error occurred while updating admin status"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting student {StudentId} as admin for project {ProjectId}: {Message}", studentId, projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while setting admin status: {ex.Message}"
            });
        }
    }
}

// Request DTOs for StudentsController
public class AllocateStudentRequest
{
    /// <summary>
    /// Set to true if the student should be an admin for this project
    /// </summary>
    [Required]
    [DefaultValue(false)]
    public bool IsAdmin { get; set; } = false;
    
    /// <summary>
    /// Optional notes for the allocation
    /// </summary>
    public string? Notes { get; set; }
}

public class DeallocateStudentRequest
{
    // StudentId removed - will be determined by authentication or other means
}

