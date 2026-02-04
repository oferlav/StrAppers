using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly IPasswordHasherService _passwordHasher;
    private readonly KickoffConfig _kickoffConfig;

    public StudentsController(ApplicationDbContext context, ILogger<StudentsController> logger, IGitHubService githubService, IKickoffService kickoffService, IPasswordHasherService passwordHasher, IOptions<KickoffConfig> kickoffConfig)
    {
        _context = context;
        _logger = logger;
        _githubService = githubService;
        _kickoffService = kickoffService;
        _passwordHasher = passwordHasher;
        _kickoffConfig = kickoffConfig.Value;
    }

    /// <summary>
    /// Set prioritized projects for a student. The first project becomes the active ProjectId and Priority1.
    /// </summary>
    /// <remarks>
    /// Route format:
    /// POST /api/Students/use/set-project-priority/{studentId}/{projectId1}/{projectId2}/{projectId3}/{projectId4}
    ///
    /// Use 0 for any projectId you want to clear (set NULL).
    /// </remarks>
    [HttpPost("use/allocate-with-priority/{studentId}/{projectId1:int}/{projectId2:int}/{projectId3:int}/{projectId4:int}")]
    public async Task<ActionResult> AllocateWithPriority(
        int studentId,
        int projectId1,
        int projectId2,
        int projectId3,
        int projectId4)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound($"Student with ID {studentId} not found.");
            }

            // Helper to normalize 0 -> null
            int? ToNullable(int value) => value <= 0 ? (int?)null : value;

            student.ProjectId = ToNullable(projectId1); // Active allocation mirrors Priority1
            student.ProjectPriority1 = ToNullable(projectId1);
            student.ProjectPriority2 = ToNullable(projectId2);
            student.ProjectPriority3 = ToNullable(projectId3);
            student.ProjectPriority4 = ToNullable(projectId4);
            student.Status = 1; // Pending
            student.StartPendingAt = DateTime.UtcNow;
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = "Student allocation with priorities set successfully (pending).",
                StudentId = student.Id,
                ProjectId = student.ProjectId,
                Priority1 = student.ProjectPriority1,
                Priority2 = student.ProjectPriority2,
                Priority3 = student.ProjectPriority3,
                Priority4 = student.ProjectPriority4,
                Status = student.Status,
                StartPendingAt = student.StartPendingAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting project priorities for student {StudentId}", studentId);
            return StatusCode(500, "An error occurred while updating project priorities.");
        }
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
    /// Get all students with a specific role (for frontend use)
    /// Only returns students with a non-null and non-empty boardId
    /// </summary>
    [HttpGet("use/by-role/{roleId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetStudentsByRole(int roleId)
    {
        try
        {
            _logger.LogInformation("Retrieving students with role ID {RoleId} and non-empty boardId", roleId);

            // Validate that the role exists
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found", roleId);
                return NotFound($"Role with ID {roleId} not found");
            }

            // Get students that have an active StudentRole with the specified RoleId and have a boardId
            var students = await _context.Students
                .Where(s => s.StudentRoles.Any(sr => sr.RoleId == roleId && sr.IsActive) 
                    && !string.IsNullOrEmpty(s.BoardId))
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
                    Status = s.Status,
                    StartPendingAt = s.StartPendingAt,
                    ProjectPriority1 = s.ProjectPriority1,
                    ProjectPriority2 = s.ProjectPriority2,
                    ProjectPriority3 = s.ProjectPriority3,
                    ProjectPriority4 = s.ProjectPriority4,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.RoleId == roleId && sr.IsActive) != null 
                        ? s.StudentRoles.FirstOrDefault(sr => sr.RoleId == roleId && sr.IsActive).RoleId 
                        : (int?)null,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.RoleId == roleId && sr.IsActive) != null 
                        ? s.StudentRoles.FirstOrDefault(sr => sr.RoleId == roleId && sr.IsActive).Role.Name 
                        : null,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();

            _logger.LogInformation("Found {Count} students with role ID {RoleId} and non-empty boardId", students.Count, roleId);

            return Ok(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students with role ID {RoleId}: {Message}", roleId, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving students with role ID {roleId}");
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

            // Validate GitHub username only if provided (non-developer roles may not have GitHub)
            if (!string.IsNullOrWhiteSpace(request.GithubUser))
            {
                _logger.LogInformation("Validating GitHub user: {GithubUser}", request.GithubUser);
                var isValidGitHubUser = await _githubService.ValidateGitHubUserAsync(request.GithubUser);

                if (!isValidGitHubUser)
                {
                    _logger.LogWarning("GitHub user {GithubUser} does not exist", request.GithubUser);
                    return BadRequest($"GitHub user '{request.GithubUser}' does not exist. Please provide a valid GitHub username.");
                }

                _logger.LogInformation("GitHub user {GithubUser} validated successfully", request.GithubUser);
            }
            else
            {
                _logger.LogInformation("GitHub username not provided - skipping validation (allowed for non-developer roles)");
            }

            // Handle programming language (allow 0 to be treated as null)
            int? programmingLanguageId = request.ProgrammingLanguageId;
            if (programmingLanguageId.HasValue && programmingLanguageId.Value == 0)
            {
                programmingLanguageId = null;
            }

            // Validate programming language if provided
            if (programmingLanguageId.HasValue)
            {
                _logger.LogInformation("Validating programming language with ID {ProgrammingLanguageId}", programmingLanguageId.Value);
                var programmingLanguage = await _context.ProgrammingLanguages
                    .FirstOrDefaultAsync(pl => pl.Id == programmingLanguageId.Value && pl.IsActive);

                if (programmingLanguage == null)
                {
                    _logger.LogWarning("Programming language with ID {ProgrammingLanguageId} not found or not active", programmingLanguageId.Value);
                    return BadRequest($"Programming language with ID {programmingLanguageId.Value} not found or not active");
                }
                _logger.LogInformation("Programming language {LanguageName} validated successfully", programmingLanguage.Name);
            }

            // Hash password if provided
            string? passwordHash = null;
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogInformation("Hashing password for student with email {Email}", request.Email);
                passwordHash = _passwordHasher.HashPassword(request.Password);
            }

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
                GithubUser = request.GithubUser ?? string.Empty, // GitHub username (optional)
                Photo = request.Photo, // Base64 encoded image or URL
                ProgrammingLanguageId = programmingLanguageId, // Programming language preference (null allowed)
                ProjectId = null, // Default to null
                IsAdmin = false, // Default to false
                BoardId = null, // Default to null
                CreatedAt = DateTime.UtcNow,
                Status = 0,
                PasswordHash = passwordHash,
                // Work preferences
                MinutesToWork = request.MinutesToWork,
                HybridWork = request.HybridWork ?? false,
                HomeWork = request.HomeWork ?? false,
                FullTimeWork = request.FullTimeWork ?? false,
                PartTimeWork = request.PartTimeWork ?? false,
                FreelanceWork = request.FreelanceWork ?? false,
                TravelWork = request.TravelWork ?? false,
                NightShiftWork = request.NightShiftWork ?? false,
                RelocationWork = request.RelocationWork ?? false,
                StudentWork = request.StudentWork ?? false,
                MultilingualWork = request.MultilingualWork ?? false,
                CV = request.CV // Base64 encoded CV file
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
    /// Deallocate a student from a project and add the project to their priority list
    /// </summary>
    /// <param name="projectId">The project ID to deallocate the student from</param>
    /// <param name="studentId">The student ID to deallocate</param>
    /// <param name="request">Request body (legacy parameter, not used)</param>
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

            // Step 1: Set student.ProjectId = null (deallocate)
            student.ProjectId = null;
            
            // Step 2: Set student.IsAdmin = false
            student.IsAdmin = false;
            
            // Step 4: Set the lowest available projectPriority(X) = projectId
            if (!student.ProjectPriority1.HasValue || student.ProjectPriority1 == 0)
            {
                student.ProjectPriority1 = projectId;
                _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectPriority1 set to {ProjectId}", studentId, projectId);
            }
            else if (!student.ProjectPriority2.HasValue || student.ProjectPriority2 == 0)
            {
                student.ProjectPriority2 = projectId;
                _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectPriority2 set to {ProjectId}", studentId, projectId);
            }
            else if (!student.ProjectPriority3.HasValue || student.ProjectPriority3 == 0)
            {
                student.ProjectPriority3 = projectId;
                _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectPriority3 set to {ProjectId}", studentId, projectId);
            }
            else if (!student.ProjectPriority4.HasValue || student.ProjectPriority4 == 0)
            {
                student.ProjectPriority4 = projectId;
                _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectPriority4 set to {ProjectId}", studentId, projectId);
            }
            else
            {
                _logger.LogWarning("ALLOCATE: Student {StudentId} already has all 4 priorities set. Cannot add project {ProjectId} to priority list.", studentId, projectId);
                return BadRequest("Student already has all 4 project priorities set. Cannot add more projects to priority list.");
            }
            
            student.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("ALLOCATE: Student {StudentId} ProjectId set to NULL, IsAdmin set to false", studentId);

            // Step 3: Set project.Kickoff = false
            project.Kickoff = false;
            _logger.LogInformation("ALLOCATE: Project {ProjectId} Kickoff set to false", projectId);

            // Save all changes
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} deallocated from project {ProjectId} and project added to priority list", studentId, projectId);
            return Ok(new 
            { 
                Success = true, 
                Message = "Student deallocated from project and project added to priority list successfully.",
                ProjectId = student.ProjectId,
                ProjectPriority1 = student.ProjectPriority1,
                ProjectPriority2 = student.ProjectPriority2,
                ProjectPriority3 = student.ProjectPriority3,
                ProjectPriority4 = student.ProjectPriority4,
                IsAdmin = student.IsAdmin,
                KickoffStatus = project.Kickoff
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing allocation for student {StudentId} and project {ProjectId}", studentId, projectId);
            return StatusCode(500, "An error occurred while processing the allocation");
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

            // Check if project appears in ProjectId or any ProjectPriority field
            bool isAllocated = student.ProjectId == projectId ||
                               student.ProjectPriority1 == projectId ||
                               student.ProjectPriority2 == projectId ||
                               student.ProjectPriority3 == projectId ||
                               student.ProjectPriority4 == projectId;

            if (!isAllocated)
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
            
            // Clear any ProjectPriority fields that contain this projectId
            if (student.ProjectPriority1 == projectId)
            {
                student.ProjectPriority1 = null;
                _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectPriority1 cleared (was {ProjectId})", studentId, projectId);
            }
            if (student.ProjectPriority2 == projectId)
            {
                student.ProjectPriority2 = null;
                _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectPriority2 cleared (was {ProjectId})", studentId, projectId);
            }
            if (student.ProjectPriority3 == projectId)
            {
                student.ProjectPriority3 = null;
                _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectPriority3 cleared (was {ProjectId})", studentId, projectId);
            }
            if (student.ProjectPriority4 == projectId)
            {
                student.ProjectPriority4 = null;
                _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectPriority4 cleared (was {ProjectId})", studentId, projectId);
            }
            
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

            // Validate and update GitHub username if provided (non-developer roles may not have GitHub)
            if (request.GithubUser != null && !string.IsNullOrWhiteSpace(request.GithubUser))
            {
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
            else if (request.GithubUser != null && string.IsNullOrWhiteSpace(request.GithubUser))
            {
                // Allow clearing GitHub username (set to empty string)
                student.GithubUser = string.Empty;
                _logger.LogInformation("GitHub username cleared for student {StudentId}", id);
            }
            // If request.GithubUser is null, don't update the field (leave existing value)

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

            // Handle programming language update if provided
            if (request.ProgrammingLanguageId.HasValue)
            {
                var programmingLanguageId = request.ProgrammingLanguageId.Value;

                if (programmingLanguageId == 0)
                {
                    student.ProgrammingLanguageId = null;
                }
                else
                {
                    _logger.LogInformation("Validating programming language with ID {ProgrammingLanguageId}", programmingLanguageId);
                    var programmingLanguage = await _context.ProgrammingLanguages
                        .FirstOrDefaultAsync(pl => pl.Id == programmingLanguageId && pl.IsActive);

                    if (programmingLanguage == null)
                    {
                        _logger.LogWarning("Programming language with ID {ProgrammingLanguageId} not found or not active", programmingLanguageId);
                        return BadRequest($"Programming language with ID {programmingLanguageId} not found or not active");
                    }
                    student.ProgrammingLanguageId = programmingLanguageId;
                }
            }

            // Update work preferences if provided
            if (request.MinutesToWork.HasValue)
                student.MinutesToWork = request.MinutesToWork.Value;

            if (request.HybridWork.HasValue)
                student.HybridWork = request.HybridWork.Value;

            if (request.HomeWork.HasValue)
                student.HomeWork = request.HomeWork.Value;

            if (request.FullTimeWork.HasValue)
                student.FullTimeWork = request.FullTimeWork.Value;

            if (request.PartTimeWork.HasValue)
                student.PartTimeWork = request.PartTimeWork.Value;

            if (request.FreelanceWork.HasValue)
                student.FreelanceWork = request.FreelanceWork.Value;

            if (request.TravelWork.HasValue)
                student.TravelWork = request.TravelWork.Value;

            if (request.NightShiftWork.HasValue)
                student.NightShiftWork = request.NightShiftWork.Value;

            if (request.RelocationWork.HasValue)
                student.RelocationWork = request.RelocationWork.Value;

            if (request.StudentWork.HasValue)
                student.StudentWork = request.StudentWork.Value;

            if (request.MultilingualWork.HasValue)
                student.MultilingualWork = request.MultilingualWork.Value;

            // Update CV if provided
            if (request.CV != null)
                student.CV = request.CV;

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
                .Include(s => s.ProgrammingLanguage)
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
                ProgrammingLanguageId = student.ProgrammingLanguageId,
                ProgrammingLanguageName = student.ProgrammingLanguage?.Name,
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
    /// Get all projects from a student's priority fields (ProjectPriority1-4)
    /// </summary>
    /// <param name="email">The student's email address</param>
    [HttpGet("use/allocated-projects/{email}")]
    public async Task<ActionResult<IEnumerable<Project>>> GetAllocatedProjects(string email)
    {
        try
        {
            _logger.LogInformation("Getting allocated projects for student with email: {Email}", email);

            // Find the student by email
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == email);

            if (student == null)
            {
                _logger.LogWarning("Student with email {Email} not found", email);
                return NotFound($"Student with email {email} not found.");
            }

            _logger.LogInformation("Student found with ID {StudentId}. Checking priority fields: Priority1={P1}, Priority2={P2}, Priority3={P3}, Priority4={P4}",
                student.Id, student.ProjectPriority1, student.ProjectPriority2, student.ProjectPriority3, student.ProjectPriority4);

            // Collect all non-null project priority IDs
            var projectIds = new List<int>();
            
            if (student.ProjectPriority1.HasValue && student.ProjectPriority1.Value > 0)
                projectIds.Add(student.ProjectPriority1.Value);
            
            if (student.ProjectPriority2.HasValue && student.ProjectPriority2.Value > 0)
                projectIds.Add(student.ProjectPriority2.Value);
            
            if (student.ProjectPriority3.HasValue && student.ProjectPriority3.Value > 0)
                projectIds.Add(student.ProjectPriority3.Value);
            
            if (student.ProjectPriority4.HasValue && student.ProjectPriority4.Value > 0)
                projectIds.Add(student.ProjectPriority4.Value);

            if (projectIds.Count == 0)
            {
                _logger.LogInformation("No project priorities set for student {StudentId}", student.Id);
                return Ok(new List<Project>()); // Return empty list
            }

            // Remove duplicates (in case same project appears in multiple priority fields)
            projectIds = projectIds.Distinct().ToList();

            _logger.LogInformation("Found {Count} unique project IDs in priority fields: [{ProjectIds}]", 
                projectIds.Count, string.Join(", ", projectIds));

            // Get all projects matching these IDs
            var projects = await _context.Projects
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ExtendedDescription = p.ExtendedDescription,
                    Priority = p.Priority,
                    OrganizationId = p.OrganizationId,
                    IsAvailable = p.IsAvailable,
                    Kickoff = p.Kickoff,
                    CriteriaIds = p.CriteriaIds,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                    // Exclude SystemDesign, SystemDesignDoc, and Organization to avoid serialization issues
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} projects for student {StudentId}", projects.Count, student.Id);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allocated projects for student with email {Email}: {Message}", email, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving allocated projects: {ex.Message}");
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
            
            // Get students with role information (materialize first so we can compute roleNames in C#)
            var studentsRaw = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId == boardId && s.IsAvailable)
                .ToListAsync();

            var students = studentsRaw.Select(s =>
            {
                var roleName = s.StudentRoles?
                    .Where(sr => sr.IsActive && sr.Role != null)
                    .Select(sr => sr.Role!.Name)
                    .FirstOrDefault() ?? "Team Member";
                var roleNames = (roleName.Contains("Fullstack", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Full Stack", StringComparison.OrdinalIgnoreCase))
                    ? new[] { roleName, "Backend Developer", "Frontend Developer" }
                    : new[] { roleName };
                return (object)new
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
                    RoleId = s.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.RoleId,
                    RoleName = roleName,
                    RoleNames = roleNames,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                };
            }).ToList();

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
    /// Get all available programming languages
    /// </summary>
    [HttpGet("use/get-programming-language")]
    public async Task<ActionResult<IEnumerable<object>>> GetProgrammingLanguages()
    {
        try
        {
            _logger.LogInformation("Getting all available programming languages");
            
            var languages = await _context.ProgrammingLanguages
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new
                {
                    l.Id,
                    l.Name,
                    l.ReleaseYear,
                    l.Creator,
                    l.Description,
                    l.IsActive
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} active programming languages", languages.Count);
            return Ok(languages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving programming languages");
            return StatusCode(500, "An error occurred while retrieving programming languages");
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
                .Where(s => s.ProjectId.HasValue && s.ProjectId > 0 && (s.BoardId == null || s.BoardId == "") && s.Status < 2)
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

    /// <summary>
    /// Login endpoint for students - verifies email and password
    /// </summary>
    [HttpPost("use/login")]
    public async Task<ActionResult<object>> LoginStudent(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for student with email {Email}", request.Email);

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempt with missing email or password");
                return BadRequest(new { Success = false, Message = "Email and password are required" });
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == request.Email);

            if (student == null)
            {
                _logger.LogWarning("Login attempt failed: Student with email {Email} not found", request.Email);
                return Unauthorized(new { Success = false, Message = "Invalid email or password" });
            }

            if (string.IsNullOrWhiteSpace(student.PasswordHash))
            {
                _logger.LogWarning("Login attempt failed: Student with email {Email} has no password set", request.Email);
                return Unauthorized(new { Success = false, Message = "Password not set for this account" });
            }

            bool isValidPassword = _passwordHasher.VerifyPassword(student.PasswordHash, request.Password);

            if (!isValidPassword)
            {
                _logger.LogWarning("Login attempt failed: Invalid password for student with email {Email}", request.Email);
                return Unauthorized(new { Success = false, Message = "Invalid email or password" });
            }

            _logger.LogInformation("Login successful for student with email {Email}", request.Email);

            return Ok(new
            {
                Success = true,
                Message = "Login successful",
                Student = new
                {
                    Id = student.Id,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    Email = student.Email
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during student login for email {Email}", request.Email);
            return StatusCode(500, new { Success = false, Message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Check if allocating a student to a project would break KickoffConfig RequireDeveloperRule
    /// Only checks for role conflicts (e.g., Fullstack + Frontend/Backend, multiple Fullstacks)
    /// Does NOT block if roles are missing (e.g., missing Frontend when Backend exists)
    /// </summary>
    /// <param name="projectId">The project ID to check allocation for</param>
    /// <param name="studentId">The student ID to check if can be allocated</param>
    /// <returns>Object with isAllocatable (bool) and message (string) explaining why allocation is not allowed if false</returns>
    [HttpGet("use/is-allocatable/{projectId}/{studentId}")]
    public async Task<ActionResult<object>> IsStudentAllocatable(int projectId, int studentId)
    {
        try
        {
            _logger.LogInformation("Checking if student {StudentId} can be allocated to project {ProjectId}", studentId, projectId);

            // Get the student to be allocated
            var studentToAllocate = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (studentToAllocate == null)
            {
                _logger.LogWarning("Student {StudentId} not found", studentId);
                return NotFound($"Student with ID {studentId} not found");
            }

            // Get student's active role types
            var studentRoleTypes = studentToAllocate.StudentRoles
                .Where(sr => sr.IsActive)
                .Select(sr => new { Type = sr.Role?.Type ?? 0, Name = sr.Role?.Name ?? "" })
                .ToList();

            _logger.LogInformation("Student {StudentId} roles: [{Roles}]", 
                studentId, string.Join(", ", studentRoleTypes.Select(r => $"Type={r.Type}, Name='{r.Name}'")));

            if (!studentRoleTypes.Any())
            {
                _logger.LogWarning("Student {StudentId} has no active roles", studentId);
                return Ok(new
                {
                    isAllocatable = true,
                    message = "Student has no active roles - allocation allowed"
                });
            }

            // Get all students with Status < 2 that have this projectId in any ProjectPriority field
            // This matches the logic used in /api/Projects/use/get-students/{id}
            var studentsAlreadyAllocated = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.Id != studentId &&
                           s.Status.HasValue && 
                           s.Status < 2 && // Status < 2 (same as get-students endpoint)
                           (s.ProjectPriority1 == projectId ||
                            s.ProjectPriority2 == projectId ||
                            s.ProjectPriority3 == projectId ||
                            s.ProjectPriority4 == projectId))
                .ToListAsync();

            _logger.LogInformation("Found {Count} students with Status < 2 already allocated or pending allocation to project {ProjectId} (excluding student {StudentId})", 
                studentsAlreadyAllocated.Count, projectId, studentId);
            
            // Log details for debugging
            foreach (var student in studentsAlreadyAllocated)
            {
                var activeRoles = student.StudentRoles.Where(sr => sr.IsActive).ToList();
                var roleInfo = string.Join(", ", activeRoles.Select(r => $"Type={r.Role?.Type}, Name={r.Role?.Name}"));
                _logger.LogInformation("  - Student ID: {StudentId}, Status: {Status}, ProjectPriority1: {P1}, ProjectPriority2: {P2}, ProjectPriority3: {P3}, ProjectPriority4: {P4}, Roles: [{Roles}]", 
                    student.Id, student.Status, student.ProjectPriority1, student.ProjectPriority2, student.ProjectPriority3, student.ProjectPriority4, roleInfo);
            }

            // Only check RequireDeveloperRule if enabled
            if (!_kickoffConfig.RequireDeveloperRule)
            {
                _logger.LogInformation("✅ RequireDeveloperRule is disabled - allocation allowed");
                return Ok(new
                {
                    isAllocatable = true,
                    message = "Allocation is allowed - RequireDeveloperRule is disabled"
                });
            }

            // Get role types from already allocated students
            var allocatedRoleTypes = studentsAlreadyAllocated
                .SelectMany(s => s.StudentRoles.Where(sr => sr.IsActive))
                .Select(sr => new { Type = sr.Role?.Type ?? 0, Name = sr.Role?.Name ?? "" })
                .ToList();

            _logger.LogInformation("Allocated students' roles: [{Roles}]", 
                string.Join(", ", allocatedRoleTypes.Select(r => $"Type={r.Type}, Name='{r.Name}'")));

            // Count Fullstack developers (Type=1) - check for "Full Stack Developer" or "Fullstack"
            var allocatedFullstackCount = allocatedRoleTypes.Count(r => r.Type == 1);
            var studentHasFullstack = studentRoleTypes.Any(r => r.Type == 1);

            // Check for Frontend/Backend developers (Type=2 with name containing Frontend/Backend)
            var allocatedHasFrontend = allocatedRoleTypes.Any(r => r.Type == 2 && 
                (r.Name.Contains("Frontend", StringComparison.OrdinalIgnoreCase) || 
                 r.Name.Contains("Front-end", StringComparison.OrdinalIgnoreCase)));
            var allocatedHasBackend = allocatedRoleTypes.Any(r => r.Type == 2 && 
                (r.Name.Contains("Backend", StringComparison.OrdinalIgnoreCase) || 
                 r.Name.Contains("Back-end", StringComparison.OrdinalIgnoreCase)));

            var studentHasFrontend = studentRoleTypes.Any(r => r.Type == 2 && 
                (r.Name.Contains("Frontend", StringComparison.OrdinalIgnoreCase) || 
                 r.Name.Contains("Front-end", StringComparison.OrdinalIgnoreCase)));
            var studentHasBackend = studentRoleTypes.Any(r => r.Type == 2 && 
                (r.Name.Contains("Backend", StringComparison.OrdinalIgnoreCase) || 
                 r.Name.Contains("Back-end", StringComparison.OrdinalIgnoreCase)));

            // Calculate counts after allocation
            var newFullstackCount = allocatedFullstackCount + (studentHasFullstack ? 1 : 0);
            var newHasFrontend = allocatedHasFrontend || studentHasFrontend;
            var newHasBackend = allocatedHasBackend || studentHasBackend;

            _logger.LogInformation("Developer rule check: Allocated Fullstack={AllocatedFS}, Student Fullstack={StudentFS}, New Fullstack={NewFS}", 
                allocatedFullstackCount, studentHasFullstack, newFullstackCount);
            _logger.LogInformation("Developer rule check: Allocated Frontend={AllocatedFE}, Backend={AllocatedBE}", allocatedHasFrontend, allocatedHasBackend);
            _logger.LogInformation("Developer rule check: Student Frontend={StudentFE}, Backend={StudentBE}", studentHasFrontend, studentHasBackend);
            _logger.LogInformation("Developer rule check: After allocation Frontend={NewFE}, Backend={NewBE}", newHasFrontend, newHasBackend);
            _logger.LogInformation("Developer rule check: Violation check - newFullstackCount==1: {Check1}, newHasFrontend: {Check2}, newHasBackend: {Check3}, ShouldBlock: {ShouldBlock}", 
                newFullstackCount == 1, newHasFrontend, newHasBackend, newFullstackCount == 1 && (newHasFrontend || newHasBackend));

            // Check for violations (only block conflicts, not missing roles)
            
            // Violation 1: Multiple Fullstack developers (>1)
            if (newFullstackCount > 1)
            {
                var message = $"Allocation would violate RequireDeveloperRule: Multiple Fullstack developers would be allocated ({newFullstackCount} found, maximum 1 allowed, Type=1)";
                _logger.LogInformation("❌ Allocation would break developer rule: Multiple Fullstack developers ({Count})", newFullstackCount);
                return Ok(new
                {
                    isAllocatable = false,
                    message = message
                });
            }

            // Violation 2: Fullstack + Frontend/Backend conflict
            // If exactly one Fullstack exists (or will exist), there must be NO Frontend and NO Backend
            // Check both: if Fullstack already exists AND student has Frontend/Backend, OR if student is Fullstack AND Frontend/Backend already exists
            bool hasFullstackConflict = false;
            List<string> conflictRoles = new List<string>();
            
            if (newFullstackCount == 1)
            {
                if (newHasFrontend)
                {
                    hasFullstackConflict = true;
                    conflictRoles.Add("Frontend");
                }
                if (newHasBackend)
                {
                    hasFullstackConflict = true;
                    conflictRoles.Add("Backend");
                }
            }
            
            if (hasFullstackConflict)
            {
                var message = $"Allocation would violate RequireDeveloperRule: Fullstack developer (Type=1) cannot coexist with {string.Join(" and ", conflictRoles)} developer(s). If a Fullstack developer exists, Frontend and Backend developers are not allowed.";
                _logger.LogInformation("❌ Allocation would break developer rule: Fullstack + {Conflicts} | allocatedFullstackCount={AllocatedFS}, studentHasFullstack={StudentFS}, newFullstackCount={NewFS}, newHasFrontend={NewFE}, newHasBackend={NewBE}", 
                    string.Join("/", conflictRoles), allocatedFullstackCount, studentHasFullstack, newFullstackCount, newHasFrontend, newHasBackend);
                return Ok(new
                {
                    isAllocatable = false,
                    message = message
                });
            }

            // Note: We do NOT block if Frontend is missing but Backend exists (or vice versa)
            // Missing roles are allowed - we only block conflicts

            _logger.LogInformation("✅ Allocation is allowed: Student {StudentId} can be allocated to project {ProjectId} - no developer rule conflicts", studentId, projectId);
            return Ok(new
            {
                isAllocatable = true,
                message = "Allocation is allowed and does not violate RequireDeveloperRule"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if student {StudentId} can be allocated to project {ProjectId}", studentId, projectId);
            return StatusCode(500, "An error occurred while checking allocation feasibility");
        }
    }

    /// <summary>
    /// Change password endpoint for students
    /// </summary>
    [HttpPost("use/change-password")]
    public async Task<ActionResult<object>> ChangeStudentPassword(ChangePasswordRequest request)
    {
        try
        {
            _logger.LogInformation("Password change request for student with email {Email}", request.Email);

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                _logger.LogWarning("Password change attempt with missing email or new password");
                return BadRequest(new { Success = false, Message = "Email and new password are required" });
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == request.Email);

            if (student == null)
            {
                _logger.LogWarning("Password change failed: Student with email {Email} not found", request.Email);
                return NotFound(new { Success = false, Message = "Student not found" });
            }

            // Hash the new password
            string passwordHash = _passwordHasher.HashPassword(request.NewPassword);
            student.PasswordHash = passwordHash;
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password changed successfully for student with email {Email}", request.Email);

            return Ok(new
            {
                Success = true,
                Message = "Password changed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for student with email {Email}", request.Email);
            return StatusCode(500, new { Success = false, Message = "An error occurred while changing password" });
        }
    }
}

// Request DTOs for StudentsController
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

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

