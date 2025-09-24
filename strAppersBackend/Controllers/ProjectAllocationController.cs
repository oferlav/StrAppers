using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
// using strAppersBackend.Services; // SLACK TEMPORARILY DISABLED

namespace strAppersBackend.Controllers
{
// DISABLED - Functionality moved to other controllers
[ApiController]
[Route("api/[controller]")]
public class ProjectAllocationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectAllocationController> _logger;
        // private readonly SlackService _slackService; // SLACK TEMPORARILY DISABLED

        public ProjectAllocationController(ApplicationDbContext context, ILogger<ProjectAllocationController> logger) // SlackService slackService - SLACK TEMPORARILY DISABLED
        {
            _context = context;
            _logger = logger;
            // _slackService = slackService; // SLACK TEMPORARILY DISABLED
        }

        #region Project Allocation Core Methods

        /// <summary>
        /// Get all available projects for allocation (projects without admin)
        /// </summary>
        [HttpGet("use/available-projects")]
        [Obsolete("This method is disabled. Use ProjectsController instead.")]
        public async Task<ActionResult<IEnumerable<AvailableProject>>> GetAvailableProjects()
        {
            try
            {
                var availableProjects = await _context.Projects
                    .Include(p => p.Organization)
                    .Where(p => p.IsAvailable) // Only available projects
                    .Select(p => new AvailableProject
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Description = p.Description,
                        Priority = p.Priority,
                        OrganizationName = p.Organization != null ? p.Organization.Name : "No Organization",
                        IsAvailable = p.IsAvailable
                    })
                    .OrderBy(p => p.Title)
                    .ToListAsync();

                return Ok(availableProjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available projects");
                return StatusCode(500, "An error occurred while retrieving available projects");
            }
        }

        /// <summary>
        /// Allocate a student to a project
        /// </summary>
        [HttpPost("use/allocate")]
        [Obsolete("This method is disabled. Use StudentsController instead.")]
        public async Task<ActionResult<ProjectAllocationResponse>> AllocateStudentToProject(ProjectAllocationRequest request)
        {
            try
            {
                // Validate the request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if student exists
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return NotFound($"Student with ID {request.StudentId} not found");
                }

                // Check if student is already allocated to a project
                if (student.BoardId != null)
                {
                    return BadRequest($"Student {student.FirstName} {student.LastName} is already allocated to a project");
                }

                // Check if project exists
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // Business Rule: If student wants to be admin, check if project already has admin
                bool adminAllocationFailed = false;
                string adminMessage = "";
                
                if (request.IsAdmin)
                {
                    adminAllocationFailed = true;
                    adminMessage = "We could not allocate you as Admin because an Admin is already allocated to this project";
                }

                // Allocate student to project (always happens, even if admin allocation fails)
                // Create or get ProjectBoard for this project
                var projectBoard = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.ProjectId == request.ProjectId);
                
                if (projectBoard == null)
                {
                    projectBoard = new ProjectBoard
                    {
                        Id = $"board_{request.ProjectId}_{DateTime.UtcNow.Ticks}",
                        ProjectId = request.ProjectId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ProjectBoards.Add(projectBoard);
                }
                
                student.BoardId = projectBoard.Id;
                student.IsAdmin = request.IsAdmin && !adminAllocationFailed; // Only set as admin if no conflict
                student.UpdatedAt = DateTime.UtcNow;

                // Update project admin status if student is becoming admin (and no conflict)
                if (request.IsAdmin && !adminAllocationFailed)
                {
                    // Admin status is now handled in ProjectBoard
                    project.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Load updated data for response
                var updatedStudent = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                var updatedProject = await _context.Projects
                    .Include(p => p.Organization)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                _logger.LogInformation("Student {StudentId} allocated to project {ProjectId} with admin status: {IsAdmin}", 
                    request.StudentId, request.ProjectId, request.IsAdmin);

                string successMessage = $"Student successfully allocated to project '{project.Title}'";
                if (request.IsAdmin && !adminAllocationFailed)
                {
                    successMessage += " as admin";
                }
                else if (adminAllocationFailed)
                {
                    successMessage += $". {adminMessage}";
                }

                return Ok(new ProjectAllocationResponse
                {
                    Success = true,
                    Message = successMessage,
                    Student = updatedStudent,
                    Project = updatedProject
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while allocating student to project");
                return StatusCode(500, "An error occurred while saving the allocation to the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while allocating student to project");
                return StatusCode(500, "An unexpected error occurred while processing the allocation");
            }
        }

        /// <summary>
        /// Remove student from project allocation
        /// </summary>
        [HttpPost("use/deallocate/{studentId}")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult<ProjectAllocationResponse>> DeallocateStudent(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return NotFound($"Student with ID {studentId} not found");
                }

                if (student.BoardId == null)
                {
                    return BadRequest($"Student {student.FirstName} {student.LastName} is not allocated to any project");
                }

                var projectBoard = await _context.ProjectBoards
                    .Include(pb => pb.Project)
                    .FirstOrDefaultAsync(pb => pb.Id == student.BoardId);
                var project = projectBoard?.Project;

                // If student was admin, update project admin status
                if (student.IsAdmin && project != null)
                {
                    // Admin status is now handled in ProjectBoard
                    project.UpdatedAt = DateTime.UtcNow;
                }

                // Remove allocation
                var projectTitle = project?.Title ?? "Unknown Project";
                student.BoardId = null;
                student.IsAdmin = false;
                student.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Student {StudentId} deallocated from project", studentId);

                return Ok(new ProjectAllocationResponse
                {
                    Success = true,
                    Message = $"Student successfully removed from project '{projectTitle}'",
                    Student = student,
                    Project = null
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while deallocating student from project");
                return StatusCode(500, "An error occurred while saving the deallocation to the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deallocating student from project");
                return StatusCode(500, "An unexpected error occurred while processing the deallocation");
            }
        }

        #endregion

        #region Project Status Management

        /// <summary>
        /// Change project status from 'New' (1) to 'Planning' (2) - Admin only
        /// </summary>
        [HttpPost("change-status-to-planning")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult<ChangeProjectStatusResponse>> ChangeProjectStatusToPlanning(ChangeProjectStatusRequest request)
        {
            try
            {
                // Validate the request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if student exists and is allocated to the project
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return NotFound($"Student with ID {request.StudentId} not found");
                }

                // Check if student is allocated to the project
                if (student.BoardId == null)
                {
                    return BadRequest($"Student {student.FirstName} {student.LastName} is not allocated to this project");
                }

                // Business Rule: Only Admin can change project status
                if (!student.IsAdmin)
                {
                    return BadRequest("Only Admin user can activate the project");
                }

                // Get the project
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // Check if project status is 'New' (StatusId = 1)
                // DISABLED - Project.StatusId removed (moved to ProjectBoard)
                if (false) // Always allow for now
                {
                    return BadRequest($"Project status can only be changed from 'New' to 'Planning'.");
                }

                // Business Rule: Project must have a student with Backend Developer role
                // DISABLED - Project.Students removed
                var hasBackendDeveloper = false; // Always false for now
                // .Any(s => s.StudentRoles.Any(sr => sr.Role.Name == "Backend Developer" && sr.IsActive));

                if (!hasBackendDeveloper)
                {
                    return BadRequest("Project status can only be changed from 'New' to 'Planning' if it has a student with Backend Developer role allocated");
                }

                // Change project status to 'Planning' (StatusId = 2)
                // DISABLED - Project.StatusId removed (moved to ProjectBoard)
                // project.StatusId = 2;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Automatically create Slack team when project moves to Planning status
                try
                {
                    _logger.LogInformation("Project {ProjectId} moved to Planning status. Creating Slack team automatically.", project.Id);
                    
                    // DISABLED - Project.Students removed
                    var students = new List<Student>(); // Empty for now
                    // var slackResult = await _slackService.CreateProjectTeamAsync(project, students); // SLACK TEMPORARILY DISABLED
                    
                    // if (slackResult.Success) // SLACK TEMPORARILY DISABLED
                    // {
                    //     // Send welcome message
                    //     await _slackService.SendWelcomeMessageAsync(slackResult.ChannelId!, project, students);
                    //     _logger.LogInformation("Slack team created successfully for project {ProjectId}. Channel: {ChannelName}", 
                    //         project.Id, slackResult.ChannelName);
                    // }
                    // else
                    // {
                    //     _logger.LogWarning("Failed to create Slack team for project {ProjectId}: {Error}", 
                    //         project.Id, slackResult.ErrorMessage);
                    // }
                }
                catch (Exception slackEx)
                {
                    _logger.LogError(slackEx, "Error creating Slack team for project {ProjectId} during status change", project.Id);
                    // Don't fail the status change if Slack creation fails
                }

                // Load updated project data for response
                var updatedProject = await _context.Projects
                    .Include(p => p.Organization)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                _logger.LogInformation("Project {ProjectId} status changed from 'New' to 'Planning' by admin student {StudentId}", 
                    request.ProjectId, request.StudentId);

                return Ok(new ChangeProjectStatusResponse
                {
                    Success = true,
                    Message = $"Project '{project.Title}' status successfully changed from 'New' to 'Planning'",
                    Project = updatedProject
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while changing project status");
                return StatusCode(500, "An error occurred while updating the project status in the database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while changing project status");
                return StatusCode(500, "An unexpected error occurred while changing the project status");
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get students allocated to a specific project (for frontend use)
        /// </summary>
        [HttpGet("use/project/{projectId}/students")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult<IEnumerable<Student>>> GetProjectStudentsForFrontend(int projectId)
        {
            try
            {
                // First verify the project exists
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {projectId} not found");
                }

                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .Where(s => s.BoardId != null)
                    .OrderBy(s => s.IsAdmin ? 0 : 1) // Admins first
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {StudentCount} students for project {ProjectId} ({ProjectTitle})", 
                    students.Count, projectId, project.Title);

                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving students for the project");
            }
        }

        /// <summary>
        /// Get students allocated to a specific project (internal use)
        /// </summary>
        [HttpGet("project/{projectId}/students")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult<IEnumerable<Student>>> GetProjectStudents(int projectId)
        {
            try
            {
                var students = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                    .Where(s => s.BoardId != null)
                    .OrderBy(s => s.IsAdmin ? 0 : 1) // Admins first
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while retrieving students for the project");
            }
        }

        /// <summary>
        /// Get projects allocated to a specific student
        /// </summary>
        [HttpGet("student/{studentId}/project")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult<Project>> GetStudentProject(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.ProjectBoard!)
                    .ThenInclude(pb => pb!.Project)
                    .ThenInclude(p => p!.Organization)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return NotFound($"Student with ID {studentId} not found");
                }

                if (student.BoardId == null)
                {
                    return NotFound($"Student {student.FirstName} {student.LastName} is not allocated to any project");
                }

                return Ok(new
                {
                    student = new
                    {
                        id = student.Id,
                        firstName = student.FirstName,
                        lastName = student.LastName,
                        email = student.Email,
                        boardId = student.BoardId
                    },
                    project = student.ProjectBoard?.Project != null ? new
                    {
                        id = student.ProjectBoard.Project.Id,
                        title = student.ProjectBoard.Project.Title,
                        description = student.ProjectBoard.Project.Description
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project for student {StudentId}", studentId);
                return StatusCode(500, "An error occurred while retrieving the student's project");
            }
        }

        #endregion

        #region Join Requests Management

        /// <summary>
        /// Get all pending join requests
        /// </summary>
        [HttpGet("join-requests")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult> GetJoinRequests()
        {
            try
            {
                var joinRequests = await _context.JoinRequests
                    .Where(jr => !jr.Added)
                    .OrderByDescending(jr => jr.JoinDate)
                    .Select(jr => new
                    {
                        jr.Id,
                        jr.ChannelName,
                        jr.ChannelId,
                        jr.StudentEmail,
                        jr.StudentFirstName,
                        jr.StudentLastName,
                        jr.ProjectTitle,
                        jr.JoinDate,
                        jr.Notes,
                        jr.ErrorMessage
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = joinRequests.Count,
                    joinRequests = joinRequests
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving join requests");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Mark a join request as added (manually invited)
        /// </summary>
        [HttpPut("join-requests/{id}/mark-added")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult> MarkJoinRequestAsAdded(int id)
        {
            try
            {
                var joinRequest = await _context.JoinRequests.FindAsync(id);
                if (joinRequest == null)
                {
                    return NotFound($"Join request with ID {id} not found");
                }

                joinRequest.Added = true;
                joinRequest.AddedDate = DateTime.UtcNow;
                joinRequest.Notes = "Manually invited to Slack workspace";

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Join request marked as added",
                    joinRequest = new
                    {
                        joinRequest.Id,
                        joinRequest.StudentEmail,
                        joinRequest.ChannelName,
                        joinRequest.Added,
                        joinRequest.AddedDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking join request {Id} as added", id);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get join requests statistics
        /// </summary>
        [HttpGet("join-requests/stats")]
        [Obsolete("This method is disabled.")]
        public async Task<ActionResult> GetJoinRequestsStats()
        {
            try
            {
                var totalRequests = await _context.JoinRequests.CountAsync();
                var pendingRequests = await _context.JoinRequests.CountAsync(jr => !jr.Added);
                var addedRequests = await _context.JoinRequests.CountAsync(jr => jr.Added);

                var recentRequests = await _context.JoinRequests
                    .Where(jr => jr.JoinDate >= DateTime.UtcNow.AddDays(-7))
                    .CountAsync();

                return Ok(new
                {
                    success = true,
                    stats = new
                    {
                        totalRequests,
                        pendingRequests,
                        addedRequests,
                        recentRequests
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving join requests statistics");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        #endregion
    }
}