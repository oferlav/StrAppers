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

    public BoardsController(ApplicationDbContext context, ILogger<BoardsController> logger, ITrelloService trelloService, IAIService aiService, IGitHubService gitHubService, IMicrosoftGraphService graphService, ISmtpEmailService smtpEmailService, IConfiguration configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
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

            // Create Neon database for the project
            string? dbConnectionString = null;
            try
            {
                var dbName = $"AppDB_{trelloBoardId}";
                _logger.LogInformation("Creating Neon database: {DbName}", dbName);
                
                var neonApiKey = _configuration["Neon:ApiKey"];
                var neonBaseUrl = _configuration["Neon:BaseUrl"];
                var neonProjectId = _configuration["Neon:ProjectId"];
                var neonBranchId = _configuration["Neon:BranchId"];
                var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

                if (!string.IsNullOrWhiteSpace(neonApiKey) && neonApiKey != "your-neon-api-key-here" &&
                    !string.IsNullOrWhiteSpace(neonBaseUrl) &&
                    !string.IsNullOrWhiteSpace(neonProjectId) &&
                    !string.IsNullOrWhiteSpace(neonBranchId))
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    // Create database request
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

                    var apiUrl = $"{neonBaseUrl}/projects/{neonProjectId}/branches/{neonBranchId}/databases";
                    var response = await httpClient.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var jsonDoc = JsonDocument.Parse(responseContent);

                        // Get connection string
                        var connectionUrl = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(neonBranchId)}&pooled=false";
                        var connResponse = await httpClient.GetAsync(connectionUrl);
                        
                        if (connResponse.IsSuccessStatusCode)
                        {
                            var connContent = await connResponse.Content.ReadAsStringAsync();
                            var connDoc = JsonDocument.Parse(connContent);
                            if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                            {
                                var originalConnectionString = uriProp.GetString();
                                _logger.LogInformation("✅ [NEON] Successfully created Neon database '{DbName}' and retrieved initial connection string", dbName);
                                _logger.LogInformation("🔐 [NEON] Creating isolated database role for database '{DbName}' to prevent cross-database access", dbName);
                                
                                // Create an isolated role for this database and update connection string
                                dbConnectionString = await CreateIsolatedDatabaseRole(originalConnectionString, dbName);
                                
                                if (!string.IsNullOrEmpty(dbConnectionString))
                                {
                                    _logger.LogInformation("✅ [NEON] Successfully created isolated role and updated connection string for database '{DbName}'", dbName);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [NEON] Failed to create isolated role, using original connection string for database '{DbName}'", dbName);
                                    dbConnectionString = originalConnectionString;
                                }
                                
                                // Execute the initial database schema script to create TestProjects table
                                if (!string.IsNullOrEmpty(dbConnectionString))
                                {
                                    await ExecuteInitialDatabaseSchema(dbConnectionString, dbName);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [NEON] Connection string URI not found in Neon API response for database '{DbName}'", dbName);
                            }
                        }
                        else
                        {
                            var connErrorContent = await connResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("⚠️ [NEON] Failed to retrieve connection string for database '{DbName}': {StatusCode} - {Error}", dbName, connResponse.StatusCode, connErrorContent);
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("❌ [NEON] Failed to create Neon database '{DbName}': {StatusCode} - {Error}", dbName, response.StatusCode, errorContent);
                    }
                }
                else
                {
                    _logger.LogWarning("Neon configuration is incomplete, skipping database creation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating Neon database, continuing without it");
                // Don't fail the entire process if database creation fails
            }

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
            
            string? backendRepositoryUrl = null;
            string? frontendRepositoryUrl = null;
            string? publishUrl = null;
            List<string> addedCollaborators = new List<string>();
            List<string> failedCollaborators = new List<string>();
            string? webApiUrl = null;
            string? swaggerUrl = null;
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

                    // Sanitize name for Railway
                    var sanitizedName = System.Text.RegularExpressions.Regex.Replace(railwayHostName.ToLowerInvariant(), @"[^a-z0-9-_]", "-");

                    // Create Railway project
                    var createProjectMutation = new
                    {
                        query = @"
                    mutation CreateProject($name: String!) {
                        projectCreate(input: { name: $name }) {
                            id
                            name
                        }
                    }",
                        variables = new
                        {
                            name = sanitizedName
                        }
                    };

                    var requestBody = System.Text.Json.JsonSerializer.Serialize(createProjectMutation);
                    var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(railwayApiUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                        var root = jsonDoc.RootElement;

                        // projectId already declared at higher scope
                        if (root.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (dataObj.TryGetProperty("projectCreate", out var projectObj) && projectObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                if (projectObj.TryGetProperty("id", out var idProp) && idProp.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    projectId = idProp.GetString();
                                }
                            }
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
                            var serviceResponse = await httpClient.PostAsync(railwayApiUrl, serviceContent);
                            var serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();

                            if (serviceResponse.IsSuccessStatusCode)
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
                                _logger.LogWarning("⚠️ [RAILWAY] Failed to create service: StatusCode={StatusCode}, Response={Response}", 
                                    serviceResponse.StatusCode, serviceResponseContent);
                                
                                // Check for errors in GraphQL response
                                try
                                {
                                    var errorDoc = System.Text.Json.JsonDocument.Parse(serviceResponseContent);
                                    if (errorDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                                    {
                                        foreach (var error in errorsProp.EnumerateArray())
                                        {
                                            var errorMessage = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                            _logger.LogError("❌ [RAILWAY] GraphQL error: {ErrorMessage}", errorMessage);
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning(parseEx, "⚠️ [RAILWAY] Could not parse error response");
                                }
                            }

                            // Don't set a fallback project URL - we need the actual service URL
                            // The webApiUrl will be set when the domain is created, or polled later
                            if (string.IsNullOrEmpty(webApiUrl))
                            {
                                _logger.LogInformation("⚠️ [RAILWAY] Service URL not available yet - will be set when domain is created");
                            }
                            
                            // Set environment variable DATABASE_URL on the service (if service was created)
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
                            _logger.LogWarning("Railway project created but ProjectId not found in response");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create Railway host: {StatusCode} - {Error}", response.StatusCode, responseContent);
                    }
                }
                else
                {
                    _logger.LogWarning("Railway configuration is incomplete, skipping Railway host creation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating Railway host, continuing without it");
                // Don't fail the entire process if Railway host creation fails
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
                                        // Frontend repo name is boardId directly (no prefix)
                                        publishUrl = _gitHubService.GetGitHubPagesUrl(frontendOwner, frontendRepoNameFromUrl);
                                        _logger.LogWarning("⚠️ [FRONTEND] Deployment may not have triggered: {Message}", deployResponse.Message);
                                    }
                                }
                                catch (Exception deployEx)
                                {
                                    _logger.LogWarning(deployEx, "⚠️ [FRONTEND] Error calling deployment controller, using fallback");
                                    // Frontend repo name is boardId directly (no prefix)
                                    publishUrl = _gitHubService.GetGitHubPagesUrl(frontendOwner, frontendRepoNameFromUrl);
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
                        _logger.LogWarning("⚠️ [FRONTEND] Failed to create frontend repository: {Error}", frontendResponse.ErrorMessage);
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
                                        _logger.LogWarning("[GITHUB] ⚠️ Failed to add RAILWAY_TOKEN secret");
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
                                        _logger.LogWarning("[GITHUB] ⚠️ Failed to add RAILWAY_SERVICE_ID secret");
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
                                                projectId, railwayServiceId, environmentId, programmingLanguage);
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
                                                _logger.LogWarning("⚠️ [RAILWAY] Domain creation may have failed - will poll for service URL");
                                                // Start polling for service URL in background
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
                                                _logger.LogWarning("⚠️ [BACKEND] Deployment may not have triggered: {Message}", deployResponse.Message);
                                            }
                                        }
                                        catch (Exception deployEx)
                                        {
                                            _logger.LogWarning(deployEx, "⚠️ [BACKEND] Error calling deployment controller");
                                        }
                                    }
                                }
                                catch (Exception railwayEx)
                                {
                                    _logger.LogWarning(railwayEx, "[GITHUB] ⚠️ Error setting up Railway integration, continuing anyway");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [BACKEND] Failed to create backend commit");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [BACKEND] Failed to create backend repository: {Error}", backendResponse.ErrorMessage);
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
                GithubBackendUrl = backendRepositoryUrl,
                GithubFrontendUrl = frontendRepositoryUrl,
                WebApiUrl = swaggerUrl ?? webApiUrl, // Swagger URL from Railway deployment (fallback to base URL if swagger URL not set)
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
                GithubBackendUrl = projectBoard.GithubBackendUrl,
                GithubFrontendUrl = projectBoard.GithubFrontendUrl,
                WebApiUrl = projectBoard.WebApiUrl,
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
    /// Creates an isolated database role for a specific database to prevent cross-database access
    /// </summary>
    private async Task<string?> CreateIsolatedDatabaseRole(string connectionString, string dbName)
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
            
            // Revoke CONNECT from PUBLIC on this database
            using var revokePublicCmd = new NpgsqlCommand($@"REVOKE CONNECT ON DATABASE ""{dbName}"" FROM PUBLIC;", postgresConn);
            await revokePublicCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("🔒 [NEON] Revoked CONNECT privilege from PUBLIC on database '{DbName}'", dbName);
            
            // Grant CONNECT to the new role
            using var grantConnectCmd = new NpgsqlCommand($@"GRANT CONNECT ON DATABASE ""{dbName}"" TO {roleName};", postgresConn);
            await grantConnectCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("✅ [NEON] Granted CONNECT privilege to role {RoleName} on database '{DbName}'", roleName, dbName);
            
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
            
            _logger.LogInformation("✅ [NEON] Successfully created isolated role and connection string for database '{DbName}'", dbName);
            return isolatedConnectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [NEON] Error creating isolated database role for database '{DbName}': {Message}", dbName, ex.Message);
            return null;
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
    private async Task ExecuteInitialDatabaseSchema(string connectionString, string dbName)
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
            var sqlScript = @"
-- TestProjects table for initial project setup
-- This table is used for testing and learning database interactions

CREATE TABLE IF NOT EXISTS ""TestProjects"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(255) NOT NULL
);

-- Insert mock data (only if table is empty)
INSERT INTO ""TestProjects"" (""Name"") 
SELECT * FROM (VALUES
    ('Sample Project 1'),
    ('Sample Project 2'),
    ('Sample Project 3'),
    ('Learning Project'),
    ('Test Project')
) AS v(""Name"")
WHERE NOT EXISTS (SELECT 1 FROM ""TestProjects"");
";
            
            using var conn = new NpgsqlConnection(builder.ConnectionString);
            // Wait for database to become available (Neon databases may take a moment to propagate)
            await WaitForDatabaseAvailableAsync(conn, dbName, maxRetries: 10, delayMs: 1000);
            _logger.LogDebug("📊 [NEON] Connected to database '{DbName}' to execute schema script", dbName);
            
            using var cmd = new NpgsqlCommand(sqlScript, conn);
            await cmd.ExecuteNonQueryAsync();
            
            _logger.LogInformation("✅ [NEON] Successfully executed initial database schema for database '{DbName}'. TestProjects table created with mock data.", dbName);
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
    private async Task SetRailwayBuildSettings(HttpClient httpClient, string railwayApiUrl, string railwayApiToken, string projectId, string serviceId, string environmentId, string programmingLanguage)
    {
        try
        {
            _logger.LogInformation("[RAILWAY] Setting build environment variables (language: {Language})", programmingLanguage);
            
            // Build commands vary by programming language
            // NOTE: Backend files are at root level (not in backend/ subdirectory)
            var buildVariables = programmingLanguage?.ToLowerInvariant() switch
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
            var newConfigJs = _gitHubService.GenerateConfigJs(serviceUrl);
            
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