using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Helpers;
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

    public StudentsController(ApplicationDbContext context, ILogger<StudentsController> logger, IGitHubService githubService, IKickoffService kickoffService, IPasswordHasherService passwordHasher)
    {
        _context = context;
        _logger = logger;
        _githubService = githubService;
        _kickoffService = kickoffService;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Add a project instance for a student when all projects appear "taken". Ensures the student can be allocated
    /// by creating a new ProjectInstance (e.g. new cohort) and assigning the student to it.
    /// </summary>
    /// <param name="studentId">The student ID to allocate</param>
    /// <returns>Success true with ProjectId/InstanceId when a new instance was created; Success false when conditions are not met (do nothing).</returns>
    [HttpPost("use/add-project-instance")]
    public async Task<ActionResult<object>> AddProjectInstance([FromQuery] int studentId)
    {
        try
        {
            var student = await _context.Students
                .Include(s => s.StudentRoles.Where(sr => sr.IsActive))
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
                return NotFound($"Student with ID {studentId} not found.");

            if (student.InstanceId.HasValue)
            {
                _logger.LogInformation("AddProjectInstance: Student {StudentId} already has InstanceId {InstanceId}, skipping.", studentId, student.InstanceId);
                return Ok(new { Success = false, Message = "Student already has a project instance." });
            }

            int? targetRoleId = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.RoleId;
            if (!targetRoleId.HasValue)
            {
                _logger.LogWarning("AddProjectInstance: Student {StudentId} has no active role.", studentId);
                return BadRequest("Student has no active role.");
            }

            var availableProjectIds = await _context.Projects
                .Where(p => p.IsAvailable)
                .Select(p => p.Id)
                .ToListAsync();

            if (availableProjectIds.Count == 0)
            {
                _logger.LogInformation("AddProjectInstance: No available projects.");
                return Ok(new { Success = false, Message = "No available projects." });
            }

            // Role compatibility: same RoleId, or if target is Fullstack (6) then 6, 2, 3 allowed
            bool RoleCompatible(int? otherRoleId)
            {
                if (!otherRoleId.HasValue) return false;
                if (otherRoleId.Value == targetRoleId!.Value) return true;
                if (targetRoleId.Value == 6 && (otherRoleId.Value == 6 || otherRoleId.Value == 2 || otherRoleId.Value == 3))
                    return true;
                return false;
            }

            // Students with InstanceId=null, project in priorities, Status 0/1/2
            var studentsUnallocated = await _context.Students
                .Include(s => s.StudentRoles.Where(sr => sr.IsActive))
                .Where(s => s.Id != studentId
                    && s.InstanceId == null
                    && s.Status.HasValue && s.Status >= 0 && s.Status <= 2
                    && (s.ProjectPriority1.HasValue || s.ProjectPriority2.HasValue || s.ProjectPriority3.HasValue || s.ProjectPriority4.HasValue))
                .ToListAsync();

            // Return false only if we find a project where NO student in the pool has a role compatible with the target (i.e. no "slot" for target's role).
            foreach (var projId in availableProjectIds)
            {
                var withThisProject = studentsUnallocated.Where(s =>
                    s.ProjectPriority1 == projId || s.ProjectPriority2 == projId || s.ProjectPriority3 == projId || s.ProjectPriority4 == projId).ToList();
                if (withThisProject.Count == 0) continue; // No one else in pool for this project – condition met.
                bool atLeastOneCompatible = withThisProject.Any(s =>
                    RoleCompatible(s.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.RoleId));
                if (!atLeastOneCompatible)
                {
                    _logger.LogInformation(
                        "AddProjectInstance: Project {ProjectId} – no unallocated student with this project in their priorities has a role compatible with target (RoleId {TargetRoleId}); cannot add instance.",
                        projId, targetRoleId);
                    return Ok(new { Success = false, Message = "A project has no student with compatible role; cannot add instance." });
                }
            }

            // Walk ProjectInstances table row by row; for each (ProjectId, InstanceId) repeat the same logic:
            // students with this InstanceId and this project in priorities (Status 0/1/2) must have at least one compatible role.
            var projectInstanceRows = await _context.ProjectInstances
                .Select(pi => new { pi.ProjectId, pi.InstanceId })
                .ToListAsync();

            foreach (var row in projectInstanceRows)
            {
                var studentsInThisInstanceWithThisProject = await _context.Students
                    .Include(s => s.StudentRoles.Where(sr => sr.IsActive))
                    .Where(s => s.Id != studentId
                        && s.InstanceId == row.InstanceId
                        && s.Status.HasValue && s.Status >= 0 && s.Status <= 2
                        && (s.ProjectPriority1 == row.ProjectId || s.ProjectPriority2 == row.ProjectId
                            || s.ProjectPriority3 == row.ProjectId || s.ProjectPriority4 == row.ProjectId))
                    .ToListAsync();

                // If this instance has no students with this project in priorities, condition is not met – return false.
                if (studentsInThisInstanceWithThisProject.Count == 0)
                {
                    _logger.LogInformation(
                        "AddProjectInstance: ProjectInstances row ProjectId={ProjectId}, InstanceId={InstanceId} – no students in Students table with this project in priorities and this InstanceId; cannot add instance.",
                        row.ProjectId, row.InstanceId);
                    return Ok(new { Success = false, Message = "An existing instance has no matching students; cannot add instance." });
                }

                bool atLeastOneCompatible = studentsInThisInstanceWithThisProject.Any(s =>
                    RoleCompatible(s.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.RoleId));
                if (!atLeastOneCompatible)
                {
                    _logger.LogInformation(
                        "AddProjectInstance: ProjectInstances row ProjectId={ProjectId}, InstanceId={InstanceId} – no student has role compatible with target (RoleId {TargetRoleId}); cannot add instance.",
                        row.ProjectId, row.InstanceId, targetRoleId);
                    return Ok(new { Success = false, Message = "An existing instance has no student with compatible role; cannot add instance." });
                }
            }

            // All checks passed: pick next InstanceId and choose project for even distribution
            var nextInstanceId = projectInstanceRows.Count > 0 ? projectInstanceRows.Max(r => r.InstanceId) + 1 : 1;

            // Instance count per project (among available only)
            var instanceCountByProject = projectInstanceRows
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Count());

            var projectsWithZeroInstances = availableProjectIds
                .Where(pid => !instanceCountByProject.ContainsKey(pid) || instanceCountByProject[pid] == 0)
                .ToList();

            int chosenProjectId;
            var random = new Random();
            if (projectsWithZeroInstances.Count > 0)
            {
                // Prefer projects with no instances: random among those
                chosenProjectId = projectsWithZeroInstances[random.Next(projectsWithZeroInstances.Count)];
            }
            else
            {
                // All available projects already have at least one instance
                var minCount = availableProjectIds.Min(pid => instanceCountByProject.GetValueOrDefault(pid, 0));
                var projectsWithFewest = availableProjectIds
                    .Where(pid => instanceCountByProject.GetValueOrDefault(pid, 0) == minCount)
                    .ToList();
                if (projectsWithFewest.Count == 1)
                    chosenProjectId = projectsWithFewest[0];
                else
                    // Tie (even distribution): random from all available projects
                    chosenProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
            }

            var projectInstance = new ProjectInstance
            {
                ProjectId = chosenProjectId,
                InstanceId = nextInstanceId
            };
            _context.ProjectInstances.Add(projectInstance);
            student.InstanceId = nextInstanceId;
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("AddProjectInstance: Created ProjectInstance ProjectId={ProjectId}, InstanceId={InstanceId} and assigned student {StudentId}.", chosenProjectId, nextInstanceId, studentId);
            return Ok(new
            {
                Success = true,
                Message = "New project instance created and student assigned.",
                ProjectId = chosenProjectId,
                InstanceId = nextInstanceId,
                StudentId = studentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddProjectInstance for student {StudentId}", studentId);
            return StatusCode(500, "An error occurred while adding project instance.");
        }
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

            // Guard: never overwrite an active student's ProjectId or Status
            if (student.Status == 3 && !string.IsNullOrWhiteSpace(student.BoardId))
            {
                _logger.LogWarning(
                    "AllocateWithPriority blocked for active student {StudentId} (Status=3, BoardId={BoardId}): only priority fields updated.",
                    studentId, student.BoardId);

                // Only update priority slots — leave ProjectId, Status, BoardId untouched
                student.ProjectPriority1 = ToNullable(projectId1);
                student.ProjectPriority2 = ToNullable(projectId2);
                student.ProjectPriority3 = ToNullable(projectId3);
                student.ProjectPriority4 = ToNullable(projectId4);
                student.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Priority fields updated. ProjectId and Status unchanged (student is active on a board).",
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

            student.ProjectId = ToNullable(projectId1); // Active allocation mirrors Priority1
            student.ProjectPriority1 = ToNullable(projectId1);
            student.ProjectPriority2 = ToNullable(projectId2);
            student.ProjectPriority3 = ToNullable(projectId3);
            student.ProjectPriority4 = ToNullable(projectId4);
            var previousStatus = student.Status;
            student.Status = 1; // Pending
            if (previousStatus is null or 0)
            {
                student.StartPendingAt = DateTime.UtcNow;
            }
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

            // Validate institute coupon and resolve InstituteId
            int? resolvedInstituteId = 1; // default institute
            string? resolvedCoupon = null;
            if (!string.IsNullOrWhiteSpace(request.InstituteCoupon))
            {
                _logger.LogInformation("Validating institute coupon: {Coupon}", request.InstituteCoupon);
                var instituteProject = await _context.InstituteProjects
                    .FirstOrDefaultAsync(p => p.Coupon == request.InstituteCoupon);
                if (instituteProject == null)
                {
                    _logger.LogWarning("Institute coupon '{Coupon}' not found in InstituteProjects", request.InstituteCoupon);
                    return BadRequest($"Institute coupon '{request.InstituteCoupon}' is not valid.");
                }
                resolvedInstituteId = instituteProject.InstituteId;
                resolvedCoupon = request.InstituteCoupon;
                _logger.LogInformation("Institute coupon validated, InstituteId={InstituteId}", instituteProject.InstituteId);
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
            var photo = StudentPhotoHelper.CompressStudentPhotoIfNeeded(request.Photo, _logger);
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
                Photo = photo, // Base64 encoded image or URL (compressed if larger than 2 MB)
                ProgrammingLanguageId = programmingLanguageId, // Programming language preference (null allowed)
                InstituteId = resolvedInstituteId,
                Coupon = resolvedCoupon,
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
                EmployerExposure = request.EmployerExposure ?? true,
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
                EmployerExposure = student.EmployerExposure,
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

            // Protection: do not clear ProjectId when student is fully allocated (Status=3, has board) to a *different* project.
            if (student.Status == 3 && !string.IsNullOrWhiteSpace(student.BoardId) && student.ProjectId.HasValue && student.ProjectId != projectId)
            {
                _logger.LogWarning("ALLOCATE blocked: Student {StudentId} is allocated to project {CurrentProjectId} with board; cannot deallocate from project {RequestedProjectId}.", studentId, student.ProjectId, projectId);
                return BadRequest($"Student is allocated to another project (has board). Use deallocate from the current project first, or deallocate from project {projectId} only if that is their current project.");
            }

            // Step 1: Set student.ProjectId = null (deallocate from current project)
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

            if (student.Status is null or 0)
            {
                student.Status = 1;
                student.StartPendingAt = DateTime.UtcNow;
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

            // Only clear ProjectId when we're actually deallocating from their *current* project. If we're only removing this project from a priority slot, leave ProjectId unchanged so we don't wipe an active allocation.
            if (student.ProjectId == projectId)
            {
                student.ProjectId = null;
                student.IsAdmin = false;
                _logger.LogInformation("DEALLOCATE: Student {StudentId} ProjectId set to NULL (was current project)", studentId);
            }
            else
            {
                _logger.LogInformation("DEALLOCATE: Student {StudentId} removing project {ProjectId} from priority list only (current ProjectId={CurrentProjectId} unchanged)", studentId, projectId, student.ProjectId);
            }
            
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

    // ──────────────────────────────────────────────────────────────────────────
    // Institute project allocation (InstitutePriority1-4). InstituteId=0/1 uses
    // the original ProjectPriority endpoints above — these are institute-only.
    // ──────────────────────────────────────────────────────────────────────────

    [HttpPost("use/institute/allocate/{instituteProjectId}/{studentId}")]
    public async Task<ActionResult> AllocateStudentToInstituteProject(int instituteProjectId, int studentId, [FromBody] AllocateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound($"Student with ID {studentId} not found.");

            var project = await _context.InstituteProjects.FindAsync(instituteProjectId);
            if (project == null)
                return NotFound($"Institute project with ID {instituteProjectId} not found.");

            if (!student.InstitutePriority1.HasValue || student.InstitutePriority1 == 0)
                student.InstitutePriority1 = instituteProjectId;
            else if (!student.InstitutePriority2.HasValue || student.InstitutePriority2 == 0)
                student.InstitutePriority2 = instituteProjectId;
            else if (!student.InstitutePriority3.HasValue || student.InstitutePriority3 == 0)
                student.InstitutePriority3 = instituteProjectId;
            else if (!student.InstitutePriority4.HasValue || student.InstitutePriority4 == 0)
                student.InstitutePriority4 = instituteProjectId;
            else
                return BadRequest("Student already has all 4 institute project priorities set.");

            if (student.Status is null or 0)
            {
                student.Status = 1;
                student.StartPendingAt = DateTime.UtcNow;
            }
            student.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = "Student added to institute project priority list.",
                InstitutePriority1 = student.InstitutePriority1,
                InstitutePriority2 = student.InstitutePriority2,
                InstitutePriority3 = student.InstitutePriority3,
                InstitutePriority4 = student.InstitutePriority4
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating student {StudentId} to institute project {ProjectId}", studentId, instituteProjectId);
            return StatusCode(500, "An error occurred while processing the allocation.");
        }
    }

    [HttpPost("use/institute/deallocate/{instituteProjectId}/{studentId}")]
    public async Task<ActionResult> DeallocateStudentFromInstituteProject(int instituteProjectId, int studentId, [FromBody] DeallocateStudentRequest request)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound($"Student with ID {studentId} not found.");

            bool isAllocated = student.InstitutePriority1 == instituteProjectId ||
                               student.InstitutePriority2 == instituteProjectId ||
                               student.InstitutePriority3 == instituteProjectId ||
                               student.InstitutePriority4 == instituteProjectId;

            if (!isAllocated)
                return BadRequest("Student is not allocated to this institute project.");

            if (student.InstitutePriority1 == instituteProjectId) student.InstitutePriority1 = null;
            if (student.InstitutePriority2 == instituteProjectId) student.InstitutePriority2 = null;
            if (student.InstitutePriority3 == instituteProjectId) student.InstitutePriority3 = null;
            if (student.InstitutePriority4 == instituteProjectId) student.InstitutePriority4 = null;

            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Student removed from institute project priority list." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deallocating student {StudentId} from institute project {ProjectId}", studentId, instituteProjectId);
            return StatusCode(500, "An error occurred while processing the deallocation.");
        }
    }

    [HttpGet("use/institute/allocated-projects/{email}")]
    public async Task<ActionResult> GetAllocatedInstituteProjects(string email)
    {
        try
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == email);
            if (student == null)
                return NotFound($"Student with email {email} not found.");

            var projectIds = new List<int>();
            if (student.InstitutePriority1.HasValue && student.InstitutePriority1.Value > 0) projectIds.Add(student.InstitutePriority1.Value);
            if (student.InstitutePriority2.HasValue && student.InstitutePriority2.Value > 0) projectIds.Add(student.InstitutePriority2.Value);
            if (student.InstitutePriority3.HasValue && student.InstitutePriority3.Value > 0) projectIds.Add(student.InstitutePriority3.Value);
            if (student.InstitutePriority4.HasValue && student.InstitutePriority4.Value > 0) projectIds.Add(student.InstitutePriority4.Value);

            if (projectIds.Count == 0)
                return Ok(new List<object>());

            projectIds = projectIds.Distinct().ToList();

            var projects = await _context.InstituteProjects
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Description,
                    p.ExtendedDescription,
                    p.Priority,
                    p.IsAvailable,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allocated institute projects for {Email}", email);
            return StatusCode(500, "An error occurred while retrieving allocated institute projects.");
        }
    }

    [HttpGet("use/institute/is-allocatable/{instituteProjectId}/{studentId}")]
    public async Task<ActionResult<object>> IsStudentInstituteAllocatable(int instituteProjectId, int studentId)
    {
        try
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound($"Student with ID {studentId} not found.");

            var project = await _context.InstituteProjects.FindAsync(instituteProjectId);
            if (project == null)
                return NotFound($"Institute project with ID {instituteProjectId} not found.");

            if (!project.IsAvailable)
                return Ok(new { isAllocatable = false, message = "This institute project is not currently available." });

            return Ok(new { isAllocatable = true, message = "Allocation is allowed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking allocatability for student {StudentId} / institute project {ProjectId}", studentId, instituteProjectId);
            return StatusCode(500, "An error occurred while checking allocation feasibility.");
        }
    }

    /// <summary>
    /// Returns roles for the dropdown based on an institute coupon.
    /// Resolves: coupon → InstituteProjects → active InstituteTemplate (with SquadId) → InstituteSquadRoles.
    /// Falls back to the global Roles table when no active template with a SquadId is found.
    /// </summary>
    [HttpGet("use/institute/roles-by-coupon/{coupon}")]
    public async Task<ActionResult> GetRolesByCoupon(string coupon)
    {
        try
        {
            var projects = await _context.InstituteProjects
                .Where(p => p.Coupon == coupon)
                .ToListAsync();

            if (!projects.Any())
                return BadRequest($"Coupon '{coupon}' is not valid.");

            var projectIds = projects.Select(p => p.Id).ToList();

            var templates = await _context.InstituteTemplates
                .Include(t => t.Squad)
                    .ThenInclude(s => s!.Roles)
                .Where(t => t.IsActive &&
                            t.InstituteProjectId != null &&
                            projectIds.Contains(t.InstituteProjectId.Value))
                .ToListAsync();

            var distinctSquadRoles = templates
                .Where(t => t.Squad != null)
                .SelectMany(t => t.Squad!.Roles.Where(r => r.IsActive))
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.Name)
                .ToList();

            if (distinctSquadRoles.Any())
            {
                var squadRoles = distinctSquadRoles
                    .Select(r => new { id = r.Id, name = r.Name, type = r.Type, skillId = r.SkillId, isTechnical = r.IsTechnical, customerEngagement = r.CustomerEngagement })
                    .ToList();

                return Ok(new { source = "squad", roles = (object)squadRoles });
            }

            // Scenario 2: no squad templates — check for institute-specific base roles
            // (admin customised the global catalog for this institute via Roles Config).
            var instituteId = projects.Select(p => p.InstituteId).FirstOrDefault();
            if (instituteId > 0)
            {
                var baseRoles = await _context.Roles
                    .Where(r => r.InstituteId == instituteId && r.SquadId == null && r.IsActive)
                    .OrderBy(r => r.Name)
                    .Select(r => new { id = r.Id, name = r.Name, type = r.Type, skillId = r.SkillId, isTechnical = r.IsTechnical, customerEngagement = r.CustomerEngagement })
                    .ToListAsync();

                if (baseRoles.Any())
                    return Ok(new { source = "institute", roles = (object)baseRoles });
            }

            // Fallback: no institute base roles → return global (InstituteId=null) roles
            var defaultRoles = await _context.Roles
                .Where(r => r.InstituteId == null)
                .OrderBy(r => r.Name)
                .Select(r => new { id = r.Id, name = r.Name, type = r.Type })
                .ToListAsync();

            return Ok(new { source = "default", roles = (object)defaultRoles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for coupon {Coupon}", coupon);
            return StatusCode(500, "An error occurred while retrieving roles for the coupon.");
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

            // Update photo if provided (can be base64 or URL; compressed if larger than 2 MB)
            if (request.Photo != null)
                student.Photo = StudentPhotoHelper.CompressStudentPhotoIfNeeded(request.Photo, _logger);

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

            if (request.EmployerExposure.HasValue)
                student.EmployerExposure = request.EmployerExposure.Value;

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
                .Include(s => s.ProjectBoard)
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
                SuperUser = student.SuperUser,
                BoardId = student.BoardId,
                IsAvailable = student.IsAvailable,
                EmployerExposure = student.EmployerExposure,
                ProgrammingLanguageId = student.ProgrammingLanguageId,
                ProgrammingLanguageName = student.ProgrammingLanguage?.Name,
                RoleId = roleInfo?.RoleId,
                RoleName = roleInfo?.Role?.Name,
                RoleSkillId = roleInfo?.Role?.SkillId,
                RoleType = roleInfo?.Role?.Type,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt,
                MinutesToWork = student.MinutesToWork,
                HybridWork = student.HybridWork,
                HomeWork = student.HomeWork,
                FullTimeWork = student.FullTimeWork,
                PartTimeWork = student.PartTimeWork,
                FreelanceWork = student.FreelanceWork,
                TravelWork = student.TravelWork,
                NightShiftWork = student.NightShiftWork,
                RelocationWork = student.RelocationWork,
                StudentWork = student.StudentWork,
                MultilingualWork = student.MultilingualWork,
                AssistMe = student.AssistMe,
                NextMeetingTime = student.NextMeetingTime,
                NextMeetingUrl = student.NextMeetingUrl,
                B2c = student.B2c,
                RoleIndex = student.RoleIndex,
                IsSingleRole = student.ProjectBoard?.IsSingleRole ?? false,
                InstituteId = student.InstituteId,
                Coupon = student.Coupon
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student with email {Email}: {Message}", email, ex.Message);
            return StatusCode(500, $"An error occurred while retrieving the student: {ex.Message}");
        }
    }

    /// <summary>
    /// Student self-service: set <see cref="Student.AssistMe"/>. Blocked while <see cref="Student.NextMeetingTime"/> is in the future.
    /// </summary>
    [HttpPost("use/set-assist-me")]
    public async Task<ActionResult<object>> SetAssistMe([FromBody] SetAssistMeRequest request)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Body required." });

        var email = (request.Email ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { success = false, message = "Email is required." });

        try
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == email);
            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            var now = DateTime.UtcNow;
            if (student.NextMeetingTime.HasValue && student.NextMeetingTime.Value > now)
            {
                return Conflict(new
                {
                    success = false,
                    message = "A meeting is already scheduled. You can change this after the meeting time passes."
                });
            }

            student.AssistMe = request.AssistMe;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} AssistMe set to {AssistMe}", student.Id, student.AssistMe);

            return Ok(new
            {
                success = true,
                assistMe = student.AssistMe,
                nextMeetingTime = student.NextMeetingTime,
                nextMeetingUrl = student.NextMeetingUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetAssistMe failed for {Email}", email);
            return StatusCode(500, new { success = false, message = "Failed to update assist flag." });
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

            var boardIsSingleRole = await _context.ProjectBoards
                .Where(b => b.Id == boardId)
                .Select(b => b.IsSingleRole)
                .FirstOrDefaultAsync();

            var students = studentsRaw.Select(s =>
            {
                var roleName = s.StudentRoles?
                    .Where(sr => sr.IsActive && sr.Role != null)
                    .Select(sr => sr.Role!.Name)
                    .FirstOrDefault() ?? "Team Member";
                var isFullStack = roleName.Contains("Fullstack", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Full Stack", StringComparison.OrdinalIgnoreCase);
                var roleNames = (!boardIsSingleRole && isFullStack)
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
                    RoleIndex = s.RoleIndex,
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
    /// Get students (including photo/avatars) who: have at least one project in ProjectPriority1-4 that is available,
    /// Status is 0, 1, or 2, and student IsAvailable is true.
    /// </summary>
    [HttpGet("use/project-allocated-no-board-avatars")]
    public async Task<ActionResult<IEnumerable<object>>> GetProjectAllocatedNoBoardAvatars()
    {
        try
        {
            _logger.LogInformation("Getting students for project-allocated-no-board-avatars (with photos)");

            var availableProjectIds = await _context.Projects
                .Where(p => p.IsAvailable)
                .Select(p => p.Id)
                .ToListAsync();

            var students = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.IsAvailable
                    && s.Status.HasValue && s.Status >= 0 && s.Status <= 2
                    && ((s.ProjectPriority1.HasValue && availableProjectIds.Contains(s.ProjectPriority1.Value))
                        || (s.ProjectPriority2.HasValue && availableProjectIds.Contains(s.ProjectPriority2.Value))
                        || (s.ProjectPriority3.HasValue && availableProjectIds.Contains(s.ProjectPriority3.Value))
                        || (s.ProjectPriority4.HasValue && availableProjectIds.Contains(s.ProjectPriority4.Value))))
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
                    Status = s.Status,
                    Photo = s.Photo,
                    RoleId = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).RoleId,
                    RoleName = s.StudentRoles.FirstOrDefault(sr => sr.IsActive).Role.Name
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} students for project-allocated-no-board-avatars", students.Count);

            return Ok(new
            {
                Success = true,
                Count = students.Count,
                Students = students
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while retrieving project-allocated-no-board-avatars: {Message}", ex.Message);
            return StatusCode(500, "Database error occurred while retrieving students");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project-allocated-no-board-avatars: {Message}", ex.Message);
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
                    Email = student.Email,
                    Status = student.Status,
                    BoardId = student.BoardId,
                    ProjectId = student.ProjectId
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
    /// Lightweight check before a student applies to a project (priority allocation).
    /// Does not enforce developer roster mix (fullstack vs FE/BE): multiple developer types may apply to the same project;
    /// composition is validated when the team can kick off / get a board (see <see cref="IKickoffService.ShouldKickoffBeTrue"/>).
    /// </summary>
    [HttpGet("use/is-allocatable/{projectId}/{studentId}")]
    public async Task<ActionResult<object>> IsStudentAllocatable(int projectId, int studentId)
    {
        try
        {
            _logger.LogInformation("Checking if student {StudentId} can be allocated to project {ProjectId}", studentId, projectId);

            var studentToAllocate = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (studentToAllocate == null)
            {
                _logger.LogWarning("Student {StudentId} not found", studentId);
                return NotFound($"Student with ID {studentId} not found");
            }

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

            _logger.LogInformation(
                "✅ Student {StudentId} may apply to project {ProjectId}; developer mix is not gated here (kickoff/board formation enforces roster rules)",
                studentId, projectId);
            return Ok(new
            {
                isAllocatable = true,
                message = "Allocation is allowed. Developer composition is validated when the team can kick off / receive a board, not at application time."
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

public class SetAssistMeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool AssistMe { get; set; }
}

