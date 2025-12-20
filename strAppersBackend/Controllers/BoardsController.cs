using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http;
using System.Net;

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
                var fallbackSprintPlan = CreateFallbackSprintPlan(project, students, roleGroups, projectLengthWeeks, sprintLengthWeeks);
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
                    Priority = t.Priority,
                    ChecklistItems = t.ChecklistItems ?? new List<string>()
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

            // Send welcome emails to all team members (before any meeting-related emails)
            _logger.LogInformation("Sending welcome emails to {Count} team members", students.Count);
            var welcomeRecipients = students
                .Where(s => !string.IsNullOrWhiteSpace(s.Email) && !string.IsNullOrWhiteSpace(s.FirstName))
                .Select(s => (s.Email, s.FirstName))
                .ToList();
            
            if (welcomeRecipients.Any())
            {
                var welcomeEmailsSent = await _smtpEmailService.SendBulkWelcomeEmailsAsync(
                    welcomeRecipients,
                    project.Title,
                    projectLengthWeeks
                );

                if (welcomeEmailsSent)
                {
                    _logger.LogInformation("All welcome emails sent successfully to {Count} team members", welcomeRecipients.Count);
                }
                else
                {
                    _logger.LogWarning("Some welcome emails failed to send to team members");
                }
            }
            else
            {
                _logger.LogWarning("No valid student emails/names found for welcome emails");
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

            // Initialize meetingUrl variable (will be set after ProjectBoard is created)
            string? meetingUrl = null;

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
                NextMeetingUrl = meetingUrl, // Will be updated after Teams meeting is created
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
            
            // Commit transaction BEFORE calling Teams endpoint so TeamsController can see the ProjectBoard
            try
            {
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully - ProjectBoard is now visible to other endpoints");
            }
            catch (Exception commitEx)
            {
                _logger.LogError(commitEx, "Error committing transaction: {CommitException}", commitEx.Message);
                throw;
            }
            
            // Create Teams meeting AFTER ProjectBoard is committed (so TeamsController can find it)
            // Use the create-meeting-smtp-for-board-auth endpoint which handles custom URLs and tracking
            if (!string.IsNullOrEmpty(request.Title) && !string.IsNullOrEmpty(request.DateTime) && request.DurationMinutes.HasValue)
            {
                try
                {
                    _logger.LogInformation("Creating Teams meeting via create-meeting-smtp-for-board-auth: {Title} at {DateTime} for {DurationMinutes} minutes", 
                        request.Title, request.DateTime, request.DurationMinutes);

                    // Get student emails for attendees
                    var attendeeEmails = students
                        .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                        .Select(s => s.Email)
                        .ToList();

                    if (attendeeEmails.Any() && !string.IsNullOrEmpty(trelloBoardId))
                    {
                        // Call the create-meeting-smtp-for-board-auth endpoint
                        // This will create custom tracking URLs and send emails with those URLs
                        var teamsMeetingRequest = new strAppersBackend.Controllers.CreateTeamsMeetingForBoardRequest
                        {
                            BoardId = trelloBoardId,
                            Title = request.Title,
                            DateTime = request.DateTime,
                            DurationMinutes = request.DurationMinutes.Value,
                            Attendees = attendeeEmails
                        };

                        // Sanitize title to remove newlines that might cause JSON parsing issues
                        var sanitizedTitle = request.Title?.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim() ?? "";
                        teamsMeetingRequest.Title = sanitizedTitle;
                        
                        // Use HttpClient to call our own API endpoint
                        using var httpClient = new HttpClient();
                        httpClient.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");
                        
                        // Serialize with proper options - standard encoder will escape newlines properly
                        var jsonContent = JsonSerializer.Serialize(teamsMeetingRequest, new JsonSerializerOptions 
                        { 
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        });
                        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                        
                        _logger.LogInformation("Calling create-meeting-smtp-for-board-auth for board {BoardId} with {AttendeeCount} attendees", 
                            trelloBoardId, attendeeEmails.Count);
                        
                        var response = await httpClient.PostAsync("/api/Teams/use/create-meeting-smtp-for-board-auth", content);
                        var responseContent = await response.Content.ReadAsStringAsync();
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            if (result.TryGetProperty("actualMeetingUrl", out var actualUrlElement))
                            {
                                meetingUrl = actualUrlElement.GetString();
                                _logger.LogInformation("Teams meeting created successfully via create-meeting-smtp-for-board-auth: {MeetingUrl}", meetingUrl);
                                
                                // Update ProjectBoard with the actual meeting URL (transaction already committed)
                                projectBoard.NextMeetingUrl = meetingUrl;
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("Updated ProjectBoard with meeting URL: {MeetingUrl}", meetingUrl);
                            }
                            else
                            {
                                _logger.LogWarning("Teams meeting created but no actualMeetingUrl in response. Full response: {Response}", responseContent);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Teams meeting creation failed via create-meeting-smtp-for-board-auth: {StatusCode} - {Content}. Board creation will continue without meeting.", 
                                response.StatusCode, responseContent);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No valid student emails or board ID found for Teams meeting attendees");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating Teams meeting via create-meeting-smtp-for-board-auth: {ErrorMessage}. Board creation will continue without meeting.", ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("Teams meeting details not provided, skipping meeting creation");
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
                Observed = board.Observed,
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
                Observed = projectBoard.Observed,
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
    /// Get comprehensive board information for a specific student
    /// Returns: boardId, project name, organization name, team members, startDate, dueDate, weeksLeft (rounded int), next meeting, recent activities, and stats
    /// Route: /api/Boards/use/student/{studentId}
    /// </summary>
    [HttpGet("student/{studentId}")]
    public async Task<ActionResult<object>> GetBoardForStudent(int studentId)
    {
        try
        {
            _logger.LogInformation("Getting board information for StudentId {StudentId}", studentId);

            // Get student with board and project information
            var student = await _context.Students
                .Include(s => s.ProjectBoard)
                    .ThenInclude(pb => pb.Project)
                        .ThenInclude(p => p.Organization)
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                _logger.LogWarning("Student with ID {StudentId} not found", studentId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Student with ID {studentId} not found"
                });
            }

            if (string.IsNullOrEmpty(student.BoardId))
            {
                _logger.LogWarning("Student {StudentId} does not have a board assigned", studentId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Student with ID {studentId} does not have a board assigned"
                });
            }

            var board = student.ProjectBoard;
            if (board == null)
            {
                _logger.LogWarning("Board {BoardId} not found for student {StudentId}", student.BoardId, studentId);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Board {student.BoardId} not found"
                });
            }

            var project = board.Project;
            if (project == null)
            {
                _logger.LogWarning("Project not found for board {BoardId}", board.Id);
                return NotFound(new
                {
                    Success = false,
                    Message = $"Project not found for board {board.Id}"
                });
            }

            // Get project dates
            var startDate = board.StartDate ?? board.CreatedAt;
            var dueDate = board.DueDate;
            
            // Calculate weeks left using DueDate if available, otherwise calculate from config
            int weeksLeft = 0;
            if (dueDate.HasValue)
            {
            var today = DateTime.UtcNow.Date;
                var dueDateValue = dueDate.Value.Date;
                var daysLeft = (dueDateValue - today).TotalDays;
                weeksLeft = Math.Max(0, (int)Math.Round(daysLeft / 7.0));
            }
            else
            {
                // Fallback to config-based calculation if DueDate is not set
                var projectLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:ProjectLengthInWeeks", 12);
                var today = DateTime.UtcNow.Date;
                var startDateValue = startDate.Date;
                var endDate = startDateValue.AddDays(projectLengthWeeks * 7);
                var daysLeft = (endDate - today).TotalDays;
                weeksLeft = Math.Max(0, (int)Math.Round(daysLeft / 7.0));
            }

            // Get team members (students with same BoardId)
            var teamMembers = await _context.Students
                .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId == board.Id && s.IsAvailable)
                .Select(s => new
                {
                    Id = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Email = s.Email,
                    LinkedInUrl = s.LinkedInUrl,
                    Photo = s.Photo,
                    Roles = s.StudentRoles
                        .Where(sr => sr.IsActive)
                        .Select(sr => new
                        {
                            RoleId = sr.RoleId,
                            RoleName = sr.Role != null ? sr.Role.Name : null
                        })
                        .ToList()
                })
                .ToListAsync();

            // Get student's roles for filtering cards by role labels
            // Cards are linked to students via role tags (labels) in Trello
            var studentRoles = student.StudentRoles
                .Where(sr => sr.IsActive && sr.Role != null)
                .Select(sr => sr.Role!.Name)
                .Distinct()
                .ToList();
            
            _logger.LogInformation("Student {StudentId} has {RoleCount} active role(s): {Roles}", 
                studentId, studentRoles.Count, string.Join(", ", studentRoles));

            var recentActivities = new List<object>();
            var studentStats = new
            {
                TotalCards = 0,
                CompletedCards = 0,
                OverdueCards = 0,
                InProgressCards = 0,
                AssignedCards = 0
            };

            // Get student's Trello member ID for filtering activities
            string? studentTrelloMemberId = null;

            try
            {
                // Get board members from Trello to find student's Trello member ID (for activities only)
                var membersResult = await _trelloService.GetBoardMembersWithEmailResolutionAsync(board.Id);
                
                // Convert to JSON to extract member information
                var membersJson = JsonSerializer.Serialize(membersResult);
                var membersElement = JsonSerializer.Deserialize<JsonElement>(membersJson);
                
                if (membersElement.TryGetProperty("Members", out var membersProp) && membersProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var member in membersProp.EnumerateArray())
                    {
                        var email = member.TryGetProperty("Email", out var emailProp) ? emailProp.GetString() : "";
                        if (string.Equals(email, student.Email, StringComparison.OrdinalIgnoreCase))
                        {
                            studentTrelloMemberId = member.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
                            break;
                        }
                    }
                }

                // Get cards filtered by student's role labels
                // Cards are linked to students via role tags (labels) in Trello
                var allStudentCards = new List<JsonElement>();
                var cardsByRole = new Dictionary<string, List<JsonElement>>(); // Track cards per role for detailed logging
                var allListMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Map ListId to ListName for completion checking
                
                foreach (var roleName in studentRoles)
                {
                    try
                    {
                        var cardsByLabel = await _trelloService.GetCardsAndListsByLabelAsync(board.Id, roleName);
                        var cardsJson = JsonSerializer.Serialize(cardsByLabel);
                        var cardsElement = JsonSerializer.Deserialize<JsonElement>(cardsJson);
                        
                        // Check if the request was successful and label was found
                        var success = cardsElement.TryGetProperty("Success", out var successProp) && successProp.GetBoolean();
                        var labelFound = cardsElement.TryGetProperty("LabelFound", out var labelFoundProp) && labelFoundProp.GetBoolean();
                        
                        if (success && labelFound && cardsElement.TryGetProperty("Cards", out var cardsProp) && cardsProp.ValueKind == JsonValueKind.Array)
                        {
                            // Get lists to map ListId to ListName for completion checking (store globally for reuse)
                            if (cardsElement.TryGetProperty("Lists", out var listsProp) && listsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var list in listsProp.EnumerateArray())
                                {
                                    var listId = list.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                                (list.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                                    var listName = list.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                                  (list.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                                    if (!string.IsNullOrEmpty(listId) && !string.IsNullOrEmpty(listName))
                                    {
                                        allListMappings[listId] = listName;
                                    }
                                }
                            }
                            
                            foreach (var card in cardsProp.EnumerateArray())
                            {
                                // GetCardsAndListsByLabelAsync already filters cards by label, so cards returned should have the label
                                // Verify the card has the role label (check both PascalCase and camelCase property names)
                                var hasRoleLabel = false;
                                JsonElement cardLabelsProp;
                                
                                // Try PascalCase first (original format from TrelloService)
                                if (card.TryGetProperty("Labels", out cardLabelsProp) && cardLabelsProp.ValueKind == JsonValueKind.Array)
                                {
                                    hasRoleLabel = cardLabelsProp.EnumerateArray().Any(l =>
                                    {
                                        var labelName = l.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                                      (l.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                                        return !string.IsNullOrEmpty(labelName) && string.Equals(labelName, roleName, StringComparison.OrdinalIgnoreCase);
                                    });
                                }
                                // Try camelCase (if serialization changed it)
                                else if (card.TryGetProperty("labels", out cardLabelsProp) && cardLabelsProp.ValueKind == JsonValueKind.Array)
                                {
                                    hasRoleLabel = cardLabelsProp.EnumerateArray().Any(l =>
                                    {
                                        var labelName = l.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                                      (l.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                                        return !string.IsNullOrEmpty(labelName) && string.Equals(labelName, roleName, StringComparison.OrdinalIgnoreCase);
                                    });
                                }
                                
                                // Since GetCardsAndListsByLabelAsync already filtered by label, if we can't find Labels property,
                                // it might be a serialization issue - log warning but trust the service filtering
                                if (!hasRoleLabel && !card.TryGetProperty("Labels", out _) && !card.TryGetProperty("labels", out _))
                                {
                                    _logger.LogWarning("Card does not have Labels property - trusting service filtering for role '{RoleName}'", roleName);
                                    hasRoleLabel = true; // Trust the service since it already filtered
                                }
                                
                                // Only add card if it has the role label
                                if (hasRoleLabel)
                                {
                                    var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                                (card.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                                    var cardName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                                  (card.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                                    
                                    if (!string.IsNullOrEmpty(cardId) && !allStudentCards.Any(c => 
                                    {
                                        var existingId = c.TryGetProperty("Id", out var existingIdProp) ? existingIdProp.GetString() : 
                                                        (c.TryGetProperty("id", out var existingIdProp2) ? existingIdProp2.GetString() : "");
                                        return existingId == cardId;
                                    }))
                                {
                                    allStudentCards.Add(card);
                                        
                                        // Track card per role for detailed logging
                                        if (!cardsByRole.ContainsKey(roleName))
                                        {
                                            cardsByRole[roleName] = new List<JsonElement>();
                                        }
                                        cardsByRole[roleName].Add(card);
                                        
                                        _logger.LogDebug("Added card '{CardName}' (ID: {CardId}) with role label '{RoleName}' for student {StudentId}", 
                                            cardName, cardId, roleName, studentId);
                                    }
                                }
                                else
                                {
                                    var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                                (card.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                                    var cardName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                                  (card.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                                    _logger.LogWarning("Card '{CardName}' (ID: {CardId}) from label filter does not have role label '{RoleName}' - skipping", 
                                        cardName, cardId, roleName);
                                }
                            }
                            
                            _logger.LogInformation("Found {CardCount} cards with role label '{RoleName}' for student {StudentId}", 
                                cardsProp.GetArrayLength(), roleName, studentId);
                        }
                        else
                        {
                            if (!success)
                            {
                                var message = cardsElement.TryGetProperty("Message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                _logger.LogWarning("Request failed for label '{RoleName}' for student {StudentId}. Message: {Message}", 
                                    roleName, studentId, message);
                            }
                            else if (!labelFound)
                            {
                                _logger.LogWarning("Label '{RoleName}' not found on board for student {StudentId}. No cards will be included for this role.", 
                                    roleName, studentId);
                            }
                            else
                            {
                                _logger.LogWarning("Cards property not found in response for label '{RoleName}' for student {StudentId}", 
                                    roleName, studentId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch cards for role '{RoleName}' for student {StudentId}", roleName, studentId);
                    }
                }

                _logger.LogInformation("Total student-level cards found: {CardCount} for student {StudentId} with roles: {Roles}", 
                    allStudentCards.Count, studentId, string.Join(", ", studentRoles));

                // Calculate student-level stats from cards filtered by role labels
                // Cards are linked to students via role tags (labels) in Trello
                // Calculate stats per role for detailed logging
                    var now = DateTime.UtcNow;
                    var totalCards = allStudentCards.Count;
                    var completedCards = 0;
                    var overdueCards = 0;
                    var inProgressCards = 0;

                if (allStudentCards.Any())
                {
                    _logger.LogInformation("Calculating stats for {CardCount} total student-level cards (across all roles) for student {StudentId}", 
                        allStudentCards.Count, studentId);
                    
                    _logger.LogInformation("List mappings found: {ListCount} lists", allListMappings.Count);
                    foreach (var listMapping in allListMappings)
                    {
                        _logger.LogDebug("List ID {ListId} -> Name: {ListName}", listMapping.Key, listMapping.Value);
                    }
                    
                    // Calculate stats per role for detailed logging
                    foreach (var roleEntry in cardsByRole)
                    {
                        var roleName = roleEntry.Key;
                        var roleCards = roleEntry.Value;
                        var roleCompleted = 0;
                        var roleOverdue = 0;
                        var roleInProgress = 0;

                        foreach (var card in roleCards)
                        {
                            var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                        (card.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                            var cardName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                          (card.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                            
                            // Check both PascalCase and camelCase property names (serialization may change casing)
                            var isClosed = false;
                            if (card.TryGetProperty("Closed", out var closedProp))
                            {
                                isClosed = closedProp.GetBoolean();
                            }
                            else if (card.TryGetProperty("closed", out var closedProp2))
                            {
                                isClosed = closedProp2.GetBoolean();
                            }
                            
                            var dueComplete = false;
                            if (card.TryGetProperty("DueComplete", out var dueCompleteProp))
                            {
                                dueComplete = dueCompleteProp.GetBoolean();
                            }
                            else if (card.TryGetProperty("dueComplete", out var dueCompleteProp2))
                            {
                                dueComplete = dueCompleteProp2.GetBoolean();
                            }
                            
                            // Check if card is in a "Done" or "Completed" list
                            var listId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : 
                                       (card.TryGetProperty("listId", out var listIdProp2) ? listIdProp2.GetString() : "");
                            var isInDoneList = false;
                            var listName = "";
                            if (!string.IsNullOrEmpty(listId) && allListMappings.TryGetValue(listId, out listName))
                            {
                                isInDoneList = listName.Contains("Done", StringComparison.OrdinalIgnoreCase) || 
                                             listName.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
                                             listName.Contains("Complete", StringComparison.OrdinalIgnoreCase);
                            }
                            
                            // Card is completed if: closed=true OR in Done/Completed list OR dueComplete=true
                            var isCompleted = isClosed || isInDoneList || dueComplete;
                            
                            var dueDateStr = card.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : 
                                           (card.TryGetProperty("dueDate", out var dueProp2) ? dueProp2.GetString() : null);
                            var isOverdue = false;
                            
                            if (!string.IsNullOrEmpty(dueDateStr) && DateTime.TryParse(dueDateStr, out var cardDueDate))
                            {
                                isOverdue = cardDueDate < now && !isCompleted;
                            }

                            _logger.LogDebug("Card '{CardName}' (ID: {CardId}) - Role: {RoleName}, List: {ListName}, Closed: {Closed}, DueComplete: {DueComplete}, InDoneList: {InDoneList}, IsCompleted: {IsCompleted}, IsOverdue: {IsOverdue}", 
                                cardName, cardId, roleName, listName ?? "Unknown", isClosed, dueComplete, isInDoneList, isCompleted, isOverdue);

                            if (isCompleted)
                                roleCompleted++;
                            else if (isOverdue)
                                roleOverdue++;
                            else
                                roleInProgress++;
                        }

                        _logger.LogInformation("Stats for role '{RoleName}' - student {StudentId}: Total={Total}, Completed={Completed}, Overdue={Overdue}, InProgress={InProgress}", 
                            roleName, studentId, roleCards.Count, roleCompleted, roleOverdue, roleInProgress);
                    }

                    // Calculate aggregate stats
                    foreach (var card in allStudentCards)
                    {
                        var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                    (card.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                        var cardName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : 
                                      (card.TryGetProperty("name", out var nameProp2) ? nameProp2.GetString() : "");
                        
                        // Check both PascalCase and camelCase property names (serialization may change casing)
                        var isClosed = false;
                        if (card.TryGetProperty("Closed", out var closedProp))
                        {
                            isClosed = closedProp.GetBoolean();
                        }
                        else if (card.TryGetProperty("closed", out var closedProp2))
                        {
                            isClosed = closedProp2.GetBoolean();
                        }
                        
                        var dueComplete = false;
                        if (card.TryGetProperty("DueComplete", out var dueCompleteProp))
                        {
                            dueComplete = dueCompleteProp.GetBoolean();
                        }
                        else if (card.TryGetProperty("dueComplete", out var dueCompleteProp2))
                        {
                            dueComplete = dueCompleteProp2.GetBoolean();
                        }
                        
                        // Check if card is in a "Done" or "Completed" list
                        var listId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : 
                                   (card.TryGetProperty("listId", out var listIdProp2) ? listIdProp2.GetString() : "");
                        var isInDoneList = false;
                        var listName = "";
                        if (!string.IsNullOrEmpty(listId) && allListMappings.TryGetValue(listId, out listName))
                        {
                            isInDoneList = listName.Contains("Done", StringComparison.OrdinalIgnoreCase) || 
                                         listName.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
                                         listName.Contains("Complete", StringComparison.OrdinalIgnoreCase);
                        }
                        
                        // Card is completed if: closed=true OR in Done/Completed list OR dueComplete=true
                        var isCompleted = isClosed || isInDoneList || dueComplete;
                        
                        var dueDateStr = card.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : 
                                       (card.TryGetProperty("dueDate", out var dueProp2) ? dueProp2.GetString() : null);
                        var isOverdue = false;
                        
                        if (!string.IsNullOrEmpty(dueDateStr) && DateTime.TryParse(dueDateStr, out var cardDueDate))
                        {
                            isOverdue = cardDueDate < now && !isCompleted;
                        }

                        if (isCompleted)
                            completedCards++;
                        else if (isOverdue)
                            overdueCards++;
                        else
                            inProgressCards++;
                    }

                    studentStats = new
                    {
                        TotalCards = totalCards,
                        CompletedCards = completedCards,
                        OverdueCards = overdueCards,
                        InProgressCards = inProgressCards,
                        AssignedCards = totalCards
                    };
                    
                    _logger.LogInformation("Aggregate student-level stats for student {StudentId}: Total={Total}, Completed={Completed}, Overdue={Overdue}, InProgress={InProgress}", 
                        studentId, totalCards, completedCards, overdueCards, inProgressCards);
                }
                else
                {
                    _logger.LogInformation("No student-level cards found for student {StudentId} with roles: {Roles}. Stats will be zero.", 
                        studentId, string.Join(", ", studentRoles));
                    // studentStats already initialized to zeros above
                }

                // Get recent activities for the student (filtered by role labels)
                // Only include activities for cards that have the student's role labels
                // Note: We filter by role labels, not by member ID, since Trello may not have user ID info
                try
                {
                    using var httpClient = new HttpClient();
                    // Include comprehensive action types for card changes:
                    // - updateCard: covers due date, description, name, closed status, and any card property changes
                    // - commentCard: comments on cards
                    // - createCard: card creation
                    // - updateCheckItemStateOnCard: marking checklist items as complete/incomplete
                    // - addChecklistToCard: adding checklists to cards
                    // - removeChecklistFromCard: removing checklists from cards
                    // - updateChecklist: updating checklist properties
                    // - addAttachmentToCard: adding attachments
                    // - addMemberToCard: adding members to cards
                    // - removeMemberFromCard: removing members from cards
                    // Note: We don't filter by member ID since Trello may not have user info - we filter by role labels instead
                    var actionsUrl = $"https://api.trello.com/1/boards/{board.Id}/actions?filter=updateCard,commentCard,createCard,updateCheckItemStateOnCard,addChecklistToCard,removeChecklistFromCard,updateChecklist,addAttachmentToCard,addMemberToCard,removeMemberFromCard&limit=100&key={_configuration["TrelloConfig:ApiKey"]}&token={_configuration["TrelloConfig:ApiToken"]}";
                    var actionsResponse = await httpClient.GetAsync(actionsUrl);
                        
                        if (actionsResponse.IsSuccessStatusCode)
                        {
                            var actionsContent = await actionsResponse.Content.ReadAsStringAsync();
                            var actionsData = JsonSerializer.Deserialize<JsonElement[]>(actionsContent);
                            
                            // Get set of card IDs that belong to student (have student's role labels)
                            var studentCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var card in allStudentCards)
                            {
                                var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                            (card.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                                if (!string.IsNullOrEmpty(cardId))
                                {
                                    studentCardIds.Add(cardId);
                                }
                            }
                            
                            _logger.LogInformation("Filtering recent activities by role labels for student {StudentId}: Found {CardCount} student cards (with role labels: {Roles}), {ActionCount} total board actions", 
                                studentId, studentCardIds.Count, string.Join(", ", studentRoles), actionsData?.Length ?? 0);
                            
                            var filteredActions = new List<object>();
                            var activitiesByRole = new Dictionary<string, int>();
                            
                            foreach (var a in actionsData ?? Array.Empty<JsonElement>())
                            {
                                var actionType = a.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "";
                                var cardId = "";
                                
                                // Extract card ID from different action types
                                if (a.TryGetProperty("data", out var dataProp))
                                {
                                    // Most actions have card.id
                                    if (dataProp.TryGetProperty("card", out var cardProp))
                                    {
                                        cardId = cardProp.TryGetProperty("id", out var cardIdProp) ? cardIdProp.GetString() : "";
                                    }
                                    // Some actions might have cardId directly
                                    if (string.IsNullOrEmpty(cardId))
                                    {
                                        cardId = dataProp.TryGetProperty("cardId", out var cardIdProp2) ? cardIdProp2.GetString() : "";
                                    }
                                }
                                
                                // Only include activities for cards that belong to the student (have student's role labels)
                                if (!string.IsNullOrEmpty(cardId) && studentCardIds.Contains(cardId))
                                {
                                    // Determine which role this card belongs to for logging
                                    var cardRole = "Unknown";
                                    foreach (var roleEntry in cardsByRole)
                                    {
                                        if (roleEntry.Value.Any(c =>
                                        {
                                            var cId = c.TryGetProperty("Id", out var idProp) ? idProp.GetString() : 
                                                     (c.TryGetProperty("id", out var idProp2) ? idProp2.GetString() : "");
                                            return cId == cardId;
                                        }))
                                        {
                                            cardRole = roleEntry.Key;
                                            break;
                                        }
                                    }
                                    
                                    if (!activitiesByRole.ContainsKey(cardRole))
                                    {
                                        activitiesByRole[cardRole] = 0;
                                    }
                                    activitiesByRole[cardRole]++;
                                    
                                    // Extract detailed information based on action type
                                    var actionData = a.TryGetProperty("data", out var dataProp2) ? dataProp2 : default;
                                    var cardName = "";
                                    var cardData = actionData.ValueKind != JsonValueKind.Undefined && actionData.TryGetProperty("card", out var cardProp2) ? cardProp2 : default;
                                    if (cardData.ValueKind != JsonValueKind.Undefined)
                                    {
                                        cardName = cardData.TryGetProperty("name", out var cardNameProp) ? cardNameProp.GetString() : "";
                                    }
                                    
                                    var text = actionData.ValueKind != JsonValueKind.Undefined && actionData.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                                    
                                    // Extract checklist information for checklist-related actions
                                    var checklistName = "";
                                    var checkItemName = "";
                                    var checkItemState = "";
                                    if (actionData.ValueKind != JsonValueKind.Undefined)
                                    {
                                        if (actionData.TryGetProperty("checklist", out var checklistProp))
                                        {
                                            checklistName = checklistProp.TryGetProperty("name", out var checklistNameProp) ? checklistNameProp.GetString() : "";
                                        }
                                        if (actionData.TryGetProperty("checkItem", out var checkItemProp))
                                        {
                                            checkItemName = checkItemProp.TryGetProperty("name", out var checkItemNameProp) ? checkItemNameProp.GetString() : "";
                                            checkItemState = checkItemProp.TryGetProperty("state", out var checkItemStateProp) ? checkItemStateProp.GetString() : "";
                                        }
                                    }
                                    
                                    // Extract old/new values for updateCard actions (e.g., due date changes, description changes)
                                    var oldValue = "";
                                    var newValue = "";
                                    if (actionType == "updateCard" && actionData.ValueKind != JsonValueKind.Undefined)
                                    {
                                        if (actionData.TryGetProperty("old", out var oldProp))
                                        {
                                            // Check for common fields that might have changed
                                            if (oldProp.TryGetProperty("due", out var oldDueProp))
                                            {
                                                oldValue = $"Due: {oldDueProp.GetString()}";
                                            }
                                            else if (oldProp.TryGetProperty("desc", out var oldDescProp))
                                            {
                                                oldValue = $"Description changed";
                                            }
                                            else if (oldProp.TryGetProperty("name", out var oldNameProp))
                                            {
                                                oldValue = $"Name: {oldNameProp.GetString()}";
                                            }
                                            else if (oldProp.TryGetProperty("closed", out var oldClosedProp))
                                            {
                                                oldValue = oldClosedProp.GetBoolean() ? "Card was open" : "Card was closed";
                                            }
                                        }
                                        if (actionData.TryGetProperty("card", out var newCardProp))
                                        {
                                            if (newCardProp.TryGetProperty("due", out var newDueProp))
                                            {
                                                newValue = $"Due: {newDueProp.GetString()}";
                                            }
                                            else if (newCardProp.TryGetProperty("desc", out var newDescProp))
                                            {
                                                newValue = "Description updated";
                                            }
                                            else if (newCardProp.TryGetProperty("name", out var newNameProp))
                                            {
                                                newValue = $"Name: {newNameProp.GetString()}";
                                            }
                                            else if (newCardProp.TryGetProperty("closed", out var newClosedProp))
                                            {
                                                newValue = newClosedProp.GetBoolean() ? "Card closed" : "Card opened";
                                            }
                                        }
                                    }
                                    
                                    filteredActions.Add(new
                                    {
                                        Type = actionType,
                                Date = a.TryGetProperty("date", out var dateProp) ? dateProp.GetString() : "",
                                        Data = new
                                {
                                            Card = !string.IsNullOrEmpty(cardId) ? new
                                    {
                                                Name = cardName,
                                                Id = cardId
                                    } : null,
                                            Text = text,
                                            ChecklistName = !string.IsNullOrEmpty(checklistName) ? checklistName : null,
                                            CheckItemName = !string.IsNullOrEmpty(checkItemName) ? checkItemName : null,
                                            CheckItemState = !string.IsNullOrEmpty(checkItemState) ? checkItemState : null,
                                            OldValue = !string.IsNullOrEmpty(oldValue) ? oldValue : null,
                                            NewValue = !string.IsNullOrEmpty(newValue) ? newValue : null
                                        },
                                MemberCreator = a.TryGetProperty("memberCreator", out var memberProp) ? new
                                {
                                    FullName = memberProp.TryGetProperty("fullName", out var nameProp) ? nameProp.GetString() : ""
                                } : null
                                    });
                                }
                            }
                            
                            recentActivities = filteredActions.Cast<object>().ToList();
                            
                            _logger.LogInformation("Recent activities filtered for student {StudentId}: {FilteredCount} activities (from {TotalCount} total) included", 
                                studentId, filteredActions.Count, actionsData?.Length ?? 0);
                            
                            foreach (var roleActivity in activitiesByRole)
                            {
                                _logger.LogInformation("Activities for role '{RoleName}' - student {StudentId}: {ActivityCount} activities", 
                                    roleActivity.Key, studentId, roleActivity.Value);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to fetch activities from Trello API for student {StudentId}: Status {StatusCode}", 
                                studentId, actionsResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch Trello activities for student {StudentId}", studentId);
                    }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching Trello data for student {StudentId}: {Error}", studentId, ex.Message);
            }

            // Add meeting participation activities from BoardMeetings
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email) && !string.IsNullOrWhiteSpace(board.Id))
                {
                    var attendedMeetings = await _context.BoardMeetings
                        .Where(bm => bm.BoardId == board.Id && bm.StudentEmail == student.Email && bm.Attended && bm.JoinTime.HasValue)
                        .OrderByDescending(bm => bm.JoinTime)
                        .Take(10)
                        .ToListAsync();
                    
                    foreach (var meeting in attendedMeetings)
                    {
                        recentActivities.Add(new
                        {
                            Type = "meetingParticipation",
                            Date = meeting.JoinTime!.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            Data = new
                            {
                                MeetingTime = meeting.MeetingTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                JoinTime = meeting.JoinTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                Description = $"Joined team meeting"
                            },
                            MemberCreator = (object?)null
                        });
                    }
                    
                    _logger.LogInformation("Added {MeetingCount} meeting participation activities for student {StudentId}", 
                        attendedMeetings.Count, studentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding meeting participation activities for student {StudentId}: {Message}", studentId, ex.Message);
            }

            // Add GitHub commit activities
            try
            {
                if (!string.IsNullOrWhiteSpace(student.GithubUser) && !string.IsNullOrWhiteSpace(board.GithubUrl))
                {
                    var githubUrl = board.GithubUrl.Trim();
                    if (githubUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                        githubUrl.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = githubUrl.Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase)
                                           .Replace("http://github.com/", "", StringComparison.OrdinalIgnoreCase)
                                           .TrimEnd('/');
                        
                        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        {
                            path = path.Substring(0, path.Length - 4);
                        }
                        
                        var parts = path.Split('/');
                        if (parts.Length >= 2)
                        {
                            var owner = parts[0];
                            var repo = parts[1];
                            
                            // Fetch recent commits (up to 10) for this user
                            using var httpClient = new HttpClient();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                            
                            var commitsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?author={student.GithubUser}&per_page=10";
                            var commitsResponse = await httpClient.GetAsync(commitsUrl);
                            
                            if (commitsResponse.IsSuccessStatusCode)
                            {
                                var commitsContent = await commitsResponse.Content.ReadAsStringAsync();
                                var commitsData = JsonSerializer.Deserialize<JsonElement[]>(commitsContent, new JsonSerializerOptions 
                                { 
                                    PropertyNameCaseInsensitive = true 
                                });
                                
                                if (commitsData != null && commitsData.Length > 0)
                                {
                                    foreach (var commit in commitsData)
                                    {
                                        if (commit.TryGetProperty("commit", out var commitProp))
                                        {
                                            DateTime? commitDate = null;
                                            string commitMessage = string.Empty;
                                            
                                            if (commitProp.TryGetProperty("author", out var authorProp) && 
                                                authorProp.TryGetProperty("date", out var dateProp))
                                            {
                                                var commitDateStr = dateProp.GetString();
                                                if (DateTime.TryParse(commitDateStr, out var parsedDate))
                                                {
                                                    commitDate = parsedDate;
                                                }
                                            }
                                            
                                            if (commitProp.TryGetProperty("message", out var messageProp))
                                            {
                                                commitMessage = messageProp.GetString() ?? string.Empty;
                                                // Truncate long commit messages
                                                if (commitMessage.Length > 100)
                                                {
                                                    commitMessage = commitMessage.Substring(0, 97) + "...";
                                                }
                                            }
                                            
                                            if (commitDate.HasValue)
                                            {
                                                recentActivities.Add(new
                                                {
                                                    Type = "githubCommit",
                                                    Date = commitDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                                    Data = new
                                                    {
                                                        CommitMessage = commitMessage,
                                                        CommitDate = commitDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                                        Description = $"Committed: {commitMessage}"
                                                    },
                                                    MemberCreator = (object?)null
                                                });
                                            }
                                        }
                                    }
                                    
                                    _logger.LogInformation("Added {CommitCount} GitHub commit activities for student {StudentId}", 
                                        commitsData.Length, studentId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to fetch GitHub commits for student {StudentId}: Status {StatusCode}", 
                                    studentId, commitsResponse.StatusCode);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding GitHub commit activities for student {StudentId}: {Message}", studentId, ex.Message);
            }

            // Sort all activities by date (most recent first) and limit to 10
            // Convert to list of dynamic objects to sort by date
            var activitiesWithDates = recentActivities.Select(a =>
            {
                // Use reflection to get Date property from anonymous type
                var dateStr = a.GetType().GetProperty("Date")?.GetValue(a)?.ToString() ?? "";
                DateTime date = DateTime.MinValue;
                if (!string.IsNullOrEmpty(dateStr))
                {
                    DateTime.TryParse(dateStr, out date);
                }
                return new { Activity = a, Date = date };
            })
            .OrderByDescending(x => x.Date)
            .Take(10)
            .Select(x => x.Activity)
            .ToList();
            
            recentActivities = activitiesWithDates;
            
            _logger.LogInformation("Combined and sorted activities for student {StudentId}: {TotalCount} activities (limited to 10 most recent)", 
                studentId, recentActivities.Count);

            // Get last GitHub commit info (date and message) for the student
            Services.GitHubCommitInfo? lastCommitInfo = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(student.GithubUser) && !string.IsNullOrWhiteSpace(board.GithubUrl))
                {
                    // Parse GitHub URL to extract owner and repo
                    // Format: https://github.com/owner/repo or https://github.com/owner/repo.git
                    var githubUrl = board.GithubUrl.Trim();
                    if (githubUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                        githubUrl.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = githubUrl.Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase)
                                           .Replace("http://github.com/", "", StringComparison.OrdinalIgnoreCase)
                                           .TrimEnd('/');
                        
                        // Remove .git suffix if present
                        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        {
                            path = path.Substring(0, path.Length - 4);
                        }
                        
                        var parts = path.Split('/');
                        if (parts.Length >= 2)
                        {
                            var owner = parts[0];
                            var repo = parts[1];
                            
                            _logger.LogInformation("Fetching last GitHub commit for student {StudentId} (GitHubUser: {GithubUser}) in repo {Owner}/{Repo}", 
                                studentId, student.GithubUser, owner, repo);
                            
                            lastCommitInfo = await _gitHubService.GetLastCommitInfoByUserAsync(owner, repo, student.GithubUser);
                            
                            if (lastCommitInfo != null)
                            {
                                _logger.LogInformation("Found last commit for student {StudentId}: Date={CommitDate}, Message={CommitMessage}", 
                                    studentId, lastCommitInfo.CommitDate, lastCommitInfo.CommitMessage);
                            }
                            else
                            {
                                _logger.LogInformation("No commits found for student {StudentId} (GitHubUser: {GithubUser}) in repo {Owner}/{Repo}", 
                                    studentId, student.GithubUser, owner, repo);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Invalid GitHub URL format for board {BoardId}: {GithubUrl}", board.Id, board.GithubUrl);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("GitHub URL does not match expected format for board {BoardId}: {GithubUrl}", board.Id, board.GithubUrl);
                    }
                }
                else
                {
                    _logger.LogDebug("Skipping GitHub commit lookup for student {StudentId}: GithubUser={GithubUser}, GithubUrl={GithubUrl}", 
                        studentId, student.GithubUser ?? "null", board.GithubUrl ?? "null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching GitHub commit info for student {StudentId}: {Message}", studentId, ex.Message);
                // Don't fail the entire request if GitHub API call fails
            }

            // Analyze BoardMeetings for attendance statistics
            int attendedCalls = 0;
            int notAttendingCalls = 0;
            double participationRate = 0.0;
            
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email) && !string.IsNullOrWhiteSpace(board.Id))
                {
                    var currentTime = DateTime.UtcNow;
                    
                    // Get all meetings for this student and board
                    var meetings = await _context.BoardMeetings
                        .Where(bm => bm.BoardId == board.Id && bm.StudentEmail == student.Email)
                        .ToListAsync();
                    
                    _logger.LogInformation("Found {MeetingCount} BoardMeetings records for student {StudentId} (Email: {Email}) on board {BoardId}", 
                        meetings.Count, studentId, student.Email, board.Id);
                    
                    // Count attended calls
                    attendedCalls = meetings.Count(m => m.Attended);
                    
                    // Count not attending calls (Attended = false AND MeetingTime has passed)
                    notAttendingCalls = meetings.Count(m => !m.Attended && m.MeetingTime < currentTime);
                    
                    // Calculate participation rate
                    // Participation rate = attended / (attended + not attending) * 100
                    // Only calculate if there are meetings that have passed
                    var totalPastMeetings = attendedCalls + notAttendingCalls;
                    if (totalPastMeetings > 0)
                    {
                        participationRate = Math.Round((double)attendedCalls / totalPastMeetings * 100, 2);
                    }
                    
                    _logger.LogInformation("Meeting statistics for student {StudentId}: Attended={Attended}, NotAttending={NotAttending}, ParticipationRate={Rate}%", 
                        studentId, attendedCalls, notAttendingCalls, participationRate);
                }
                else
                {
                    _logger.LogDebug("Skipping meeting statistics for student {StudentId}: Email={Email}, BoardId={BoardId}", 
                        studentId, student.Email ?? "null", board.Id ?? "null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing BoardMeetings for student {StudentId}: {Message}", studentId, ex.Message);
                // Don't fail the entire request if meeting analysis fails
            }

            return Ok(new
            {
                Success = true,
                BoardId = board.Id,
                ProjectName = project.Title,
                OrganizationName = project.Organization?.Name ?? "N/A",
                OrganizationWebsite = project.Organization?.Website,
                OrgLogo = project.Organization?.Logo,
                TeamMembers = teamMembers,
                StartDate = startDate,
                DueDate = dueDate,
                WeeksLeft = weeksLeft,
                NextMeetingUrl = board.NextMeetingUrl,
                NextTeamMeeting = new
                {
                    Time = board.NextMeetingTime,
                    Url = board.NextMeetingUrl
                },
                RecentActivities = recentActivities,
                Stats = studentStats,
                GitHubCommit = lastCommitInfo != null ? new
                {
                    LastCommitDate = lastCommitInfo.CommitDate,
                    LastCommitMessage = lastCommitInfo.CommitMessage
                } : null,
                MeetingStatistics = new
                {
                    AttendedCalls = attendedCalls,
                    NotAttendingCalls = notAttendingCalls,
                    ParticipationRate = participationRate
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting board information for StudentId {StudentId}", studentId);
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while retrieving board information: {ex.Message}"
            });
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

    private SprintPlan CreateFallbackSprintPlan(Project project, List<Student> students, List<RoleInfo> teamRoles, int projectLengthWeeks, int sprintLengthWeeks)
    {
        var sprints = new List<Sprint>();
        var totalSprints = projectLengthWeeks / sprintLengthWeeks; // Calculate from configuration
        
        for (int i = 1; i <= totalSprints; i++)
        {
            var sprintStartDate = DateTime.UtcNow.AddDays((i - 1) * sprintLengthWeeks * 7);
            var sprintEndDate = sprintStartDate.AddDays(sprintLengthWeeks * 7 - 1);
            
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