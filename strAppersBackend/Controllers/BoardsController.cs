using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace strAppersBackend.Controllers;

/// <summary>
/// Response model for ProjectBoard information (excluding sensitive/private data)
/// </summary>
public class ProjectBoardInfoResponse
{
    public string Id { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? StatusId { get; set; }
    public string? PublishUrl { get; set; }
    public string? MovieUrl { get; set; }
    
    // Optional: Include project and status information
    public string? ProjectTitle { get; set; }
    public string? StatusName { get; set; }
}

[ApiController]
[Route("api/[controller]/use")]
public class BoardsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BoardsController> _logger;
    private readonly ITrelloService _trelloService;
    private readonly IAIService _aiService;
    private readonly IGitHubService _gitHubService;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ISmtpEmailService _smtpEmailService;
    private readonly IConfiguration _configuration;

    public BoardsController(ApplicationDbContext context, ILogger<BoardsController> logger, ITrelloService trelloService, IAIService aiService, IGitHubService gitHubService, IMicrosoftGraphService graphService, ISmtpEmailService smtpEmailService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _trelloService = trelloService;
        _aiService = aiService;
        _gitHubService = gitHubService;
        _graphService = graphService;
        _smtpEmailService = smtpEmailService;
        _configuration = configuration;
    }

    /// <summary>
    /// Test simple Trello board creation
    /// </summary>
    [HttpGet("test-trello-board")]
    public async Task<IActionResult> TestTrelloBoardCreation()
    {
        try
        {
            _logger.LogInformation("Testing Trello board creation with Standard plan");
            
            // Use TrelloService to create a test board
            var testRequest = new TrelloProjectCreationRequest
            {
                ProjectId = 999999, // Test project ID
                ProjectTitle = "Test Board",
                ProjectDescription = "Test board creation for Standard plan",
                StudentEmails = new List<string>(),
                ProjectLengthWeeks = 12,
                SprintLengthWeeks = 1,
                TeamMembers = new List<TrelloTeamMember>(),
                SprintPlan = new TrelloSprintPlan()
            };
            
            var response = await _trelloService.CreateProjectWithSprintsAsync(testRequest, "Test Board");
            
            if (response.Success)
            {
                _logger.LogInformation("Test board creation successful: {BoardId}", response.BoardId);
                return Ok(new { 
                    success = true, 
                    message = "Test board created successfully", 
                    boardId = response.BoardId,
                    boardUrl = response.BoardUrl 
                });
            }
            else
            {
                _logger.LogError("Test board creation failed: {Error}", response.Message);
                return BadRequest(new { 
                    success = false, 
                    message = "Test board creation failed", 
                    error = response.Message 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Trello board creation");
            return StatusCode(500, new { success = false, message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Test Trello API connectivity
    /// </summary>
    [HttpGet("test-trello")]
    public async Task<IActionResult> TestTrello()
    {
        try
        {
            _logger.LogInformation("Testing Trello API connectivity");
            
            // Test basic API access
            var testUrl = "https://api.trello.com/1/members/me?key=119469a58c27c3e19a931c6f4343b536&token=ATTA201956c0f870d3c1964885d2c530bd1f6ed2babacb844ff175234acd868d69cbBBFF5366";
            
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(testUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Trello API test successful: {Content}", content);
                return Ok(new { success = true, message = "Trello API is accessible", data = content });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Trello API test failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return BadRequest(new { success = false, message = $"Trello API test failed: {response.StatusCode}", error = errorContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Trello API");
            return StatusCode(500, new { success = false, message = "Error testing Trello API", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new project board with sprint planning and Trello integration
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<CreateBoardResponse>> CreateBoard([FromBody] CreateBoardRequest request)
    {
        try
        {
            _logger.LogInformation("Starting board creation for project {ProjectId} with {StudentCount} students", 
                request.ProjectId, request.StudentIds.Count);

            using var transaction = await _context.Database.BeginTransactionAsync();

            // Validate project exists
            _logger.LogInformation("Validating project {ProjectId}", request.ProjectId);
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found", request.ProjectId);
                return NotFound($"Project with ID {request.ProjectId} not found.");
            }
            _logger.LogInformation("Project found: {ProjectTitle}", project.Title);

            // Validate students exist and are available
            _logger.LogInformation("Validating students: {StudentIds}", string.Join(",", request.StudentIds));
            var students = await _context.Students
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Where(s => request.StudentIds.Contains(s.Id) && s.IsAvailable)
                .ToListAsync();

            _logger.LogInformation("Found {FoundCount} students out of {RequestedCount} requested", 
                students.Count, request.StudentIds.Count);

            if (students.Count != request.StudentIds.Count)
            {
                _logger.LogWarning("Student validation failed. Found {FoundCount}, requested {RequestedCount}", 
                    students.Count, request.StudentIds.Count);
                return BadRequest("One or more students not found or not available.");
            }

            // Get configuration values
            var projectLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:ProjectLengthInWeeks", 12);
            var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
            _logger.LogInformation("Using configuration: ProjectLength={ProjectLength} weeks, SprintLength={SprintLength} weeks", 
                projectLengthWeeks, sprintLengthWeeks);

            // Generate sprint plan using AI
            _logger.LogInformation("Generating sprint plan using AI service");
            
            // Check for null references
            if (_aiService == null)
            {
                _logger.LogError("AI Service is null");
                return StatusCode(500, "AI Service not available");
            }
            
            if (students == null || !students.Any())
            {
                _logger.LogError("Students list is null or empty");
                return StatusCode(500, "No students available");
            }
            
            _logger.LogInformation("Creating SprintPlanningRequest with {StudentCount} students", students.Count);
            
            // Extract unique roles from students to create TeamRoles
            var roleGroups = students
                .Where(s => s.StudentRoles != null)
                .SelectMany(s => s.StudentRoles)
                .Where(sr => sr?.Role != null)
                .GroupBy(sr => new { sr.RoleId, sr.Role.Name })
                .Select(g => new strAppersBackend.Models.RoleInfo
                {
                    RoleId = g.Key.RoleId,
                    RoleName = g.Key.Name,
                    StudentCount = g.Count()
                })
                .ToList();
            
            _logger.LogInformation("Extracted {RoleCount} unique roles: {Roles}", 
                roleGroups.Count, 
                string.Join(", ", roleGroups.Select(r => $"{r.RoleName} ({r.StudentCount} students)")));
            
            var sprintPlanRequest = new SprintPlanningRequest
            {
                ProjectId = request.ProjectId,
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                StartDate = DateTime.UtcNow, // Start from today
                SystemDesign = project.SystemDesign, // Include system design for AI sprint generation
                TeamRoles = roleGroups, // Include team roles for proper task distribution
                Students = students.Select(s => 
                {
                    _logger.LogInformation("Processing student {StudentId}: {FirstName} {LastName}", s.Id, s.FirstName, s.LastName);
                    
                    if (s.StudentRoles == null)
                    {
                        _logger.LogWarning("Student {StudentId} has null StudentRoles", s.Id);
                        return new strAppersBackend.Models.StudentInfo 
                        { 
                            Id = s.Id, 
                            Name = $"{s.FirstName} {s.LastName}", 
                            Email = s.Email,
                            Roles = new List<string>()
                        };
                    }
                    
                    var roles = s.StudentRoles.Select(sr => 
                    {
                        if (sr?.Role == null)
                        {
                            _logger.LogWarning("StudentRole or Role is null for student {StudentId}", s.Id);
                            return "Unknown";
                        }
                        return sr.Role.Name;
                    }).ToList();
                    
                    return new strAppersBackend.Models.StudentInfo 
                    { 
                        Id = s.Id, 
                        Name = $"{s.FirstName} {s.LastName}", 
                        Email = s.Email,
                        Roles = roles
                    };
                }).ToList()
            };
            
            _logger.LogInformation("Calling AI service with {RequestStudentCount} students", sprintPlanRequest.Students.Count);
            var sprintPlanResponse = await _aiService.GenerateSprintPlanAsync(sprintPlanRequest);
            
            if (sprintPlanResponse == null)
            {
                _logger.LogError("AI service returned null response");
                return StatusCode(500, "AI service returned null response");
            }
            
            if (sprintPlanResponse.SprintPlan == null)
            {
                _logger.LogWarning("AI service returned null SprintPlan: {ErrorMessage}. Proceeding with basic sprint plan.", sprintPlanResponse.Message);
                
                // Create a basic fallback sprint plan
                var fallbackSprintPlan = CreateFallbackSprintPlan(project, students, roleGroups);
                sprintPlanResponse = new SprintPlanningResponse
                {
                    Success = true,
                    SprintPlan = fallbackSprintPlan,
                    Message = "Using fallback sprint plan due to AI service issues"
                };
            }
            
            _logger.LogInformation("AI sprint plan generated successfully with {SprintCount} sprints", sprintPlanResponse.SprintPlan.Sprints?.Count ?? 0);
            // Note: SprintPlan is no longer stored in ProjectBoard model

            // Create Trello board first to get the board ID
            _logger.LogInformation("Creating Trello board");
            
            // Check for null references
            if (_trelloService == null)
            {
                _logger.LogError("Trello Service is null");
                return StatusCode(500, "Trello Service not available");
            }
            
            if (project == null)
            {
                _logger.LogError("Project is null");
                return StatusCode(500, "Project is null");
            }
            
            _logger.LogInformation("Converting AI SprintPlan to TrelloSprintPlan format");
            
            // Convert AI SprintPlan to TrelloSprintPlan format
            var trelloSprintPlan = new TrelloSprintPlan
            {
                Lists = sprintPlanResponse.SprintPlan.Sprints?.Select(s => new TrelloList
                {
                    Name = s.Name,
                    Position = s.SprintNumber
                }).ToList() ?? new List<TrelloList>(),
                Cards = sprintPlanResponse.SprintPlan.Sprints?.SelectMany(s => s.Tasks?.Select(t => new TrelloCard
                {
                    Name = t.Title,
                    Description = t.Description,
                    ListName = s.Name,
                    RoleName = t.RoleName,
                    DueDate = s.EndDate,
                    Priority = t.Priority
                }) ?? new List<TrelloCard>()).ToList() ?? new List<TrelloCard>(),
                TotalSprints = sprintPlanResponse.SprintPlan.TotalSprints,
                TotalTasks = sprintPlanResponse.SprintPlan.TotalTasks,
                EstimatedWeeks = sprintPlanResponse.SprintPlan.EstimatedWeeks
            };

            _logger.LogInformation("Creating TrelloProjectCreationRequest");
            var trelloRequest = new TrelloProjectCreationRequest
            {
                ProjectId = request.ProjectId,
                ProjectTitle = project.Title,
                ProjectDescription = project.Description,
                StudentEmails = students.Select(s => s.Email).ToList(),
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                TeamMembers = students.Select(s => new TrelloTeamMember
                {
                    Email = s.Email,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    RoleId = s.StudentRoles?.FirstOrDefault()?.RoleId ?? 0,
                    RoleName = s.StudentRoles?.FirstOrDefault()?.Role?.Name ?? "Team Member"
                }).ToList(),
                SprintPlan = trelloSprintPlan
            };
            
            _logger.LogInformation("Calling Trello service to create board");
            var trelloResponse = await _trelloService.CreateProjectWithSprintsAsync(trelloRequest, project.Title);
            
            if (trelloResponse == null)
            {
                _logger.LogError("Trello service returned null response");
                return StatusCode(500, "Trello service returned null response");
            }
            
            if (string.IsNullOrEmpty(trelloResponse.BoardId))
            {
                _logger.LogError("Trello board creation failed - BoardId is null or empty");
                return StatusCode(500, "Failed to create Trello board - no board ID returned");
            }
            
            _logger.LogInformation("Trello board created with ID: {BoardId}, URL: {BoardUrl}", trelloResponse.BoardId, trelloResponse.BoardUrl);
            var trelloBoardId = trelloResponse.BoardId;

            // Create GitHub repository using the board ID
            _logger.LogInformation("Creating GitHub repository for project {ProjectTitle} with board ID {BoardId}", project.Title, trelloBoardId);
            
            // Extract GitHub usernames from students, filtering out empty ones
            var githubUsernames = students
                .Where(s => !string.IsNullOrWhiteSpace(s.GithubUser))
                .Select(s => s.GithubUser)
                .Distinct()
                .ToList();
            
            _logger.LogInformation("Found {GitHubUserCount} GitHub usernames: {GitHubUsers}", 
                githubUsernames.Count, string.Join(", ", githubUsernames));
            
            string? repositoryUrl = null;
            string? publishUrl = null;
            List<string> addedCollaborators = new List<string>();
            List<string> failedCollaborators = new List<string>();
            
            if (githubUsernames.Any())
            {
                var githubRequest = new CreateRepositoryRequest
                {
                    Name = trelloBoardId,
                    Description = SanitizeRepoDescription(project.Description ?? $"Project repository for {project.Title}"),
                    IsPrivate = false,  // Public repository to enable GitHub Pages on free plan
                    Collaborators = githubUsernames,
                    ProjectTitle = project.Title  // Pass project title for HTML page headline
                };
                
                _logger.LogInformation("Creating GitHub repository: {RepoName} with {CollaboratorCount} collaborators", 
                    githubRequest.Name, githubRequest.Collaborators.Count);
                
                var githubResponse = await _gitHubService.CreateRepositoryAsync(githubRequest);
                
                if (githubResponse.Success)
                {
                    repositoryUrl = githubResponse.RepositoryUrl;
                    publishUrl = githubResponse.GitHubPagesUrl;
                    addedCollaborators = githubResponse.AddedCollaborators;
                    failedCollaborators = githubResponse.FailedCollaborators;
                    
                    _logger.LogInformation("GitHub repository created successfully: {RepositoryUrl}", repositoryUrl);
                    _logger.LogInformation("GitHub Pages URL: {PublishUrl}", publishUrl);
                    _logger.LogInformation("Added {AddedCount} collaborators, {FailedCount} failed", 
                        addedCollaborators.Count, failedCollaborators.Count);
                }
                else
                {
                    _logger.LogWarning("GitHub repository creation failed: {ErrorMessage}. Board creation will continue without repository.", githubResponse.ErrorMessage);
                    // Note: We don't fail the entire process, just log the warning
                    // The board will still be created successfully
                }
            }
            else
            {
                _logger.LogWarning("No GitHub usernames found for students, skipping repository creation");
            }

            // Create Teams meeting if meeting details are provided
            string? meetingUrl = null;
            if (!string.IsNullOrEmpty(request.Title) && !string.IsNullOrEmpty(request.DateTime) && request.DurationMinutes.HasValue)
            {
                try
                {
                    _logger.LogInformation("Creating Teams meeting: {Title} at {DateTime} for {DurationMinutes} minutes", 
                        request.Title, request.DateTime, request.DurationMinutes);

                    // Get student emails for attendees
                    var attendeeEmails = students
                        .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                        .Select(s => s.Email)
                        .ToList();

                    if (attendeeEmails.Any())
                    {
                        var teamsRequest = new CreateTeamsMeetingRequest
                        {
                            Title = request.Title,
                            DateTime = request.DateTime,
                            DurationMinutes = request.DurationMinutes.Value,
                            Attendees = attendeeEmails
                        };

                        var teamsResponse = await _graphService.CreateTeamsMeetingWithoutAttendeesAsync(teamsRequest);
                        
                        if (teamsResponse.Success && !string.IsNullOrEmpty(teamsResponse.JoinUrl))
                        {
                            meetingUrl = teamsResponse.JoinUrl;
                            _logger.LogInformation("Teams meeting created successfully: {MeetingUrl}", meetingUrl);
                            
                            // Send SMTP email invitations to all attendees
                            _logger.LogInformation("Sending SMTP email invitations to {Count} attendees", attendeeEmails.Count);
                            
                            // Parse start and end times for email
                            if (System.DateTime.TryParse(request.DateTime, out var startTime))
                            {
                                var endTime = startTime.AddMinutes(request.DurationMinutes.Value);
                                
                                var emailsSent = await _smtpEmailService.SendBulkMeetingEmailsAsync(
                                    attendeeEmails, 
                                    request.Title, 
                                    startTime, 
                                    endTime, 
                                    teamsResponse.JoinUrl,
                                    $"Join this Teams meeting to discuss: {request.Title}"
                                );

                                if (emailsSent)
                                {
                                    _logger.LogInformation("All SMTP email invitations sent successfully to {Count} attendees", attendeeEmails.Count);
                                }
                                else
                                {
                                    _logger.LogWarning("Some SMTP email invitations failed to send to attendees: {Attendees}", string.Join(", ", attendeeEmails));
                                }
                            }
                            else
                            {
                                _logger.LogError("Invalid DateTime format for Teams meeting: {DateTime}", request.DateTime);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Teams meeting creation failed: {ErrorMessage}. Board creation will continue without meeting.", teamsResponse.Message);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No valid student emails found for Teams meeting attendees");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating Teams meeting: {ErrorMessage}. Board creation will continue without meeting.", ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("Teams meeting details not provided, skipping meeting creation");
            }

            // Find admin student (first one with IsAdmin = true)
            var adminStudent = students.FirstOrDefault(s => s.IsAdmin);
            _logger.LogInformation("Admin student: {AdminId}", adminStudent?.Id);

            // Parse meeting time for NextMeetingTime field
            DateTime? nextMeetingTime = null;
            if (!string.IsNullOrEmpty(request.DateTime) && System.DateTime.TryParse(request.DateTime, out var parsedMeetingTime))
            {
                // Ensure the DateTime is UTC for PostgreSQL compatibility
                if (parsedMeetingTime.Kind == DateTimeKind.Unspecified)
                {
                    // Assume the input is in local time and convert to UTC
                    nextMeetingTime = DateTime.SpecifyKind(parsedMeetingTime, DateTimeKind.Utc);
                }
                else if (parsedMeetingTime.Kind == DateTimeKind.Local)
                {
                    // Convert from local time to UTC
                    nextMeetingTime = parsedMeetingTime.ToUniversalTime();
                }
                else
                {
                    // Already UTC
                    nextMeetingTime = parsedMeetingTime;
                }
                
                _logger.LogInformation("Setting NextMeetingTime to: {MeetingTime} (UTC)", nextMeetingTime);
            }

            // Set project Kickoff flag to false when board is created
            project.Kickoff = false;
            _logger.LogInformation("Set Kickoff flag to false for project {ProjectId} when board was created", request.ProjectId);

            // Create ProjectBoard record
            var projectBoard = new ProjectBoard
            {
                Id = trelloBoardId,
                ProjectId = request.ProjectId,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(projectLengthWeeks * 7),
                StatusId = 1, // New status
                AdminId = adminStudent?.Id,
                SprintPlan = System.Text.Json.JsonSerializer.Serialize(sprintPlanResponse.SprintPlan),
                BoardUrl = trelloResponse.BoardUrl,
                NextMeetingTime = nextMeetingTime,
                NextMeetingUrl = meetingUrl,
                GithubUrl = repositoryUrl,
                PublishUrl = publishUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Creating ProjectBoard record");
            _context.ProjectBoards.Add(projectBoard);

            // Update students with BoardId
            _logger.LogInformation("Updating students with BoardId");
            foreach (var student in students)
            {
                student.BoardId = trelloBoardId;
                student.Status = 3; // Set to pending/in-board status
                student.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Saving changes to database");
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Database changes saved successfully");
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Error saving changes to database: {SaveException}", saveEx.Message);
                throw;
            }
            
            try
            {
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully");
            }
            catch (Exception commitEx)
            {
                _logger.LogError(commitEx, "Error committing transaction: {CommitException}", commitEx.Message);
                throw;
            }

            _logger.LogInformation("Successfully created board {BoardId} for project {ProjectId}", trelloBoardId, request.ProjectId);

            var message = repositoryUrl != null 
                ? "Board and repository created successfully!" 
                : "Board created successfully! (GitHub repository creation was skipped or failed)";

            return Ok(new CreateBoardResponse
            {
                Success = true,
                Message = message,
                BoardId = trelloBoardId,
                BoardUrl = trelloResponse.BoardUrl,
                RepositoryUrl = repositoryUrl,
                PublishUrl = publishUrl,
                MeetingUrl = meetingUrl,
                ProjectId = request.ProjectId,
                StudentCount = students.Count,
                InvitedUsers = trelloResponse.InvitedUsers,
                AddedCollaborators = addedCollaborators,
                FailedCollaborators = failedCollaborators
            });
        }
        catch (Exception ex)
        {
            var innerException = ex.InnerException?.Message ?? "No inner exception";
            _logger.LogError(ex, "Error creating board for project {ProjectId}. Exception: {ExceptionMessage}. Inner Exception: {InnerException}", 
                request.ProjectId, ex.Message, innerException);
            return StatusCode(500, $"An error occurred while creating the board: {ex.Message}. Inner Exception: {innerException}");
        }
    }

    /// <summary>
    /// Set admin for a board
    /// </summary>
    [HttpPost("set-admin")]
    public async Task<ActionResult<SetAdminResponse>> SetAdmin([FromBody] SetAdminRequest request)
    {
        try
        {
            var board = await _context.ProjectBoards
                .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);

            if (board == null)
            {
                return NotFound($"Board with ID {request.BoardId} not found.");
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.IsAvailable);

            if (student == null)
            {
                return BadRequest("Student not found or not available.");
            }

            // Update admin
            board.AdminId = request.StudentId;
            board.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully set admin {StudentId} for board {BoardId}", request.StudentId, request.BoardId);

            return Ok(new SetAdminResponse
            {
                Success = true,
                Message = "Admin set successfully!",
                BoardId = request.BoardId,
                StudentId = request.StudentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting admin for board {BoardId}", request.BoardId);
            return StatusCode(500, "An error occurred while setting the admin");
        }
    }

    /// <summary>
    /// Add a message to the board's group chat
    /// </summary>
    /// <param name="request">Chat message request</param>
    /// <returns>Success response</returns>
    [HttpPost("chat-add")]
    public async Task<ActionResult<object>> AddChatMessage([FromBody] AddChatMessageRequest request)
    {
        try
        {
            _logger.LogInformation("Adding chat message for BoardId {BoardId} from {Email}", request.BoardId, request.Email);

            var board = await _context.ProjectBoards
                .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);

            if (board == null)
            {
                _logger.LogWarning("Board with ID {BoardId} not found", request.BoardId);
                return NotFound(new
                {
                    success = false,
                    message = $"Board with ID {request.BoardId} not found"
                });
            }

            // Get current chat content
            var currentChat = board.GroupChat ?? "";
            
            // Create new chat message
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var newMessage = $"[{timestamp}] {request.Email}: {request.Text}\n";
            
            // Append to existing chat
            board.GroupChat = currentChat + newMessage;
            board.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully added chat message for BoardId {BoardId}", request.BoardId);

            return Ok(new
            {
                success = true,
                message = "Chat message added successfully",
                boardId = request.BoardId,
                timestamp = timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding chat message for board {BoardId}", request.BoardId);
            return StatusCode(500, "An error occurred while adding chat message");
        }
    }

    /// <summary>
    /// Get chat information for a board
    /// </summary>
    /// <param name="boardId">The Board ID to retrieve chat for</param>
    /// <returns>Chat information</returns>
    [HttpGet("chat")]
    public async Task<ActionResult<object>> GetChat([FromQuery] string boardId)
    {
        try
        {
            // Check if GetChat logs should be disabled (default: true = disabled)
            var disableLogs = _configuration.GetValue<bool>("Logging:DisableGetChatLogs", true);

            if (!disableLogs)
            {
            _logger.LogInformation("Getting chat information for BoardId {BoardId}", boardId);
            }

            var board = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .Include(pb => pb.Admin)
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (board == null)
            {
                if (!disableLogs)
            {
                _logger.LogWarning("Board with ID {BoardId} not found", boardId);
                }
                return NotFound(new
                {
                    success = false,
                    message = $"Board with ID {boardId} not found"
                });
            }

            return Ok(new
            {
                success = true,
                boardId = board.Id,
                groupChat = board.GroupChat,
                projectTitle = board.Project?.Title,
                adminName = board.Admin != null ? $"{board.Admin.FirstName} {board.Admin.LastName}" : null
            });
        }
        catch (Exception ex)
        {
            // Always log errors, even if other logs are disabled
            _logger.LogError(ex, "Error getting chat information for board {BoardId}", boardId);
            return StatusCode(500, "An error occurred while retrieving chat information");
        }
    }

    /// <summary>
    /// Get ProjectBoard information by BoardId
    /// </summary>
    /// <param name="boardId">The Board ID to retrieve</param>
    /// <returns>ProjectBoard information</returns>
    [HttpGet("{boardId}")]
    public async Task<ActionResult<object>> GetBoard(string boardId)
    {
        try
        {
            _logger.LogInformation("Getting board information for BoardId {BoardId}", boardId);

            var board = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .Include(pb => pb.Admin)
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (board == null)
            {
                _logger.LogWarning("Board with ID {BoardId} not found", boardId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Board with ID {boardId} not found"
                });
            }

            _logger.LogInformation("Found board {BoardId} for project {ProjectId}", boardId, board.ProjectId);

            return Ok(new
            {
                Success = true,
                BoardId = board.Id,
                ProjectId = board.ProjectId,
                ProjectTitle = board.Project?.Title,
                ProjectDescription = board.Project?.Description,
                StartDate = board.StartDate,
                EndDate = board.EndDate,
                DueDate = board.DueDate,
                StatusId = board.StatusId,
                AdminId = board.AdminId,
                AdminName = board.Admin != null ? $"{board.Admin.FirstName} {board.Admin.LastName}" : null,
                AdminEmail = board.Admin?.Email,
                BoardUrl = board.BoardUrl,
                PublishUrl = board.PublishUrl,
                MovieUrl = board.MovieUrl,
                NextMeetingTime = board.NextMeetingTime,
                NextMeetingUrl = board.NextMeetingUrl,
                GithubUrl = board.GithubUrl,
                SprintPlan = board.SprintPlan,
                CreatedAt = board.CreatedAt,
                UpdatedAt = board.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting board information for BoardId {BoardId}", boardId);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error getting board information: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get comprehensive Trello board statistics
    /// </summary>
    /// <param name="boardId">The Trello board ID to get stats for</param>
    /// <returns>Comprehensive Trello board statistics</returns>
    [HttpGet("stats/{boardId}")]
    public async Task<ActionResult<object>> GetBoardStats(string boardId)
    {
        try
        {
            _logger.LogInformation("Getting Trello stats for Board {BoardId}", boardId);

            // Get ProjectBoard record from database
            var projectBoard = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (projectBoard == null)
            {
                _logger.LogWarning("ProjectBoard with BoardId {BoardId} not found", boardId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Board with ID {boardId} not found"
                });
            }

            // Get Trello stats using the Trello board ID
            var stats = await _trelloService.GetProjectStatsAsync(boardId);

            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                ProjectId = projectBoard.ProjectId,
                ProjectName = projectBoard.Project.Title,
                BoardUrl = projectBoard.BoardUrl,
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Trello stats for Board {BoardId}", boardId);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error getting Trello stats: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get board members with enhanced email resolution
    /// </summary>
    /// <param name="boardId">The Trello board ID to get members for</param>
    /// <returns>Board members with resolved email addresses</returns>
    [HttpGet("members/{boardId}")]
    public async Task<ActionResult<object>> GetBoardMembers(string boardId)
    {
        try
        {
            _logger.LogInformation("Getting board members with email resolution for Board {BoardId}", boardId);

            // Get ProjectBoard record from database
            var projectBoard = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (projectBoard == null)
            {
                _logger.LogWarning("ProjectBoard with BoardId {BoardId} not found", boardId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Board with ID {boardId} not found"
                });
            }

            // Get enhanced member information with email resolution
            var membersResult = await _trelloService.GetBoardMembersWithEmailResolutionAsync(boardId);

            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                ProjectId = projectBoard.ProjectId,
                ProjectName = projectBoard.Project.Title,
                BoardUrl = projectBoard.BoardUrl,
                Members = membersResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting board members for Board {BoardId}", boardId);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error getting board members: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get the count of students assigned to a specific board
    /// </summary>
    /// <param name="boardId">The board ID to count students for</param>
    /// <returns>Number of students with the same boardId</returns>
    [HttpGet("member-count/{boardId}")]
    public async Task<ActionResult<object>> GetMemberCountForBoard(string boardId)
    {
        try
        {
            _logger.LogInformation("Getting member count for Board {BoardId}", boardId);

            // Validate that the board exists
            var projectBoard = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (projectBoard == null)
            {
                _logger.LogWarning("Board {BoardId} not found", boardId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Board with ID {boardId} not found"
                });
            }

            // Count students assigned to this board
            var memberCount = await _context.Students
                .Where(s => s.BoardId == boardId)
                .CountAsync();

            _logger.LogInformation("Found {MemberCount} students for Board {BoardId}", memberCount, boardId);

            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                ProjectId = projectBoard.ProjectId,
                ProjectTitle = projectBoard.Project?.Title,
                BoardUrl = projectBoard.BoardUrl,
                MemberCount = memberCount,
                Message = $"Found {memberCount} students assigned to board {boardId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member count for Board {BoardId}", boardId);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error getting member count: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get the number of boards assigned to a specific project
    /// </summary>
    /// <param name="projectId">The project ID to count boards for</param>
    /// <returns>Number of boards with Status < 4 and IsAvailable=true</returns>
    [HttpGet("use/count/{projectId}")]
    public async Task<ActionResult<object>> GetBoardCountForProject(int projectId)
    {
        try
        {
            _logger.LogInformation("Getting board count for Project {ProjectId}", projectId);

            // Validate that the project exists and is available
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found", projectId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Project with ID {projectId} not found"
                });
            }

            // Count boards for the project with conditions: Status < 4 and Project.IsAvailable = true
            var boardCount = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .Where(pb => pb.ProjectId == projectId && 
                            pb.StatusId < 4 && 
                            pb.Project.IsAvailable)
                .CountAsync();

            _logger.LogInformation("Found {BoardCount} boards for Project {ProjectId} with Status < 4 and IsAvailable=true", 
                boardCount, projectId);

            return Ok(new
            {
                Success = true,
                ProjectId = projectId,
                ProjectTitle = project.Title,
                ProjectIsAvailable = project.IsAvailable,
                BoardCount = boardCount,
                Message = $"Found {boardCount} boards for project {projectId}"
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while counting boards for project {ProjectId}: {Message}", projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = "Database error occurred while counting boards"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting boards for project {ProjectId}: {Message}", projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while counting boards: {ex.Message}"
            });
        }
    }


    /// <summary>
    /// Test endpoint to debug AI prompt generation (for debugging use)
    /// </summary>
    [HttpPost("debug-prompt")]
    public async Task<ActionResult<object>> DebugAIPrompt(DebugPromptRequest request)
    {
        try
        {
            _logger.LogInformation("Debug AI prompt for Project {ProjectId} with {UserCount} users", request.ProjectId, request.Users?.Count ?? 0);

            if (request.Users == null || !request.Users.Any())
            {
                return BadRequest(new { error = "Users list is required" });
            }

            // Create a simple test prompt for debugging
            var prompt = $@"
Project ID: {request.ProjectId}
User Count: {request.Users.Count}
Users: {string.Join(", ", request.Users)}

This is a test prompt for debugging AI sprint planning.
The actual prompt generation would require access to project details and student information.
";

                return Ok(new
                {
                success = true,
                prompt = prompt,
                projectId = request.ProjectId,
                userCount = request.Users.Count,
                message = "Debug prompt generated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating debug prompt");
            return StatusCode(500, new { success = false, message = "Error generating debug prompt", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all project board information for a specific project (excluding sensitive data)
    /// </summary>
    [HttpGet("boards")]
    public async Task<ActionResult<List<ProjectBoardInfoResponse>>> GetProjectBoards([FromQuery] int projectId)
    {
        try
        {
            _logger.LogInformation("Retrieving project board information for ProjectId {ProjectId}", projectId);

            // Validate project exists
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists)
            {
                _logger.LogWarning("Project {ProjectId} not found", projectId);
                return NotFound(new { 
                    Success = false, 
                    Message = $"Project with ID {projectId} not found" 
                });
            }

            // Get all project board data excluding SprintPlan, BoardUrl, and AdminId
            // Only return boards for available projects
            var projectBoards = await _context.ProjectBoards
                .Include(pb => pb.Project)
                .Include(pb => pb.Status)
                .Where(pb => pb.ProjectId == projectId && pb.Project.IsAvailable == true)
                .Select(pb => new ProjectBoardInfoResponse
                {
                    Id = pb.Id,
                    ProjectId = pb.ProjectId,
                    StartDate = pb.StartDate,
                    EndDate = pb.EndDate,
                    DueDate = pb.DueDate,
                    CreatedAt = pb.CreatedAt,
                    UpdatedAt = pb.UpdatedAt,
                    StatusId = pb.StatusId,
                    PublishUrl = pb.PublishUrl,
                    MovieUrl = pb.MovieUrl,
                    ProjectTitle = pb.Project.Title,
                    StatusName = pb.Status != null ? pb.Status.Name : null
                })
                .ToListAsync();

            if (projectBoards == null || !projectBoards.Any())
            {
                _logger.LogInformation("No project boards found for ProjectId {ProjectId}", projectId);
                return NotFound(new { 
                    Success = false, 
                    Message = $"No project boards found for project ID {projectId}" 
                });
            }

            _logger.LogInformation("Successfully retrieved {Count} project board(s) for ProjectId {ProjectId}", projectBoards.Count, projectId);
            return Ok(projectBoards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project board information for ProjectId {ProjectId}: {Message}", projectId, ex.Message);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while retrieving project board information: {ex.Message}"
            });
        }
    }

    private SprintPlan CreateFallbackSprintPlan(Project project, List<Student> students, List<RoleInfo> teamRoles)
    {
        var sprints = new List<Sprint>();
        var totalSprints = 12; // Default 12 weeks
        
        for (int i = 1; i <= totalSprints; i++)
        {
            var sprintStartDate = DateTime.UtcNow.AddDays((i - 1) * 7);
            var sprintEndDate = sprintStartDate.AddDays(6);
            
            var tasks = new List<ProjectTask>();
            var taskId = 1;
            
            // Create basic tasks for each role
            foreach (var role in teamRoles)
            {
                for (int j = 0; j < 2; j++) // 2 tasks per role per sprint
                {
                    tasks.Add(new ProjectTask
                    {
                        Id = $"task{taskId++}",
                        Title = $"{role.RoleName} Task {j + 1} - Sprint {i}",
                        Description = $"Basic development task for {role.RoleName} in sprint {i}",
                        RoleId = role.RoleId,
                        RoleName = role.RoleName,
                        EstimatedHours = 8,
                        Priority = 1,
                        Dependencies = new List<string>()
                    });
                }
            }
            
            sprints.Add(new Sprint
            {
                SprintNumber = i,
                Name = $"Sprint {i}",
                StartDate = sprintStartDate,
                EndDate = sprintEndDate,
                Tasks = tasks
            });
        }
        
        return new SprintPlan
        {
            Sprints = sprints,
            TotalSprints = totalSprints,
            TotalTasks = sprints.Sum(s => s.Tasks?.Count ?? 0),
            EstimatedWeeks = totalSprints
        };
    }

    private static string SanitizeRepoDescription(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        // Remove control characters
        var cleaned = new string(input.Where(ch => !char.IsControl(ch) || ch == ' ').ToArray());
        // Normalize whitespace
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        // Truncate to GitHub-friendly length
        const int maxLen = 140;
        if (cleaned.Length > maxLen) cleaned = cleaned.Substring(0, maxLen);
        return cleaned;
    }
}

// Request/Response DTOs
public class CreateBoardRequest
{
    public int ProjectId { get; set; }
    public List<int> StudentIds { get; set; } = new();
    public string? Title { get; set; }
    public string? DateTime { get; set; } // Format: "2024-01-15T14:30:00Z"
    public int? DurationMinutes { get; set; }
}

public class CreateBoardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string? BoardUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? PublishUrl { get; set; }
    public string? MeetingUrl { get; set; }
    public int ProjectId { get; set; }
    public int StudentCount { get; set; }
    public List<TrelloInvitedUser> InvitedUsers { get; set; } = new List<TrelloInvitedUser>();
    public List<string> AddedCollaborators { get; set; } = new List<string>();
    public List<string> FailedCollaborators { get; set; } = new List<string>();
}

public class SetAdminRequest
{
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
}

public class SetAdminResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
}

/// <summary>
/// Request model for debug prompt endpoint
/// </summary>
public class DebugPromptRequest
{
    public int ProjectId { get; set; }
    public List<int> Users { get; set; } = new List<int>();
}

/// <summary>
/// Request model for adding chat messages
/// </summary>
public class AddChatMessageRequest
{
    public string BoardId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}