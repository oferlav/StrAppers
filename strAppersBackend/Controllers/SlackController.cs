// SLACK CONTROLLER TEMPORARILY DISABLED
/*
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
// using strAppersBackend.Services; // SLACK TEMPORARILY DISABLED
using System.ComponentModel.DataAnnotations;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SlackController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SlackController> _logger;
        // private readonly SlackService _slackService; // SLACK TEMPORARILY DISABLED

        public SlackController(ApplicationDbContext context, ILogger<SlackController> logger) // SlackService slackService - SLACK TEMPORARILY DISABLED
        {
            _context = context;
            _logger = logger;
            // _slackService = slackService; // SLACK TEMPORARILY DISABLED
        }

        #region Debug Methods

        /// <summary>
        /// Debug endpoint to test basic functionality
        /// </summary>
        [HttpPost("debug/test-response")]
        public async Task<ActionResult> TestResponse([FromBody] CreateProjectTeamWithStatusRequest request)
        {
            try
            {
                _logger.LogInformation("Testing response creation for project {ProjectId}", request.ProjectId);
                
                // Just return a simple response to test if the issue is with response serialization
                return Ok(new
                {
                    Success = true,
                    Message = "Debug test successful",
                    ProjectId = request.ProjectId,
                    RequestStudentId = request.RequestStudentId,
                    StudentCount = request.StudentIds.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug test failed: {Error}", ex.Message);
                return StatusCode(500, $"Debug test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Simplified version of team creation for debugging
        /// </summary>
        [HttpPost("debug/simple-team-creation")]
        public async Task<ActionResult> SimpleTeamCreation([FromBody] CreateProjectTeamWithStatusRequest request)
        {
            try
            {
                _logger.LogInformation("Starting simplified team creation for project {ProjectId}", request.ProjectId);
                
                // Just do the basic operations and return minimal response
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
                if (project == null)
                {
                    return NotFound($"Project {request.ProjectId} not found");
                }
                
                var students = await _context.Students
                    .Where(s => request.StudentIds.Contains(s.Id))
                    .ToListAsync();
                
                // Return minimal success response
                return Ok(new
                {
                    Success = true,
                    Message = "Simplified team creation test successful",
                    ProjectId = project.Id,
                    ProjectTitle = project.Title,
                    StudentCount = students.Count,
                    StudentNames = students.Select(s => $"{s.FirstName} {s.LastName}").ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simplified team creation failed: {Error}", ex.Message);
                return StatusCode(500, $"Simplified test failed: {ex.Message}");
            }
        }

        #endregion

        #region Slack Team Management

        /// <summary>
        /// MAIN METHOD: Create new Slack channel, add team members, update join requests, and change project status
        /// This is the primary method for creating Slack teams with full workflow integration
        /// </summary>
        [HttpPost("use/create-project-team-with-status-change")]
        public async Task<ActionResult<SlackTeamCreationWithStatusResult>> CreateProjectTeamWithStatusChange([FromBody] CreateProjectTeamWithStatusRequest request)
        {
            try
            {
                // Validate the request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Starting complete project team creation workflow for project {ProjectId} with {StudentCount} students by request student {RequestStudentId}", 
                    request.ProjectId, request.StudentIds.Count, request.RequestStudentId);

                // 1. Check if requesting student is admin
                var requestStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == request.RequestStudentId);

                if (requestStudent == null)
                {
                    return NotFound($"Requesting student with ID {request.RequestStudentId} not found");
                }

                if (!requestStudent.IsAdmin)
                {
                    return BadRequest("Only admins can create teams");
                }

                // 2. Get project with current students
                var project = await _context.Projects
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Major)
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Year)
                    .Include(p => p.Organization)
                    .Include(p => p.Status)
                    .Include(p => p.Students)
                        .ThenInclude(s => s.StudentRoles)
                            .ThenInclude(sr => sr.Role)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // 3. Validate project status is 'New' (StatusId = 1)
                if (project.StatusId != 1)
                {
                    return BadRequest($"Project status must be 'New' to create team. Current status is '{project.Status.Name}'");
                }

                // 4. Get students by IDs and validate they exist
                var students = await _context.Students
                    .Include(s => s.Major)
                    .Include(s => s.Year)
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .Where(s => request.StudentIds.Contains(s.Id))
                    .ToListAsync();

                if (students.Count != request.StudentIds.Count)
                {
                    var foundIds = students.Select(s => s.Id).ToList();
                    var missingIds = request.StudentIds.Except(foundIds).ToList();
                    return BadRequest($"Some students not found. Missing student IDs: {string.Join(", ", missingIds)}");
                }

                // 5. Business Rule: Project must have a student with Backend Developer role (Role.Id = 3)
                var hasBackendDeveloper = students
                    .Any(s => s.StudentRoles.Any(sr => sr.Role.Id == 3 && sr.IsActive));

                if (!hasBackendDeveloper)
                {
                    return BadRequest("Project must have at least one student with Backend Developer role to create team");
                }

                // 6. Business Rule: At least one student must be admin
                var adminStudents = students.Where(s => s.IsAdmin).ToList();
                if (!adminStudents.Any())
                {
                    return BadRequest("At least one student must be designated as admin to create team");
                }

                // 7. Create Slack team
                _logger.LogInformation("Creating Slack team for project {ProjectId} with {StudentCount} students", project.Id, students.Count);
                var slackResult = await _slackService.CreateProjectTeamAsync(project, students);

                if (!slackResult.Success)
                {
                    _logger.LogError("Failed to create Slack team for project {ProjectId}: {Error}", 
                        project.Id, slackResult.ErrorMessage);
                    return BadRequest($"Failed to create Slack team: {slackResult.ErrorMessage}");
                }

                _logger.LogInformation("Successfully created Slack team for project {ProjectId}. Channel: {ChannelName}, ID: {ChannelId}", 
                    project.Id, slackResult.ChannelName, slackResult.ChannelId);

                // 8. Send welcome message if requested
                if (request.SendWelcomeMessage)
                {
                    _logger.LogInformation("Sending welcome message to Slack channel {ChannelId} for project {ProjectId}", 
                        slackResult.ChannelId, project.Id);
                    try
                    {
                        slackResult.WelcomeMessageSent = await _slackService.SendWelcomeMessageAsync(
                            slackResult.ChannelId!, project, students);
                        _logger.LogInformation("Welcome message sent successfully for project {ProjectId}: {Sent}", 
                            project.Id, slackResult.WelcomeMessageSent);
                    }
                    catch (Exception welcomeEx)
                    {
                        _logger.LogWarning(welcomeEx, "Failed to send welcome message for project {ProjectId}, continuing without it", project.Id);
                        slackResult.WelcomeMessageSent = false;
                    }
                }

                // 9. Update JoinRequests table with all students
                _logger.LogInformation("Creating {StudentCount} join requests for project {ProjectId}", students.Count, project.Id);
                
                // Check for existing join requests to avoid duplicates
                var existingJoinRequests = await _context.JoinRequests
                    .Where(jr => jr.ProjectId == project.Id && !jr.Added)
                    .ToListAsync();
                
                if (existingJoinRequests.Any())
                {
                    _logger.LogWarning("Found {ExistingCount} existing unprocessed join requests for project {ProjectId}. " +
                        "This might indicate a previous incomplete team creation. Will clean up existing requests first.", existingJoinRequests.Count, project.Id);
                    
                    // Remove existing unprocessed join requests to avoid conflicts
                    _context.JoinRequests.RemoveRange(existingJoinRequests);
                    _logger.LogInformation("Removed {RemovedCount} existing join requests for project {ProjectId}", existingJoinRequests.Count, project.Id);
                }
                
                var joinRequests = new List<JoinRequest>();
                foreach (var student in students)
                {
                    // Validate required fields before creating join request
                    if (string.IsNullOrEmpty(student.Email))
                    {
                        _logger.LogError("Student {StudentId} has no email address", student.Id);
                        return BadRequest($"Student {student.Id} ({student.FirstName} {student.LastName}) has no email address. Cannot create join request.");
                    }
                    
                    if (string.IsNullOrEmpty(slackResult.ChannelName))
                    {
                        _logger.LogError("Slack channel name is null or empty for project {ProjectId}", project.Id);
                        return BadRequest("Slack channel name is missing. Cannot create join requests.");
                    }
                    
                    if (string.IsNullOrEmpty(slackResult.ChannelId))
                    {
                        _logger.LogError("Slack channel ID is null or empty for project {ProjectId}", project.Id);
                        return BadRequest("Slack channel ID is missing. Cannot create join requests.");
                    }

                    var joinRequest = new JoinRequest
                    {
                        ChannelName = slackResult.ChannelName,
                        ChannelId = slackResult.ChannelId,
                        StudentId = student.Id, // Added missing StudentId field
                        StudentEmail = student.Email,
                        StudentFirstName = student.FirstName,
                        StudentLastName = student.LastName,
                        ProjectTitle = project.Title,
                        ProjectId = project.Id,
                        JoinDate = DateTime.UtcNow,
                        Notes = "Automatically created during team setup",
                        Added = false // Will be marked as added when student joins workspace
                    };
                    joinRequests.Add(joinRequest);
                }

                _logger.LogInformation("Adding {JoinRequestCount} join requests to database for project {ProjectId}", joinRequests.Count, project.Id);
                
                // Log details of each join request being created
                foreach (var jr in joinRequests)
                {
                    _logger.LogDebug("Creating join request: StudentId={StudentId}, Email={Email}, ChannelName={ChannelName}, ChannelId={ChannelId}", 
                        jr.StudentId, jr.StudentEmail, jr.ChannelName, jr.ChannelId);
                }
                
                _context.JoinRequests.AddRange(joinRequests);

                // 10. Change project status from 'New' (1) to 'Planning' (2)
                _logger.LogInformation("Updating project {ProjectId} status from {OldStatus} to Planning", project.Id, project.StatusId);
                project.StatusId = 2;
                project.UpdatedAt = DateTime.UtcNow;

                // 11. Save all changes
                _logger.LogInformation("Saving all changes to database for project {ProjectId}: {JoinRequestCount} join requests, project status update", 
                    project.Id, joinRequests.Count);
                
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved all changes to database for project {ProjectId}", project.Id);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database save failed for project {ProjectId}. Inner exception: {InnerException}. " +
                        "Stack trace: {StackTrace}", project.Id, dbEx.InnerException?.Message, dbEx.StackTrace);
                    
                    // Log the specific entities that were being saved
                    _logger.LogError("Failed to save: {JoinRequestCount} join requests and project status update for project {ProjectId}", 
                        joinRequests.Count, project.Id);
                    
                    // Check if it's a specific constraint violation
                    var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    string specificError;
                    
                    if (errorMessage.Contains("UNIQUE constraint") || errorMessage.Contains("duplicate key"))
                    {
                        specificError = $"Duplicate data detected for project {project.Id}. This might be due to existing join requests or project status conflicts. " +
                            $"Error: {errorMessage}";
                    }
                    else if (errorMessage.Contains("FOREIGN KEY constraint"))
                    {
                        specificError = $"Referenced data not found for project {project.Id}. Please verify that all project and student IDs are valid. " +
                            $"Error: {errorMessage}";
                    }
                    else if (errorMessage.Contains("NOT NULL constraint"))
                    {
                        specificError = $"Required field is missing for project {project.Id}. Please check that all required data is provided. " +
                            $"Error: {errorMessage}";
                    }
                    else
                    {
                        specificError = $"Database error during team creation for project {project.Id}: {errorMessage}";
                    }
                    
                    // Re-throw to be caught by the outer catch block with more context
                    throw new InvalidOperationException(specificError, dbEx);
                }

                // 12. Load updated project data for response
                _logger.LogInformation("Loading updated project data for response for project {ProjectId}", project.Id);
                
                Project? updatedProject = null;
                try
                {
                    updatedProject = await _context.Projects
                        .Include(p => p.Organization)
                        .Include(p => p.Status)
                        .Include(p => p.Students)
                        .FirstOrDefaultAsync(p => p.Id == request.ProjectId);
                    
                    _logger.LogInformation("Successfully loaded updated project data for project {ProjectId}. " +
                        "Organization: {OrgName}, Status: {StatusName}, Students: {StudentCount}", 
                        project.Id, updatedProject?.Organization?.Name, updatedProject?.Status?.Name, updatedProject?.Students?.Count);
                }
                catch (Exception loadEx)
                {
                    _logger.LogError(loadEx, "Error loading updated project data for project {ProjectId}", project.Id);
                    // Continue without the updated project data
                }

                if (updatedProject == null)
                {
                    _logger.LogWarning("Could not load updated project data for project {ProjectId}, will use original project data", project.Id);
                    updatedProject = project; // Use the original project data
                }

                _logger.LogInformation("Successfully completed project team creation workflow for project {ProjectId}. " +
                    "Slack team created: {ChannelName}, Project status changed to Planning, {JoinRequestCount} join requests created", 
                    project.Id, slackResult.ChannelName, joinRequests.Count);

                _logger.LogInformation("Creating response object for project {ProjectId}", project.Id);
                
                try
                {
                    // Recalculate admin students to ensure we have the current data
                    var currentAdminStudents = students.Where(s => s.IsAdmin).ToList();
                    _logger.LogInformation("Found {AdminCount} admin students for project {ProjectId}", currentAdminStudents.Count, project.Id);
                    
                    var response = new SlackTeamCreationWithStatusResult
                    {
                        Success = true,
                        Message = $"Project team created successfully! Slack channel '{slackResult.ChannelName}' created, project status changed to 'Planning', and {joinRequests.Count} join requests created.",
                        Project = updatedProject,
                        SlackTeam = new SlackTeamInfo
                        {
                            ProjectId = project.Id,
                            ProjectTitle = project.Title,
                            ChannelId = slackResult.ChannelId,
                            ChannelName = slackResult.ChannelName,
                            TeamName = slackResult.TeamName,
                            TotalMembers = students.Count,
                            AdminCount = currentAdminStudents.Count,
                            RegularMemberCount = students.Count - currentAdminStudents.Count,
                            Members = students.Select(s => new SlackMemberInfo
                            {
                                StudentId = s.Id,
                                FirstName = s.FirstName,
                                LastName = s.LastName,
                                Email = s.Email,
                                IsAdmin = s.IsAdmin,
                                IsInSlack = false // Will be updated when they join
                            }).ToList()
                        },
                        SlackCreationResult = slackResult,
                        JoinRequestsCreated = joinRequests.Count,
                        ProjectStatusChanged = true,
                        NewProjectStatus = "Planning"
                    };
                    
                    _logger.LogInformation("Successfully created response object for project {ProjectId}", project.Id);
                    return Ok(response);
                }
                catch (Exception responseEx)
                {
                    _logger.LogError(responseEx, "Error creating response object for project {ProjectId}. Stack trace: {StackTrace}", 
                        project.Id, responseEx.StackTrace);
                    
                    // Return a simplified success response since the team was created successfully
                    return Ok(new SlackTeamCreationWithStatusResult
                    {
                        Success = true,
                        Message = $"Project team created successfully! Slack channel '{slackResult.ChannelName}' created, project status changed to 'Planning', and {joinRequests.Count} join requests created. (Note: Some response details may be incomplete due to a response generation issue)",
                        Project = null, // Simplified response
                        SlackTeam = null, // Simplified response
                        SlackCreationResult = null, // Simplified response
                        JoinRequestsCreated = joinRequests.Count,
                        ProjectStatusChanged = true,
                        NewProjectStatus = "Planning"
                    });
                }
            }
            catch (InvalidOperationException ex) when (ex.InnerException is DbUpdateException)
            {
                _logger.LogError(ex, "Database operation failed during project team creation workflow for project {ProjectId}. " +
                    "Slack team may have been created successfully but database tracking failed.", request.ProjectId);
                
                // Return a partial success response since Slack team was created
                return StatusCode(500, new SlackTeamCreationWithStatusResult
                {
                    Success = false,
                    Message = $"Slack team was created successfully, but database tracking failed: {ex.Message}. " +
                             "Please check the Slack workspace for the new team and manually update the project status if needed.",
                    Project = null,
                    SlackTeam = null,
                    SlackCreationResult = null,
                    JoinRequestsCreated = 0,
                    ProjectStatusChanged = false,
                    NewProjectStatus = null
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during project team creation workflow for project {ProjectId}", request.ProjectId);
                
                // Provide more specific error messages based on the exception
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                
                if (errorMessage.Contains("UNIQUE constraint") || errorMessage.Contains("duplicate key"))
                {
                    return StatusCode(500, "Database error: Duplicate data detected. This might be due to existing join requests or project status conflicts.");
                }
                else if (errorMessage.Contains("FOREIGN KEY constraint"))
                {
                    return StatusCode(500, "Database error: Referenced data not found. Please verify that all project and student IDs are valid.");
                }
                else if (errorMessage.Contains("NOT NULL constraint"))
                {
                    return StatusCode(500, "Database error: Required field is missing. Please check that all required data is provided.");
                }
                else
                {
                    return StatusCode(500, $"Database error during team creation: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during project team creation workflow for project {ProjectId}. " +
                    "Error: {ErrorMessage}, Stack trace: {StackTrace}", 
                    request.ProjectId, ex.Message, ex.StackTrace);
                
                // Return a basic success response since the team was likely created
                return Ok(new
                {
                    Success = true,
                    Message = "Team creation completed with some issues. Please check the logs for details.",
                    ProjectId = request.ProjectId,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Create a Slack team for a project with all allocated students
        /// </summary>
        [HttpPost("create-team")]
        public async Task<ActionResult<SlackTeamCreationResult>> CreateSlackTeam([FromBody] SlackTeamCreationRequest request)
        {
            try
            {
                // Get project with allocated students
                var project = await _context.Projects
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Major)
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Year)
                    .Include(p => p.Organization)
                    .Include(p => p.Status)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // Get all allocated students
                var students = project.Students.ToList();
                if (!students.Any())
                {
                    return BadRequest("No students allocated to this project. Cannot create Slack team without members.");
                }

                _logger.LogInformation("Creating Slack team for project {ProjectId} with {StudentCount} students", 
                    project.Id, students.Count);

                // Create Slack team
                var result = await _slackService.CreateProjectTeamAsync(project, students);

                if (result.Success && request.SendWelcomeMessage)
                {
                    // Send welcome message
                    result.WelcomeMessageSent = await _slackService.SendWelcomeMessageAsync(
                        result.ChannelId!, project, students);
                }

                _logger.LogInformation("Slack team creation completed for project {ProjectId}. Success: {Success}", 
                    project.Id, result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Slack team for project {ProjectId}", request.ProjectId);
                return StatusCode(500, "An error occurred while creating the Slack team");
            }
        }

        /// <summary>
        /// Get Slack team information for a project
        /// </summary>
        [HttpGet("team-info/{projectId}")]
        public async Task<ActionResult<SlackTeamInfo>> GetSlackTeamInfo(int projectId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Major)
                    .Include(p => p.Students)
                        .ThenInclude(s => s.Year)
                    .Include(p => p.Organization)
                    .Include(p => p.Status)
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {projectId} not found");
                }

                var students = project.Students.ToList();
                var adminStudents = students.Where(s => s.IsAdmin).ToList();
                var regularStudents = students.Where(s => !s.IsAdmin).ToList();

                var teamInfo = new SlackTeamInfo
                {
                    ProjectId = project.Id,
                    ProjectTitle = project.Title,
                    TeamName = GenerateTeamName(project),
                    TotalMembers = students.Count,
                    AdminCount = adminStudents.Count,
                    RegularMemberCount = regularStudents.Count,
                    Members = students.Select(s => new SlackMemberInfo
                    {
                        StudentId = s.Id,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        Email = s.Email,
                        IsAdmin = s.IsAdmin,
                        IsInSlack = false // This would need to be checked via Slack API
                    }).ToList()
                };

                return Ok(teamInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Slack team info for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while getting Slack team information");
            }
        }

        /// <summary>
        /// Join a Slack channel
        /// </summary>
        [HttpPost("join-channel")]
        public async Task<ActionResult> JoinSlackChannel([FromBody] JoinChannelRequest request)
        {
            try
            {
                var result = await _slackService.JoinChannelAsync(request.ChannelId);
                
                if (result)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Successfully joined the Slack channel",
                        channelId = request.ChannelId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to join the Slack channel"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining Slack channel {ChannelId}", request.ChannelId);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "An unexpected error occurred while joining the channel"
                });
            }
        }

        #endregion

        #region Slack Diagnostics

        /// <summary>
        /// Test basic Slack connection
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<ActionResult> TestSlackConnection()
        {
            try
            {
                // Test basic Slack API connection
                var result = await _slackService.TestConnectionAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Slack connection test successful",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack connection");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack connection test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack bot information
        /// </summary>
        [HttpGet("test-bot-info")]
        public async Task<ActionResult> TestSlackBotInfo()
        {
            try
            {
                // Test bot info
                var result = await _slackService.TestBotInfoAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Slack bot info test successful",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack bot info");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack bot info test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack user lookup by email
        /// </summary>
        [HttpPost("test-user-lookup")]
        public async Task<ActionResult> TestSlackUserLookup([FromBody] TestUserLookupRequest request)
        {
            try
            {
                // Test user lookup
                var result = await _slackService.TestUserLookupAsync(request.Email);
                
                return Ok(new
                {
                    success = true,
                    message = "Slack user lookup test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack user lookup");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack user lookup test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack users list API
        /// </summary>
        [HttpGet("test-users-list")]
        public async Task<ActionResult> TestSlackUsersList()
        {
            try
            {
                // Test users list API
                var result = await _slackService.TestUsersListAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Slack users list test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack users list");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack users list test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack bot permissions
        /// </summary>
        [HttpGet("test-bot-permissions")]
        public async Task<ActionResult> TestSlackBotPermissions()
        {
            try
            {
                // Test bot permissions
                var result = await _slackService.TestBotPermissionsAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Slack bot permissions test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack bot permissions");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack bot permissions test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack channel visibility
        /// </summary>
        [HttpGet("test-channel-visibility/{channelId}")]
        public async Task<ActionResult> TestSlackChannelVisibility(string channelId)
        {
            try
            {
                // Test channel visibility
                var result = await _slackService.TestChannelVisibilityAsync(channelId);
                
                return Ok(new
                {
                    success = true,
                    message = "Slack channel visibility test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack channel visibility for channel {ChannelId}", channelId);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack channel visibility test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack channel membership
        /// </summary>
        [HttpGet("test-channel-membership/{channelId}")]
        public async Task<ActionResult> TestSlackChannelMembership(string channelId)
        {
            try
            {
                // Test channel membership
                var result = await _slackService.TestChannelMembershipAsync(channelId);
                
                return Ok(new
                {
                    success = true,
                    message = "Slack channel membership test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack channel membership for channel {ChannelId}", channelId);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack channel membership test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack channels list
        /// </summary>
        [HttpGet("test-channels-list")]
        public async Task<ActionResult> TestSlackChannelsList()
        {
            try
            {
                // Test channels list
                var result = await _slackService.TestChannelsListAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "Slack channels list test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack channels list");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack channels list test failed"
                });
            }
        }

        /// <summary>
        /// Test detailed Slack channel creation
        /// </summary>
        [HttpPost("test-channel-creation-detailed")]
        public async Task<ActionResult> TestSlackChannelCreationDetailed([FromBody] TestChannelCreationDetailedRequest request)
        {
            try
            {
                // Test detailed channel creation
                var result = await _slackService.TestChannelCreationWithDetailsAsync(request.TeamName);
                
                return Ok(new
                {
                    success = true,
                    message = "Slack detailed channel creation test completed",
                    details = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing detailed Slack channel creation for team {TeamName}", request.TeamName);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack detailed channel creation test failed"
                });
            }
        }

        /// <summary>
        /// Test Slack channel deletion
        /// </summary>
        [HttpDelete("test-delete-channel/{channelId}")]
        public async Task<ActionResult> TestSlackDeleteChannel(string channelId)
        {
            try
            {
                // Test channel deletion
                var result = await _slackService.DeleteChannelAsync(channelId);
                
                return Ok(new
                {
                    success = result,
                    message = result ? "Channel archived successfully" : "Failed to archive channel",
                    channelId = channelId,
                    note = "Slack channels are archived, not deleted. Archived channels can be restored."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack channel deletion for channel: {ChannelId}", channelId);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    channelId = channelId
                });
            }
        }

        #endregion

        #region Slack Test Workflows

        /// <summary>
        /// Test the complete Slack team creation workflow with improved user experience
        /// </summary>
        [HttpPost("test-team-creation-workflow")]
        public async Task<ActionResult> TestSlackTeamCreationWorkflow([FromBody] TestSlackTeamWorkflowRequest request)
        {
            try
            {
                _logger.LogInformation("Testing complete Slack team creation workflow for project {ProjectId} with email {Email}", 
                    request.ProjectId, request.TestEmail);

                // Get project
                var project = await _context.Projects
                    .Include(p => p.Organization)
                    .Include(p => p.Status)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // Create a test student object for the email
                var testStudent = new Student
                {
                    Id = 999, // Test ID
                    FirstName = "Test",
                    LastName = "User",
                    Email = request.TestEmail,
                    MajorId = 1,
                    YearId = 3,
                    LinkedInUrl = "https://linkedin.com/in/testuser",
                    OrganizationId = project.OrganizationId,
                    ProjectId = project.Id,
                    IsAdmin = false
                };

                var students = new List<Student> { testStudent };

                // Create Slack team using the updated workflow
                var result = await _slackService.CreateProjectTeamAsync(project, students);

                if (result.Success)
                {
                    // Send welcome message
                    result.WelcomeMessageSent = await _slackService.SendWelcomeMessageAsync(
                        result.ChannelId!, project, students);

                    _logger.LogInformation("Test Slack team creation workflow completed successfully. Channel: {ChannelName}", 
                        result.ChannelName);

                    return Ok(new
                    {
                        success = true,
                        message = "Slack team creation workflow test completed successfully!",
                        channelId = result.ChannelId,
                        channelName = result.ChannelName,
                        teamName = result.TeamName,
                        welcomeMessageSent = result.WelcomeMessageSent,
                        testEmail = request.TestEmail,
                        projectTitle = project.Title,
                        projectDescription = project.Description,
                        organizationName = project.Organization?.Name,
                        memberResults = result.MemberResults,
                        instructions = new
                        {
                            step1 = "Contact the workspace administrator to request a Slack invitation",
                            step2 = "Provide your email address to the administrator",
                            step3 = "Once you receive and accept the invitation, you'll be automatically added to the project channel",
                            step4 = "You'll see a welcome message with project details"
                        },
                        contactInfo = new
                        {
                            adminEmail = "admin@techuniversity.edu", // You should configure this
                            adminName = "Project Administrator",
                            workspaceName = "StrAppers",
                            note = "Contact the administrator to request a Slack workspace invitation"
                        },
                        links = new
                        {
                            channelDirect = $"https://team1-odc9167.slack.com/channels/{result.ChannelName}",
                            note = "Channel link will work only after you're invited to the workspace"
                        },
                        workflowFeatures = new
                        {
                            creatorInvisible = "Channel creator is automatically removed to maintain invisibility",
                            botInvited = "Bot is automatically invited to send welcome messages",
                            privateChannel = "Private channel created for project security",
                            automaticStudentAddition = "Students are automatically added once they're in the workspace"
                        }
                    });
                }
                else
                {
                    _logger.LogError("Test Slack team creation workflow failed: {Error}", result.ErrorMessage);
                    return BadRequest(new
                    {
                        success = false,
                        message = "Slack team creation workflow test failed",
                        error = result.ErrorMessage,
                        projectId = request.ProjectId,
                        testEmail = request.TestEmail
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Slack team creation workflow for project {ProjectId}", request.ProjectId);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "Slack team creation workflow test failed"
                });
            }
        }

        /// <summary>
        /// TEST ENDPOINT: Create a test Slack channel with a single user
        /// </summary>
        [HttpPost("test-channel")]
        public async Task<ActionResult<object>> TestSlackChannel([FromBody] TestSlackChannelRequest request)
        {
            try
            {
                _logger.LogInformation("Testing Slack channel creation for project {ProjectId} with email {Email}", 
                    request.ProjectId, request.TestEmail);

                // Get project
                var project = await _context.Projects
                    .Include(p => p.Organization)
                    .Include(p => p.Status)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {request.ProjectId} not found");
                }

                // Create a test student object for the email
                var testStudent = new Student
                {
                    Id = 999, // Test ID
                    FirstName = "Test",
                    LastName = "User",
                    Email = request.TestEmail,
                    MajorId = 1,
                    YearId = 3,
                    LinkedInUrl = "https://linkedin.com/in/testuser",
                    OrganizationId = project.OrganizationId,
                    ProjectId = project.Id,
                    IsAdmin = false
                };

                var students = new List<Student> { testStudent };

                // Create Slack team
                var result = await _slackService.CreateProjectTeamAsync(project, students);

                if (result.Success)
                {
                    // Send welcome message
                    result.WelcomeMessageSent = await _slackService.SendWelcomeMessageAsync(
                        result.ChannelId!, project, students);

                    _logger.LogInformation("Test Slack channel created successfully. Channel: {ChannelName}", 
                        result.ChannelName);

                    return Ok(new
                    {
                        success = true,
                        message = "Test Slack channel created successfully!",
                        channelId = result.ChannelId,
                        channelName = result.ChannelName,
                        teamName = result.TeamName,
                        welcomeMessageSent = result.WelcomeMessageSent,
                        testEmail = request.TestEmail,
                        projectTitle = project.Title,
                        memberResults = result.MemberResults,
                        instructions = new
                        {
                            step1 = "Invite the student to your Slack workspace manually",
                            step2 = "Once they accept the workspace invitation, they'll be automatically added to the project channel",
                            step3 = "Or share the direct channel link with them",
                            step4 = "They'll see a welcome message with project details once added"
                        },
                        links = new
                        {
                            workspaceInvite = "https://team1-odc9167.slack.com/invite",
                            channelDirect = $"https://team1-odc9167.slack.com/channels/{result.ChannelName}",
                            note = "Workspace invitation must be sent manually (Enterprise feature required for automatic invitations)"
                        }
                    });
                }
                else
                {
                    _logger.LogError("Failed to create test Slack channel: {Error}", result.ErrorMessage);
                    return BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage,
                        message = "Failed to create test Slack channel. Check logs for details.",
                        troubleshooting = new
                        {
                            note = "If you get 'name_taken' error, the channel already exists from a previous test",
                            suggestion = "Try with a different project ID or check if the channel exists in Slack"
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test Slack channel creation");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = "An unexpected error occurred while creating test Slack channel"
                });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generate team name for a project
        /// </summary>
        private string GenerateTeamName(Project project)
        {
            // Remove spaces and special characters from project title
            var cleanTitle = System.Text.RegularExpressions.Regex.Replace(project.Title, @"[^a-zA-Z0-9]", "");
            
            // Generate team name: ProjectId_ProjectTitleNoSpaces_Team
            var teamName = $"{project.Id}_{cleanTitle}_Team";
            
            // Slack channel names must be lowercase and max 80 characters
            teamName = teamName.ToLower();
            if (teamName.Length > 80)
            {
                teamName = teamName.Substring(0, 80);
            }
            
            return teamName;
        }

        #endregion
    }

    #region Request/Response Models

    public class SlackTeamCreationRequest
    {
        public int ProjectId { get; set; }
        public bool SendWelcomeMessage { get; set; } = true;
    }

    public class TestSlackChannelRequest
    {
        public int ProjectId { get; set; }
        public string TestEmail { get; set; } = string.Empty;
    }

    public class TestSlackTeamWorkflowRequest
    {
        public int ProjectId { get; set; }
        public string TestEmail { get; set; } = string.Empty;
    }

    public class JoinChannelRequest
    {
        public string ChannelId { get; set; } = string.Empty;
    }

    public class TestUserLookupRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class TestChannelCreationDetailedRequest
    {
        public string TeamName { get; set; } = string.Empty;
    }

    public class CreateProjectTeamWithStatusRequest
    {
        [Required]
        public int ProjectId { get; set; }
        
        [Required]
        public int RequestStudentId { get; set; }
        
        [Required]
        [MinLength(1, ErrorMessage = "At least one student ID is required")]
        public List<int> StudentIds { get; set; } = new List<int>();
        
        public bool SendWelcomeMessage { get; set; } = true;
    }

    public class SlackTeamCreationWithStatusResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Project? Project { get; set; }
        public SlackTeamInfo? SlackTeam { get; set; }
        public SlackTeamCreationResult? SlackCreationResult { get; set; }
        public int JoinRequestsCreated { get; set; }
        public bool ProjectStatusChanged { get; set; }
        public string? NewProjectStatus { get; set; }
    }

    #endregion
}
*/
