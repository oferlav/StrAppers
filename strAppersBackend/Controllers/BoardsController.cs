using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using System.Net;
using Npgsql;
using System.Security.Cryptography;

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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<TestingConfig> _testingConfig;

    public BoardsController(ApplicationDbContext context, ILogger<BoardsController> logger, ITrelloService trelloService, IAIService aiService, IGitHubService gitHubService, IMicrosoftGraphService graphService, ISmtpEmailService smtpEmailService, IConfiguration configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IOptions<TestingConfig> testingConfig)
    {
        _context = context;
        _logger = logger;
        _trelloService = trelloService;
        _aiService = aiService;
        _gitHubService = gitHubService;
        _graphService = graphService;
        _smtpEmailService = smtpEmailService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _testingConfig = testingConfig;
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
                .Include(s => s.ProgrammingLanguage)  // Include ProgrammingLanguage for backend code generation
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

            var firstDayOfWeekStr = _configuration["Trello:FirstDayOfWeek"] ?? "Sunday";
            var localTimeStr = _configuration["Trello:LocalTime"] ?? "GMT+2";
            var firstDayOfWeek = strAppersBackend.Services.TrelloBoardScheduleHelper.ParseFirstDayOfWeek(firstDayOfWeekStr);
            var localOffset = strAppersBackend.Services.TrelloBoardScheduleHelper.ParseLocalTimeOffset(localTimeStr);
            var kickoffUtc = strAppersBackend.Services.TrelloBoardScheduleHelper.GetNextKickoffUtc(firstDayOfWeek, localOffset);
            var kickoffDateTimeIso = kickoffUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            _logger.LogInformation("Trello schedule: FirstDayOfWeek={FirstDayOfWeek}, LocalTime={LocalTime}, Kickoff (first day of week 10:00 local) UTC={KickoffUtc}", firstDayOfWeekStr, localTimeStr, kickoffUtc);

            var useDBProjectBoard = _configuration.GetValue<bool>("Trello:UseDBProjectBoard", true);
            var visibleSprints = _configuration.GetValue<int>("Trello:VisibleSprints", 2);
            var overriddenSprints = new Dictionary<int, (string ListId, DateTime? DueDateUtc)>();
            TrelloProjectCreationRequest? trelloRequest = null;
            object? sprintPlanForStorage = null;
            var useSavedTrelloJson = false;
            if (useDBProjectBoard && !string.IsNullOrWhiteSpace(project.TrelloBoardJson))
            {
                try
                {
                    var saved = System.Text.Json.JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
                    if (saved != null && saved.SprintPlan != null)
                    {
                        useSavedTrelloJson = true;
                        trelloRequest = saved;
                        trelloRequest.ProjectId = request.ProjectId;
                        trelloRequest.ProjectTitle = project.Title ?? trelloRequest.ProjectTitle;
                        trelloRequest.ProjectDescription = project.Description ?? trelloRequest.ProjectDescription;
                        trelloRequest.StudentEmails = students.Select(s => s.Email).ToList();
                        trelloRequest.ProjectLengthWeeks = projectLengthWeeks;
                        trelloRequest.SprintLengthWeeks = sprintLengthWeeks;
                        trelloRequest.TeamMembers = students.Select(s => new TrelloTeamMember
                        {
                            Email = s.Email,
                            FirstName = s.FirstName,
                            LastName = s.LastName,
                            RoleId = s.StudentRoles?.FirstOrDefault()?.RoleId ?? 0,
                            RoleName = s.StudentRoles?.FirstOrDefault()?.Role?.Name ?? "Team Member"
                        }).ToList();
                        sprintPlanForStorage = trelloRequest.SprintPlan;
                        // Filter out cards for roles not in the current team (full template may have all roles)
                        var teamRoleNames = students
                            .SelectMany(s => s.StudentRoles ?? Array.Empty<StudentRole>())
                            .Where(sr => sr?.Role != null)
                            .Select(sr => sr!.Role!.Name)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        // When team has Full Stack Developer, include both Frontend and Backend developer cards from template
                        if (teamRoleNames.Any(r => r.Contains("Fullstack", StringComparison.OrdinalIgnoreCase) || r.Contains("Full Stack", StringComparison.OrdinalIgnoreCase)))
                        {
                            teamRoleNames.Add("Frontend Developer");
                            teamRoleNames.Add("Backend Developer");
                        }
                        if (trelloRequest.SprintPlan?.Cards != null && teamRoleNames.Count > 0)
                        {
                            var before = trelloRequest.SprintPlan.Cards.Count;
                            trelloRequest.SprintPlan.Cards = trelloRequest.SprintPlan.Cards
                                .Where(c => !string.IsNullOrEmpty(c.RoleName) && teamRoleNames.Contains(c.RoleName))
                                .ToList();
                            var removed = before - trelloRequest.SprintPlan.Cards.Count;
                            if (removed > 0)
                                _logger.LogInformation("Filtered {Removed} cards for roles not in team (template had all roles). Sending {Count} cards to Trello.", removed, trelloRequest.SprintPlan.Cards.Count);
                        }
                        // UseDBProjectBoard: override saved sprint due dates (first day of week = sprint start, last day of weekend = due)
                        if (useDBProjectBoard && trelloRequest.SprintPlan?.Cards != null)
                        {
                            var sprintWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
                            var offsetMin = (int)localOffset.TotalMinutes;
                            foreach (var card in trelloRequest.SprintPlan.Cards)
                            {
                                var listName = card.ListName ?? "";
                                var sprintMatch = Regex.Match(listName, @"Sprint\s*(\d+)", RegexOptions.IgnoreCase);
                                if (sprintMatch.Success && int.TryParse(sprintMatch.Groups[1].Value, out var sprintNum) && sprintNum >= 1)
                                {
                                    card.DueDate = GetSprintDueDateUtc(kickoffUtc, sprintNum, firstDayOfWeek, offsetMin, sprintWeeks);
                                }
                            }
                            _logger.LogInformation("[BOARD-CREATE] Overrode sprint due dates from Trello:FirstDayOfWeek={FirstDay}, LocalTime={LocalTime}", firstDayOfWeekStr, localTimeStr);
                        }
                        _logger.LogInformation("Using saved TrelloBoardJson for project {ProjectId} instead of AI", request.ProjectId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize Project.TrelloBoardJson, falling back to AI");
                }
            }

            if (!useSavedTrelloJson)
            {
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
            
            // Fetch ProjectModules for this project to pass module IDs to AI
            var projectModules = await _context.ProjectModules
                .Where(pm => pm.ProjectId == request.ProjectId && pm.ModuleType != 3) // Exclude data model modules (ModuleType 3)
                .OrderBy(pm => pm.Sequence)
                .Select(pm => new strAppersBackend.Models.ProjectModuleInfo
                {
                    Id = pm.Id,
                    Title = pm.Title,
                    Description = pm.Description
                })
                .ToListAsync();
            
            _logger.LogInformation("Found {ModuleCount} project modules for ProjectId {ProjectId}", 
                projectModules.Count, request.ProjectId);
            
            var sprintPlanRequest = new SprintPlanningRequest
            {
                ProjectId = request.ProjectId,
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                StartDate = DateTime.UtcNow, // Start from today
                SystemDesign = project.SystemDesign, // Include system design for AI sprint generation
                TeamRoles = roleGroups, // Include team roles for proper task distribution
                ProjectModules = projectModules, // Include project modules with their database IDs
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
            
            SprintPlanningResponse? sprintPlanResponse;
            
            // Check if we should skip AI service (for testing)
            _logger.LogInformation("[TESTING CONFIG] Before AI service check - SkipAIService: {SkipAIService}", _testingConfig.Value.SkipAIService);
            if (_testingConfig.Value.SkipAIService)
            {
                _logger.LogInformation("Testing mode: Skipping AI service, using fallback sprint plan");
                var fallbackSprintPlan = CreateFallbackSprintPlan(project, students, roleGroups, projectLengthWeeks, sprintLengthWeeks);
                sprintPlanResponse = new SprintPlanningResponse
                {
                    Success = true,
                    SprintPlan = fallbackSprintPlan,
                    Message = "Using fallback sprint plan (Testing mode: SkipAIService=true)"
                };
            }
            else
            {
                _logger.LogInformation("Calling AI service with {RequestStudentCount} students", sprintPlanRequest.Students.Count);
                sprintPlanResponse = await _aiService.GenerateSprintPlanAsync(sprintPlanRequest);
                
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
                    EstimatedHours = t.EstimatedHours,
                    Status = t.Status ?? "To Do",
                    Risk = t.Risk ?? "Medium",
                    ModuleId = t.ModuleId ?? string.Empty,
                    CardId = t.CardId ?? string.Empty,
                    Dependencies = t.Dependencies ?? new List<string>(),
                    Branched = t.Branched,
                    ChecklistItems = t.ChecklistItems ?? new List<string>()
                }) ?? new List<TrelloCard>()).ToList() ?? new List<TrelloCard>(),
                TotalSprints = sprintPlanResponse.SprintPlan.TotalSprints,
                TotalTasks = sprintPlanResponse.SprintPlan.TotalTasks,
                EstimatedWeeks = sprintPlanResponse.SprintPlan.EstimatedWeeks
            };

            _logger.LogInformation("Creating TrelloProjectCreationRequest");
                trelloRequest = new TrelloProjectCreationRequest
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
                sprintPlanForStorage = sprintPlanResponse.SprintPlan;
            }

            if (trelloRequest == null)
            {
                _logger.LogError("TrelloProjectCreationRequest is null (neither saved JSON nor AI path produced a request)");
                return StatusCode(500, "Failed to build Trello board request");
            }

            // When NextSprintOnlyVisability is true, create VisibleSprints (system) + 1 (empty) + Bugs. E.g. VisibleSprints=2 → Sprint1, Sprint2 (system), Sprint3 (empty), Bugs.
            var nextSprintOnlyVisability = _configuration.GetValue<bool>("Trello:NextSprintOnlyVisability", true);
            if (nextSprintOnlyVisability && trelloRequest.SprintPlan?.Lists != null && trelloRequest.SprintPlan.Lists.Count > visibleSprints + 1)
            {
                var canonicalSprintNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7" };
                var listNamesToInclude = canonicalSprintNames.Take(visibleSprints + 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
                listNamesToInclude.Add("Bugs");
                var listsToInclude = new List<TrelloList>();
                foreach (var name in canonicalSprintNames.Take(visibleSprints + 1))
                {
                    var found = trelloRequest.SprintPlan.Lists.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                        listsToInclude.Add(found);
                }
                var bugsList = trelloRequest.SprintPlan.Lists.FirstOrDefault(l => string.Equals(l.Name, "Bugs", StringComparison.OrdinalIgnoreCase));
                if (bugsList != null && !listsToInclude.Contains(bugsList))
                    listsToInclude.Add(bugsList);
                var listNames = listsToInclude.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                trelloRequest.SprintPlan.Lists = listsToInclude;
                if (trelloRequest.SprintPlan.Cards != null)
                {
                    trelloRequest.SprintPlan.Cards = trelloRequest.SprintPlan.Cards
                        .Where(c => c.ListName != null && listNames.Contains(c.ListName))
                        .ToList();
                }
                _logger.LogInformation("NextSprintOnlyVisability: limited board to VisibleSprints={VisibleSprints} (Sprint 1..{LastSystem} system, Sprint {Empty} empty) + Bugs ({ListNames}), {CardCount} cards",
                    visibleSprints, visibleSprints, visibleSprints + 1, string.Join(", ", listNames), trelloRequest.SprintPlan.Cards?.Count ?? 0);
            }

            // Ensure list order is canonical (Sprint 1, 2, ... 7, Bugs last) and Position = index so Trello creates lists left-to-right correctly
            if (trelloRequest.SprintPlan?.Lists != null && trelloRequest.SprintPlan.Lists.Count > 0)
            {
                var canonicalNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7", "Bugs" };
                var used = new HashSet<TrelloList>();
                var ordered = new List<TrelloList>();
                foreach (var name in canonicalNames)
                {
                    var found = trelloRequest.SprintPlan.Lists.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (found != null && !used.Contains(found))
                    {
                        used.Add(found);
                        ordered.Add(found);
                    }
                }
                foreach (var l in trelloRequest.SprintPlan.Lists)
                    if (!used.Contains(l))
                        ordered.Add(l);
                trelloRequest.SprintPlan.Lists = ordered;
                // Use 1-based pos: Trello treats pos=0 as "append to end", which put Sprint 1 last (after Bugs). pos=1,2,... keeps left-to-right order.
                for (var i = 0; i < ordered.Count; i++)
                    ordered[i].Position = i + 1;
            }

            // Override sprint due dates: sprint starts first day of week, due = last day of weekend (and when UseDBProjectBoard, override saved JSON dates)
            if (trelloRequest.SprintPlan?.Lists != null && trelloRequest.SprintPlan.Cards != null)
            {
                foreach (var list in trelloRequest.SprintPlan.Lists)
                {
                    var sprintNum = ParseSprintNumberFromListName(list.Name);
                    if (sprintNum.HasValue && sprintNum.Value >= 1)
                    {
                        var (startUtc, dueUtc) = strAppersBackend.Services.TrelloBoardScheduleHelper.GetSprintStartAndDueUtc(sprintNum.Value, kickoffUtc, firstDayOfWeek, localOffset);
                        list.EndDate = dueUtc;
                        foreach (var card in trelloRequest.SprintPlan.Cards.Where(c => string.Equals(c.ListName, list.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            card.DueDate = dueUtc;
                        }
                        _logger.LogDebug("Sprint {SprintNum} list '{ListName}': DueDate set to {DueUtc}", sprintNum.Value, list.Name, dueUtc);
                    }
                }
            }

            TrelloProjectCreationResponse? trelloResponse;
            string trelloBoardId;
            
            // Declare URL variables early so they can be used in SkipTrelloApi block
            string? backendRepositoryUrl = null;
            string? frontendRepositoryUrl = null;
            string? webApiUrl = null;
            string? swaggerUrl = null;
            
            // Check if we should skip Trello API (for testing)
            _logger.LogInformation("[TESTING CONFIG] Before Trello API check - SkipTrelloApi: {SkipTrelloApi}", _testingConfig.Value.SkipTrelloApi);
            if (_testingConfig.Value.SkipTrelloApi)
            {
                _logger.LogInformation("Testing mode: Skipping Trello API, generating mock board ID");
                // Generate a mock board ID (24 hex characters, similar to Trello format)
                trelloBoardId = Guid.NewGuid().ToString("N").Substring(0, 24);
                trelloResponse = new TrelloProjectCreationResponse
                {
                    Success = true,
                    BoardId = trelloBoardId,
                    BoardUrl = $"https://trello.com/b/{trelloBoardId}",
                    BoardName = project.Title,
                    Message = "Mock Trello board (Testing mode: SkipTrelloApi=true)"
                };
                _logger.LogInformation("Mock Trello board created with ID: {BoardId}, URL: {BoardUrl}", trelloBoardId, trelloResponse.BoardUrl);
                
                // Set mock URLs for testing (will be updated later when actual URLs are available)
                webApiUrl = $"https://webapi{trelloBoardId}.up.railway.app";
                swaggerUrl = $"{webApiUrl}/swagger";
                backendRepositoryUrl = $"https://github.com/skill-in-projects/backend_{trelloBoardId}";
                frontendRepositoryUrl = $"https://github.com/skill-in-projects/{trelloBoardId}";
                _logger.LogInformation("Testing mode: Set mock URLs - WebApiUrl: {WebApiUrl}, BackendUrl: {BackendUrl}, FrontendUrl: {FrontendUrl}", 
                    webApiUrl, backendRepositoryUrl, frontendRepositoryUrl);
            }
            else
            {
                _logger.LogInformation("Calling Trello service to create board");
                trelloResponse = await _trelloService.CreateProjectWithSprintsAsync(trelloRequest, project.Title);
                
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
                if (!string.IsNullOrEmpty(trelloResponse.SystemBoardId))
                {
                    _logger.LogInformation("SystemBoard created with ID: {SystemBoardId}, URL: {SystemBoardUrl}", trelloResponse.SystemBoardId, trelloResponse.SystemBoardUrl);
                }
                trelloBoardId = trelloResponse.BoardId;

                // When CreatePMEmptyBoard and UseDBProjectBoard: override Sprint 1..VisibleSprints on EmptyBoard with SystemBoard content (no merge)
                if (useDBProjectBoard && !string.IsNullOrEmpty(trelloResponse.SystemBoardId))
                {
                    for (var sprintNum = 1; sprintNum <= visibleSprints; sprintNum++)
                    {
                        var sprintNameNoSpace = $"Sprint{sprintNum}";
                        var sprintNameWithSpace = $"Sprint {sprintNum}";
                        _logger.LogInformation("[BOARD-CREATE] Overriding {SprintName} on EmptyBoard (BoardId={BoardId}, ProjectId={ProjectId})", sprintNameWithSpace, trelloResponse.BoardId, request.ProjectId);
                        var systemSprint = await _trelloService.GetSprintFromBoardAsync(trelloResponse.SystemBoardId, sprintNameNoSpace)
                            ?? await _trelloService.GetSprintFromBoardAsync(trelloResponse.SystemBoardId, sprintNameWithSpace);
                        if (systemSprint?.Cards == null || systemSprint.Cards.Count == 0)
                        {
                            _logger.LogWarning("[BOARD-CREATE] SystemBoard {SprintName} not found or has no cards (SystemBoardId={SystemBoardId}). Skipping override.", sprintNameWithSpace, trelloResponse.SystemBoardId);
                            continue;
                        }
                        var liveSprint = await _trelloService.GetSprintFromBoardAsync(trelloResponse.BoardId, sprintNameNoSpace)
                            ?? await _trelloService.GetSprintFromBoardAsync(trelloResponse.BoardId, sprintNameWithSpace);
                        if (liveSprint == null)
                        {
                            _logger.LogWarning("[BOARD-CREATE] Live board {SprintName} list not found (BoardId={BoardId}). Skipping override.", sprintNameWithSpace, trelloResponse.BoardId);
                            continue;
                        }
                        var (overrideSuccess, overrideError) = await _trelloService.OverrideSprintOnBoardAsync(trelloResponse.BoardId, liveSprint.ListId, systemSprint.Cards);
                        if (!overrideSuccess)
                        {
                            _logger.LogWarning("[BOARD-CREATE] Failed to override {SprintName} on EmptyBoard: {Error}", sprintNameWithSpace, overrideError);
                        }
                        else
                        {
                            _logger.LogInformation("[BOARD-CREATE] {SprintName} on EmptyBoard overridden with SystemBoard. Will upsert ProjectBoardSprintMerge after ProjectBoard is saved.", sprintNameWithSpace);
                            var dueDateRaw = systemSprint.Cards.Count > 0 ? systemSprint.Cards[0].DueDate : null;
                            overriddenSprints[sprintNum] = (liveSprint.ListId, dueDateRaw.HasValue ? ToUtcForDb(dueDateRaw.Value) : (DateTime?)null);
                        }
                    }
                }
                else
                {
                    if (!useDBProjectBoard)
                        _logger.LogInformation("[BOARD-CREATE] UseDBProjectBoard is false; skipping sprint overrides and ProjectBoardSprintMerge.");
                    else if (string.IsNullOrEmpty(trelloResponse.SystemBoardId))
                        _logger.LogInformation("[BOARD-CREATE] No SystemBoardId (CreatePMEmptyBoard may be false or single board); skipping sprint overrides and ProjectBoardSprintMerge.");
                }
            }

            // Create Neon database for the project
            string? dbConnectionString = null;
            string? dbPassword = null;
            string? createdNeonProjectId = null;
            string? createdBranchId = null;
            try
            {
                var dbName = $"AppDB_{trelloBoardId}";
                _logger.LogInformation("Creating Neon database: {DbName}", dbName);
                
                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (!string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here" &&
                    !string.IsNullOrWhiteSpace(neonBaseUrl))
                {
                    // Step 0: Create a new Neon project for this tenant (project-per-tenant isolation)
                    var projectName = $"Project-{trelloBoardId}";
                    _logger.LogInformation("🏗️ [NEON] Creating isolated Neon project for database '{DbName}': {ProjectName}", dbName, projectName);
                    var projectResult = await CreateNeonProjectAsync(neonApiKey, neonBaseUrl, projectName);
                    
                    if (!projectResult.Success || string.IsNullOrEmpty(projectResult.ProjectId))
                    {
                        _logger.LogError("❌ [NEON] Failed to create Neon project for database '{DbName}': {Error}", 
                            dbName, projectResult.ErrorMessage ?? "Unknown error");
                        throw new InvalidOperationException($"Failed to create Neon project for database '{dbName}': {projectResult.ErrorMessage}");
                    }

                    createdNeonProjectId = projectResult.ProjectId;
                    var defaultBranchId = projectResult.DefaultBranchId;
                    var projectOperationIds = projectResult.OperationIds;
                    _logger.LogInformation("✅ [NEON] Created Neon project '{ProjectId}' for database '{DbName}'", createdNeonProjectId, dbName);

                    // Step 0.5: Wait for Project Creation Operations to Finish
                    if (projectOperationIds.Count > 0)
                    {
                        _logger.LogInformation("⏳ [NEON] Step 0.5: Waiting for {Count} project creation operations to finish before creating branch", 
                            projectOperationIds.Count);

                        using var projectHttpClient = _httpClientFactory.CreateClient();
                        projectHttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                        projectHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        var projectReady = false;
                        var projectOperationPollCount = 0;
                        var maxProjectOperationPolls = 60; // 60 retries × 2 seconds = 2 minutes max
                        var projectOperationPollDelay = 2000;
                        var trackedProjectOperations = new Dictionary<string, string>();
                        var projectOperationsApiUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/operations";

                        while (projectOperationPollCount < maxProjectOperationPolls && !projectReady)
                        {
                            try
                            {
                                var opsResponse = await projectHttpClient.GetAsync(projectOperationsApiUrl);
                                if (opsResponse.IsSuccessStatusCode)
                                {
                                    var opsContent = await opsResponse.Content.ReadAsStringAsync();
                                    var opsDoc = JsonDocument.Parse(opsContent);

                                    trackedProjectOperations.Clear();

                                    if (opsDoc.RootElement.TryGetProperty("operations", out var opsArray) && opsArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var op in opsArray.EnumerateArray())
                                        {
                                            if (op.TryGetProperty("id", out var idProp))
                                            {
                                                var opId = idProp.GetString();
                                                if (projectOperationIds.Contains(opId))
                                                {
                                                    var opStatus = op.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                                                    var opAction = op.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : "unknown";
                                                    trackedProjectOperations[opId] = $"{opAction}:{opStatus}";
                                                }
                                            }
                                        }
                                    }

                                    if (projectOperationIds.Count > 0)
                                    {
                                        var allTracked = projectOperationIds.All(id => trackedProjectOperations.ContainsKey(id));
                                        var allFinished = allTracked && trackedProjectOperations.Values.All(status => 
                                            status.Contains(":finished") || status.Contains(":completed"));

                                        if (allFinished && allTracked)
                                        {
                                            projectReady = true;
                                            _logger.LogInformation("✅ [NEON] Step 0.5 SUCCESS: All {Count} project creation operations finished after {Polls} polls", 
                                                projectOperationIds.Count, projectOperationPollCount);
                                            break;
                                        }
                                    }
                                }

                                if (!projectReady)
                                {
                                    projectOperationPollCount++;
                                    if (projectOperationPollCount < maxProjectOperationPolls)
                                    {
                                        var statuses = string.Join(", ", trackedProjectOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                                        _logger.LogInformation("⏳ [NEON] Step 0.5: Waiting for project operations (poll {Poll}/{MaxPolls}, found {Found}/{Total}). Statuses: {Statuses}", 
                                            projectOperationPollCount, maxProjectOperationPolls, trackedProjectOperations.Count, projectOperationIds.Count, statuses);
                                        await Task.Delay(projectOperationPollDelay);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ [NEON] Error polling project operations (attempt {Attempt})", projectOperationPollCount + 1);
                                projectOperationPollCount++;
                                if (projectOperationPollCount < maxProjectOperationPolls)
                                {
                                    await Task.Delay(projectOperationPollDelay);
                                }
                            }
                        }

                        if (!projectReady)
                        {
                            _logger.LogWarning("⚠️ [NEON] Step 0.5: Project operations did not finish, but proceeding with branch creation anyway");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("⏳ [NEON] Step 0.5: No project creation operations found, proceeding immediately");
                    }

                    // Step 1: Create a new branch in the new project (use default branch as parent if available)
                    _logger.LogInformation("🌿 [NEON] Creating branch in isolated project for database '{DbName}'", dbName);
                    var branchResult = await CreateNeonBranchAsync(neonApiKey, neonBaseUrl, createdNeonProjectId, parentBranchId: defaultBranchId);
                    
                    if (!branchResult.Success || string.IsNullOrEmpty(branchResult.BranchId))
                    {
                        _logger.LogError("❌ [NEON] Failed to create branch for database '{DbName}': {Error}", 
                            dbName, branchResult.ErrorMessage ?? "Unknown error");
                        throw new InvalidOperationException($"Failed to create Neon branch for database '{dbName}': {branchResult.ErrorMessage}");
                    }

                    createdBranchId = branchResult.BranchId;
                    var endpointHost = branchResult.EndpointHost;
                    var operationIds = branchResult.OperationIds;
                    
                    _logger.LogInformation("✅ [NEON] Created branch '{BranchId}' for database '{DbName}'", createdBranchId, dbName);
                    if (!string.IsNullOrEmpty(branchResult.EndpointId))
                    {
                        _logger.LogInformation("🌐 [NEON] Branch endpoint ID from API: {EndpointId}", branchResult.EndpointId);
                    }
                    if (!string.IsNullOrEmpty(endpointHost))
                    {
                        _logger.LogInformation("🌐 [NEON] Branch endpoint host from API: {EndpointHost}", endpointHost);
                    }
                    if (operationIds.Count > 0)
                    {
                        _logger.LogInformation("🔄 [NEON] Tracking {Count} operations: {OperationIds}", operationIds.Count, string.Join(", ", operationIds));
                    }

                    // Step 2: Poll Operations API until SPECIFIC operations from Step 1 are ready
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var branchReady = false; // Declare outside if block for use in Step 2.5
                    
                    if (operationIds.Count > 0)
                    {
                        _logger.LogInformation("🔍 [NEON] Step 2: Polling operations API for {Count} operations until branch '{BranchId}' is ready", 
                            operationIds.Count, createdBranchId);

                        var operationsApiUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/operations";
                        var maxOperationPolls = 60; // 60 retries × 2 seconds = 2 minutes max
                        var operationPollDelay = 2000; // 2 seconds
                        var operationPollCount = 0;
                        var trackedOperations = new Dictionary<string, string>(); // operationId -> status

                        while (operationPollCount < maxOperationPolls && !branchReady)
                        {
                            try
                            {
                                var opsResponse = await httpClient.GetAsync(operationsApiUrl);
                                if (opsResponse.IsSuccessStatusCode)
                                {
                                    var opsContent = await opsResponse.Content.ReadAsStringAsync();
                                    var opsDoc = JsonDocument.Parse(opsContent);

                                    trackedOperations.Clear();
                                    
                                    if (opsDoc.RootElement.TryGetProperty("operations", out var opsArray) && opsArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var op in opsArray.EnumerateArray())
                                        {
                                            if (op.TryGetProperty("id", out var idProp))
                                            {
                                                var opId = idProp.GetString();
                                                // Only track operations we got from Step 1
                                                if (operationIds.Contains(opId))
                                                {
                                                    var opStatus = op.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";
                                                    trackedOperations[opId] = opStatus;
                                                }
                                            }
                                        }
                                    }

                                    // Check if ALL tracked operations from Step 1 are finished
                                    if (operationIds.Count > 0)
                                    {
                                        var allTracked = operationIds.All(id => trackedOperations.ContainsKey(id));
                                        var allFinished = allTracked && trackedOperations.Values.All(status => 
                                            status == "finished" || status == "completed");

                                        if (allFinished && allTracked)
                                        {
                                            branchReady = true;
                                            _logger.LogInformation("✅ [NEON] Step 2 SUCCESS: All {Count} operations finished for branch '{BranchId}' after {Polls} polls", 
                                                operationIds.Count, createdBranchId, operationPollCount);
                                            break;
                                        }
                                    }
                                }

                                if (!branchReady)
                                {
                                    operationPollCount++;
                                    if (operationPollCount < maxOperationPolls)
                                    {
                                        var statuses = string.Join(", ", trackedOperations.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                                        _logger.LogInformation("⏳ [NEON] Step 2: Waiting for operations (poll {Poll}/{MaxPolls}, found {Found}/{Total}). Statuses: {Statuses}", 
                                            operationPollCount, maxOperationPolls, trackedOperations.Count, operationIds.Count, statuses);
                                        await Task.Delay(operationPollDelay);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ [NEON] Error polling operations (attempt {Attempt})", operationPollCount + 1);
                                operationPollCount++;
                                if (operationPollCount < maxOperationPolls)
                                {
                                    await Task.Delay(operationPollDelay);
                                }
                            }
                        }

                        if (!branchReady)
                        {
                            _logger.LogWarning("⚠️ [NEON] Step 2: Operations polling did not confirm all operations finished after {Polls} polls, but proceeding anyway", operationPollCount);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [NEON] No operation IDs found in branch creation response - skipping operations polling");
                    }

                    // ============================================================================
                    // STEP 2.5: Verify endpoint after operations finish (endpoint may have changed)
                    // ============================================================================
                    string? verifiedEndpointHost = endpointHost;
                    string? verifiedEndpointId = branchResult.EndpointId;
                    
                    // Verify endpoint regardless of operations polling status (as long as we have a branch ID)
                    if (!string.IsNullOrEmpty(createdBranchId))
                    {
                        _logger.LogInformation("🔍 [NEON] Step 2.5: Verifying endpoint after operations finished for branch '{BranchId}'", createdBranchId);
                        _logger.LogInformation("🔍 [NEON] Step 2.5: Original endpoint from Step 1: {EndpointHost}", endpointHost);
                        
                        try
                        {
                            // First try: Query branch API (may not have endpoints, but worth checking)
                            var verifyBranchApiUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}";
                            var branchApiResponse = await httpClient.GetAsync(verifyBranchApiUrl);
                            
                            if (branchApiResponse.IsSuccessStatusCode)
                            {
                                var branchApiContent = await branchApiResponse.Content.ReadAsStringAsync();
                                var branchApiDoc = JsonDocument.Parse(branchApiContent);
                                
                                // Try to get endpoint from branch API response
                                var endpointsArray = default(JsonElement);
                                var endpointsFound = false;
                                
                                if (branchApiDoc.RootElement.TryGetProperty("endpoints", out var rootEndpointsProp) && 
                                    rootEndpointsProp.ValueKind == JsonValueKind.Array)
                                {
                                    endpointsArray = rootEndpointsProp;
                                    endpointsFound = true;
                                    _logger.LogInformation("🔍 [NEON] Step 2.5: Found 'endpoints' array at root level");
                                }
                                else if (branchApiDoc.RootElement.TryGetProperty("branch", out var branchObj) &&
                                         branchObj.TryGetProperty("endpoints", out var branchEndpointsProp) && 
                                         branchEndpointsProp.ValueKind == JsonValueKind.Array)
                                {
                                    endpointsArray = branchEndpointsProp;
                                    endpointsFound = true;
                                    _logger.LogInformation("🔍 [NEON] Step 2.5: Found 'endpoints' array under 'branch'");
                                }
                                
                                if (endpointsFound && endpointsArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ep in endpointsArray.EnumerateArray())
                                    {
                                        if (ep.TryGetProperty("branch_id", out var epBranchIdProp) &&
                                            epBranchIdProp.GetString() == createdBranchId)
                                        {
                                            if (ep.TryGetProperty("host", out var epHostProp))
                                            {
                                                var verifiedHost = epHostProp.GetString();
                                                if (!string.IsNullOrEmpty(verifiedHost))
                                                {
                                                    verifiedEndpointHost = verifiedHost;
                                                    _logger.LogInformation("✅ [NEON] Step 2.5: Verified endpoint host from branch API: {EndpointHost}", verifiedEndpointHost);
                                                    
                                                    if (ep.TryGetProperty("id", out var epIdProp))
                                                    {
                                                        verifiedEndpointId = epIdProp.GetString();
                                                        _logger.LogInformation("✅ [NEON] Step 2.5: Verified endpoint ID: {EndpointId}", verifiedEndpointId);
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                // Fallback: Query project endpoints API if branch API didn't have endpoints
                                if (string.IsNullOrEmpty(verifiedEndpointHost) || verifiedEndpointHost == endpointHost)
                                {
                                    _logger.LogInformation("🔍 [NEON] Step 2.5: Branch API did not provide endpoint, querying project endpoints API");
                                    
                                    var projectEndpointsUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/endpoints";
                                    var endpointsResponse = await httpClient.GetAsync(projectEndpointsUrl);
                                    
                                    if (endpointsResponse.IsSuccessStatusCode)
                                    {
                                        var endpointsContent = await endpointsResponse.Content.ReadAsStringAsync();
                                        var endpointsDoc = JsonDocument.Parse(endpointsContent);
                                        
                                        if (endpointsDoc.RootElement.TryGetProperty("endpoints", out var projectEndpointsProp) && 
                                            projectEndpointsProp.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var ep in projectEndpointsProp.EnumerateArray())
                                            {
                                                if (ep.TryGetProperty("branch_id", out var epBranchIdProp) &&
                                                    epBranchIdProp.GetString() == createdBranchId)
                                                {
                                                    if (ep.TryGetProperty("host", out var epHostProp))
                                                    {
                                                        var verifiedHost = epHostProp.GetString();
                                                        if (!string.IsNullOrEmpty(verifiedHost))
                                                        {
                                                            verifiedEndpointHost = verifiedHost;
                                                            _logger.LogInformation("✅ [NEON] Step 2.5: Verified endpoint host from project endpoints API: {EndpointHost}", verifiedEndpointHost);
                                                            
                                                            if (ep.TryGetProperty("id", out var epIdProp))
                                                            {
                                                                verifiedEndpointId = epIdProp.GetString();
                                                                _logger.LogInformation("✅ [NEON] Step 2.5: Verified endpoint ID: {EndpointId}", verifiedEndpointId);
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                if (string.IsNullOrEmpty(verifiedEndpointHost) || verifiedEndpointHost == endpointHost)
                                {
                                    _logger.LogInformation("✅ [NEON] Step 2.5: Using original endpoint from Step 1: {EndpointHost}", endpointHost);
                                }
                                else
                                {
                                    _logger.LogInformation("✅ [NEON] Step 2.5: Endpoint verified and updated: {OriginalEndpoint} -> {VerifiedEndpoint}", 
                                        endpointHost, verifiedEndpointHost);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [NEON] Step 2.5: Failed to query branch API: {StatusCode}. Using original endpoint from Step 1", 
                                    branchApiResponse.StatusCode);
                            }
                        }
                        catch (Exception verifyEx)
                        {
                            _logger.LogWarning(verifyEx, "⚠️ [NEON] Step 2.5: Exception verifying endpoint, using original from Step 1");
                        }
                    }
                    
                    // Use verified endpoint if available, otherwise fall back to original
                    endpointHost = verifiedEndpointHost ?? endpointHost;
                    if (!string.IsNullOrEmpty(verifiedEndpointId))
                    {
                        _logger.LogInformation("🌐 [NEON] Step 2.5: Final verified endpoint ID: {EndpointId}", verifiedEndpointId);
                    }

                    // Step 3: Create database in the new branch
                    _logger.LogInformation("📦 [NEON] Step 3: Creating database '{DbName}' in branch '{BranchId}'", dbName, createdBranchId);

                    var createDbRequest = new
                    {
                        database = new
                        {
                            name = dbName,
                            owner_name = neonDefaultOwnerName
                        }
                    };

                    var requestBody = JsonSerializer.Serialize(createDbRequest);
                    var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

                    var apiUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}/databases";
                    var response = await httpClient.PostAsync(apiUrl, content);
                    
                    // Retry logic for database creation (Neon may have locks from previous operations)
                    var maxDbRetries = 5;
                    var dbRetryDelay = 3000; // 3 seconds between retries
                    var retryCount = 0;
                    
                    // Retry if we get 423 Locked (project has running conflicting operations)
                    while (response.StatusCode == System.Net.HttpStatusCode.Locked && retryCount < maxDbRetries)
                    {
                        retryCount++;
                        _logger.LogWarning("⚠️ [NEON] Database creation locked (attempt {Attempt}/{MaxRetries}). " +
                            "Neon project has running conflicting operations. Waiting {DelayMs}ms before retry...", 
                            retryCount, maxDbRetries, dbRetryDelay);
                        await Task.Delay(dbRetryDelay);
                        response = await httpClient.PostAsync(apiUrl, content);
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("✅ [NEON] Step 3 SUCCESS: Database '{DbName}' created in branch '{BranchId}'", dbName, createdBranchId);

                        // ============================================================================
                        // STEP 4: Get Password and Construct Connection String
                        // Use endpoint from Step 1 (branch creation response) + password from connection_uri API
                        // ============================================================================
                        _logger.LogInformation("🔗 [NEON] Step 4: Constructing connection string using endpoint from branch creation + password from connection_uri API");

                        if (string.IsNullOrEmpty(endpointHost))
                        {
                            _logger.LogError("❌ [NEON] CRITICAL: Endpoint host not available from branch creation response. Cannot construct connection string.");
                            throw new InvalidOperationException(
                                $"Endpoint host not available from branch creation response for branch '{createdBranchId}'. " +
                                "This is a critical requirement - board creation cannot proceed.");
                        }

                        string? originalConnectionString = null;
                        string? extractedPassword = null;
                        string? extractedUsername = neonDefaultOwnerName;
                        int branchEndpointPort = 5432;
                        
                        // Get password from connection_uri API (we only need the password, not the endpoint)
                        var connectionUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(createdBranchId)}&pooled=false";
                        
                        var passwordExtractionRetries = 5;
                        var passwordExtractionDelay = 2000;
                        var passwordExtractionCount = 0;
                        
                        while (passwordExtractionCount < passwordExtractionRetries && string.IsNullOrEmpty(extractedPassword))
                        {
                            try
                            {
                                var connResponse = await httpClient.GetAsync(connectionUrl);
                                if (connResponse.IsSuccessStatusCode)
                                {
                                    var connContent = await connResponse.Content.ReadAsStringAsync();
                                    var connDoc = JsonDocument.Parse(connContent);
                                    if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                                    {
                                        var tempConnString = uriProp.GetString();
                                        if (!string.IsNullOrEmpty(tempConnString))
                                        {
                                            var uri = new Uri(tempConnString);
                                            var userInfo = uri.UserInfo.Split(':');
                                            extractedUsername = userInfo.Length > 0 ? userInfo[0] : neonDefaultOwnerName;
                                            extractedPassword = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                                            
                                            if (!string.IsNullOrEmpty(extractedPassword))
                                            {
                                                _logger.LogInformation("✅ [NEON] Step 4: Password extracted successfully");
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var errorContent = await connResponse.Content.ReadAsStringAsync();
                                    _logger.LogWarning("⚠️ [NEON] Step 4: Failed to get connection string (attempt {Attempt}/{MaxRetries}): {StatusCode} - {Error}", 
                                        passwordExtractionCount + 1, passwordExtractionRetries, connResponse.StatusCode, errorContent);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ [NEON] Step 4: Exception during password extraction (attempt {Attempt}/{MaxRetries})", 
                                    passwordExtractionCount + 1, passwordExtractionRetries);
                            }
                            
                            passwordExtractionCount++;
                            if (passwordExtractionCount < passwordExtractionRetries)
                            {
                                await Task.Delay(passwordExtractionDelay);
                            }
                        }
                        
                        if (string.IsNullOrEmpty(extractedPassword))
                        {
                            _logger.LogError("❌ [NEON] CRITICAL: Failed to extract password after {Attempts} attempts. Cannot proceed without password.", passwordExtractionCount);
                            throw new InvalidOperationException(
                                $"Failed to extract password from connection string for branch '{createdBranchId}' after {passwordExtractionCount} attempts. " +
                                "This is a critical requirement - board creation cannot proceed.");
                        }
                        
                        // Construct connection string using endpoint from Step 1 + password from connection_uri API
                        originalConnectionString = $"postgresql://{extractedUsername}:{Uri.EscapeDataString(extractedPassword)}@{endpointHost}:{branchEndpointPort}/{dbName}?sslmode=require";
                        
                        _logger.LogInformation("✅✅ [NEON] Step 4 SUCCESS: Connection string constructed using endpoint from branch creation. " +
                            "Branch: '{BranchId}', Endpoint: '{EndpointHost}', Port: {Port}", 
                            createdBranchId, endpointHost, branchEndpointPort);
                        
                        // ============================================================================
                        // OLD APPROACH (COMMENTED OUT - CAN BE REVERTED IF NEEDED):
                        // Previous complex retry logic with branch API polling
                        // ============================================================================
                        /*
                        // Get connection string using the created branch ID
                        // CRITICAL: Always use createdBranchId to ensure we get the connection string for the correct branch
                        // Neon branches need time for their endpoints to be provisioned, so we retry with validation
                        var connectionUrl_OLD = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(createdBranchId)}&pooled=false";
                        _logger.LogInformation("🔗 [NEON] Retrieving connection string for database '{DbName}' using branch ID '{BranchId}'", dbName, createdBranchId);
                        
                        // Use the endpoint ID from branch creation response (NOT derived from branch ID)
                        // The endpoint ID is a separate field returned by the API (e.g., "ep-cool-darkness-123456")
                        // IMPORTANT: Extract just the ID part (last segment after last dash) for comparison
                        // because the connection string only contains the ID part, not the full "ep-<name>-<id>" format
                        var fullEndpointId = branchResult.EndpointId ?? "";
                        var expectedEndpointId = "";
                        if (!string.IsNullOrEmpty(fullEndpointId))
                        {
                            // Extract the ID part from "ep-<name>-<id>" format
                            // Split by '-' and take the last part
                            var endpointIdParts = fullEndpointId.Split('-');
                            if (endpointIdParts.Length >= 3)
                            {
                                expectedEndpointId = endpointIdParts[endpointIdParts.Length - 1]; // Last part is the ID
                                _logger.LogInformation("✅ [NEON] Using endpoint ID from branch creation response: {FullEndpointId} -> {EndpointId}", 
                                    fullEndpointId, expectedEndpointId);
                            }
                            else
                            {
                                // If format is unexpected, use the whole string
                                expectedEndpointId = fullEndpointId;
                                _logger.LogWarning("⚠️ [NEON] Unexpected endpoint ID format: {FullEndpointId}, using as-is", fullEndpointId);
                            }
                        }
                        
                        if (string.IsNullOrEmpty(expectedEndpointId))
                        {
                            _logger.LogWarning("⚠️ [NEON] Endpoint ID not found in branch creation response. " +
                                "Will validate connection string using branch ID matching instead.");
                            // Fallback: try to extract from branch ID (but this may not work correctly)
                            var branchIdParts = createdBranchId.Split('-');
                            if (branchIdParts.Length >= 3)
                            {
                                expectedEndpointId = branchIdParts[branchIdParts.Length - 1];
                                _logger.LogWarning("⚠️ [NEON] Using fallback endpoint ID extraction from branch ID: {EndpointId}", expectedEndpointId);
                            }
                        }
                        
                        // Retry logic with STRICT validation - Neon endpoints may not be ready immediately
                        // CRITICAL: We MUST wait until we get the correct connection string matching the branch ID
                        // Neon documentation indicates endpoints can take 10-30 seconds, but we'll wait up to 5 minutes
                        // FALSE-POSITIVE IS UNACCEPTABLE - we will NOT proceed with a mismatched connection string
                        var maxConnectionRetries = 100; // 100 retries × 3 seconds = up to 5 minutes
                        var connectionRetryDelay = 3000; // 3 seconds between retries
                        string? originalConnectionString = null;
                        var connectionRetryCount = 0;
                        var maxWaitTimeMinutes = 5;
                        var startTime = DateTime.UtcNow;
                        
                        _logger.LogInformation("🔍 [NEON] Starting STRICT validation for connection string - will wait up to {MaxMinutes} minutes for correct endpoint", maxWaitTimeMinutes);
                        
                        while (connectionRetryCount < maxConnectionRetries)
                        {
                            // Check if we've exceeded maximum wait time
                            var elapsed = DateTime.UtcNow - startTime;
                            if (elapsed.TotalMinutes > maxWaitTimeMinutes)
                            {
                                _logger.LogError("❌ [NEON] CRITICAL: Exceeded maximum wait time ({MaxMinutes} minutes) for branch '{BranchId}' endpoint to be ready. " +
                                    "Cannot proceed without correct connection string.", maxWaitTimeMinutes, createdBranchId);
                                throw new InvalidOperationException(
                                    $"Failed to retrieve correct connection string for branch '{createdBranchId}' after {maxWaitTimeMinutes} minutes. " +
                                    $"Expected endpoint ID: {expectedEndpointId}. " +
                                    "This is a critical requirement - board creation cannot proceed with an incorrect connection string.");
                            }
                            
                            var connResponse = await httpClient.GetAsync(connectionUrl);
                            
                            if (connResponse.IsSuccessStatusCode)
                            {
                                var connContent = await connResponse.Content.ReadAsStringAsync();
                                var connDoc = JsonDocument.Parse(connContent);
                                if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                                {
                                    var retrievedConnectionString = uriProp.GetString();
                                    
                                    // STRICT VALIDATION: Verify the connection string is from the correct branch by checking the endpoint ID
                                    // The endpoint ID should match the one from the branch creation response
                                    if (!string.IsNullOrEmpty(retrievedConnectionString) && !string.IsNullOrEmpty(expectedEndpointId))
                                    {
                                        // Extract actual endpoint ID from connection string
                                        // Format: ep-<name>-<id>.gwc.azure.neon.tech or ep-<name>-<id>.us-east-2.aws.neon.tech
                                        var endpointMatch = System.Text.RegularExpressions.Regex.Match(retrievedConnectionString, 
                                            @"ep-[a-zA-Z0-9\-]+-([a-zA-Z0-9]+)\.(gwc\.azure|us-east-2\.aws)\.neon\.tech", 
                                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                        var actualEndpointId = endpointMatch.Success ? endpointMatch.Groups[1].Value : null;
                                        
                                        if (!string.IsNullOrEmpty(actualEndpointId) && actualEndpointId.Equals(expectedEndpointId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            // SUCCESS: Connection string endpoint ID matches the one from branch creation
                                            originalConnectionString = retrievedConnectionString;
                                            _logger.LogInformation("✅ [NEON] SUCCESS: Verified connection string matches branch endpoint ID '{EndpointId}' after {Attempts} attempts ({ElapsedSeconds}s)", 
                                                expectedEndpointId, connectionRetryCount + 1, elapsed.TotalSeconds);
                                            break;
                                        }
                                        else
                                        {
                                            // MISMATCH: Log the actual endpoint ID found
                                            connectionRetryCount++;
                                            _logger.LogWarning("⚠️ [NEON] Connection string endpoint ID mismatch (attempt {Attempt}/{MaxRetries}, elapsed: {ElapsedSeconds}s). " +
                                                "Expected: {ExpectedId}, Got: {ActualId}. Branch endpoint not ready yet. Waiting {DelayMs}ms...", 
                                                connectionRetryCount, maxConnectionRetries, elapsed.TotalSeconds, expectedEndpointId, actualEndpointId ?? "unknown", connectionRetryDelay);
                                            await Task.Delay(connectionRetryDelay);
                                            // Continue retrying - DO NOT accept mismatched connection string
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(retrievedConnectionString) && string.IsNullOrEmpty(expectedEndpointId))
                                    {
                                        // No endpoint ID to validate against - this should not happen if branch creation succeeded
                                        connectionRetryCount++;
                                        if (connectionRetryCount < maxConnectionRetries)
                                        {
                                            _logger.LogWarning("⚠️ [NEON] Cannot validate connection string - endpoint ID not available from branch creation. " +
                                                "This should not happen. Retrying... (attempt {Attempt}/{MaxRetries})", 
                                                connectionRetryCount, maxConnectionRetries);
                                            await Task.Delay(connectionRetryDelay);
                                        }
                                        else
                                        {
                                            _logger.LogError("❌ [NEON] CRITICAL: Cannot validate connection string - endpoint ID not available from branch creation response. " +
                                                "This indicates a problem with branch creation. Board creation cannot proceed.");
                                            throw new InvalidOperationException(
                                                $"Cannot validate connection string for branch '{createdBranchId}' - endpoint ID not available from branch creation response. " +
                                                "This is a critical requirement - board creation cannot proceed.");
                                        }
                                    }
                                    else
                                    {
                                        // Cannot validate - this should not happen if branch ID is valid
                                        connectionRetryCount++;
                                        if (string.IsNullOrEmpty(expectedEndpointId))
                                        {
                                            _logger.LogWarning("⚠️ [NEON] Cannot validate connection string - expected endpoint ID is empty. " +
                                                "Branch ID format may be invalid: {BranchId}", createdBranchId);
                                        }
                                        if (connectionRetryCount < maxConnectionRetries)
                                        {
                                            await Task.Delay(connectionRetryDelay);
                                        }
                                        else
                                        {
                                            _logger.LogError("❌ [NEON] CRITICAL: Cannot validate connection string after {MaxRetries} retries. " +
                                                "Expected endpoint ID is missing or connection string is invalid.", maxConnectionRetries);
                                            throw new InvalidOperationException(
                                                $"Cannot validate connection string for branch '{createdBranchId}' after {maxConnectionRetries} retries. " +
                                                "This is a critical requirement - board creation cannot proceed.");
                                        }
                                    }
                                }
                                else
                                {
                                    connectionRetryCount++;
                                    if (connectionRetryCount < maxConnectionRetries)
                                    {
                                        _logger.LogWarning("⚠️ [NEON] Connection string URI not found in Neon API response (attempt {Attempt}). Retrying...", 
                                            connectionRetryCount);
                                        await Task.Delay(connectionRetryDelay);
                                    }
                                    else
                                    {
                                        _logger.LogError("❌ [NEON] CRITICAL: Connection string URI not found in Neon API response after {MaxRetries} retries.", maxConnectionRetries);
                                        throw new InvalidOperationException(
                                            $"Connection string URI not found in Neon API response for branch '{createdBranchId}' after {maxConnectionRetries} retries. " +
                                            "This is a critical requirement - board creation cannot proceed.");
                                    }
                                }
                            }
                            else
                            {
                                connectionRetryCount++;
                                if (connectionRetryCount < maxConnectionRetries)
                                {
                                    var errorContent = await connResponse.Content.ReadAsStringAsync();
                                    var retryElapsed = DateTime.UtcNow - startTime;
                                    _logger.LogWarning("⚠️ [NEON] Failed to retrieve connection string (attempt {Attempt}/{MaxRetries}, elapsed: {ElapsedSeconds}s): {StatusCode} - {Error}. Retrying...", 
                                        connectionRetryCount, maxConnectionRetries, retryElapsed.TotalSeconds, connResponse.StatusCode, errorContent);
                                    await Task.Delay(connectionRetryDelay);
                                }
                                else
                                {
                                    var errorContent = await connResponse.Content.ReadAsStringAsync();
                                    var totalElapsed = DateTime.UtcNow - startTime;
                                    _logger.LogError("❌ [NEON] CRITICAL: Failed to retrieve connection string after {MaxRetries} retries ({ElapsedSeconds}s): {StatusCode} - {Error}", 
                                        maxConnectionRetries, totalElapsed.TotalSeconds, connResponse.StatusCode, errorContent);
                                    throw new InvalidOperationException(
                                        $"Failed to retrieve connection string for branch '{createdBranchId}' after {maxConnectionRetries} retries ({totalElapsed.TotalMinutes} minutes). " +
                                        $"Status: {connResponse.StatusCode}, Error: {errorContent}. " +
                                        "This is a critical requirement - board creation cannot proceed.");
                                }
                            }
                        }
                        
                        // FINAL VALIDATION: Ensure we have a valid connection string before proceeding
                        if (string.IsNullOrEmpty(originalConnectionString))
                        {
                            var elapsed = DateTime.UtcNow - startTime;
                            _logger.LogError("❌ [NEON] CRITICAL: No valid connection string retrieved for branch '{BranchId}' after {Attempts} attempts ({ElapsedMinutes} minutes). " +
                                "Expected endpoint ID: {ExpectedId}. FALSE-POSITIVE IS UNACCEPTABLE - board creation cannot proceed.", 
                                createdBranchId, connectionRetryCount, elapsed.TotalMinutes, expectedEndpointId);
                            throw new InvalidOperationException(
                                $"Failed to retrieve correct connection string for branch '{createdBranchId}' after {connectionRetryCount} attempts ({elapsed.TotalMinutes} minutes). " +
                                $"Expected endpoint ID: {expectedEndpointId}. " +
                                "FALSE-POSITIVE IS UNACCEPTABLE - board creation cannot proceed without a verified correct connection string.");
                        }
                        
                        // FINAL VERIFICATION: Query the branch to confirm the connection string's endpoint belongs to this branch
                        // This is a double-check to ensure the connection string and branch ID are consistent
                        try
                        {
                            var branchVerifyUrl = $"{neonBaseUrl}/projects/{createdNeonProjectId}/branches/{Uri.EscapeDataString(createdBranchId)}";
                            _logger.LogInformation("🔍 [NEON] Verifying connection string endpoint belongs to branch '{BranchId}'...", createdBranchId);
                            var branchVerifyResponse = await httpClient.GetAsync(branchVerifyUrl);
                            
                            if (branchVerifyResponse.IsSuccessStatusCode)
                            {
                                var branchVerifyContent = await branchVerifyResponse.Content.ReadAsStringAsync();
                                var branchVerifyDoc = JsonDocument.Parse(branchVerifyContent);
                                
                                // Extract endpoint ID from connection string for verification
                                var endpointMatch = System.Text.RegularExpressions.Regex.Match(originalConnectionString, 
                                    @"ep-[a-zA-Z0-9\-]+-([a-zA-Z0-9]+)\.(gwc\.azure|us-east-2\.aws)\.neon\.tech", 
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                var connectionStringEndpointId = endpointMatch.Success ? endpointMatch.Groups[1].Value : null;
                                
                                // Check if branch has endpoints array
                                if (!string.IsNullOrEmpty(connectionStringEndpointId) && 
                                    branchVerifyDoc.RootElement.TryGetProperty("branch", out var verifyBranchObj) &&
                                    verifyBranchObj.TryGetProperty("endpoints", out var verifyEndpointsProp) && 
                                    verifyEndpointsProp.ValueKind == JsonValueKind.Array)
                                {
                                    bool endpointFound = false;
                                    foreach (var endpoint in verifyEndpointsProp.EnumerateArray())
                                    {
                                        if (endpoint.TryGetProperty("id", out var epIdProp))
                                        {
                                            var epId = epIdProp.GetString();
                                            // Extract ID part from full endpoint ID format
                                            var epIdParts = epId?.Split('-');
                                            var epIdPart = epIdParts != null && epIdParts.Length >= 3 ? epIdParts[epIdParts.Length - 1] : epId;
                                            
                                            if (epIdPart?.Equals(connectionStringEndpointId, StringComparison.OrdinalIgnoreCase) == true)
                                            {
                                                endpointFound = true;
                                                _logger.LogInformation("✅ [NEON] VERIFIED: Connection string endpoint '{EndpointId}' belongs to branch '{BranchId}'", 
                                                    connectionStringEndpointId, createdBranchId);
                                                break;
                                            }
                                        }
                                    }
                                    
                                    if (!endpointFound)
                                    {
                                        _logger.LogWarning("⚠️ [NEON] WARNING: Connection string endpoint '{EndpointId}' not found in branch '{BranchId}' endpoints. " +
                                            "This may indicate a mismatch, but proceeding as endpoint ID validation already passed.", 
                                            connectionStringEndpointId, createdBranchId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [NEON] Could not verify endpoint belongs to branch - branch endpoints not available in response");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [NEON] Could not verify endpoint belongs to branch - branch query failed: {StatusCode}", 
                                    branchVerifyResponse.StatusCode);
                            }
                        }
                        catch (Exception verifyEx)
                        {
                            _logger.LogWarning(verifyEx, "⚠️ [NEON] Error during final branch verification (non-critical): {Error}", verifyEx.Message);
                            // Don't fail board creation if verification fails - we already validated the endpoint ID matches
                        }
                        
                        // At this point, originalConnectionString is guaranteed to be non-null and validated
                        // (we throw an exception above if it's null or doesn't match the branch ID)
                        _logger.LogInformation("✅ [NEON] Successfully created Neon database '{DbName}' and retrieved VALIDATED connection string matching branch ID", dbName);
                        // END OF OLD APPROACH COMMENT BLOCK
                        */
                        
                        // At this point, originalConnectionString is guaranteed to be non-null and validated
                        // (we throw an exception above if endpoint host or password is missing)
                        _logger.LogInformation("✅ [NEON] Successfully created Neon database '{DbName}' and constructed connection string", dbName);
                        _logger.LogInformation("🔐 [NEON] Creating isolated database role for database '{DbName}' to prevent cross-database access", dbName);
                        
                        // Create an isolated role for this database and update connection string
                        // Retry logic for timing issues (database may need time to propagate)
                        dbConnectionString = null;
                        dbPassword = null;
                        IsolatedRoleResult? roleResult = null;
                        var maxRoleRetries = 3;
                        var roleRetryDelay = 2000;
                        
                        for (int retry = 0; retry < maxRoleRetries; retry++)
                        {
                            try
                            {
                                roleResult = await CreateIsolatedDatabaseRole(originalConnectionString, dbName);
                                if (roleResult != null && !string.IsNullOrEmpty(roleResult.ConnectionString))
                                {
                                    dbConnectionString = roleResult.ConnectionString;
                                    dbPassword = roleResult.Password;
                                    _logger.LogInformation("✅ [NEON] Successfully created isolated role and updated connection string for database '{DbName}' (attempt {Attempt})", 
                                        dbName, retry + 1);
                                    _logger.LogInformation("🔐 [NEON] Database password saved: {HasPassword} (length: {PasswordLength})", 
                                        !string.IsNullOrEmpty(dbPassword), dbPassword?.Length ?? 0);
                                    break;
                                }
                            }
                            catch (PostgresException pgEx) when (pgEx.SqlState == "3D000" && retry < maxRoleRetries - 1)
                            {
                                // Database not available yet - retry after delay
                                _logger.LogWarning("⚠️ [NEON] Database '{DbName}' not yet available for role creation (attempt {Attempt}/{MaxRetries}). " +
                                    "Waiting {DelayMs}ms before retry...", dbName, retry + 1, maxRoleRetries, roleRetryDelay);
                                await Task.Delay(roleRetryDelay);
                            }
                            catch (Exception roleEx)
                            {
                                if (retry < maxRoleRetries - 1)
                                {
                                    _logger.LogWarning("⚠️ [NEON] Error creating isolated role for database '{DbName}' (attempt {Attempt}/{MaxRetries}): {Error}. " +
                                        "Retrying...", dbName, retry + 1, maxRoleRetries, roleEx.Message);
                                    await Task.Delay(roleRetryDelay);
                                }
                                else
                                {
                                    // Last attempt failed - this is a critical security issue
                                    _logger.LogError(roleEx, "❌ [NEON] CRITICAL: Failed to create isolated role for database '{DbName}' after {MaxRetries} attempts. " +
                                        "This is a security requirement. Board creation will continue but database access may be limited.", 
                                        dbName, maxRoleRetries);
                                    // Don't set dbConnectionString - this will prevent using neondb_owner
                                    // The board creation will fail or use a different approach
                                }
                            }
                        }
                        
                        if (string.IsNullOrEmpty(dbConnectionString))
                        {
                            // Critical security failure - cannot proceed with owner connection
                            _logger.LogError("❌ [NEON] CRITICAL SECURITY ISSUE: Cannot create isolated role for database '{DbName}'. " +
                                "Cannot proceed with board creation using owner connection for security reasons.", dbName);
                            throw new InvalidOperationException($"Failed to create isolated database role for '{dbName}' after {maxRoleRetries} attempts. " +
                                "This is a security requirement and board creation cannot proceed without it.");
                        }
                        
                        // Execute the initial database schema script to create TestProjects table
                        // Use the owner connection (neondb_owner) to ensure we have full permissions for table creation and data insertion
                        // The isolated role will be able to query the table after permissions are granted
                        var isolatedRoleName = roleResult?.RoleName;
                        if (!string.IsNullOrEmpty(originalConnectionString))
                        {
                            await ExecuteInitialDatabaseSchema(originalConnectionString, dbName, isolatedRoleName);
                        }
                        else if (!string.IsNullOrEmpty(dbConnectionString))
                        {
                            // Fallback: Use isolated role connection if owner connection is not available
                            _logger.LogWarning("⚠️ [NEON] Using isolated role connection for schema script (owner connection not available)");
                            await ExecuteInitialDatabaseSchema(dbConnectionString, dbName, isolatedRoleName);
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        if (response.StatusCode == System.Net.HttpStatusCode.Locked)
                        {
                            _logger.LogError("❌ [NEON] Failed to create Neon database '{DbName}' after {Retries} retries: " +
                                "Project is locked with running conflicting operations. Error: {Error}", 
                                dbName, retryCount, errorContent);
                        }
                        else
                        {
                            _logger.LogError("❌ [NEON] Failed to create Neon database '{DbName}': {StatusCode} - {Error}", 
                                dbName, response.StatusCode, errorContent);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Neon configuration is incomplete, skipping database creation");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("connection string") || ex.Message.Contains("branch") || ex.Message.Contains("endpoint"))
            {
                // CRITICAL: Connection string validation failures MUST fail the entire board creation
                // We cannot proceed if we don't have a verified correct connection string
                _logger.LogError(ex, "❌ [NEON] CRITICAL: Connection string validation failed. Board creation cannot proceed. Error: {Error}", ex.Message);
                throw; // Re-throw to fail the entire board creation
            }
            catch (Exception ex)
            {
                // CRITICAL: Neon database creation failures MUST fail the entire board creation
                // Database is a critical component - board cannot function without it
                _logger.LogError(ex, "❌ [NEON] CRITICAL: Database creation failed. Board creation cannot proceed. Error: {Error}", ex.Message);
                throw; // Re-throw to fail the entire board creation
            }
            
            // OPTIMIZATION NOTE: Railway can deploy without DATABASE_URL initially
            // If connection string validation is still in progress, Railway will start deploying
            // We'll update Railway's DATABASE_URL once the validated connection string is ready (at the end of board creation)

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
            
            // Note: backendRepositoryUrl, frontendRepositoryUrl, webApiUrl, and swaggerUrl are already declared earlier (before Trello creation)
            string? publishUrl = null;
            List<string> addedCollaborators = new List<string>();
            List<string> failedCollaborators = new List<string>();
            string? railwayServiceId = null;
            string? environmentId = null;
            string? programmingLanguage = null;
            
            // Find the Backend/Fullstack developer and their programming language
            var developerStudent = students.FirstOrDefault(s => 
                s.StudentRoles?.Any(sr => sr.IsActive && (sr.Role?.Type == 1 || sr.Role?.Type == 2)) ?? false);
            
            if (developerStudent != null)
            {
                _logger.LogInformation("Found developer student: StudentId={StudentId}, ProgrammingLanguageId={ProgrammingLanguageId}, HasProgrammingLanguage={HasProgrammingLanguage}", 
                    developerStudent.Id, 
                    developerStudent.ProgrammingLanguageId, 
                    developerStudent.ProgrammingLanguage != null);
                
                if (developerStudent.ProgrammingLanguage != null)
                {
                    programmingLanguage = developerStudent.ProgrammingLanguage.Name;
                    _logger.LogInformation("✅ Using programming language '{Language}' from student {StudentId}", 
                        programmingLanguage, developerStudent.Id);
                }
                else
                {
                    _logger.LogWarning("⚠️ Developer student {StudentId} found but ProgrammingLanguage is null (ProgrammingLanguageId={ProgrammingLanguageId}). Backend code generation will be skipped.", 
                        developerStudent.Id, developerStudent.ProgrammingLanguageId);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No Backend/Fullstack developer found. Checking students: {StudentCount} students", students.Count);
                foreach (var student in students)
                {
                    var roles = student.StudentRoles?
                        .Where(sr => sr.IsActive)
                        .Select(sr => $"{sr.Role?.Name} (Type={sr.Role?.Type})")
                        .ToList() ?? new List<string>();
                    _logger.LogInformation("  Student {StudentId}: Roles=[{Roles}]", student.Id, string.Join(", ", roles));
                }
            }
            
            // Create Railway host for Web API
            string? railwayApiToken = null;
            string? railwayApiUrl = null;
            string? projectId = null; // Declare at higher scope for use after Railway service creation
            try
            {
                var railwayHostName = $"WebApi_{trelloBoardId}";
                _logger.LogInformation("Creating Railway host: {HostName}", railwayHostName);
                
                railwayApiToken = _configuration["Railway:ApiToken"];
                railwayApiUrl = _configuration["Railway:ApiUrl"];
                
                if (!string.IsNullOrWhiteSpace(railwayApiToken) && 
                    railwayApiToken != "your-railway-api-token-here" &&
                    !string.IsNullOrWhiteSpace(railwayApiUrl))
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

                    // Sanitize name for Railway service
                    var sanitizedName = System.Text.RegularExpressions.Regex.Replace(railwayHostName.ToLowerInvariant(), @"[^a-z0-9-_]", "-");

                    // Retry logic configuration
                    var maxRailwayRetries = 5;
                    var railwayRetryDelay = 3000; // 3 seconds between retries

                    // Use shared Railway project instead of creating a new one
                    projectId = _configuration["Railway:SharedProjectId"];

                    if (string.IsNullOrWhiteSpace(projectId))
                    {
                        _logger.LogError("❌ [RAILWAY] Shared project ID not configured. Cannot create Railway service.");
                        // Continue without Railway service - board creation can still proceed
                    }
                    else
                    {
                        // Verify shared project exists (optional check)
                        var verifyProjectQuery = new
                        {
                            query = @"
                                query GetProject($projectId: String!) {
                                    project(id: $projectId) {
                                        id
                                        name
                                    }
                                }",
                            variables = new { projectId = projectId }
                        };

                        var verifyBody = System.Text.Json.JsonSerializer.Serialize(verifyProjectQuery);
                        var verifyContent = new StringContent(verifyBody, System.Text.Encoding.UTF8, "application/json");
                        var verifyResponse = await httpClient.PostAsync(railwayApiUrl, verifyContent);

                        if (verifyResponse.IsSuccessStatusCode)
                        {
                            var verifyDoc = System.Text.Json.JsonDocument.Parse(await verifyResponse.Content.ReadAsStringAsync());
                            if (verifyDoc.RootElement.TryGetProperty("data", out var verifyDataObj) &&
                                verifyDataObj.TryGetProperty("project", out var verifyProjectObj) &&
                                verifyProjectObj.TryGetProperty("id", out var verifyIdProp))
                            {
                                var verifiedProjectId = verifyIdProp.GetString();
                                if (verifiedProjectId == projectId)
                                {
                                    _logger.LogInformation("✅ [RAILWAY] Verified shared project exists: {ProjectId}", projectId);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [RAILWAY] Project ID mismatch. Expected: {Expected}, Got: {Actual}", 
                                        projectId, verifiedProjectId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [RAILWAY] Could not verify shared project {ProjectId}. Proceeding anyway.", projectId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [RAILWAY] Failed to verify shared project {ProjectId}. StatusCode: {StatusCode}. Proceeding anyway.", 
                                projectId, verifyResponse.StatusCode);
                        }

                        if (!string.IsNullOrEmpty(projectId))
                        {
                            // Create Railway service within the project
                            var createServiceMutation = new
                            {
                                query = @"
                    mutation CreateService($projectId: String!, $name: String) {
                        serviceCreate(input: { projectId: $projectId, name: $name }) {
                            id
                            name
                        }
                    }",
                                variables = new
                                {
                                    projectId = projectId,
                                    name = sanitizedName
                                }
                            };

                            var serviceRequestBody = System.Text.Json.JsonSerializer.Serialize(createServiceMutation);
                            var serviceContent = new StringContent(serviceRequestBody, System.Text.Encoding.UTF8, "application/json");
                            
                            // Retry logic for Railway service creation
                            HttpResponseMessage? serviceResponse = null;
                            string? serviceResponseContent = null;
                            var serviceRetryCount = 0;
                            
                            while (serviceRetryCount < maxRailwayRetries)
                            {
                                serviceResponse = await httpClient.PostAsync(railwayApiUrl, serviceContent);
                                serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();
                                
                                if (serviceResponse.IsSuccessStatusCode)
                                {
                                    break; // Success, exit retry loop
                                }
                                
                                // Retry on 503 ServiceUnavailable or 502 Bad Gateway (transient errors)
                                if ((serviceResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                                     serviceResponse.StatusCode == System.Net.HttpStatusCode.BadGateway) && 
                                    serviceRetryCount < maxRailwayRetries - 1)
                                {
                                    serviceRetryCount++;
                                    _logger.LogWarning("⚠️ [RAILWAY] Railway API returned {StatusCode} when creating service (attempt {Attempt}/{MaxRetries}). " +
                                        "Waiting {DelayMs}ms before retry...", serviceResponse.StatusCode, serviceRetryCount, maxRailwayRetries, railwayRetryDelay);
                                    await Task.Delay(railwayRetryDelay);
                                }
                                else
                                {
                                    // Non-retryable error or max retries reached
                                    break;
                                }
                            }

                            if (serviceResponse != null && serviceResponse.IsSuccessStatusCode)
                            {
                                var serviceJsonDoc = System.Text.Json.JsonDocument.Parse(serviceResponseContent);
                                var serviceRoot = serviceJsonDoc.RootElement;

                                if (serviceRoot.TryGetProperty("data", out var serviceDataObj))
                                {
                                    if (serviceDataObj.TryGetProperty("serviceCreate", out var serviceObj))
                                    {
                                        if (serviceObj.TryGetProperty("id", out var serviceIdProp))
                                        {
                                            railwayServiceId = serviceIdProp.GetString();
                                            _logger.LogInformation("✅ [RAILWAY] Service created: ID={ServiceId}, Name={ServiceName}", 
                                                railwayServiceId, sanitizedName);
                                            _logger.LogInformation("🔍 [RAILWAY] DIAGNOSTIC: Service {ServiceId} will be configured with backend build settings", railwayServiceId);
                                        }
                                    }
                                }
                                _logger.LogInformation("✅ [RAILWAY] Service created successfully: ServiceId={ServiceId}, URL={WebApiUrl}", railwayServiceId, webApiUrl);
                            }
                            else
                            {
                                // CRITICAL: Railway service creation failed - board creation cannot proceed
                                _logger.LogError("❌ [RAILWAY] CRITICAL: Failed to create Railway service after {Retries} attempts: StatusCode={StatusCode}, Response={Response}", 
                                    serviceRetryCount + 1, serviceResponse?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError, serviceResponseContent ?? "No response");
                                
                                // Check for errors in GraphQL response
                                string? errorMessages = null;
                                try
                                {
                                    if (!string.IsNullOrEmpty(serviceResponseContent))
                                    {
                                        var errorDoc = System.Text.Json.JsonDocument.Parse(serviceResponseContent);
                                        if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                                        {
                                            var errors = new List<string>();
                                            foreach (var error in errorsProp.EnumerateArray())
                                            {
                                                var errorMessage = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                                errors.Add(errorMessage ?? "Unknown error");
                                                _logger.LogError("❌ [RAILWAY] GraphQL error: {ErrorMessage}", errorMessage);
                                            }
                                            errorMessages = string.Join("; ", errors);
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response");
                                }
                                
                                throw new InvalidOperationException(
                                    $"Failed to create Railway service after {serviceRetryCount + 1} attempts. " +
                                    $"Status: {serviceResponse?.StatusCode}, Error: {errorMessages ?? serviceResponseContent ?? "Unknown error"}. " +
                                    "Railway deployment is required - board creation cannot proceed.");
                            }

                            // Don't set a fallback project URL - we need the actual service URL
                            // The webApiUrl will be set when the domain is created, or polled later
                            if (string.IsNullOrEmpty(webApiUrl))
                            {
                                _logger.LogInformation("⚠️ [RAILWAY] Service URL not available yet - will be set when domain is created");
                            }
                            
                            // Set environment variable DATABASE_URL on the service (if service was created)
                            _logger.LogInformation("🔍 [RAILWAY] Checking conditions for DATABASE_URL: ServiceId={HasServiceId}, HasConnectionString={HasConn}, ProjectId={HasProjectId}", 
                                !string.IsNullOrEmpty(railwayServiceId), !string.IsNullOrEmpty(dbConnectionString), !string.IsNullOrEmpty(projectId));
                            
                            if (!string.IsNullOrEmpty(railwayServiceId) && !string.IsNullOrEmpty(dbConnectionString) && !string.IsNullOrEmpty(projectId))
                            {
                                try
                                {
                                    // First, query the project to get its environments (usually one default environment)
                                    _logger.LogInformation("🔍 [RAILWAY] Querying project {ProjectId} to get environment ID", projectId);
                                    var getProjectQuery = new
                                    {
                                        query = @"
                    query GetProject($projectId: String!) {
                        project(id: $projectId) {
                            id
                            environments {
                                edges {
                                    node {
                                        id
                                    }
                                }
                            }
                        }
                    }",
                                        variables = new
                                        {
                                            projectId = projectId
                                        }
                                    };

                                    var queryRequestBody = System.Text.Json.JsonSerializer.Serialize(getProjectQuery);
                                    var queryContent = new StringContent(queryRequestBody, System.Text.Encoding.UTF8, "application/json");
                                    var queryResponse = await httpClient.PostAsync(railwayApiUrl, queryContent);
                                    var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();

                                    if (queryResponse.IsSuccessStatusCode)
                                    {
                                        var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);
                                        if (queryDoc.RootElement.TryGetProperty("data", out var queryDataObj) &&
                                            queryDataObj.TryGetProperty("project", out var projectObj) &&
                                            projectObj.TryGetProperty("environments", out var environmentsProp) &&
                                            environmentsProp.TryGetProperty("edges", out var edgesProp))
                                        {
                                            var edges = edgesProp.EnumerateArray().ToList();
                                            if (edges.Count > 0)
                                            {
                                                // Use the first environment (typically the default one)
                                                if (edges[0].TryGetProperty("node", out var nodeProp) &&
                                                    nodeProp.TryGetProperty("id", out var envIdProp))
                                                {
                                                    environmentId = envIdProp.GetString();
                                                    _logger.LogInformation("✅ [RAILWAY] Retrieved environment ID: {EnvironmentId} from project {ProjectId}", environmentId, projectId);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [RAILWAY] Failed to query project for environment ID: {StatusCode} - {Error}", 
                                            queryResponse.StatusCode, queryResponseContent);
                                    }

                                    if (string.IsNullOrEmpty(environmentId))
                                    {
                                        _logger.LogWarning("⚠️ [RAILWAY] Cannot set DATABASE_URL: Environment ID not found for project {ProjectId}", projectId);
                                    }
                                    else
                                    {
                                        // Railway GraphQL mutation to set environment variable
                                        // variableUpsert returns Boolean, so we don't select fields
                                        // Both projectId and environmentId are required
                                        var setEnvMutation = new
                                        {
                                            query = @"
                    mutation SetVariable($projectId: String!, $environmentId: String!, $serviceId: String!, $name: String!, $value: String!) {
                        variableUpsert(input: { 
                            projectId: $projectId
                            environmentId: $environmentId
                            serviceId: $serviceId
                            name: $name
                            value: $value
                        })
                    }",
                                            variables = new
                                            {
                                                projectId = projectId,
                                                environmentId = environmentId,
                                                serviceId = railwayServiceId,
                                                name = "DATABASE_URL",
                                                value = dbConnectionString
                                            }
                                        };

                                        _logger.LogInformation("🔧 [RAILWAY] Setting DATABASE_URL environment variable on service {ServiceId} in environment {EnvironmentId}", railwayServiceId, environmentId);
                                        var envRequestBody = System.Text.Json.JsonSerializer.Serialize(setEnvMutation);
                                        _logger.LogDebug("🔧 [RAILWAY] Environment variable mutation: {Mutation}", envRequestBody);
                                        var envContent = new StringContent(envRequestBody, System.Text.Encoding.UTF8, "application/json");
                                        var envResponse = await httpClient.PostAsync(railwayApiUrl, envContent);
                                        var envResponseContent = await envResponse.Content.ReadAsStringAsync();

                                        if (envResponse.IsSuccessStatusCode)
                                        {
                                            _logger.LogInformation("✅ [RAILWAY] Successfully set DATABASE_URL environment variable on Railway service {ServiceId}", railwayServiceId);
                                            // Note: Verification query removed - the 200 response from variableUpsert confirms the variable was set
                                        }
                                        else
                                        {
                                            _logger.LogWarning("⚠️ [RAILWAY] Failed to set DATABASE_URL environment variable: {StatusCode} - {Error}", 
                                                envResponse.StatusCode, envResponseContent);
                                            
                                            // Check for errors in GraphQL response
                                            try
                                            {
                                                var errorDoc = System.Text.Json.JsonDocument.Parse(envResponseContent);
                                                if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                                                {
                                                    foreach (var error in errorsProp.EnumerateArray())
                                                    {
                                                        var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                                        _logger.LogError("❌ [RAILWAY] GraphQL error setting environment variable: {ErrorMessage}", errorMsg);
                                                    }
                                                }
                                            }
                                            catch (Exception parseEx)
                                            {
                                                _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response when setting environment variable");
                                            }
                                        }
                                    }
                                }
                                catch (Exception envEx)
                                {
                                    _logger.LogWarning(envEx, "❌ [RAILWAY] Error setting Railway environment variable, continuing anyway");
                                }
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(railwayServiceId))
                                {
                                    _logger.LogWarning("⚠️ [RAILWAY] Cannot set DATABASE_URL: Railway service was not created (ServiceId is null)");
                                }
                                if (string.IsNullOrEmpty(dbConnectionString))
                                {
                                    _logger.LogWarning("⚠️ [RAILWAY] Cannot set DATABASE_URL: Database connection string is not available");
                                }
                                if (string.IsNullOrEmpty(projectId))
                                {
                                    _logger.LogWarning("⚠️ [RAILWAY] Cannot set DATABASE_URL: Railway project ID is not available");
                                }
                            }

                            swaggerUrl = $"{webApiUrl}/swagger"; // Swagger URL is typically at /swagger endpoint
                            _logger.LogInformation("Railway service created successfully: ProjectId={ProjectId}, ServiceId={ServiceId}, URL={WebApiUrl}", 
                                projectId, railwayServiceId ?? "none", webApiUrl);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [RAILWAY] Service creation failed or service ID not found. Board creation will continue without Railway service.");
                        }
                    }
                }
                else
                {
                    // CRITICAL: Railway configuration is incomplete - board creation cannot proceed
                    _logger.LogError("❌ [RAILWAY] CRITICAL: Railway configuration is incomplete. Railway deployment is required - board creation cannot proceed.");
                    throw new InvalidOperationException(
                        "Railway configuration is incomplete. Railway deployment is required for board creation. " +
                        "Please ensure Railway:ApiToken and Railway:ApiUrl are configured.");
                }
            }
            catch (Exception ex)
            {
                // CRITICAL: Railway deployment failures MUST fail the entire board creation
                // Railway is a critical component - board cannot function without backend deployment
                _logger.LogError(ex, "❌ [RAILWAY] CRITICAL: Railway deployment failed. Board creation cannot proceed. Error: {Error}", ex.Message);
                throw; // Re-throw to fail the entire board creation
            }
            
            if (githubUsernames.Any())
            {
                var githubToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(githubToken))
                {
                    _logger.LogWarning("⚠️ [GITHUB] GitHub access token not configured, skipping repository creation");
                }
                else
                {
                    // Step 1: Create Frontend Repository
                    // Use boardId directly (no prefix) so GitHub Pages URL matches: https://skill-in-projects.github.io/{boardId}/
                    var frontendRepoName = trelloBoardId;
                    _logger.LogInformation("📦 [FRONTEND] Creating frontend repository: {RepoName}", frontendRepoName);
                    
                    var frontendRequest = new CreateRepositoryRequest
                    {
                        Name = frontendRepoName,
                        Description = SanitizeRepoDescription($"Frontend repository for {project.Title}"),
                        IsPrivate = false,  // Public repository to enable GitHub Pages on free plan
                        Collaborators = githubUsernames,
                        ProjectTitle = project.Title,
                        WebApiUrl = webApiUrl  // Pass API URL for config.js
                    };
                    
                    var frontendResponse = await _gitHubService.CreateRepositoryAsync(frontendRequest);
                    
                    if (frontendResponse.Success && !string.IsNullOrEmpty(frontendResponse.RepositoryUrl))
                    {
                        frontendRepositoryUrl = frontendResponse.RepositoryUrl;
                        addedCollaborators.AddRange(frontendResponse.AddedCollaborators);
                        failedCollaborators.AddRange(frontendResponse.FailedCollaborators);
                        
                        _logger.LogInformation("✅ [FRONTEND] Repository created: {RepositoryUrl}", frontendRepositoryUrl);
                        
                        // Extract owner and repo name for commit creation
                        var frontendUri = new Uri(frontendRepositoryUrl);
                        var frontendPathParts = frontendUri.AbsolutePath.TrimStart('/').Split('/');
                        if (frontendPathParts.Length >= 2)
                        {
                            var frontendOwner = frontendPathParts[0];
                            var frontendRepoNameFromUrl = frontendPathParts[1];
                            
                            // Step A: Create README.md immediately after repo creation (before branch protection)
                            _logger.LogInformation("📝 [GITHUB] Step A: Creating initial README.md for frontend repository {Owner}/{Repo}", frontendOwner, frontendRepoNameFromUrl);
                            var readmeSuccess = await _gitHubService.CreateInitialReadmeAsync(frontendOwner, frontendRepoNameFromUrl, project.Title, githubToken, isFrontend: true, webApiUrl: webApiUrl);
                            if (readmeSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Step A: Frontend repository README.md created successfully");
                            }
                            else
                            {
                                // CRITICAL: README creation failed - board creation cannot proceed
                                _logger.LogError("❌ [GITHUB] CRITICAL: Failed to create frontend repository README.md");
                                throw new InvalidOperationException(
                                    "Failed to create frontend repository README.md. " +
                                    "Repository initialization is required - board creation cannot proceed.");
                            }

                            // Step B: Create webhook for frontend repository
                            var webhookSecret = _configuration["GitHub:WebhookSecret"];
                            var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://dev.skill-in.com";
                            var webhookUrl = $"{apiBaseUrl}/api/Mentor/github-webhook";
                            
                            if (!string.IsNullOrEmpty(webhookSecret))
                            {
                                _logger.LogInformation("🔗 [GITHUB] Step B: Creating webhook for frontend repository {Owner}/{Repo}", frontendOwner, frontendRepoNameFromUrl);
                                var webhookSuccess = await _gitHubService.CreateWebhookAsync(frontendOwner, frontendRepoNameFromUrl, webhookUrl, webhookSecret, githubToken);
                                if (webhookSuccess)
                                {
                                    _logger.LogInformation("✅ [GITHUB] Step B: Frontend repository webhook created successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [GITHUB] Step B: Failed to create frontend repository webhook (non-critical)");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [GITHUB] Step B: WebhookSecret not configured, skipping webhook creation");
                            }
                            
                            // Step C: Create branch protection (after README and webhook)
                            _logger.LogInformation("🛡️ [GITHUB] Step C: Creating branch protection for frontend repository {Owner}/{Repo}", frontendOwner, frontendRepoNameFromUrl);
                            var branchProtectionSuccess = await _gitHubService.CreateBranchProtectionAsync(frontendOwner, frontendRepoNameFromUrl, "main", githubToken);
                            if (branchProtectionSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Step C: Frontend repository branch protection created successfully");
                            }
                            else
                            {
                                // CRITICAL: Frontend repository branch protection creation failed - board creation cannot proceed
                                _logger.LogError("❌ [GITHUB] CRITICAL: Failed to create frontend repository branch protection");
                                throw new InvalidOperationException(
                                    "Failed to create frontend repository branch protection. " +
                                    "GitHub branch protection is required - board creation cannot proceed.");
                            }
                            
                            // Create GitHub ruleset AFTER branch protection (to avoid blocking branch creation during initialization)
                            _logger.LogInformation("🔒 [GITHUB] Creating ruleset for frontend repository {Owner}/{Repo} (after branch protection)", frontendOwner, frontendRepoNameFromUrl);
                            var rulesetSuccess = await _gitHubService.CreateRepositoryRulesetAsync(frontendOwner, frontendRepoNameFromUrl, "Frontend", githubToken);
                            if (rulesetSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Frontend repository ruleset created successfully");
                            }
                            else
                            {
                                // Ruleset creation failure is not critical - log warning but continue
                                _logger.LogWarning("⚠️ [GITHUB] Failed to create frontend repository ruleset (non-critical)");
                            }
                            
                            // Create frontend-only commit (files at root, no workflows)
                            var frontendCommitSuccess = await _gitHubService.CreateFrontendOnlyCommitAsync(
                                frontendOwner, frontendRepoNameFromUrl, project.Title, githubToken, webApiUrl);
                            
                            if (frontendCommitSuccess)
                            {
                                _logger.LogInformation("✅ [FRONTEND] Frontend-only commit created successfully");
                                
                                // Deploy frontend using DeploymentController
                                try
                                {
                                    var deploymentController = new DeploymentController(
                                        _loggerFactory.CreateLogger<DeploymentController>(),
                                        _gitHubService,
                                        _httpClientFactory,
                                        _configuration);
                                    
                                    var deployResponse = await deploymentController.DeployFrontendRepositoryAsync(frontendRepositoryUrl);
                                    if (deployResponse.Success && !string.IsNullOrEmpty(deployResponse.DeploymentUrl))
                                    {
                                        publishUrl = deployResponse.DeploymentUrl;
                                        _logger.LogInformation("✅ [FRONTEND] Deployment triggered: {PagesUrl}", publishUrl);
                                        _logger.LogInformation("📊 [FRONTEND] Workflow status: {Status}, Run ID: {RunId}", 
                                            deployResponse.Status, deployResponse.WorkflowRunId);
                                    }
                                    else
                                    {
                                        // CRITICAL: Frontend deployment failed - board creation cannot proceed
                                        _logger.LogError("❌ [FRONTEND] CRITICAL: Deployment failed to trigger: {Message}", deployResponse.Message);
                                        throw new InvalidOperationException(
                                            $"Failed to trigger frontend deployment: {deployResponse.Message}. " +
                                            "Frontend deployment is required - board creation cannot proceed.");
                                    }
                                }
                                catch (Exception deployEx)
                                {
                                    // CRITICAL: Frontend deployment exception - board creation cannot proceed
                                    _logger.LogError(deployEx, "❌ [FRONTEND] CRITICAL: Error calling deployment controller");
                                    throw new InvalidOperationException(
                                        $"Failed to trigger frontend deployment: {deployEx.Message}. " +
                                        "Frontend deployment is required - board creation cannot proceed.");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [FRONTEND] Failed to create frontend commit");
                            }
                        }
                    }
                    else
                    {
                        // CRITICAL: Frontend repository creation failed - board creation cannot proceed
                        _logger.LogError("❌ [FRONTEND] CRITICAL: Failed to create frontend repository: {Error}", frontendResponse.ErrorMessage);
                        throw new InvalidOperationException(
                            $"Failed to create frontend GitHub repository: {frontendResponse.ErrorMessage}. " +
                            "GitHub repository creation is required - board creation cannot proceed.");
                    }
                    
                    // Step 2: Create Backend Repository
                    var backendRepoName = $"backend_{trelloBoardId}";
                    _logger.LogInformation("📦 [BACKEND] Creating backend repository: {RepoName}", backendRepoName);
                    
                    var backendRequest = new CreateRepositoryRequest
                    {
                        Name = backendRepoName,
                        Description = SanitizeRepoDescription($"Backend API repository for {project.Title}"),
                        IsPrivate = false,
                        Collaborators = githubUsernames,
                        ProjectTitle = project.Title,
                        DatabaseConnectionString = dbConnectionString,
                        WebApiUrl = webApiUrl,
                        SwaggerUrl = swaggerUrl,
                        ProgrammingLanguage = programmingLanguage
                    };
                    
                    var backendResponse = await _gitHubService.CreateRepositoryAsync(backendRequest);
                    
                    if (backendResponse.Success && !string.IsNullOrEmpty(backendResponse.RepositoryUrl))
                    {
                        backendRepositoryUrl = backendResponse.RepositoryUrl;
                        addedCollaborators.AddRange(backendResponse.AddedCollaborators);
                        failedCollaborators.AddRange(backendResponse.FailedCollaborators);
                        
                        _logger.LogInformation("✅ [BACKEND] Repository created: {RepositoryUrl}", backendRepositoryUrl);
                        
                        // Extract owner and repo name
                        var backendUri = new Uri(backendRepositoryUrl);
                        var backendPathParts = backendUri.AbsolutePath.TrimStart('/').Split('/');
                        if (backendPathParts.Length >= 2)
                        {
                            var backendOwner = backendPathParts[0];
                            var backendRepoNameFromUrl = backendPathParts[1];
                            
                            // Step A: Create README.md immediately after repo creation (before branch protection)
                            _logger.LogInformation("📝 [GITHUB] Step A: Creating initial README.md for backend repository {Owner}/{Repo}", backendOwner, backendRepoNameFromUrl);
                            var readmeSuccess = await _gitHubService.CreateInitialReadmeAsync(backendOwner, backendRepoNameFromUrl, project.Title, githubToken, isFrontend: false, webApiUrl: webApiUrl, databaseConnectionString: dbConnectionString, swaggerUrl: swaggerUrl);
                            if (readmeSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Step A: Backend repository README.md created successfully");
                            }
                            else
                            {
                                // CRITICAL: README creation failed - board creation cannot proceed
                                _logger.LogError("❌ [GITHUB] CRITICAL: Failed to create backend repository README.md");
                                throw new InvalidOperationException(
                                    "Failed to create backend repository README.md. " +
                                    "Repository initialization is required - board creation cannot proceed.");
                            }

                            // Step B: Create webhook for backend repository
                            var webhookSecretBackend = _configuration["GitHub:WebhookSecret"];
                            var apiBaseUrlBackend = _configuration["ApiBaseUrl"] ?? "https://dev.skill-in.com";
                            var webhookUrlBackend = $"{apiBaseUrlBackend}/api/Mentor/github-webhook";
                            
                            if (!string.IsNullOrEmpty(webhookSecretBackend))
                            {
                                _logger.LogInformation("🔗 [GITHUB] Step B: Creating webhook for backend repository {Owner}/{Repo}", backendOwner, backendRepoNameFromUrl);
                                var webhookSuccessBackend = await _gitHubService.CreateWebhookAsync(backendOwner, backendRepoNameFromUrl, webhookUrlBackend, webhookSecretBackend, githubToken);
                                if (webhookSuccessBackend)
                                {
                                    _logger.LogInformation("✅ [GITHUB] Step B: Backend repository webhook created successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [GITHUB] Step B: Failed to create backend repository webhook (non-critical)");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [GITHUB] Step B: WebhookSecret not configured, skipping webhook creation");
                            }
                            
                            // Step C: Create branch protection (after README and webhook)
                            _logger.LogInformation("🛡️ [GITHUB] Step C: Creating branch protection for backend repository {Owner}/{Repo}", backendOwner, backendRepoNameFromUrl);
                            var branchProtectionSuccess = await _gitHubService.CreateBranchProtectionAsync(backendOwner, backendRepoNameFromUrl, "main", githubToken);
                            if (branchProtectionSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Step C: Backend repository branch protection created successfully");
                            }
                            else
                            {
                                // CRITICAL: Backend repository branch protection creation failed - board creation cannot proceed
                                _logger.LogError("❌ [GITHUB] CRITICAL: Failed to create backend repository branch protection");
                                throw new InvalidOperationException(
                                    "Failed to create backend repository branch protection. " +
                                    "GitHub branch protection is required - board creation cannot proceed.");
                            }
                            
                            // Create GitHub ruleset AFTER branch protection (to avoid blocking branch creation during initialization)
                            _logger.LogInformation("🔒 [GITHUB] Creating ruleset for backend repository {Owner}/{Repo} (after branch protection)", backendOwner, backendRepoNameFromUrl);
                            var rulesetSuccess = await _gitHubService.CreateRepositoryRulesetAsync(backendOwner, backendRepoNameFromUrl, "Backend", githubToken);
                            if (rulesetSuccess)
                            {
                                _logger.LogInformation("✅ [GITHUB] Backend repository ruleset created successfully");
                            }
                            else
                            {
                                // Ruleset creation failure is not critical - log warning but continue
                                _logger.LogWarning("⚠️ [GITHUB] Failed to create backend repository ruleset (non-critical)");
                            }
                            
                            // Create backend-only commit (files at root, no workflows)
                            var backendCommitSuccess = await _gitHubService.CreateBackendOnlyCommitAsync(
                                backendOwner, backendRepoNameFromUrl, project.Title, githubToken, 
                                programmingLanguage ?? "c#", dbConnectionString, webApiUrl, swaggerUrl);
                            
                            if (backendCommitSuccess)
                            {
                                _logger.LogInformation("✅ [BACKEND] Backend-only commit created successfully");
                                
                                // Add Railway secrets and connect to Railway (if Railway service was created)
                                if (!string.IsNullOrEmpty(railwayServiceId) && !string.IsNullOrEmpty(railwayApiToken))
                    {
                                try
                                {
                                    _logger.LogInformation("[GITHUB] Adding Railway secrets to backend repository {Owner}/{Repo}", backendOwner, backendRepoNameFromUrl);
                                    
                                    // Add RAILWAY_TOKEN secret
                                    var tokenSecretSuccess = await _gitHubService.CreateOrUpdateRepositorySecretAsync(
                                        backendOwner, backendRepoNameFromUrl, "RAILWAY_TOKEN", railwayApiToken, githubToken);
                                    
                                    if (tokenSecretSuccess)
                                    {
                                        _logger.LogInformation("[GITHUB] ✅ Successfully added RAILWAY_TOKEN secret");
                                    }
                                    else
                                    {
                                        // CRITICAL: RAILWAY_TOKEN secret creation failed - board creation cannot proceed
                                        _logger.LogError("[GITHUB] ❌ CRITICAL: Failed to add RAILWAY_TOKEN secret");
                                        throw new InvalidOperationException(
                                            "Failed to add RAILWAY_TOKEN secret to GitHub repository. " +
                                            "GitHub secrets are required for Railway deployment - board creation cannot proceed.");
                                    }
                                    
                                    // Add RAILWAY_SERVICE_ID secret
                                    var serviceIdSecretSuccess = await _gitHubService.CreateOrUpdateRepositorySecretAsync(
                                        backendOwner, backendRepoNameFromUrl, "RAILWAY_SERVICE_ID", railwayServiceId, githubToken);
                                    
                                    if (serviceIdSecretSuccess)
                                    {
                                        _logger.LogInformation("[GITHUB] ✅ Successfully added RAILWAY_SERVICE_ID secret");
                                    }
                                    else
                                    {
                                        // CRITICAL: RAILWAY_SERVICE_ID secret creation failed - board creation cannot proceed
                                        _logger.LogError("[GITHUB] ❌ CRITICAL: Failed to add RAILWAY_SERVICE_ID secret");
                                        throw new InvalidOperationException(
                                            "Failed to add RAILWAY_SERVICE_ID secret to GitHub repository. " +
                                            "GitHub secrets are required for Railway deployment - board creation cannot proceed.");
                                    }
                                    
                                    // Connect GitHub repository to Railway service
                                    if (!string.IsNullOrEmpty(railwayApiUrl))
                                    {
                                        using var railwayHttpClient = _httpClientFactory.CreateClient();
                                        railwayHttpClient.DefaultRequestHeaders.Authorization = 
                                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
                                        railwayHttpClient.DefaultRequestHeaders.Accept.Add(
                                            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                                        railwayHttpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");
                                        
                                        await ConnectGitHubRepositoryToRailway(railwayHttpClient, railwayApiUrl, railwayApiToken, railwayServiceId, backendRepositoryUrl);
                                        
                                        // Set Railway build environment variables (for root-level files, no "cd backend &&" needed)
                                        if (!string.IsNullOrEmpty(environmentId) && !string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(programmingLanguage))
                                        {
                                            await SetRailwayBuildSettings(railwayHttpClient, railwayApiUrl, railwayApiToken, 
                                                projectId, railwayServiceId, environmentId, programmingLanguage, trelloBoardId);
                                        }
                                        
                                        // Create public domain for the service
                                        string? domainUrl = null;
                                        int targetPort = GetDefaultPortForLanguage(programmingLanguage);
                                        _logger.LogInformation("🌐 [RAILWAY] Creating domain for language: {Language}, target port: {Port}", 
                                            programmingLanguage ?? "unknown", targetPort);
                                        
                                        if (!string.IsNullOrEmpty(environmentId))
                                        {
                                            domainUrl = await CreateRailwayServiceDomain(
                                                railwayHttpClient, railwayApiUrl, railwayApiToken, 
                                                railwayServiceId, environmentId, targetPort: targetPort);
                                            
                                            if (!string.IsNullOrEmpty(domainUrl))
                                            {
                                                _logger.LogInformation("✅ [RAILWAY] Domain created successfully: {DomainUrl}", domainUrl);
                                                // Update webApiUrl and swaggerUrl with the actual domain
                                                webApiUrl = domainUrl;
                                                swaggerUrl = $"{domainUrl}/swagger";
                                                
                                                // Update backend and frontend READMEs with full Web API URL so mentor and students see the actual URL
                                                _ = Task.Run(async () =>
                                                {
                                                    try
                                                    {
                                                        var updated = await _gitHubService.UpdateBackendReadmeWithWebApiUrlsAsync(
                                                            backendOwner, backendRepoNameFromUrl, project.Title, dbConnectionString, webApiUrl, swaggerUrl, githubToken);
                                                        if (updated)
                                                            _logger.LogInformation("✅ [BACKEND] README updated with Web API URL for mentor and students");
                                                    }
                                                    catch (Exception readmeEx)
                                                    {
                                                        _logger.LogWarning(readmeEx, "⚠️ [BACKEND] Failed to update README with Web API URL (non-blocking)");
                                                    }
                                                    if (!string.IsNullOrEmpty(frontendRepositoryUrl))
                                                    {
                                                        try
                                                        {
                                                            var frontendUri = new Uri(frontendRepositoryUrl);
                                                            var frontendPathParts = frontendUri.AbsolutePath.TrimStart('/').Split('/');
                                                            if (frontendPathParts.Length >= 2)
                                                            {
                                                                var frontendOwner = frontendPathParts[0];
                                                                var frontendRepoNameFromUrl = frontendPathParts[1];
                                                                var frontendUpdated = await _gitHubService.UpdateFrontendReadmeWithWebApiUrlsAsync(frontendOwner, frontendRepoNameFromUrl, project.Title, webApiUrl, githubToken);
                                                                if (frontendUpdated)
                                                                    _logger.LogInformation("✅ [FRONTEND] README updated with Backend API URL");
                                                            }
                                                        }
                                                        catch (Exception feEx)
                                                        {
                                                            _logger.LogWarning(feEx, "⚠️ [FRONTEND] Failed to update README with Backend API URL (non-blocking)");
                                                        }
                                                    }
                                                });
                                                
                                                // Update ProjectBoard with the backend URL (if it was already created)
                                                try
                                                {
                                                    var existingBoard = await _context.ProjectBoards.FindAsync(trelloBoardId);
                                                    if (existingBoard != null)
                                                    {
                                                        existingBoard.WebApiUrl = swaggerUrl ?? webApiUrl;
                                                        existingBoard.UpdatedAt = DateTime.UtcNow;
                                                        await _context.SaveChangesAsync();
                                                        _logger.LogInformation("✅ [PROJECTBOARD] Updated WebApiUrl to {WebApiUrl}", existingBoard.WebApiUrl);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogWarning("⚠️ [PROJECTBOARD] ProjectBoard not found for board ID {BoardId} - will set URL when board is created", trelloBoardId);
                                                    }
                                                }
                                                catch (Exception updateEx)
                                                {
                                                    _logger.LogWarning(updateEx, "⚠️ [PROJECTBOARD] Failed to update WebApiUrl, will be set when board is created");
                                                }
                                                
                                                // Update config.js in frontend repo with the service URL
                                                if (!string.IsNullOrEmpty(frontendRepositoryUrl))
                                                {
                                                    _ = Task.Run(async () =>
                                                    {
                                                        try
                                                        {
                                                            await UpdateConfigJsWithServiceUrl(
                                                                frontendRepositoryUrl, domainUrl, githubToken);
                                                            _logger.LogInformation("✅ [FRONTEND] Updated config.js with service URL");
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _logger.LogWarning(ex, "⚠️ [FRONTEND] Failed to update config.js with service URL");
                                                        }
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                // CRITICAL: Railway domain creation failed - board creation cannot proceed
                                                _logger.LogError("❌ [RAILWAY] CRITICAL: Domain creation failed - cannot proceed without service URL");
                                                throw new InvalidOperationException(
                                                    "Failed to create Railway service domain. " +
                                                    "Railway domain is required for backend deployment - board creation cannot proceed.");
                                                // Start polling for service URL in background (unreachable code, but kept for reference)
                                                if (!string.IsNullOrEmpty(railwayServiceId) && !string.IsNullOrEmpty(frontendRepositoryUrl))
                                                {
                                                    _ = Task.Run(async () =>
                                                    {
                                                        try
                                                        {
                                                            await PollAndUpdateServiceUrl(
                                                                railwayHttpClient, railwayApiUrl, railwayApiToken,
                                                                railwayServiceId, frontendRepositoryUrl, githubToken);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _logger.LogWarning(ex, "⚠️ [RAILWAY] Error polling for service URL");
                                                        }
                                                    });
                                                }
                                            }
                                        }
                                        
                                        // Deploy backend using DeploymentController
                                        try
                                        {
                                            var deploymentController = new DeploymentController(
                                                _loggerFactory.CreateLogger<DeploymentController>(),
                                                _gitHubService,
                                                _httpClientFactory,
                                                _configuration);
                                            
                                            var deployResponse = await deploymentController.DeployBackendRepositoryAsync(backendRepositoryUrl);
                                            if (deployResponse.Success)
                                            {
                                                _logger.LogInformation("✅ [BACKEND] Deployment triggered successfully");
                                                _logger.LogInformation("📊 [BACKEND] Workflow status: {Status}, Run ID: {RunId}", 
                                                    deployResponse.Status, deployResponse.WorkflowRunId);
                                                if (!string.IsNullOrEmpty(deployResponse.DeploymentUrl))
                                                {
                                                    _logger.LogInformation("🌐 [BACKEND] Deployment URL: {Url}", deployResponse.DeploymentUrl);
                                                }
                                            }
                                            else
                                            {
                                                // CRITICAL: Backend deployment failed - board creation cannot proceed
                                                _logger.LogError("❌ [BACKEND] CRITICAL: Deployment failed to trigger: {Message}", deployResponse.Message);
                                                throw new InvalidOperationException(
                                                    $"Failed to trigger backend deployment: {deployResponse.Message}. " +
                                                    "Backend deployment is required - board creation cannot proceed.");
                                            }
                                        }
                                        catch (Exception deployEx)
                                        {
                                            // CRITICAL: Backend deployment exception - board creation cannot proceed
                                            _logger.LogError(deployEx, "❌ [BACKEND] CRITICAL: Error calling deployment controller");
                                            throw new InvalidOperationException(
                                                $"Failed to trigger backend deployment: {deployEx.Message}. " +
                                                "Backend deployment is required - board creation cannot proceed.");
                                        }
                                    }
                                }
                                catch (Exception railwayEx)
                                {
                                    // CRITICAL: Railway integration setup failed - board creation cannot proceed
                                    _logger.LogError(railwayEx, "[GITHUB] ❌ CRITICAL: Error setting up Railway integration. Board creation cannot proceed.");
                                    throw new InvalidOperationException(
                                        $"Failed to set up Railway integration with GitHub repository: {railwayEx.Message}. " +
                                        "Railway integration is required - board creation cannot proceed.");
                                }
                            }
                            else
                            {
                                // CRITICAL: Backend commit creation failed - board creation cannot proceed
                                _logger.LogError("❌ [BACKEND] CRITICAL: Failed to create backend commit");
                                throw new InvalidOperationException(
                                    "Failed to create backend commit in GitHub repository. " +
                                    "GitHub commit creation is required - board creation cannot proceed.");
                            }
                        }
                    }
                    else
                    {
                        // CRITICAL: Backend repository creation failed - board creation cannot proceed
                        _logger.LogError("❌ [BACKEND] CRITICAL: Failed to create backend repository: {Error}", backendResponse.ErrorMessage);
                        throw new InvalidOperationException(
                            $"Failed to create backend GitHub repository: {backendResponse.ErrorMessage}. " +
                            "GitHub repository creation is required - board creation cannot proceed.");
                    }
                    
                    _logger.LogInformation("✅ [GITHUB] Added {AddedCount} collaborators total, {FailedCount} failed", 
                        addedCollaborators.Count, failedCollaborators.Count);
                }
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

            // Kickoff meeting: use precomputed first day of week at 10:00 local (from top of method)
            var nextMeetingTime = kickoffUtc;
            _logger.LogInformation("Setting NextMeetingTime to first day of week ({FirstDay}) at 10:00 local ({LocalTime}): {MeetingTime} (UTC)", firstDayOfWeekStr, localTimeStr, nextMeetingTime);

            // Set project Kickoff flag to false when board is created
            project.Kickoff = false;
            _logger.LogInformation("Set Kickoff flag to false for project {ProjectId} when board was created", request.ProjectId);

            // Initialize meetingUrl variable (will be set after ProjectBoard is created)
            string? meetingUrl = null;

            // If SystemBoard was created, create a ProjectBoard record for it first
            if (!string.IsNullOrEmpty(trelloResponse.SystemBoardId))
            {
                _logger.LogInformation("Creating ProjectBoard record for SystemBoard: {SystemBoardId}", trelloResponse.SystemBoardId);
                var systemProjectBoard = new ProjectBoard
                {
                    Id = trelloResponse.SystemBoardId,
                    ProjectId = request.ProjectId,
                    StartDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(projectLengthWeeks * 7),
                    StatusId = 1, // New status
                    AdminId = adminStudent?.Id,
                    SprintPlan = sprintPlanForStorage != null ? System.Text.Json.JsonSerializer.Serialize(sprintPlanForStorage) : null,
                    BoardUrl = trelloResponse.SystemBoardUrl,
                    IsSystemBoard = true, // This record is the SystemBoard (full template board)
                    // SystemBoardId is null for the SystemBoard itself (it's the reference board)
                    NextMeetingTime = null, // SystemBoard doesn't need meeting info
                    NextMeetingUrl = null,
                    GithubBackendUrl = null, // SystemBoard doesn't need these URLs
                    GithubFrontendUrl = null,
                    WebApiUrl = null,
                    PublishUrl = null,
                    DBPassword = null, // SystemBoard doesn't need database info
                    NeonProjectId = null,
                    NeonBranchId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ProjectBoards.Add(systemProjectBoard);
                
                // Save SystemBoard first so it exists when we reference it
                await _context.SaveChangesAsync();
                _logger.LogInformation("SystemBoard ProjectBoard record created and saved");
            }

            // Create ProjectBoard record for EmptyBoard (or regular board if CreatePMEmptyBoard is false)
            var projectBoard = new ProjectBoard
            {
                Id = trelloBoardId,
                ProjectId = request.ProjectId,
                IsSystemBoard = false, // EmptyBoard or single board
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(projectLengthWeeks * 7),
                StatusId = 1, // New status
                AdminId = adminStudent?.Id,
                SprintPlan = sprintPlanForStorage != null ? System.Text.Json.JsonSerializer.Serialize(sprintPlanForStorage) : null,
                BoardUrl = trelloResponse.BoardUrl,
                SystemBoardId = trelloResponse.SystemBoardId, // Store SystemBoardId if CreatePMEmptyBoard is enabled
                NextMeetingTime = nextMeetingTime,
                NextMeetingUrl = meetingUrl, // Will be updated after Teams meeting is created
                GithubBackendUrl = backendRepositoryUrl,
                GithubFrontendUrl = frontendRepositoryUrl,
                WebApiUrl = swaggerUrl ?? webApiUrl ?? $"https://webapi{trelloBoardId}.up.railway.app", // Swagger URL from Railway deployment (fallback to base URL if swagger URL not set, or default Railway pattern)
                PublishUrl = publishUrl,
                DBPassword = dbPassword, // Save the isolated role password for manual connections
                NeonProjectId = createdNeonProjectId, // Save the Neon project ID (project-per-tenant isolation)
                NeonBranchId = createdBranchId, // Save the Neon branch ID for this database (ensures isolation)
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Creating ProjectBoard record for board: {BoardId}", trelloBoardId);
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
                // Do NOT delete the SystemBoard ProjectBoard record: EmptyBoard.SystemBoardId references it (FK).
                // Deleting it would set EmptyBoard.SystemBoardId to NULL due to ON DELETE SET NULL.
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Error saving changes to database: {SaveException}", saveEx.Message);
                throw;
            }

            // Upsert ProjectBoardSprintMerge for ALL sprints on the board; sprints 1..VisibleSprints (overridden from SystemBoard) get MergedAt set (FK requires ProjectBoard to exist first)
            var sprintCount = trelloRequest?.SprintPlan?.Lists?.Count ?? 0;
            if (sprintCount <= 0 && overriddenSprints.Count > 0)
                sprintCount = overriddenSprints.Keys.Max();
            if (sprintCount > 0)
            {
                try
                {
                    for (var sprintNum = 1; sprintNum <= sprintCount; sprintNum++)
                    {
                        string? listId = null;
                        DateTime? dueDateUtc = null;
                        DateTime? mergedAt = null;
                        if (overriddenSprints.TryGetValue(sprintNum, out var overridden))
                        {
                            listId = overridden.ListId;
                            dueDateUtc = overridden.DueDateUtc;
                            mergedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            var snapshot = await _trelloService.GetSprintFromBoardAsync(trelloBoardId, $"Sprint{sprintNum}")
                                ?? await _trelloService.GetSprintFromBoardAsync(trelloBoardId, $"Sprint {sprintNum}");
                            if (snapshot == null)
                                continue;
                            listId = snapshot.ListId;
                            dueDateUtc = snapshot.Cards?.Count > 0 && snapshot.Cards[0].DueDate.HasValue
                                ? ToUtcForDb(snapshot.Cards[0].DueDate.Value)
                                : (DateTime?)null;
                        }
                        var mergeRecord = await _context.ProjectBoardSprintMerges
                            .FirstOrDefaultAsync(m => m.ProjectBoardId == trelloBoardId && m.SprintNumber == sprintNum);
                        if (mergeRecord == null)
                        {
                            mergeRecord = new ProjectBoardSprintMerge
                            {
                                ProjectBoardId = trelloBoardId,
                                SprintNumber = sprintNum,
                                MergedAt = mergedAt,
                                ListId = listId,
                                DueDate = dueDateUtc
                            };
                            _context.ProjectBoardSprintMerges.Add(mergeRecord);
                            _logger.LogInformation("[BOARD-CREATE] ProjectBoardSprintMerge added for BoardId={BoardId}, SprintNumber={SprintNumber}, MergedAt={MergedAt}", trelloBoardId, sprintNum, mergedAt != null ? "set" : "null");
                        }
                        else
                        {
                            mergeRecord.ListId = listId ?? mergeRecord.ListId;
                            mergeRecord.DueDate = dueDateUtc ?? mergeRecord.DueDate;
                            if (overriddenSprints.ContainsKey(sprintNum))
                                mergeRecord.MergedAt = DateTime.UtcNow;
                            _logger.LogInformation("[BOARD-CREATE] ProjectBoardSprintMerge updated for BoardId={BoardId}, SprintNumber={SprintNumber}", trelloBoardId, sprintNum);
                        }
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[BOARD-CREATE] ProjectBoardSprintMerge saved for all {Count} sprints, BoardId={BoardId}.", sprintCount, trelloBoardId);
                }
                catch (Exception exDb)
                {
                    _logger.LogError(exDb, "[BOARD-CREATE] Failed to upsert ProjectBoardSprintMerge for BoardId={BoardId}: {Message}", trelloBoardId, exDb.Message);
                }
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
            if (!string.IsNullOrEmpty(request.Title) && request.DurationMinutes.HasValue)
            {
                try
                {
                    _logger.LogInformation("Creating Teams meeting via create-meeting-smtp-for-board-auth: {Title} at {DateTime} (kickoff first day of week 10:00 local) for {DurationMinutes} minutes", 
                        request.Title, nextMeetingTime.ToString("yyyy-MM-ddTHH:mm:ssZ"), request.DurationMinutes);

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
                            DateTime = nextMeetingTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
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

            // OPTIMIZATION: If DATABASE_URL wasn't set earlier (connection string validation was still in progress),
            // update it now that board creation is complete
            if (!string.IsNullOrEmpty(railwayServiceId) && !string.IsNullOrEmpty(dbConnectionString) && !string.IsNullOrEmpty(projectId))
            {
                // Check if DATABASE_URL was already set (we would have set it earlier if connection string was ready)
                // If not, update it now
                _logger.LogInformation("🔄 [RAILWAY] Final check: Updating DATABASE_URL on Railway service {ServiceId} if not already set", railwayServiceId);
                try
                {
                    await UpdateRailwayDatabaseUrl(railwayServiceId, dbConnectionString, projectId, railwayApiToken, railwayApiUrl);
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "⚠️ [RAILWAY] Failed to update DATABASE_URL at end of board creation. Railway may need manual update.");
                }
            }
            else if (!string.IsNullOrEmpty(railwayServiceId) && string.IsNullOrEmpty(dbConnectionString))
            {
                _logger.LogWarning("⚠️ [RAILWAY] Railway service {ServiceId} was created but DATABASE_URL is not available. " +
                    "Connection string validation may have failed or is still in progress. Railway will deploy without database connection.", railwayServiceId);
            }

            _logger.LogInformation("Successfully created board {BoardId} for project {ProjectId}", trelloBoardId, request.ProjectId);

            var message = (backendRepositoryUrl != null || frontendRepositoryUrl != null)
                ? "Board and repositories created successfully!" 
                : "Board created successfully! (GitHub repository creation was skipped or failed)";

            return Ok(new CreateBoardResponse
            {
                Success = true,
                Message = message,
                BoardId = trelloBoardId,
                BoardUrl = trelloResponse.BoardUrl,
                RepositoryUrl = backendRepositoryUrl, // Deprecated - kept for backward compatibility
                FrontendRepositoryUrl = frontendRepositoryUrl,
                BackendRepositoryUrl = backendRepositoryUrl,
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
                GithubBackendUrl = board.GithubBackendUrl,
                GithubFrontendUrl = board.GithubFrontendUrl,
                WebApiUrl = board.WebApiUrl,
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
    /// Recursively clone a JsonNode with all object property names converted to camelCase.
    /// System.Text.Json does not apply PropertyNamingPolicy to JsonNode when serializing.
    /// </summary>
    private static JsonNode? ToCamelCaseKeys(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var prop in obj)
            {
                var key = string.IsNullOrEmpty(prop.Key) ? prop.Key : char.ToLowerInvariant(prop.Key[0]) + prop.Key.Substring(1);
                result[key] = ToCamelCaseKeys(prop.Value);
            }
            return result;
        }
        if (node is JsonArray arr)
        {
            var result = new JsonArray();
            foreach (var item in arr)
                result.Add(ToCamelCaseKeys(item));
            return result;
        }
        return node.DeepClone();
    }

    /// <summary>Converts DateTime to UTC for PostgreSQL timestamp with time zone (Npgsql rejects Local/Unspecified).</summary>
    private static DateTime ToUtcForDb(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    /// <summary>Parse Trello:LocalTime (e.g. "GMT+2", "UTC") to offset in minutes from UTC.</summary>
    private static int GetLocalTimeOffsetMinutes(string? localTime)
    {
        if (string.IsNullOrWhiteSpace(localTime)) return 0;
        if (localTime.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return 0;
        var m = Regex.Match(localTime.Trim(), @"^GMT([+-])(\d+)$", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[2].Value, out var hours))
            return (m.Groups[1].Value == "-" ? -1 : 1) * (hours * 60);
        return 0;
    }

    /// <summary>Parse Trello:FirstDayOfWeek (e.g. "Sunday", "Monday") to DayOfWeek.</summary>
    private static DayOfWeek GetFirstDayOfWeek(string? firstDay)
    {
        if (string.IsNullOrWhiteSpace(firstDay)) return DayOfWeek.Sunday;
        if (Enum.TryParse<DayOfWeek>(firstDay.Trim(), true, out var dow))
            return dow;
        return DayOfWeek.Sunday;
    }

    /// <summary>Next occurrence of firstDayOfWeek at 10:00 in local time (offsetMinutes from UTC), returned as UTC.</summary>
    private static DateTime GetNextKickoffUtc(DateTime utcNow, DayOfWeek firstDayOfWeek, int localOffsetMinutes)
    {
        var localNow = utcNow.AddMinutes(localOffsetMinutes);
        var daysUntilFirst = ((int)firstDayOfWeek - (int)localNow.DayOfWeek + 7) % 7;
        if (daysUntilFirst == 0 && (localNow.Hour > 10 || (localNow.Hour == 10 && localNow.Minute > 0)))
            daysUntilFirst = 7;
        var nextFirstDayLocal = localNow.Date.AddDays(daysUntilFirst).AddHours(10).AddMinutes(0);
        return nextFirstDayLocal.AddMinutes(-localOffsetMinutes);
    }

    /// <summary>Sprint N (1-based) due date: last day of that week in local (firstDayOfWeek = start, +6 = end), 23:59 local, as UTC.</summary>
    private static DateTime GetSprintDueDateUtc(DateTime kickoffUtc, int sprintNumber1Based, DayOfWeek firstDayOfWeek, int localOffsetMinutes, int sprintLengthWeeks = 1)
    {
        var kickoffLocal = kickoffUtc.AddMinutes(localOffsetMinutes);
        var sprintStartLocal = kickoffLocal.Date.AddDays((sprintNumber1Based - 1) * sprintLengthWeeks * 7);
        var dueLocal = sprintStartLocal.AddDays(6).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        return dueLocal.AddMinutes(-localOffsetMinutes);
    }

    /// <summary>Parse sprint number from list name (e.g. "Sprint1", "Sprint 2"). Returns null if not a sprint list.</summary>
    private static int? ParseSprintNumberFromListName(string? listName)
    {
        if (string.IsNullOrWhiteSpace(listName)) return null;
        var m = Regex.Match(listName, @"Sprint\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
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

            // Enrich Members with roleName (and roleNames for Full Stack) from our DB so frontend can match tasks by label
            var studentsOnBoard = await _context.Students
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId == boardId && s.IsAvailable)
                .ToListAsync();

            var statsJson = JsonSerializer.Serialize(stats);
            var statsNode = JsonNode.Parse(statsJson);

            // Collect all distinct card label names on the board (for unmatched members so they can match any task)
            var cardLabelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (statsNode?["Cards"] is JsonArray cardsArray)
            {
                foreach (var cardNode in cardsArray.OfType<JsonObject>())
                {
                    var labels = cardNode["Labels"] ?? cardNode["labels"];
                    if (labels is JsonArray labelsArr)
                    {
                        foreach (var labelNode in labelsArr.OfType<JsonObject>())
                        {
                            var name = labelNode["Name"]?.GetValue<string>() ?? labelNode["name"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(name))
                                cardLabelNames.Add(name);
                        }
                    }
                }
            }

            if (statsNode != null && statsNode["Members"] is JsonArray membersArray)
            {
                foreach (var memberNode in membersArray.OfType<JsonObject>())
                {
                    var email = memberNode["Email"]?.GetValue<string>() ?? memberNode["email"]?.GetValue<string>();
                    var fullName = memberNode["FullName"]?.GetValue<string>() ?? memberNode["fullName"]?.GetValue<string>();
                    var displayName = memberNode["DisplayName"]?.GetValue<string>() ?? memberNode["displayName"]?.GetValue<string>();

                    var student = (Student?)null;
                    if (!string.IsNullOrEmpty(email))
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase));
                    if (student == null && !string.IsNullOrEmpty(fullName))
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals((s.FirstName + " " + s.LastName).Trim(), fullName, StringComparison.OrdinalIgnoreCase));
                    if (student == null && !string.IsNullOrEmpty(displayName) && displayName != fullName)
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals((s.FirstName + " " + s.LastName).Trim(), displayName, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Email, displayName, StringComparison.OrdinalIgnoreCase));

                    var roleName = student != null
                        ? (student.StudentRoles?
                            .Where(sr => sr.IsActive && sr.Role != null)
                            .Select(sr => sr.Role!.Name)
                            .FirstOrDefault() ?? "Team Member")
                        : "Team Member";

                    memberNode["RoleName"] = JsonValue.Create(roleName);

                    // For Full Stack Developer, include Backend and Frontend so frontend can match cards with those labels
                    if (roleName.Contains("Fullstack", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Full Stack", StringComparison.OrdinalIgnoreCase))
                    {
                        memberNode["RoleNames"] = new JsonArray(JsonValue.Create(roleName), JsonValue.Create("Backend Developer"), JsonValue.Create("Frontend Developer"));
                    }
                    else if (student == null && cardLabelNames.Count > 0)
                    {
                        // Unmatched member (e.g. admin/facilitator): allow matching all card labels so they can be assigned any task
                        memberNode["RoleNames"] = new JsonArray(cardLabelNames.Select(n => JsonValue.Create(n)).ToArray());
                    }
                    else
                    {
                        memberNode["RoleNames"] = new JsonArray(JsonValue.Create(roleName));
                    }
                }
            }

            var camelOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            // JsonNode property names are NOT renamed by PropertyNamingPolicy when serializing; convert keys manually.
            var statsNodeCamel = statsNode != null
                ? ToCamelCaseKeys(statsNode)
                : JsonNode.Parse(JsonSerializer.Serialize(stats, camelOptions));

            var responseNode = new JsonObject
            {
                ["success"] = true,
                ["boardId"] = boardId,
                ["projectId"] = projectBoard.ProjectId,
                ["projectName"] = projectBoard.Project?.Title ?? "",
                ["boardUrl"] = projectBoard.BoardUrl ?? "",
                ["observed"] = projectBoard.Observed,
                ["githubBackendUrl"] = projectBoard.GithubBackendUrl ?? "",
                ["githubFrontendUrl"] = projectBoard.GithubFrontendUrl ?? "",
                ["webApiUrl"] = projectBoard.WebApiUrl ?? "",
                ["stats"] = statsNodeCamel
            };

            return Content(JsonSerializer.Serialize(responseNode, camelOptions), "application/json");
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

            // Enrich each member with roleName and roleNames (same as stats) so frontend can match tasks by label
            var studentsOnBoard = await _context.Students
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Where(s => s.BoardId == boardId && s.IsAvailable)
                .ToListAsync();

            var stats = await _trelloService.GetProjectStatsAsync(boardId);
            var statsJson = JsonSerializer.Serialize(stats);
            var statsNode = JsonNode.Parse(statsJson);
            var cardLabelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (statsNode?["Cards"] is JsonArray cardsArray)
            {
                foreach (var cardNode in cardsArray.OfType<JsonObject>())
                {
                    var labels = cardNode["Labels"] ?? cardNode["labels"];
                    if (labels is JsonArray labelsArr)
                    {
                        foreach (var labelNode in labelsArr.OfType<JsonObject>())
                        {
                            var name = labelNode["Name"]?.GetValue<string>() ?? labelNode["name"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(name))
                                cardLabelNames.Add(name);
                        }
                    }
                }
            }

            var membersResultJson = JsonSerializer.Serialize(membersResult);
            var membersResultNode = JsonNode.Parse(membersResultJson);
            if (membersResultNode?["Members"] is JsonArray membersArray)
            {
                foreach (var memberNode in membersArray.OfType<JsonObject>())
                {
                    var email = memberNode["Email"]?.GetValue<string>() ?? memberNode["email"]?.GetValue<string>();
                    var fullName = memberNode["FullName"]?.GetValue<string>() ?? memberNode["fullName"]?.GetValue<string>();
                    var displayName = memberNode["DisplayName"]?.GetValue<string>() ?? memberNode["displayName"]?.GetValue<string>();

                    var student = (Student?)null;
                    if (!string.IsNullOrEmpty(email))
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase));
                    if (student == null && !string.IsNullOrEmpty(fullName))
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals((s.FirstName + " " + s.LastName).Trim(), fullName, StringComparison.OrdinalIgnoreCase));
                    if (student == null && !string.IsNullOrEmpty(displayName) && displayName != fullName)
                        student = studentsOnBoard.FirstOrDefault(s => string.Equals((s.FirstName + " " + s.LastName).Trim(), displayName, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Email, displayName, StringComparison.OrdinalIgnoreCase));

                    var roleName = student != null
                        ? (student.StudentRoles?
                            .Where(sr => sr.IsActive && sr.Role != null)
                            .Select(sr => sr.Role!.Name)
                            .FirstOrDefault() ?? "Team Member")
                        : "Team Member";

                    memberNode["RoleName"] = JsonValue.Create(roleName);

                    if (roleName.Contains("Fullstack", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Full Stack", StringComparison.OrdinalIgnoreCase))
                    {
                        memberNode["RoleNames"] = new JsonArray(JsonValue.Create(roleName), JsonValue.Create("Backend Developer"), JsonValue.Create("Frontend Developer"));
                    }
                    else if (student == null && cardLabelNames.Count > 0)
                    {
                        memberNode["RoleNames"] = new JsonArray(cardLabelNames.Select(n => JsonValue.Create(n)).ToArray());
                    }
                    else
                    {
                        memberNode["RoleNames"] = new JsonArray(JsonValue.Create(roleName));
                    }
                }
            }

            var camelOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var responseNode = new JsonObject
            {
                ["success"] = true,
                ["boardId"] = boardId,
                ["projectId"] = projectBoard.ProjectId,
                ["projectName"] = projectBoard.Project?.Title ?? "",
                ["boardUrl"] = projectBoard.BoardUrl ?? "",
                ["members"] = ToCamelCaseKeys(membersResultNode) ?? membersResultNode
            };

            return Content(JsonSerializer.Serialize(responseNode, camelOptions), "application/json");
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
    // PromptType: SprintPlanning — Keep (debug-only; test prompt)
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

            // Add GitHub commit activities - use role-based repository selection
            try
            {
                if (!string.IsNullOrWhiteSpace(student.GithubUser))
                {
                    var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                    var reposToCheck = new List<(string Url, string Type)>();
                    
                    if (!string.IsNullOrEmpty(frontendRepoUrl))
                    {
                        reposToCheck.Add((frontendRepoUrl, "frontend"));
                    }
                    if (!string.IsNullOrEmpty(backendRepoUrl))
                    {
                        reposToCheck.Add((backendRepoUrl, "backend"));
                    }

                    if (reposToCheck.Any())
                    {
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                        
                        var allCommits = new List<(DateTime Date, string Message, string RepoType)>();
                        
                        foreach (var (repoUrl, repoType) in reposToCheck)
                        {
                            var githubUrl = repoUrl.Trim();
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
                                                        if (commitMessage.Length > 100)
                                                        {
                                                            commitMessage = commitMessage.Substring(0, 97) + "...";
                                                        }
                                                    }
                                                    
                                                    if (commitDate.HasValue)
                                                    {
                                                        allCommits.Add((commitDate.Value, commitMessage, repoType));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Sort all commits by date (most recent first) and limit to 10
                        var sortedCommits = allCommits.OrderByDescending(c => c.Date).Take(10);
                        
                        foreach (var (date, message, repoType) in sortedCommits)
                        {
                            var description = isFullstack 
                                ? $"[{repoType.ToUpper()}] Committed: {message}"
                                : $"Committed: {message}";
                            
                            recentActivities.Add(new
                            {
                                Type = "githubCommit",
                                Date = date.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                Data = new
                                {
                                    CommitMessage = message,
                                    CommitDate = date.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                    Description = description
                                },
                                MemberCreator = (object?)null
                            });
                        }
                        
                        if (allCommits.Any())
                        {
                            _logger.LogInformation("Added {CommitCount} GitHub commit activities for student {StudentId} from {RepoCount} repository(s)", 
                                sortedCommits.Count(), studentId, reposToCheck.Count);
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

            // Get last GitHub commit info (date and message) for the student - use role-based repository selection
            Services.GitHubCommitInfo? lastCommitInfo = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(student.GithubUser))
                {
                    var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                    
                    // For fullstack, prioritize backend repo, but check both if needed
                    // For others, use the relevant repo
                    var repoToCheck = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl;
                    
                    if (!string.IsNullOrEmpty(repoToCheck))
                    {
                        // Parse GitHub URL to extract owner and repo
                        var githubUrl = repoToCheck.Trim();
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
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No repository URL found for student {StudentId} based on role", studentId);
                    }
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
                GithubBackendUrl = board.GithubBackendUrl,
                GithubFrontendUrl = board.GithubFrontendUrl,
                WebApiUrl = board.WebApiUrl,
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

    /// <summary>
    /// Gets the appropriate GitHub repository URL(s) based on the student's role
    /// Returns: Frontend URL for Frontend Developer, Backend URL for Backend Developer, or both for Fullstack Developer
    /// </summary>
    private (string? FrontendRepoUrl, string? BackendRepoUrl, bool IsFullstack) GetRepositoryUrlsByRole(Student student)
    {
        if (student?.ProjectBoard == null)
        {
            return (null, null, false);
        }

        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var roleName = activeRole?.Role?.Name ?? "";

        if (string.IsNullOrEmpty(roleName))
        {
            // Default to backend if no role specified (backward compatibility)
            return (null, student.ProjectBoard.GithubBackendUrl, false);
        }

        var roleNameLower = roleName.ToLowerInvariant();

        // Check for Fullstack/Full Stack Developer
        if (roleNameLower.Contains("full") && (roleNameLower.Contains("stack") || roleNameLower.Contains("stack")))
        {
            return (student.ProjectBoard.GithubFrontendUrl, student.ProjectBoard.GithubBackendUrl, true);
        }

        // Check for Frontend Developer
        if (roleNameLower.Contains("frontend"))
        {
            return (student.ProjectBoard.GithubFrontendUrl, null, false);
        }

        // Check for Backend Developer
        if (roleNameLower.Contains("backend"))
        {
            return (null, student.ProjectBoard.GithubBackendUrl, false);
        }

        // Default: use backend repo for backward compatibility
        return (null, student.ProjectBoard.GithubBackendUrl, false);
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

    /// <summary>
    /// Result of creating an isolated database role
    /// </summary>
    private class IsolatedRoleResult
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of creating a Neon project
    /// </summary>
    private class NeonProjectResult
    {
        public string ProjectId { get; set; } = string.Empty;
        public string? ProjectName { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DefaultBranchId { get; set; }
        public List<string> OperationIds { get; set; } = new();
    }

    /// <summary>
    /// Result of creating a Neon branch
    /// </summary>
    private class NeonBranchResult
    {
        public string BranchId { get; set; } = string.Empty;
        public string? EndpointHost { get; set; } // Endpoint hostname if available in response
        public string? EndpointId { get; set; } // Endpoint ID from API response (e.g., "ep-cool-darkness-123456")
        public List<string> OperationIds { get; set; } = new List<string>(); // Operation IDs from branch creation
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Creates a new Neon project for complete tenant isolation (project-per-tenant model)
    /// </summary>
    private async Task<NeonProjectResult> CreateNeonProjectAsync(string neonApiKey, string neonBaseUrl, string projectName)
    {
        try
        {
            _logger.LogInformation("🏗️ [NEON] Creating new Neon project for tenant isolation: {ProjectName}", projectName);
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Create project request body with quotas
            // Quotas: 8640 seconds = 2.4 hours active/compute time, 1.2GB written data, 600MB transfer
            var createProjectRequest = new
            {
                project = new
                {
                    name = projectName,
                    settings = new
                    {
                        quota = new
                        {
                            active_time_seconds = 8640,
                            compute_time_seconds = 8640,
                            written_data_bytes = 1200000000,
                            data_transfer_bytes = 600000000
                        }
                    },
                    pg_version = 15
                }
            };

            var requestBody = JsonSerializer.Serialize(createProjectRequest);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var apiUrl = $"{neonBaseUrl}/projects";
            _logger.LogInformation("🏗️ [NEON] Calling Neon API: POST {Url}", apiUrl);

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                
                string? projectId = null;
                string? projectNameFromResponse = null;
                string? defaultBranchId = null;
                var operationIds = new List<string>();
                
                // Extract project ID from response
                if (jsonDoc.RootElement.TryGetProperty("project", out var projectObj))
                {
                    if (projectObj.TryGetProperty("id", out var projectIdProp))
                    {
                        projectId = projectIdProp.GetString();
                    }
                    if (projectObj.TryGetProperty("name", out var projectNameProp))
                    {
                        projectNameFromResponse = projectNameProp.GetString();
                    }
                }

                // Extract default branch ID from project creation response (new projects automatically create a main branch)
                if (jsonDoc.RootElement.TryGetProperty("branch", out var branchObj))
                {
                    if (branchObj.TryGetProperty("id", out var branchIdProp))
                    {
                        defaultBranchId = branchIdProp.GetString();
                        _logger.LogInformation("🔍 [NEON] Found default branch ID in project creation response: {BranchId}", defaultBranchId);
                    }
                }

                // Extract operation IDs from project creation (we need to wait for these to finish)
                if (jsonDoc.RootElement.TryGetProperty("operations", out var operationsProp) && operationsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var op in operationsProp.EnumerateArray())
                    {
                        if (op.TryGetProperty("id", out var opIdProp))
                        {
                            var opId = opIdProp.GetString();
                            if (!string.IsNullOrEmpty(opId))
                            {
                                operationIds.Add(opId);
                                _logger.LogInformation("🔍 [NEON] Found project creation operation ID: {OperationId}", opId);
                            }
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(projectId))
                {
                    _logger.LogInformation("✅ [NEON] Successfully created Neon project: {ProjectId} ({ProjectName})", projectId, projectNameFromResponse ?? projectName);
                    return new NeonProjectResult
                    {
                        Success = true,
                        ProjectId = projectId,
                        ProjectName = projectNameFromResponse ?? projectName,
                        DefaultBranchId = defaultBranchId,
                        OperationIds = operationIds
                    };
                }

                _logger.LogWarning("⚠️ [NEON] Project created but ID not found in response: {Response}", responseContent);
                return new NeonProjectResult
                {
                    Success = false,
                    ErrorMessage = "Project ID not found in response"
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ [NEON] Failed to create Neon project: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new NeonProjectResult
                {
                    Success = false,
                    ErrorMessage = $"Neon API returned {response.StatusCode}: {errorContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [NEON] Exception creating Neon project: {Error}", ex.Message);
            return new NeonProjectResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a new Neon branch for database isolation
    /// </summary>
    private async Task<NeonBranchResult> CreateNeonBranchAsync(string neonApiKey, string neonBaseUrl, string neonProjectId, string? parentBranchId = null)
    {
        try
        {
            _logger.LogInformation("🌿 [NEON] Creating new branch for database isolation in project: {ProjectId}", neonProjectId);
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Create branch request body
            // Use JsonSerializerOptions to ignore null values (Neon API doesn't accept "branch": null)
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            object createBranchRequest;
            if (!string.IsNullOrEmpty(parentBranchId))
            {
                // Create branch from parent branch
                _logger.LogInformation("🌿 [NEON] Creating branch from parent branch '{ParentBranchId}'", parentBranchId);
                createBranchRequest = new
                {
                    endpoints = new[]
                    {
                        new { type = "read_write" }
                    },
                    branch = new { parent_id = parentBranchId }
                };
            }
            else
            {
                // No parent branch - only include endpoints (will use project's default branch)
                _logger.LogInformation("🌿 [NEON] Creating branch without parent (will use project's default branch)");
                createBranchRequest = new
                {
                    endpoints = new[]
                    {
                        new { type = "read_write" }
                    }
                };
            }

            var requestBody = JsonSerializer.Serialize(createBranchRequest, jsonOptions);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var apiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches";
            _logger.LogInformation("🌿 [NEON] Calling Neon API: POST {Url}", apiUrl);

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);
                
                // Extract branch ID, endpoint information, and operation IDs from response
                // According to Neon API docs, endpoints array is at ROOT level, not inside branch object
                string? branchId = null;
                string? endpointHost = null;
                string? endpointId = null;
                var operationIds = new List<string>();
                
                // Extract branch ID
                if (jsonDoc.RootElement.TryGetProperty("branch", out var branchObj))
                {
                    if (branchObj.TryGetProperty("id", out var branchIdProp))
                    {
                        branchId = branchIdProp.GetString();
                    }
                }
                
                // Extract endpoint information from ROOT level endpoints array
                // The endpoints array contains endpoint objects with id, host, branch_id, etc.
                if (jsonDoc.RootElement.TryGetProperty("endpoints", out var endpointsProp) && endpointsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var endpoint in endpointsProp.EnumerateArray())
                    {
                        // Verify this endpoint belongs to our branch
                        if (endpoint.TryGetProperty("branch_id", out var endpointBranchIdProp))
                        {
                            var endpointBranchId = endpointBranchIdProp.GetString();
                            if (endpointBranchId == branchId) // Match the branch
                            {
                                // Extract endpoint ID (e.g., "ep-cool-darkness-123456")
                                if (endpoint.TryGetProperty("id", out var endpointIdProp))
                                {
                                    endpointId = endpointIdProp.GetString();
                                    _logger.LogInformation("🌐 [NEON] Branch endpoint ID from API: {EndpointId}", endpointId);
                                }
                                
                                // Extract endpoint host
                                if (endpoint.TryGetProperty("host", out var hostProp))
                                {
                                    endpointHost = hostProp.GetString();
                                    _logger.LogInformation("🌐 [NEON] Branch endpoint host: {EndpointHost}", endpointHost);
                                }
                                
                                break; // Use first matching endpoint
                            }
                        }
                    }
                }

                // Extract ALL operation IDs from branch creation response
                if (jsonDoc.RootElement.TryGetProperty("operations", out var operationsProp) && operationsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var op in operationsProp.EnumerateArray())
                    {
                        if (op.TryGetProperty("id", out var opIdProp))
                        {
                            var opId = opIdProp.GetString();
                            if (!string.IsNullOrEmpty(opId))
                            {
                                operationIds.Add(opId);
                                _logger.LogInformation("🔄 [NEON] Found operation ID: {OperationId}", opId);
                            }
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(branchId))
                {
                    _logger.LogInformation("✅ [NEON] Successfully created branch: {BranchId} with {OperationCount} operations", branchId, operationIds.Count);
                    if (!string.IsNullOrEmpty(endpointId))
                    {
                        _logger.LogInformation("🌐 [NEON] Branch endpoint ID: {EndpointId}", endpointId);
                    }
                    if (!string.IsNullOrEmpty(endpointHost))
                    {
                        _logger.LogInformation("🌐 [NEON] Branch endpoint host: {EndpointHost}", endpointHost);
                    }
                    return new NeonBranchResult
                    {
                        Success = true,
                        BranchId = branchId,
                        EndpointHost = endpointHost,
                        EndpointId = endpointId,
                        OperationIds = operationIds
                    };
                }

                _logger.LogWarning("⚠️ [NEON] Branch created but ID not found in response: {Response}", responseContent);
                return new NeonBranchResult
                {
                    Success = false,
                    ErrorMessage = "Branch ID not found in response"
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ [NEON] Failed to create branch: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new NeonBranchResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create branch: {response.StatusCode} - {errorContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [NEON] Exception creating Neon branch");
            return new NeonBranchResult
            {
                Success = false,
                ErrorMessage = $"Exception: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates an isolated database role for a specific database to prevent cross-database access
    /// </summary>
    private async Task<IsolatedRoleResult?> CreateIsolatedDatabaseRole(string connectionString, string dbName)
    {
        try
        {
            _logger.LogInformation("🔐 [NEON] Starting database role isolation for database: {DbName}", dbName);
            
            // Generate a unique role name based on database name (sanitized - PostgreSQL role names are case-sensitive and must be valid identifiers)
            var sanitizedDbName = dbName.ToLowerInvariant().Replace("-", "_").Replace(".", "_");
            // Ensure role name is valid PostgreSQL identifier (max 63 chars, no special chars except underscore)
            var roleName = $"db_{sanitizedDbName}_user".Substring(0, Math.Min(63, $"db_{sanitizedDbName}_user".Length));
            _logger.LogInformation("🔐 [NEON] Generated role name: {RoleName}", roleName);
            
            // Generate a secure random password
            var password = GenerateSecurePassword(32);
            _logger.LogInformation("🔐 [NEON] Generated password for role {RoleName}: {Password} (RAW - for manual connection use)", roleName, password);
            _logger.LogDebug("🔐 [NEON] Generated secure password for role {RoleName}", roleName);
            
            // Parse the connection string URI to extract components (avoiding unknown query parameters like channel_binding)
            Uri uri;
            string originalHost;
            int originalPort;
            string originalUser;
            string originalPassword;
            
            try
            {
                // If it's a URI format, parse it directly
                if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) || 
                    connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove query parameters before parsing (they may contain unsupported parameters)
                    var uriString = connectionString.Split('?')[0];
                    uri = new Uri(uriString);
                    
                    originalHost = uri.Host;
                    originalPort = uri.Port > 0 ? uri.Port : 5432;
                    
                    // Extract user info (username:password)
                    var userInfo = uri.UserInfo;
                    if (string.IsNullOrEmpty(userInfo))
                    {
                        throw new ArgumentException("Connection string missing user credentials");
                    }
                    
                    var userParts = userInfo.Split(':');
                    originalUser = Uri.UnescapeDataString(userParts[0]);
                    originalPassword = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : "";
                }
                else
                {
                    // Try parsing as standard connection string
                    var builder = new NpgsqlConnectionStringBuilder(connectionString);
                    originalHost = builder.Host;
                    originalPort = builder.Port > 0 ? builder.Port : 5432;
                    originalUser = builder.Username ?? "";
                    originalPassword = builder.Password ?? "";
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "❌ [NEON] Failed to parse connection string");
                throw new ArgumentException($"Failed to parse connection string: {parseEx.Message}", parseEx);
            }
            
            // Build connection string to postgres database (default) to create the role
            var postgresBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = originalHost,
                Port = originalPort,
                Database = "postgres",
                Username = originalUser,
                Password = originalPassword,
                SslMode = SslMode.Require
            };
            var postgresConnString = postgresBuilder.ConnectionString;
            
            // Connect to postgres database to create the role
            using var postgresConn = new NpgsqlConnection(postgresConnString);
            await postgresConn.OpenAsync();
            _logger.LogDebug("🔐 [NEON] Connected to postgres database using owner connection");
            
            // Create the role (escape single quotes in password)
            var escapedPassword = password.Replace("'", "''");
            using var createRoleCmd = new NpgsqlCommand($@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{roleName}') THEN
                        CREATE ROLE {roleName} LOGIN PASSWORD '{escapedPassword}';
                        RAISE NOTICE 'Role {roleName} created';
                    ELSE
                        -- Role exists, update password
                        ALTER ROLE {roleName} WITH PASSWORD '{escapedPassword}';
                        RAISE NOTICE 'Role {roleName} password updated';
                    END IF;
                END
                $$;", postgresConn);
            
            await createRoleCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("✅ [NEON] Created/updated role: {RoleName}", roleName);
            
            // Wait for database to appear in PostgreSQL catalog before trying to revoke/grant CONNECT
            // This is needed because Neon databases may take a moment to propagate to pg_database
            _logger.LogInformation("⏳ [NEON] Waiting for database '{DbName}' to appear in PostgreSQL catalog...", dbName);
            var dbCatalogAvailable = false;
            var maxCatalogRetries = 10;
            var catalogRetryDelay = 500;
            
            for (int i = 0; i < maxCatalogRetries; i++)
            {
                try
                {
                    using var checkDbCmd = new NpgsqlCommand($@"SELECT 1 FROM pg_database WHERE datname = '{dbName.Replace("'", "''")}';", postgresConn);
                    var result = await checkDbCmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        dbCatalogAvailable = true;
                        _logger.LogDebug("✅ [NEON] Database '{DbName}' is now available in PostgreSQL catalog (attempt {Attempt})", dbName, i + 1);
                        break;
                    }
                }
                catch (Exception checkEx)
                {
                    _logger.LogDebug("⏳ [NEON] Error checking database catalog (attempt {Attempt}): {Error}", i + 1, checkEx.Message);
                }
                
                if (i < maxCatalogRetries - 1)
                {
                    _logger.LogDebug("⏳ [NEON] Database '{DbName}' not yet in catalog (attempt {Attempt}/{MaxRetries}), waiting {DelayMs}ms...", 
                        dbName, i + 1, maxCatalogRetries, catalogRetryDelay);
                    await Task.Delay(catalogRetryDelay);
                }
            }

            if (!dbCatalogAvailable)
            {
                _logger.LogWarning("⚠️ [NEON] Database '{DbName}' not found in PostgreSQL catalog after {MaxRetries} attempts. Will retry CONNECT privileges after connecting to database.", 
                    dbName, maxCatalogRetries);
            }
            
            // Revoke CONNECT from PUBLIC on this database
            bool connectPrivilegesSet = false;
            if (dbCatalogAvailable)
            {
                try
                {
                    using var revokePublicCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""{dbName}"" FROM PUBLIC;", postgresConn);
                    await revokePublicCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("🔒 [NEON] Revoked CONNECT privilege from PUBLIC on database '{DbName}'", dbName);
                    
                    // Grant CONNECT to the new role
                    using var grantConnectCmd = new NpgsqlCommand($@"GRANT CONNECT ON DATABASE ""{dbName}"" TO {roleName};", postgresConn);
                    await grantConnectCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("✅ [NEON] Granted CONNECT privilege to role {RoleName} on database '{DbName}'", roleName, dbName);
                    connectPrivilegesSet = true;
                    
                    // AGGRESSIVE MULTI-TENANT ISOLATION - Prevent seeing ANY other databases
                    // This is CRITICAL for a multi-tenant Neon server hosting thousands of databases
                    try
                    {
                        // 1. Revoke ALL access to system catalogs that could expose database names
                        using var revokePublicCatalogCmd = new NpgsqlCommand($"REVOKE SELECT ON pg_database FROM PUBLIC;", postgresConn);
                        await revokePublicCatalogCmd.ExecuteNonQueryAsync();
                        _logger.LogDebug("🔒 [NEON] Revoked SELECT privilege on pg_database from PUBLIC");
                        
                        using var revokeFromRoleCmd = new NpgsqlCommand($"REVOKE SELECT ON pg_database FROM {roleName};", postgresConn);
                        await revokeFromRoleCmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("🔒 [NEON] Revoked SELECT privilege on pg_database from role {RoleName}", roleName);
                        
                        // 2. Revoke access to ALL system catalogs that might expose database info
                        var systemCatalogsToRevoke = new[]
                        {
                            "pg_shadow", "pg_roles", "pg_authid", "pg_auth_members",
                            "pg_user", "pg_group", "pg_tablespace", "pg_settings"
                        };
                        
                        foreach (var catalog in systemCatalogsToRevoke)
                        {
                            try
                            {
                                using var revokeCmd = new NpgsqlCommand($"REVOKE SELECT ON {catalog} FROM {roleName};", postgresConn);
                                await revokeCmd.ExecuteNonQueryAsync();
                                _logger.LogDebug("🔒 [NEON] Revoked SELECT on {Catalog} from role {RoleName}", catalog, roleName);
                            }
                            catch (Exception catalogRevokeEx)
                            {
                                _logger.LogDebug("⚠️ [NEON] Could not revoke SELECT on {Catalog} from role {RoleName}: {Error}", 
                                    catalog, roleName, catalogRevokeEx.Message);
                            }
                        }
                        
                        // 3. Completely revoke USAGE on pg_catalog and information_schema
                        try
                        {
                            using var revokePgCatalogCmd = new NpgsqlCommand($"REVOKE USAGE ON SCHEMA pg_catalog FROM {roleName};", postgresConn);
                            await revokePgCatalogCmd.ExecuteNonQueryAsync();
                            
                            using var revokeInfoSchemaCmd = new NpgsqlCommand($"REVOKE USAGE ON SCHEMA information_schema FROM {roleName};", postgresConn);
                            await revokeInfoSchemaCmd.ExecuteNonQueryAsync();
                            
                            _logger.LogInformation("🔒 [NEON] Revoked USAGE on pg_catalog and information_schema from role {RoleName}", roleName);
                        }
                        catch (Exception schemaEx)
                        {
                            _logger.LogWarning("⚠️ [NEON] Could not revoke USAGE on schemas from role {RoleName}: {Error}", roleName, schemaEx.Message);
                        }
                        
                        // 4. Revoke CONNECT on postgres database (critical for multi-tenant)
                        try
                        {
                            using var revokePostgresConnectCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""postgres"" FROM {roleName};", postgresConn);
                            await revokePostgresConnectCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation("🔒 [NEON] Revoked CONNECT privilege on 'postgres' database from role {RoleName}", roleName);
                        }
                        catch (Exception postgresConnectEx)
                        {
                            _logger.LogWarning("⚠️ [NEON] Could not revoke CONNECT on 'postgres' database from role {RoleName}: {Error}", 
                                roleName, postgresConnectEx.Message);
                        }
                        
                        // 5. Set ALTER ROLE to completely restrict catalog access
                        try
                        {
                            using var alterRoleSearchPathCmd = new NpgsqlCommand($"ALTER ROLE {roleName} SET search_path = '\"$user\", public';", postgresConn);
                            await alterRoleSearchPathCmd.ExecuteNonQueryAsync();
                            
                            _logger.LogInformation("🔒 [NEON] Set search_path for role {RoleName} to exclude pg_catalog", roleName);
                        }
                        catch (Exception alterRoleEx)
                        {
                            _logger.LogWarning("⚠️ [NEON] Could not set search_path for role {RoleName}: {Error}", roleName, alterRoleEx.Message);
                        }
                        
                        _logger.LogInformation("✅ [NEON] Applied aggressive multi-tenant isolation for role {RoleName}", roleName);
                    }
                    catch (Exception isolationEx)
                    {
                        _logger.LogError(isolationEx, "❌ [NEON] CRITICAL: Failed to apply multi-tenant isolation for role {RoleName}. " +
                            "This is a security requirement for multi-tenant database hosting.", roleName);
                        // Don't throw here - we'll continue and try database-level isolation
                    }
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "3D000")
                {
                    _logger.LogWarning("⚠️ [NEON] Database '{DbName}' not found when setting CONNECT privileges (catalog check passed but command failed). Will retry after connecting to database.", dbName);
                }
            }
            
            // Connect to the specific database to grant schema and table privileges
            // Wait for database to become available (Neon databases may take a moment to propagate)
            var dbSpecificBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = originalHost,
                Port = originalPort,
                Database = dbName,
                Username = originalUser,
                Password = originalPassword,
                SslMode = SslMode.Require
            };
            var dbSpecificConnString = dbSpecificBuilder.ConnectionString;
            
            // Wait for database to become available with retries
            using var dbConn = new NpgsqlConnection(dbSpecificConnString);
            await WaitForDatabaseAvailableAsync(dbConn, dbName, maxRetries: 10, delayMs: 1000);
            _logger.LogDebug("🔐 [NEON] Connected to database '{DbName}' to grant privileges", dbName);
            
            // If CONNECT privileges weren't set earlier, try again now that we've connected to the database
            // (This ensures the database is fully available)
            if (!connectPrivilegesSet)
            {
                _logger.LogInformation("🔄 [NEON] Retrying CONNECT privilege setup for database '{DbName}' now that database is connected", dbName);
                try
                {
                    // We need to use the postgres connection for database-level privileges
                    using var retryRevokeCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""{dbName}"" FROM PUBLIC;", postgresConn);
                    await retryRevokeCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("🔒 [NEON] Successfully revoked CONNECT privilege from PUBLIC on database '{DbName}' (retry)", dbName);
                    
                    using var retryGrantCmd = new NpgsqlCommand($@"GRANT CONNECT ON DATABASE ""{dbName}"" TO {roleName};", postgresConn);
                    await retryGrantCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("✅ [NEON] Successfully granted CONNECT privilege to role {RoleName} on database '{DbName}' (retry)", roleName, dbName);
                    connectPrivilegesSet = true;
                    
                    // Revoke CONNECT on "postgres" database (retry)
                    try
                    {
                        using var retryRevokePostgresConnectCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""postgres"" FROM {roleName};", postgresConn);
                        await retryRevokePostgresConnectCmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("🔒 [NEON] Revoked CONNECT privilege on 'postgres' database from role {RoleName} (retry)", roleName);
                    }
                    catch (Exception retryPostgresConnectEx)
                    {
                        _logger.LogDebug("⚠️ [NEON] Could not revoke CONNECT on 'postgres' database from role {RoleName} (retry): {Error}", roleName, retryPostgresConnectEx.Message);
                    }
                    
                    // AGGRESSIVE MULTI-TENANT ISOLATION (RETRY) - Prevent seeing ANY other databases
                    try
                    {
                        // Retry all critical isolation steps
                        using var retryRevokePublicCmd = new NpgsqlCommand($"REVOKE SELECT ON pg_database FROM PUBLIC;", postgresConn);
                        await retryRevokePublicCmd.ExecuteNonQueryAsync();
                        
                        using var retryRevokeFromRoleCmd = new NpgsqlCommand($"REVOKE SELECT ON pg_database FROM {roleName};", postgresConn);
                        await retryRevokeFromRoleCmd.ExecuteNonQueryAsync();
                        
                        // Retry revoking from other system catalogs
                        var systemCatalogsToRevoke = new[]
                        {
                            "pg_shadow", "pg_roles", "pg_authid", "pg_auth_members",
                            "pg_user", "pg_group", "pg_tablespace", "pg_settings"
                        };
                        
                        foreach (var catalog in systemCatalogsToRevoke)
                        {
                            try
                            {
                                using var retryRevokeCatalogCmd = new NpgsqlCommand($"REVOKE SELECT ON {catalog} FROM {roleName};", postgresConn);
                                await retryRevokeCatalogCmd.ExecuteNonQueryAsync();
                            }
                            catch (Exception catalogRevokeEx)
                            {
                                _logger.LogDebug("⚠️ [NEON] Could not revoke SELECT on {Catalog} from role {RoleName} (retry): {Error}", 
                                    catalog, roleName, catalogRevokeEx.Message);
                            }
                        }
                        
                        // Retry revoking USAGE on schemas
                        try
                        {
                            using var retryRevokePgCatalogCmd = new NpgsqlCommand($"REVOKE USAGE ON SCHEMA pg_catalog FROM {roleName};", postgresConn);
                            await retryRevokePgCatalogCmd.ExecuteNonQueryAsync();
                            
                            using var retryRevokeInfoSchemaCmd = new NpgsqlCommand($"REVOKE USAGE ON SCHEMA information_schema FROM {roleName};", postgresConn);
                            await retryRevokeInfoSchemaCmd.ExecuteNonQueryAsync();
                            
                            _logger.LogInformation("🔒 [NEON] Revoked USAGE on schemas from role {RoleName} (retry)", roleName);
                        }
                        catch (Exception retrySchemaEx)
                        {
                            _logger.LogWarning("⚠️ [NEON] Could not revoke USAGE on schemas from role {RoleName} (retry): {Error}", roleName, retrySchemaEx.Message);
                        }
                        
                        // Retry revoking CONNECT on postgres
                        try
                        {
                            using var retryRevokePostgresConnectCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""postgres"" FROM {roleName};", postgresConn);
                            await retryRevokePostgresConnectCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation("🔒 [NEON] Revoked CONNECT on 'postgres' database from role {RoleName} (retry)", roleName);
                        }
                        catch (Exception retryPostgresConnectEx)
                        {
                            _logger.LogDebug("⚠️ [NEON] Could not revoke CONNECT on 'postgres' database from role {RoleName} (retry): {Error}", 
                                roleName, retryPostgresConnectEx.Message);
                        }
                        
                        _logger.LogInformation("✅ [NEON] Applied aggressive multi-tenant isolation for role {RoleName} (retry)", roleName);
                    }
                    catch (Exception retryIsolationEx)
                    {
                        _logger.LogError(retryIsolationEx, "❌ [NEON] CRITICAL: Failed to apply multi-tenant isolation for role {RoleName} on retry", roleName);
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogWarning("⚠️ [NEON] Failed to set CONNECT privileges on retry for database '{DbName}': {Error}. Continuing with schema privileges...", 
                        dbName, retryEx.Message);
                }
            }
            
            // Create a completely isolated environment - replace pg_database queries
            // This is CRITICAL for multi-tenant isolation - users must NOT see other databases
            try
            {
                // Ensure we revoke SELECT on pg_database at database level too
                using var revokePgDatabaseDbCmd = new NpgsqlCommand($"REVOKE SELECT ON pg_database FROM {roleName};", postgresConn);
                await revokePgDatabaseDbCmd.ExecuteNonQueryAsync();
                
                // Create a function that ONLY returns current database (no way to see others)
                // This function uses SECURITY DEFINER to run with owner privileges but filters results
                using var createIsolatedFunctionCmd = new NpgsqlCommand($@"
                    -- Drop any existing functions with this name
                    DROP FUNCTION IF EXISTS pg_database_current_only() CASCADE;
                    
                    -- Create function that ONLY shows current database
                    -- This is the ONLY way the role can query database information
                    CREATE FUNCTION pg_database_current_only()
                    RETURNS TABLE(
                        oid oid,
                        datname name,
                        datdba oid,
                        encoding integer,
                        datcollate name,
                        datctype name,
                        datistemplate boolean,
                        datallowconn boolean,
                        datconnlimit integer,
                        datlastsysoid oid,
                        datfrozenxid xid,
                        datminmxid xid,
                        dattablespace oid,
                        datacl aclitem[]
                    )
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    STABLE
                    AS $$
                    BEGIN
                        -- ONLY return the current database - no way to see others
                        RETURN QUERY
                        SELECT * FROM pg_database 
                        WHERE datname = current_database()
                        LIMIT 1;
                    END;
                    $$;
                    
                    -- Grant ONLY this function, nothing else
                    GRANT EXECUTE ON FUNCTION pg_database_current_only() TO {roleName};
                    
                    -- Revoke ALL other system catalog access at database level
                    REVOKE ALL ON SCHEMA pg_catalog FROM {roleName};
                    REVOKE ALL ON SCHEMA information_schema FROM {roleName};
                ", dbConn);
                await createIsolatedFunctionCmd.ExecuteNonQueryAsync();
                
                _logger.LogInformation("✅ [NEON] Created isolated database function for role {RoleName} - other databases are completely hidden", roleName);
            }
            catch (Exception isolationFuncEx)
            {
                _logger.LogError(isolationFuncEx, "❌ [NEON] CRITICAL: Failed to create isolated function for role {RoleName}. " +
                    "Multi-tenant isolation may be compromised.", roleName);
                // Don't throw - continue with other privileges, but log as critical
            }
            
            // Set additional role-level parameters for security
            try
            {
                // Disable row security bypass to ensure RLS policies are enforced
                using var alterRoleRowSecurityCmd = new NpgsqlCommand($"ALTER ROLE {roleName} SET row_security = on;", postgresConn);
                await alterRoleRowSecurityCmd.ExecuteNonQueryAsync();
                
                // Set statement_timeout to prevent long-running queries that might discover databases
                using var alterRoleTimeoutCmd = new NpgsqlCommand($"ALTER ROLE {roleName} SET statement_timeout = '30s';", postgresConn);
                await alterRoleTimeoutCmd.ExecuteNonQueryAsync();
                
                _logger.LogInformation("🔒 [NEON] Set additional security parameters for role {RoleName}", roleName);
            }
            catch (Exception alterRoleEx)
            {
                _logger.LogWarning("⚠️ [NEON] Could not set additional role-level parameters for {RoleName}: {Error}", roleName, alterRoleEx.Message);
            }
            
            // Grant USAGE and CREATE on schema (CREATE is needed to create tables)
            using var grantSchemaUsageCmd = new NpgsqlCommand($"GRANT USAGE ON SCHEMA public TO {roleName};", dbConn);
            await grantSchemaUsageCmd.ExecuteNonQueryAsync();
            
            using var grantSchemaCreateCmd = new NpgsqlCommand($"GRANT CREATE ON SCHEMA public TO {roleName};", dbConn);
            await grantSchemaCreateCmd.ExecuteNonQueryAsync();
            
            // Grant privileges on all existing tables
            using var grantTablesCmd = new NpgsqlCommand($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {roleName};", dbConn);
            await grantTablesCmd.ExecuteNonQueryAsync();
            
            // Grant privileges on all existing sequences
            using var grantSequencesCmd = new NpgsqlCommand($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {roleName};", dbConn);
            await grantSequencesCmd.ExecuteNonQueryAsync();
            
            // Set default privileges for future tables and sequences
            using var defaultTablesCmd = new NpgsqlCommand($"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {roleName};", dbConn);
            await defaultTablesCmd.ExecuteNonQueryAsync();
            
            using var defaultSequencesCmd = new NpgsqlCommand($"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {roleName};", dbConn);
            await defaultSequencesCmd.ExecuteNonQueryAsync();
            
            _logger.LogInformation("✅ [NEON] Granted all necessary privileges to role {RoleName} on database '{DbName}'", roleName, dbName);
            
            // Build new connection string with the isolated role (using URI format)
            var isolatedConnectionString = $"postgresql://{roleName}:{Uri.EscapeDataString(password)}@{originalHost}:{originalPort}/{dbName}?sslmode=require";
            
            // Log connection details for manual access (since this is public/free data)
            _logger.LogInformation("🔗 [NEON] Connection string for database '{DbName}' with isolated role '{RoleName}': {ConnectionString}", 
                dbName, roleName, isolatedConnectionString);
            _logger.LogInformation("🔑 [NEON] Raw password for role '{RoleName}' (for manual pgAdmin connection): {Password}", 
                roleName, password);
            
            // Verify that we can connect with the isolated role before returning
            // This ensures the connection string is valid and the role has proper access
            try
            {
                var testBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = originalHost,
                    Port = originalPort,
                    Database = dbName,
                    Username = roleName,
                    Password = password,
                    SslMode = SslMode.Require
                };
                
                using var testConn = new NpgsqlConnection(testBuilder.ConnectionString);
                await testConn.OpenAsync();
                _logger.LogDebug("✅ [NEON] Verified isolated role connection for database '{DbName}'", dbName);
                await testConn.CloseAsync();
            }
            catch (Exception verifyEx)
            {
                _logger.LogWarning("⚠️ [NEON] Could not verify isolated role connection for database '{DbName}': {Error}. " +
                    "This may indicate the role needs more time to propagate. Connection string will still be returned.", 
                    dbName, verifyEx.Message);
            }
            
            _logger.LogInformation("✅ [NEON] Successfully created isolated role and connection string for database '{DbName}'", dbName);
            return new IsolatedRoleResult
            {
                ConnectionString = isolatedConnectionString,
                Password = password,
                RoleName = roleName
            };
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "3D000")
        {
            // Database doesn't exist - this is a timing issue, we should retry at a higher level
            _logger.LogError(pgEx, "❌ [NEON] Database '{DbName}' does not exist when creating isolated role. " +
                "This may be a timing issue - database may need more time to propagate.", dbName);
            throw; // Re-throw to allow retry at higher level
        }
        catch (Exception ex)
        {
            // For other errors, log but don't fall back to owner connection
            // The isolated role is a security requirement
            _logger.LogError(ex, "❌ [NEON] Critical error creating isolated database role for database '{DbName}': {Message}. " +
                "This is a security requirement - cannot fall back to owner connection.", dbName, ex.Message);
            throw; // Re-throw instead of returning null to prevent fallback to neondb_owner
        }
    }
    
    /// <summary>
    /// Waits for a database to become available by retrying the connection
    /// </summary>
    private async Task WaitForDatabaseAvailableAsync(NpgsqlConnection connection, string dbName, int maxRetries = 10, int delayMs = 1000)
    {
        var retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                // Close connection if already open from previous attempt
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
                
                await connection.OpenAsync();
                _logger.LogDebug("🔐 [NEON] Successfully connected to database '{DbName}' after {RetryCount} retries", dbName, retryCount);
                return; // Success - database is available
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "3D000" && retryCount < maxRetries - 1)
            {
                // Database doesn't exist yet - wait and retry
                retryCount++;
                _logger.LogInformation("⏳ [NEON] Database '{DbName}' not yet available (attempt {RetryCount}/{MaxRetries}), waiting {DelayMs}ms before retry...", 
                    dbName, retryCount, maxRetries, delayMs);
                await Task.Delay(delayMs);
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                // Other connection errors - wait and retry
                retryCount++;
                _logger.LogWarning("⏳ [NEON] Connection to database '{DbName}' failed (attempt {RetryCount}/{MaxRetries}): {Error}, waiting {DelayMs}ms before retry...", 
                    dbName, retryCount, maxRetries, ex.Message, delayMs);
                await Task.Delay(delayMs);
            }
            catch
            {
                // Last retry failed or non-retryable error - rethrow
                throw;
            }
        }
        
        // If we get here, all retries failed
        throw new InvalidOperationException($"Database '{dbName}' did not become available after {maxRetries} retries");
    }
    
    /// <summary>
    /// Generates a secure random password
    /// </summary>
    private string GenerateSecurePassword(int length)
    {
        const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return new string(bytes.Select(b => validChars[b % validChars.Length]).ToArray());
    }
    
    /// <summary>
    /// Executes the initial database schema script to create TestProjects table
    /// </summary>
    private async Task ExecuteInitialDatabaseSchema(string connectionString, string dbName, string? isolatedRoleName = null)
    {
        try
        {
            _logger.LogInformation("📊 [NEON] Executing initial database schema for database '{DbName}'", dbName);
            
            // Parse connection string properly (handle URI format with query parameters)
            NpgsqlConnectionStringBuilder builder;
            try
            {
                // Try parsing as URI format first
                if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) || 
                    connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove query parameters before parsing (they may contain unsupported parameters)
                    var uriString = connectionString.Split('?')[0];
                    var uri = new Uri(uriString);
                    
                    var host = uri.Host;
                    var port = uri.Port > 0 ? uri.Port : 5432;
                    var database = uri.AbsolutePath.TrimStart('/').Split('?')[0];
                    
                    // Extract user info (username:password)
                    var userInfo = uri.UserInfo;
                    if (string.IsNullOrEmpty(userInfo))
                    {
                        throw new ArgumentException("Connection string missing user credentials");
                    }
                    
                    var userParts = userInfo.Split(':');
                    var username = Uri.UnescapeDataString(userParts[0]);
                    var password = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : "";
                    
                    builder = new NpgsqlConnectionStringBuilder
                    {
                        Host = host,
                        Port = port,
                        Database = database,
                        Username = username,
                        Password = password,
                        SslMode = SslMode.Require
                    };
                }
                else
                {
                    // Try parsing as standard connection string
                    builder = new NpgsqlConnectionStringBuilder(connectionString);
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "❌ [NEON] Failed to parse connection string for database '{DbName}'", dbName);
                throw new ArgumentException($"Failed to parse connection string: {parseEx.Message}", parseEx);
            }
            
            // SQL script to create TestProjects table with mock data
            // Note: We explicitly set search_path to 'public' because the isolated role has restricted search_path
            var sqlScript = @"
-- Set search_path to public schema (required because isolated role has restricted search_path)
SET search_path = public, ""$user"";

-- TestProjects table for initial project setup
-- This table is used for testing and learning database interactions

CREATE TABLE IF NOT EXISTS ""TestProjects"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(255) NOT NULL
);

-- Insert mock data (only if table is empty)
-- Use a simpler approach: DELETE all rows first, then INSERT (ensures clean state)
DELETE FROM ""TestProjects"";

INSERT INTO ""TestProjects"" (""Name"") VALUES
    ('Sample Project 1'),
    ('Sample Project 2'),
    ('Sample Project 3'),
    ('Learning Project'),
    ('Test Project');
";
            
            using var conn = new NpgsqlConnection(builder.ConnectionString);
            // Wait for database to become available (Neon databases may take a moment to propagate)
            await WaitForDatabaseAvailableAsync(conn, dbName, maxRetries: 10, delayMs: 1000);
            _logger.LogDebug("📊 [NEON] Connected to database '{DbName}' to execute schema script", dbName);
            
            using var cmd = new NpgsqlCommand(sqlScript, conn);
            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("📊 [NEON] Schema script executed. Rows affected: {RowsAffected}", rowsAffected);
            
            // Verify that data was inserted
            using var verifyCmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""TestProjects"";", conn);
            var rowCount = await verifyCmd.ExecuteScalarAsync();
            var count = rowCount != null ? Convert.ToInt32(rowCount) : 0;
            
            if (count > 0)
            {
                _logger.LogInformation("✅ [NEON] Successfully executed initial database schema for database '{DbName}'. TestProjects table created with {Count} rows of mock data.", dbName, count);
            }
            else
            {
                _logger.LogWarning("⚠️ [NEON] TestProjects table created for database '{DbName}' but INSERT failed or table is empty. Row count: {Count}. Attempting manual INSERT...", dbName, count);
                
                // Try manual INSERT as fallback
                try
                {
                    using var manualInsertCmd = new NpgsqlCommand(@"
                        INSERT INTO ""TestProjects"" (""Name"") VALUES
                            ('Sample Project 1'),
                            ('Sample Project 2'),
                            ('Sample Project 3'),
                            ('Learning Project'),
                            ('Test Project');
                    ", conn);
                    var insertRows = await manualInsertCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("✅ [NEON] Manual INSERT succeeded. Rows inserted: {Rows}", insertRows);
                    
                    // Verify again
                    var verifyCount = await verifyCmd.ExecuteScalarAsync();
                    var finalCount = verifyCount != null ? Convert.ToInt32(verifyCount) : 0;
                    _logger.LogInformation("✅ [NEON] Final row count after manual INSERT: {Count}", finalCount);
                }
                catch (Exception insertEx)
                {
                    _logger.LogError(insertEx, "❌ [NEON] Manual INSERT also failed for database '{DbName}': {Error}", dbName, insertEx.Message);
                }
            }
            
            // Grant permissions on TestProjects table to isolated role (if provided)
            // This is necessary because the table was created by the owner, and default privileges
            // only apply to objects created by the role that set them
            if (!string.IsNullOrEmpty(isolatedRoleName))
            {
                try
                {
                    using var grantCmd = new NpgsqlCommand($@"
                        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE ""TestProjects"" TO {isolatedRoleName};
                        GRANT USAGE, SELECT ON SEQUENCE ""TestProjects_Id_seq"" TO {isolatedRoleName};
                    ", conn);
                    await grantCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("✅ [NEON] Granted permissions on TestProjects table to isolated role '{RoleName}'", isolatedRoleName);
                }
                catch (Exception grantEx)
                {
                    _logger.LogWarning(grantEx, "⚠️ [NEON] Failed to grant permissions on TestProjects table to isolated role '{RoleName}': {Error}", isolatedRoleName, grantEx.Message);
                    // Don't throw - this is not critical, but may cause issues with student backend queries
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [NEON] Error executing initial database schema for database '{DbName}': {Message}", dbName, ex.Message);
            // Don't throw - this is not critical for board creation
        }
    }
    
    /// <summary>
    /// Verifies that a Railway environment variable was set correctly
    /// </summary>
    /// <summary>
    /// Updates Railway service's DATABASE_URL environment variable
    /// </summary>
    private async Task UpdateRailwayDatabaseUrl(string serviceId, string connectionString, string projectId, string apiToken, string apiUrl)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

            // Get environment ID
            string? environmentId = null;
            var getProjectQuery = new
            {
                query = @"
                    query GetProject($projectId: String!) {
                        project(id: $projectId) {
                            id
                            environments {
                                edges {
                                    node {
                                        id
                                    }
                                }
                            }
                        }
                    }",
                variables = new { projectId = projectId }
            };

            var queryBody = System.Text.Json.JsonSerializer.Serialize(getProjectQuery);
            var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
            var queryResponse = await httpClient.PostAsync(apiUrl, queryContent);

            if (queryResponse.IsSuccessStatusCode)
            {
                var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
                var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);
                if (queryDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                    dataObj.TryGetProperty("project", out var projectObj) &&
                    projectObj.TryGetProperty("environments", out var environmentsProp) &&
                    environmentsProp.TryGetProperty("edges", out var edgesProp))
                {
                    var edges = edgesProp.EnumerateArray().ToList();
                    if (edges.Count > 0)
                    {
                        if (edges[0].TryGetProperty("node", out var nodeProp) &&
                            nodeProp.TryGetProperty("id", out var envIdProp))
                        {
                            environmentId = envIdProp.GetString();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(environmentId))
            {
                _logger.LogWarning("⚠️ [RAILWAY] Cannot update DATABASE_URL: Environment ID not found for project {ProjectId}", projectId);
                return;
            }

            // Set DATABASE_URL
            var setEnvMutation = new
            {
                query = @"
                    mutation SetVariable($projectId: String!, $environmentId: String!, $serviceId: String!, $name: String!, $value: String!) {
                        variableUpsert(input: { 
                            projectId: $projectId
                            environmentId: $environmentId
                            serviceId: $serviceId
                            name: $name
                            value: $value
                        })
                    }",
                variables = new
                {
                    projectId = projectId,
                    environmentId = environmentId,
                    serviceId = serviceId,
                    name = "DATABASE_URL",
                    value = connectionString
                }
            };

            _logger.LogInformation("🔧 [RAILWAY] Updating DATABASE_URL on service {ServiceId}", serviceId);
            var envBody = System.Text.Json.JsonSerializer.Serialize(setEnvMutation);
            var envContent = new StringContent(envBody, System.Text.Encoding.UTF8, "application/json");
            var envResponse = await httpClient.PostAsync(apiUrl, envContent);

            if (envResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ [RAILWAY] Successfully updated DATABASE_URL on Railway service {ServiceId}. Railway will redeploy with new environment variable.", serviceId);
            }
            else
            {
                var errorContent = await envResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("⚠️ [RAILWAY] Failed to update DATABASE_URL: {StatusCode} - {Error}", envResponse.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [RAILWAY] Error updating DATABASE_URL on Railway service {ServiceId}", serviceId);
            throw;
        }
    }

    private async Task VerifyRailwayEnvironmentVariable(HttpClient httpClient, string railwayApiUrl, string serviceId, string apiToken)
    {
        try
        {
            _logger.LogInformation("🔍 [RAILWAY] Verifying DATABASE_URL environment variable on service {ServiceId}", serviceId);
            
            // Query Railway API to get service variables
            var queryVariables = new
            {
                query = @"
                    query GetServiceVariables($serviceId: String!) {
                        service(id: $serviceId) {
                            variables {
                                name
                            }
                        }
                    }",
                variables = new
                {
                    serviceId = serviceId
                }
            };
            
            var queryBody = System.Text.Json.JsonSerializer.Serialize(queryVariables);
            var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
            
            var queryResponse = await httpClient.PostAsync(railwayApiUrl, queryContent);
            var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
            
            if (queryResponse.IsSuccessStatusCode)
            {
                var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);
                if (queryDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                    dataObj.TryGetProperty("service", out var serviceObj) &&
                    serviceObj.TryGetProperty("variables", out var variablesProp))
                {
                    var variables = variablesProp.EnumerateArray().Select(v => v.GetProperty("name").GetString()).ToList();
                    
                    if (variables.Contains("DATABASE_URL"))
                    {
                        _logger.LogInformation("✅ [RAILWAY] Verified: DATABASE_URL environment variable exists on service {ServiceId}", serviceId);
                        _logger.LogInformation("📋 [RAILWAY] Service has {Count} environment variables: {Variables}", variables.Count, string.Join(", ", variables));
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [RAILWAY] DATABASE_URL environment variable NOT FOUND on service {ServiceId}. Available variables: {Variables}", 
                            serviceId, string.Join(", ", variables));
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ [RAILWAY] Unexpected response structure when querying service variables: {Response}", queryResponseContent);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ [RAILWAY] Failed to query service variables: {StatusCode} - {Error}", 
                    queryResponse.StatusCode, queryResponseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [RAILWAY] Error verifying Railway environment variable: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Connects a GitHub repository to a Railway service
    /// </summary>
    private async Task ConnectGitHubRepositoryToRailway(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string serviceId, string repositoryUrl)
    {
        try
        {
            _logger.LogInformation("🔗 [RAILWAY] Connecting GitHub repository to Railway service {ServiceId}", serviceId);
            _logger.LogDebug("🔗 [RAILWAY] Repository URL: {RepositoryUrl}", repositoryUrl);
            
            // Parse GitHub repository URL to extract owner and repo name
            // Format: https://github.com/owner/repo-name
            var uri = new Uri(repositoryUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            
            if (pathParts.Length < 2)
            {
                _logger.LogWarning("⚠️ [RAILWAY] Invalid GitHub repository URL format: {RepositoryUrl}", repositoryUrl);
                return;
            }
            
            var repoOwner = pathParts[0];
            var repoName = pathParts[1].Replace(".git", ""); // Remove .git suffix if present
            var repoFullName = $"{repoOwner}/{repoName}";
            
            _logger.LogInformation("🔗 [RAILWAY] Extracted repo info - Owner: {Owner}, Name: {Name}, Full: {FullName}", 
                repoOwner, repoName, repoFullName);
            
            _logger.LogInformation("🚨 [RAILWAY] CRITICAL: Passing GitHub repo '{RepoFullName}' to Railway API", repoFullName);
            _logger.LogInformation("📝 [RAILWAY] Repository structure: Backend files are at root, frontend is in frontend/ folder");
            _logger.LogInformation("📝 [RAILWAY] Railway will build from root and detect backend application files automatically");
            
            // Railway GraphQL mutation to connect GitHub repository
            var connectRepoMutation = new
            {
                query = @"
                    mutation ConnectGithubRepo($id: String!, $repo: String!, $branch: String) {
                        serviceConnect(
                            id: $id
                            input: {
                                repo: $repo
                                branch: $branch
                            }
                        ) {
                            id
                        }
                    }",
                variables = new
                {
                    id = serviceId,
                    repo = repoFullName,
                    branch = "main" // Default branch
                }
            };
            
            var mutationBody = System.Text.Json.JsonSerializer.Serialize(connectRepoMutation);
            var mutationContent = new StringContent(mutationBody, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(railwayApiUrl, mutationContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[RAILWAY] Repository connected successfully");
            }
            else
            {
                _logger.LogError("[RAILWAY] Failed to connect repository: {StatusCode}", response.StatusCode);
                
                // Log GraphQL errors for debugging
                try
                {
                    var errorDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                    {
                        foreach (var error in errorsProp.EnumerateArray())
                        {
                            var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                            _logger.LogError("[RAILWAY] GraphQL error: {ErrorMessage}", errorMsg);
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RAILWAY] Error connecting repository: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Creates a public domain for a Railway service
    /// </summary>
    private async Task<string?> CreateRailwayServiceDomain(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string serviceId, string environmentId, int targetPort = 8080)
    {
        try
        {
            _logger.LogInformation("[RAILWAY] Creating public domain for service on port {Port}", targetPort);
            
            // Railway GraphQL mutation to create a service domain
            var createDomainMutation = new
            {
                query = @"
                    mutation CreateServiceDomain($input: ServiceDomainCreateInput!) {
                        serviceDomainCreate(input: $input) {
                            id
                            domain
                            targetPort
                        }
                    }",
                variables = new
                {
                    input = new
                    {
                        environmentId = environmentId,
                        serviceId = serviceId,
                        targetPort = targetPort
                    }
                }
            };
            
            var mutationBody = System.Text.Json.JsonSerializer.Serialize(createDomainMutation);
            var mutationContent = new StringContent(mutationBody, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(railwayApiUrl, mutationContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                
                // Check for GraphQL errors
                if (responseDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                {
                    foreach (var error in errorsProp.EnumerateArray())
                    {
                        var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                        _logger.LogWarning("[RAILWAY] GraphQL error creating domain: {ErrorMessage}", errorMsg);
                    }
                    return null;
                }
                
                if (responseDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                    dataObj.TryGetProperty("serviceDomainCreate", out var domainObj))
                {
                    if (domainObj.TryGetProperty("domain", out var domainProp))
                    {
                        var domain = domainProp.GetString();
                        if (!string.IsNullOrEmpty(domain))
                        {
                            // Railway domains should always use HTTPS (required for Mixed Content security)
                            // Remove any existing protocol and always use HTTPS
                            var cleanDomain = domain.Replace("http://", "").Replace("https://", "").TrimEnd('/');
                            var serviceUrl = $"https://{cleanDomain}";
                            return serviceUrl;
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("[RAILWAY] Failed to create domain: {StatusCode}", response.StatusCode);
                
                // Try to parse and log GraphQL errors
                try
                {
                    var errorDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                    {
                        foreach (var error in errorsProp.EnumerateArray())
                        {
                            var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                            _logger.LogError("[RAILWAY] GraphQL error: {ErrorMessage}", errorMsg);
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RAILWAY] Error creating domain: {Message}", ex.Message);
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the default port for a programming language
    /// </summary>
    private int GetDefaultPortForLanguage(string? programmingLanguage)
    {
        if (string.IsNullOrEmpty(programmingLanguage))
            return 8080; // Default to 8080
        
        switch (programmingLanguage.ToLowerInvariant())
        {
            case "c#":
            case "csharp":
                return 8080; // .NET default
            case "python":
                return 8080; // Match PORT environment variable (8080)
            case "nodejs":
            case "node.js":
            case "node":
                return 8080; // Match PORT environment variable (8080)
            case "java":
                return 8080; // Spring Boot default
            default:
                return 8080; // Default fallback
        }
    }
    
    /// <summary>
    /// Sets Railway build environment variables (files are at root level, not in backend/ subdirectory)
    /// Build commands vary by programming language
    /// Note: nixpacks.toml should handle most build configuration, but these variables provide additional control
    /// </summary>
    private async Task SetRailwayBuildSettings(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string projectId, string serviceId, string environmentId, string programmingLanguage, string? boardId = null)
    {
        try
        {
            _logger.LogInformation("[RAILWAY] Setting build environment variables (language: {Language})", programmingLanguage);
            
            // Build commands vary by programming language
            // NOTE: Backend files are at root level (not in backend/ subdirectory)
            // Maven environment variables are set for ALL languages to reduce log noise (only affects Java/Maven builds)
            var languageSpecificVariables = programmingLanguage?.ToLowerInvariant() switch
            {
                "c#" or "csharp" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "dotnet restore Backend.csproj && dotnet publish Backend.csproj -c Release -o /app/publish" },
                    new { name = "RAILWAY_START_COMMAND", value = "dotnet /app/publish/Backend.dll" },
                    new { name = "NIXPACKS_CSHARP_SDK_VERSION", value = "8.0" },
                    new { name = "PORT", value = "8080" }
                },
                "python" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "pip install -r requirements.txt" },
                    new { name = "RAILWAY_START_COMMAND", value = "uvicorn main:app --host 0.0.0.0 --port $PORT" },
                    new { name = "NIXPACKS_PYTHON_VERSION", value = "3.11" },
                    new { name = "PORT", value = "8080" }
                },
                "nodejs" or "node.js" or "node" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "npm install" },
                    new { name = "RAILWAY_START_COMMAND", value = "node app.js" },
                    new { name = "NIXPACKS_NODE_VERSION", value = "18" },
                    new { name = "PORT", value = "8080" }
                },
                "java" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "mvn clean package || gradle build || echo 'Build system not detected'" },
                    new { name = "RAILWAY_START_COMMAND", value = "java -jar target/*.jar || echo 'JAR not found'" },
                    new { name = "NIXPACKS_JDK_VERSION", value = "17" },
                    new { name = "PORT", value = "8080" }
                },
                _ => new[] // Default to .NET
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "dotnet restore Backend.csproj && dotnet publish Backend.csproj -c Release -o /app/publish" },
                    new { name = "RAILWAY_START_COMMAND", value = "dotnet /app/publish/Backend.dll" },
                    new { name = "NIXPACKS_CSHARP_SDK_VERSION", value = "8.0" },
                    new { name = "PORT", value = "8080" }
                }
            };
            
            // Maven environment variables - set for ALL languages to reduce log noise
            // These only affect Java/Maven builds, but setting them for all languages is safe
            var mavenVariables = new[]
            {
                new { name = "MAVEN_OPTS", value = "-Dorg.slf4j.simpleLogger.defaultLogLevel=warn -Dorg.slf4j.simpleLogger.showDateTime=false" },
                new { name = "MAVEN_ARGS", value = "-ntp -q" }
            };
            
            // Runtime error endpoint URL - set for ALL languages to enable error reporting
            var apiBaseUrl = _configuration["ApiBaseUrl"];
            var runtimeErrorVariables = new[]
            {
                !string.IsNullOrWhiteSpace(apiBaseUrl)
                    ? new { name = "RUNTIME_ERROR_ENDPOINT_URL", value = $"{apiBaseUrl.TrimEnd('/')}/api/Mentor/runtime-error" }
                    : null
            }.Where(v => v != null).ToArray();
            
            if (runtimeErrorVariables.Length > 0)
            {
                _logger.LogInformation("[RAILWAY] Setting RUNTIME_ERROR_ENDPOINT_URL: {Url}", runtimeErrorVariables[0].value);
            }
            else
            {
                _logger.LogWarning("[RAILWAY] ApiBaseUrl not configured - RUNTIME_ERROR_ENDPOINT_URL will not be set");
            }
            
            // BOARD_ID environment variable - set for ALL languages to enable boardId extraction in middleware
            var boardIdVariables = new[]
            {
                !string.IsNullOrWhiteSpace(boardId)
                    ? new { name = "BOARD_ID", value = boardId }
                    : null
            }.Where(v => v != null).ToArray();
            
            if (boardIdVariables.Length > 0)
            {
                _logger.LogInformation("[RAILWAY] Setting BOARD_ID: {BoardId}", boardIdVariables[0].value);
            }
            
            // Combine language-specific variables with Maven variables, runtime error endpoint, and board ID
            var buildVariables = languageSpecificVariables
                .Concat(mavenVariables)
                .Concat(runtimeErrorVariables)
                .Concat(boardIdVariables)
                .ToArray();
            
            // Set each environment variable
            foreach (var variable in buildVariables)
            {
                try
                {
                    var setVarMutation = new
                    {
                        query = @"
                            mutation SetVariable($projectId: String!, $environmentId: String!, $serviceId: String!, $name: String!, $value: String!) {
                                variableUpsert(input: { 
                                    projectId: $projectId
                                    environmentId: $environmentId
                                    serviceId: $serviceId
                                    name: $name
                                    value: $value
                                })
                            }",
                        variables = new
                        {
                            projectId = projectId,
                            environmentId = environmentId,
                            serviceId = serviceId,
                            name = variable.name,
                            value = variable.value
                        }
                    };
                    
                    var mutationBody = System.Text.Json.JsonSerializer.Serialize(setVarMutation);
                    var mutationContent = new StringContent(mutationBody, System.Text.Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(railwayApiUrl, mutationContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[RAILWAY] Set build variable {Name}", variable.name);
                    }
                    else
                    {
                        _logger.LogWarning("[RAILWAY] Failed to set build variable {Name}: {StatusCode}", variable.name, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [RAILWAY] Error setting build variable {Name}: {Message}", variable.name, ex.Message);
                }
            }
            
            _logger.LogInformation("[RAILWAY] Finished setting build environment variables");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [RAILWAY] Error setting Railway build settings: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Sets the Railway service root directory to prevent static site detection
    /// This is CRITICAL - tells Railway where to look for the application code
    /// </summary>
    private async Task SetRailwayServiceRootDirectory(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string serviceId, string rootDir)
    {
        try
        {
            _logger.LogInformation("📁 [RAILWAY] Setting service {ServiceId} root directory to '{RootDir}'", serviceId, rootDir);
            _logger.LogInformation("🚨 [RAILWAY] LANDING PAGE FIX: This prevents Railway from detecting index.html at repo root as static site");
            
            // Try serviceUpdate mutation to set root directory
            // Railway API may use different field names: rootDir, sourceRoot, source.rootDir, etc.
            // Try multiple variations to find what Railway API accepts
            bool success = false;
            string[] fieldNames = { "rootDir", "sourceRoot", "source.rootDir" };
            string mutationQuery = @"
                mutation UpdateService($id: String!, $input: ServiceUpdateInput!) {
                    serviceUpdate(id: $id, input: $input) {
                        id
                        name
                    }
                }";
            
            // Try 1: rootDir
            _logger.LogInformation("🔍 [RAILWAY] Attempt 1/3: Trying to set rootDir via serviceUpdate with field 'rootDir'");
            var mutation1 = new
            {
                query = mutationQuery,
                variables = new
                {
                    id = serviceId,
                    input = new
                    {
                        rootDir = rootDir
                    }
                }
            };
            var mutationBody1 = System.Text.Json.JsonSerializer.Serialize(mutation1);
            var inputJson1 = System.Text.Json.JsonSerializer.Serialize(mutation1.variables.input);
            _logger.LogInformation("🔍 [RAILWAY] Input structure: {Input}", inputJson1);
            _logger.LogDebug("📁 [RAILWAY] Update service mutation: {Mutation}", mutationBody1);
            
            var mutationContent1 = new StringContent(mutationBody1, System.Text.Encoding.UTF8, "application/json");
            var response1 = await httpClient.PostAsync(railwayApiUrl, mutationContent1);
            var responseContent1 = await response1.Content.ReadAsStringAsync();
            
            if (response1.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ [RAILWAY] Successfully set root directory to '{RootDir}' for service {ServiceId} using field 'rootDir'", 
                    rootDir, serviceId);
                _logger.LogInformation("🚨 [RAILWAY] LANDING PAGE FIX APPLIED: Railway should now build from {RootDir}/ instead of repo root", rootDir);
                success = true;
            }
            else
            {
                _logger.LogWarning("⚠️ [RAILWAY] Attempt 1 failed with field 'rootDir': {StatusCode}", response1.StatusCode);
                
                // Try 2: sourceRoot
                _logger.LogInformation("🔍 [RAILWAY] Attempt 2/3: Trying to set rootDir via serviceUpdate with field 'sourceRoot'");
                var mutation2 = new
                {
                    query = mutationQuery,
                    variables = new
                    {
                        id = serviceId,
                        input = new
                        {
                            sourceRoot = rootDir
                        }
                    }
                };
                var mutationBody2 = System.Text.Json.JsonSerializer.Serialize(mutation2);
                var inputJson2 = System.Text.Json.JsonSerializer.Serialize(mutation2.variables.input);
                _logger.LogInformation("🔍 [RAILWAY] Input structure: {Input}", inputJson2);
                
                var mutationContent2 = new StringContent(mutationBody2, System.Text.Encoding.UTF8, "application/json");
                var response2 = await httpClient.PostAsync(railwayApiUrl, mutationContent2);
                var responseContent2 = await response2.Content.ReadAsStringAsync();
                
                if (response2.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ [RAILWAY] Successfully set root directory to '{RootDir}' for service {ServiceId} using field 'sourceRoot'", 
                        rootDir, serviceId);
                    _logger.LogInformation("🚨 [RAILWAY] LANDING PAGE FIX APPLIED: Railway should now build from {RootDir}/ instead of repo root", rootDir);
                    success = true;
                }
                else
                {
                    _logger.LogWarning("⚠️ [RAILWAY] Attempt 2 failed with field 'sourceRoot': {StatusCode}", response2.StatusCode);
                    
                    // Try 3: source { rootDir }
                    _logger.LogInformation("🔍 [RAILWAY] Attempt 3/3: Trying to set rootDir via serviceUpdate with field 'source.rootDir'");
                    var mutation3 = new
                    {
                        query = mutationQuery,
                        variables = new
                        {
                            id = serviceId,
                            input = new
                            {
                                source = new
                                {
                                    rootDir = rootDir
                                }
                            }
                        }
                    };
                    var mutationBody3 = System.Text.Json.JsonSerializer.Serialize(mutation3);
                    var inputJson3 = System.Text.Json.JsonSerializer.Serialize(mutation3.variables.input);
                    _logger.LogInformation("🔍 [RAILWAY] Input structure: {Input}", inputJson3);
                    
                    var mutationContent3 = new StringContent(mutationBody3, System.Text.Encoding.UTF8, "application/json");
                    var response3 = await httpClient.PostAsync(railwayApiUrl, mutationContent3);
                    var responseContent3 = await response3.Content.ReadAsStringAsync();
                    
                    if (response3.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("✅ [RAILWAY] Successfully set root directory to '{RootDir}' for service {ServiceId} using field 'source.rootDir'", 
                            rootDir, serviceId);
                        _logger.LogInformation("🚨 [RAILWAY] LANDING PAGE FIX APPLIED: Railway should now build from {RootDir}/ instead of repo root", rootDir);
                        success = true;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [RAILWAY] Attempt 3 failed with field 'source.rootDir': {StatusCode}", response3.StatusCode);
                    }
                }
            }
            
            if (!success)
            {
                _logger.LogError("❌ [RAILWAY] ALL attempts to set rootDir via serviceUpdate FAILED");
                _logger.LogWarning("⚠️ [RAILWAY] Railway will rely on .railway/source.json file in repo (may take time to read)");
                _logger.LogWarning("🚨 [RAILWAY] LANDING PAGE ISSUE: If Railway doesn't read .railway/source.json, it will detect static site");
                _logger.LogWarning("🚨 [RAILWAY] Railway API does NOT support setting rootDir via serviceUpdate - must use .railway/source.json file");
                _logger.LogInformation("🔍 [RAILWAY] Check Railway dashboard manually: Service → Settings → Root Directory should be 'backend'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [RAILWAY] Error setting Railway service root directory: {Message}", ex.Message);
            _logger.LogWarning("⚠️ [RAILWAY] Railway will rely on .railway/source.json file in repo");
            _logger.LogWarning("🚨 [RAILWAY] LANDING PAGE RISK: Railway may not read .railway/source.json and will detect static site");
        }
    }
    
    /// <summary>
    /// OBSOLETE: Railway auto-deploys when connecting a GitHub repo, so manual trigger is not needed
    /// This method is kept for reference but should not be called
    /// </summary>
    [Obsolete("Railway auto-deploys when connecting GitHub repo - manual trigger not needed")]
    private async Task TriggerRailwayDeployment(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string serviceId, string environmentId)
    {
        try
        {
            _logger.LogInformation("🚀 [RAILWAY] Triggering Railway deployment for service {ServiceId} in environment {EnvironmentId}", serviceId, environmentId);
            
            // Railway GraphQL mutation to trigger deployment
            // Note: Railway may auto-deploy when connecting repo, but we'll try to trigger manually as well
            // Using deploymentCreate as per Railway API documentation
            var triggerDeploymentMutation = new
            {
                query = @"
                    mutation TriggerDeployment($input: DeploymentCreateInput!) {
                        deploymentCreate(input: $input) {
                            id
                            status
                            createdAt
                        }
                    }",
                variables = new
                {
                    input = new
                    {
                        serviceId = serviceId,
                        environmentId = environmentId
                    }
                }
            };
            
            var mutationBody = System.Text.Json.JsonSerializer.Serialize(triggerDeploymentMutation);
            _logger.LogDebug("🚀 [RAILWAY] Deployment trigger mutation: {Mutation}", mutationBody);
            
            var mutationContent = new StringContent(mutationBody, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(railwayApiUrl, mutationContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ [RAILWAY] Successfully triggered deployment for service {ServiceId}", serviceId);
                
                // Parse response to get deployment ID and status
                try
                {
                    var responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    if (responseDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                        dataObj.TryGetProperty("deploymentCreate", out var triggerObj))
                    {
                        if (triggerObj.TryGetProperty("id", out var idProp))
                        {
                            var deploymentId = idProp.GetString();
                            _logger.LogInformation("📦 [RAILWAY] Deployment triggered with ID: {DeploymentId}", deploymentId);
                        }
                        if (triggerObj.TryGetProperty("status", out var statusProp))
                        {
                            var status = statusProp.GetString();
                            _logger.LogInformation("📊 [RAILWAY] Deployment status: {Status}", status);
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogDebug(parseEx, "✅ [RAILWAY] Deployment triggered (parse warning only, not critical)");
                }
            }
            else
            {
                _logger.LogWarning("⚠️ [RAILWAY] Failed to trigger deployment: {StatusCode} - {Error}", 
                    response.StatusCode, responseContent);
                
                // Check for errors in GraphQL response
                try
                {
                    var errorDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                    if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                    {
                        foreach (var error in errorsProp.EnumerateArray())
                        {
                            var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                            _logger.LogError("❌ [RAILWAY] GraphQL error triggering deployment: {ErrorMessage}", errorMsg);
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [RAILWAY] Error triggering Railway deployment: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Queries Railway for service domains separately (since domains field not available on Service type)
    /// </summary>
    private async Task<string?> QueryServiceDomains(HttpClient httpClient, string railwayApiUrl, string serviceId)
    {
        try
        {
            // Try querying domains through various possible GraphQL structures
            var queries = new[]
            {
                // Try 1: Query domains by serviceId
                new
                {
                    query = @"
                        query GetServiceDomains($serviceId: String!) {
                            domains(serviceId: $serviceId) {
                                id
                                name
                            }
                        }",
                    variables = new { serviceId = serviceId }
                },
                // Try 2: Query service with domains connection pattern
                new
                {
                    query = @"
                        query GetServiceWithDomains($serviceId: String!) {
                            service(id: $serviceId) {
                                id
                                domains {
                                    edges {
                                        node {
                                            id
                                            name
                                        }
                                    }
                                }
                            }
                        }",
                    variables = new { serviceId = serviceId }
                }
            };

            foreach (var domainQuery in queries)
            {
                try
                {
                    var queryBody = System.Text.Json.JsonSerializer.Serialize(domainQuery);
                    var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
                    var queryResponse = await httpClient.PostAsync(railwayApiUrl, queryContent);
                    var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();

                    if (queryResponse.IsSuccessStatusCode)
                    {
                        var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);
                        
                        // Check for errors first
                        if (queryDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                        {
                            _logger.LogDebug("🔍 [RAILWAY] Domains query returned errors (expected if query structure is wrong): {Errors}", 
                                errorsProp.ToString());
                            continue; // Try next query structure
                        }
                        
                        // Try to extract domain from response
                        if (queryDoc.RootElement.TryGetProperty("data", out var dataObj))
                        {
                            // Pattern 1: domains array at root
                            if (dataObj.TryGetProperty("domains", out var domainsArray) && 
                                domainsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var domain in domainsArray.EnumerateArray())
                                {
                                    if (domain.TryGetProperty("name", out var nameProp))
                                    {
                                        var domainName = nameProp.GetString();
                                        if (!string.IsNullOrEmpty(domainName))
                                        {
                                            return domainName.StartsWith("http") ? domainName : $"https://{domainName}";
                                        }
                                    }
                                }
                            }
                            
                            // Pattern 2: service.domains.edges
                            if (dataObj.TryGetProperty("service", out var serviceObj) &&
                                serviceObj.TryGetProperty("domains", out var serviceDomains))
                            {
                                // Try edges pattern
                                if (serviceDomains.TryGetProperty("edges", out var edgesProp) &&
                                    edgesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var edge in edgesProp.EnumerateArray())
                                    {
                                        if (edge.TryGetProperty("node", out var nodeProp) &&
                                            nodeProp.TryGetProperty("name", out var nameProp))
                                        {
                                            var domainName = nameProp.GetString();
                                            if (!string.IsNullOrEmpty(domainName))
                                            {
                                                return domainName.StartsWith("http") ? domainName : $"https://{domainName}";
                                            }
                                        }
                                    }
                                }
                                // Try direct array
                                else if (serviceDomains.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var domain in serviceDomains.EnumerateArray())
                                    {
                                        if (domain.TryGetProperty("name", out var nameProp))
                                        {
                                            var domainName = nameProp.GetString();
                                            if (!string.IsNullOrEmpty(domainName))
                                            {
                                                return domainName.StartsWith("http") ? domainName : $"https://{domainName}";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "🔍 [RAILWAY] Error trying domains query pattern (this is expected if query structure is wrong)");
                    continue; // Try next query structure
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "🔍 [RAILWAY] Error querying service domains separately");
        }
        
        return null;
    }
    
    /// <summary>
    /// Queries Railway for the service deployment URL
    /// </summary>
    private async Task<string?> GetRailwayServiceUrl(HttpClient httpClient, string railwayApiUrl, string serviceId)
    {
        try
        {
            _logger.LogInformation("🔍 [RAILWAY] Querying service {ServiceId} for deployment URL", serviceId);
            _logger.LogDebug("🔍 [RAILWAY] Checking deployment status - looking for successful deployments with URLs");
            
            // Query Railway API for service info including deployments
            // Note: domains field is not available on Service type, will query separately if needed
            var queryDeployments = new
            {
                query = @"
                    query GetServiceDeployments($serviceId: String!) {
                        service(id: $serviceId) {
                            id
                            name
                            deployments(first: 5) {
                                edges {
                                    node {
                                        id
                                        status
                                        url
                                        createdAt
                                    }
                                }
                            }
                        }
                    }",
                variables = new
                {
                    serviceId = serviceId
                }
            };
            
            var queryBody = System.Text.Json.JsonSerializer.Serialize(queryDeployments);
            _logger.LogDebug("🔍 [RAILWAY] Query deployments: {Query}", queryBody);
            
            var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
            var queryResponse = await httpClient.PostAsync(railwayApiUrl, queryContent);
            var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
            
            if (queryResponse.IsSuccessStatusCode)
            {
                var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);
                if (queryDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                    dataObj.TryGetProperty("service", out var serviceObj))
                {
                    // Log service info and get service name
                    // Get service name and construct URL
                    string? serviceName = null;
                    string? constructedUrl = null;
                    if (serviceObj.TryGetProperty("name", out var serviceNameProp))
                    {
                        serviceName = serviceNameProp.GetString();
                        _logger.LogInformation("🔍 [RAILWAY] Service name: {Name}", serviceName);
                        
                        // Construct potential URL from service name pattern
                        // Railway typically uses: {service-name}.up.railway.app
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            // Sanitize service name for URL (lowercase, replace spaces/special chars with hyphens)
                            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(
                                serviceName.ToLowerInvariant(), 
                                @"[^a-z0-9]+", "-").Trim('-');
                            constructedUrl = $"https://{sanitizedName}.up.railway.app";
                        }
                    }
                    
                    // Try querying domains separately (domains field not available on Service type)
                    var domainsUrl = await QueryServiceDomains(httpClient, railwayApiUrl, serviceId);
                    if (!string.IsNullOrEmpty(domainsUrl))
                    {
                        _logger.LogInformation("✅ [RAILWAY] Found service URL from separate domains query: {ServiceUrl}", domainsUrl);
                        return domainsUrl;
                    }
                    
                    // Legacy check for domains field (in case API changes)
                    if (serviceObj.TryGetProperty("domains", out var domainsProp) && domainsProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        
                        // Try domains as an array
                        if (domainsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var domain in domainsProp.EnumerateArray())
                            {
                                if (domain.TryGetProperty("name", out var domainNameProp))
                                {
                                    var domainName = domainNameProp.GetString();
                                    if (!string.IsNullOrEmpty(domainName))
                                    {
                                        // Railway domains might already include protocol, or might just be the domain
                                        var serviceUrl = domainName.StartsWith("http") ? domainName : $"https://{domainName}";
                                        _logger.LogInformation("✅ [RAILWAY] Found service URL from domain: {ServiceUrl}", serviceUrl);
                                        return serviceUrl;
                                    }
                                }
                            }
                        }
                        // Try domains as an object with a name field
                        else if (domainsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (domainsProp.TryGetProperty("name", out var domainNameProp))
                            {
                                var domainName = domainNameProp.GetString();
                                if (!string.IsNullOrEmpty(domainName))
                                {
                                    var serviceUrl = domainName.StartsWith("http") ? domainName : $"https://{domainName}";
                                    _logger.LogInformation("✅ [RAILWAY] Found service URL from domain object: {ServiceUrl}", serviceUrl);
                                    return serviceUrl;
                                }
                            }
                        }
                        
                        _logger.LogDebug("🔍 [RAILWAY] Domains field exists but no valid domain name found");
                    }
                    else
                    {
                        _logger.LogDebug("🔍 [RAILWAY] Service domains not available or null");
                    }
                    
                    // Check deployments
                    bool hasSuccessfulDeployment = false;
                    if (serviceObj.TryGetProperty("deployments", out var deploymentsProp) &&
                        deploymentsProp.TryGetProperty("edges", out var edgesProp))
                    {
                        var edges = edgesProp.EnumerateArray().ToList();
                        _logger.LogInformation("📊 [RAILWAY] Found {Count} deployment(s) for service", edges.Count);
                        
                        if (edges.Count > 0)
                        {
                            // Try all deployments (not just first) to find one with a valid URL
                            foreach (var edge in edges)
                            {
                                if (edge.TryGetProperty("node", out var nodeProp))
                                {
                                    var deploymentStatus = nodeProp.TryGetProperty("status", out var statusProp) 
                                        ? statusProp.GetString() : "unknown";
                                    var deploymentId = nodeProp.TryGetProperty("id", out var idProp) 
                                        ? idProp.GetString() : "unknown";
                                    var createdAt = nodeProp.TryGetProperty("createdAt", out var createdAtProp) 
                                        ? createdAtProp.GetString() : "unknown";
                                    
                                    _logger.LogInformation("📦 [RAILWAY] Deployment: ID={Id}, Status={Status}, Created={Created}", 
                                        deploymentId, deploymentStatus, createdAt);
                                    
                                    // Check if deployment is successful
                                    if (deploymentStatus?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        hasSuccessfulDeployment = true;
                                    }
                                    
                                    // Get URL from deployment
                                    if (nodeProp.TryGetProperty("url", out var urlProp))
                                    {
                                        var deploymentUrl = urlProp.GetString();
                                        if (!string.IsNullOrEmpty(deploymentUrl) && !deploymentUrl.Contains("railway.app/project/"))
                                        {
                                            _logger.LogInformation("✅ [RAILWAY] Found service URL from deployment: {ServiceUrl}", deploymentUrl);
                                            return deploymentUrl;
                                        }
                                        else if (!string.IsNullOrEmpty(deploymentUrl))
                                        {
                                            _logger.LogDebug("🔍 [RAILWAY] Deployment URL is project URL (not service URL): {Url}", deploymentUrl);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogDebug("🔍 [RAILWAY] Deployment {Id} has no URL field (status: {Status})", deploymentId, deploymentStatus);
                                    }
                                }
                            }
                            
                            // If we have a successful deployment but no URL from deployments, use constructed URL
                            if (hasSuccessfulDeployment && !string.IsNullOrEmpty(constructedUrl))
                            {
                                _logger.LogInformation("✅ [RAILWAY] Deployment successful, using constructed service URL: {ServiceUrl}", constructedUrl);
                                return constructedUrl;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ [RAILWAY] No deployments found for service {ServiceId} yet. Railway may not have triggered deployment automatically.", serviceId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [RAILWAY] Could not access deployments in response");
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ [RAILWAY] Unexpected response structure when querying service: {Response}", 
                        queryResponseContent.Length > 500 ? queryResponseContent.Substring(0, 500) + "..." : queryResponseContent);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ [RAILWAY] Failed to query service deployments: {StatusCode} - {Error}", 
                    queryResponse.StatusCode, queryResponseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [RAILWAY] Error querying Railway service URL: {Message}", ex.Message);
        }
        
        return null;
    }
    
    /// <summary>
    /// Updates config.js in GitHub repository with the actual Railway service URL
    /// </summary>
    private async Task UpdateConfigJsWithServiceUrl(string repositoryUrl, string serviceUrl, string githubAccessToken)
    {
        try
        {
            _logger.LogInformation("📝 [GITHUB] Updating config.js with service URL: {ServiceUrl}", serviceUrl);
            
            // Parse GitHub repository URL to extract owner and repo name
            var uri = new Uri(repositoryUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            
            if (pathParts.Length < 2)
            {
                _logger.LogWarning("⚠️ [GITHUB] Invalid repository URL format: {RepositoryUrl}", repositoryUrl);
                return;
            }
            
            var repoOwner = pathParts[0];
            var repoName = pathParts[1].Replace(".git", "");
            
            _logger.LogInformation("📝 [GITHUB] Updating config.js in {Owner}/{Repo}", repoOwner, repoName);
            
            // Generate new config.js content with actual service URL
            var mentorApiBaseUrl = _configuration["ApiBaseUrl"];
            var newConfigJs = _gitHubService.GenerateConfigJs(serviceUrl, mentorApiBaseUrl);
            
            // Update the file in GitHub (config.js is now at root, not in frontend/ subdirectory)
            var success = await _gitHubService.UpdateFileAsync(
                repoOwner, 
                repoName, 
                "config.js", 
                newConfigJs, 
                "Update config.js with Railway service URL after deployment",
                githubAccessToken
            );
            
            if (success)
            {
                _logger.LogInformation("[FRONTEND] Updated config.js with service URL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FRONTEND] Error updating config.js: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Polls Railway for service deployment and updates config.js when ready
    /// Note: repositoryUrl parameter should be the frontend repository URL (since config.js is in the frontend repo)
    /// </summary>
    private async Task PollAndUpdateServiceUrl(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, 
        string serviceId, string frontendRepositoryUrl, string githubAccessToken, int maxAttempts = 10, int delaySeconds = 30)
    {
        try
        {
            _logger.LogInformation("[RAILWAY] Polling for deployment URL (max {MaxAttempts} attempts)", maxAttempts);
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                _logger.LogInformation("[RAILWAY] Polling attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                
                var serviceUrl = await GetRailwayServiceUrl(httpClient, railwayApiUrl, serviceId);
                
                if (!string.IsNullOrEmpty(serviceUrl) && !serviceUrl.Contains("railway.app/project/"))
                {
                    _logger.LogInformation("[RAILWAY] Service URL found: {ServiceUrl}", serviceUrl);
                    await UpdateConfigJsWithServiceUrl(frontendRepositoryUrl, serviceUrl, githubAccessToken);
                    return;
                }
                
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
            
            _logger.LogWarning("[RAILWAY] Service URL not found after {MaxAttempts} attempts", maxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RAILWAY] Error polling for service URL: {Message}", ex.Message);
        }
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
    /// <summary>
    /// Backend repository URL (deprecated - use FrontendRepositoryUrl and BackendRepositoryUrl instead)
    /// </summary>
    public string? RepositoryUrl { get; set; }
    /// <summary>
    /// Frontend repository URL
    /// </summary>
    public string? FrontendRepositoryUrl { get; set; }
    /// <summary>
    /// Backend repository URL
    /// </summary>
    public string? BackendRepositoryUrl { get; set; }
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