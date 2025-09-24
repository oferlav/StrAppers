using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Microsoft.Extensions.Configuration;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]/use")]
public class BoardsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BoardsController> _logger;
    private readonly ITrelloService _trelloService;
    private readonly IAIService _aiService;
    private readonly IConfiguration _configuration;

    public BoardsController(ApplicationDbContext context, ILogger<BoardsController> logger, ITrelloService trelloService, IAIService aiService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _trelloService = trelloService;
        _aiService = aiService;
        _configuration = configuration;
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
            
            var sprintPlanRequest = new SprintPlanningRequest
            {
                ProjectId = request.ProjectId,
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                StartDate = DateTime.UtcNow, // Start from today
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
                _logger.LogError("AI service returned null SprintPlan");
                return StatusCode(500, "AI service returned null SprintPlan");
            }
            
            _logger.LogInformation("AI sprint plan generated successfully with {SprintCount} sprints", sprintPlanResponse.SprintPlan.Sprints?.Count ?? 0);
            // Note: SprintPlan is no longer stored in ProjectBoard model

            // Create Trello board
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
            
            _logger.LogInformation("Trello board created with ID: {BoardId}, URL: {BoardUrl}", trelloResponse.BoardId, trelloResponse.BoardUrl);
            var trelloBoardId = trelloResponse.BoardId;

            // Find admin student (first one with IsAdmin = true)
            var adminStudent = students.FirstOrDefault(s => s.IsAdmin);
            _logger.LogInformation("Admin student: {AdminId}", adminStudent?.Id);

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

            return Ok(new CreateBoardResponse
            {
                Success = true,
                Message = "Board created successfully!",
                BoardId = trelloBoardId,
                BoardUrl = trelloResponse.BoardUrl,
                ProjectId = request.ProjectId,
                StudentCount = students.Count,
                InvitedUsers = trelloResponse.InvitedUsers
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
}

// Request/Response DTOs
public class CreateBoardRequest
{
    public int ProjectId { get; set; }
    public List<int> StudentIds { get; set; } = new();
}

public class CreateBoardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string? BoardUrl { get; set; }
    public int ProjectId { get; set; }
    public int StudentCount { get; set; }
    public List<TrelloInvitedUser> InvitedUsers { get; set; } = new List<TrelloInvitedUser>();
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