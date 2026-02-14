using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UtilitiesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubService _gitHubService;
    private readonly ITrelloService _trelloService;
    private readonly IAIService _aiService;
    private readonly IOptions<TestingConfig> _testingConfig;

    public UtilitiesController(
        ApplicationDbContext context, 
        ILogger<UtilitiesController> logger, 
        IPasswordHasherService passwordHasher, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IGitHubService gitHubService,
        ITrelloService trelloService,
        IAIService aiService,
        IOptions<TestingConfig> testingConfig)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _gitHubService = gitHubService;
        _trelloService = trelloService;
        _aiService = aiService;
        _testingConfig = testingConfig;
    }

    /// <summary>
    /// Utility endpoint to set the same password hash for ALL Students and Organizations.
    /// This is a one-time utility for testing purposes.
    /// </summary>
    [HttpPost("set-password-for-all")]
    public async Task<ActionResult<object>> SetPasswordForAll(SetPasswordForAllRequest request)
    {
        try
        {
            _logger.LogInformation("Setting password for all Students and Organizations");

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Password is required but was not provided");
                return BadRequest(new { Success = false, Message = "Password is required" });
            }

            // Hash the password once
            string passwordHash = _passwordHasher.HashPassword(request.Password);
            _logger.LogInformation("Password hashed successfully");

            // Update all Students
            var students = await _context.Students.ToListAsync();
            int studentsUpdated = 0;
            foreach (var student in students)
            {
                student.PasswordHash = passwordHash;
                student.UpdatedAt = DateTime.UtcNow;
                studentsUpdated++;
            }

            // Update all Organizations
            var organizations = await _context.Organizations.ToListAsync();
            int organizationsUpdated = 0;
            foreach (var organization in organizations)
            {
                organization.PasswordHash = passwordHash;
                organization.UpdatedAt = DateTime.UtcNow;
                organizationsUpdated++;
            }

            // Save all changes
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password set successfully for {StudentsCount} students and {OrganizationsCount} organizations", 
                studentsUpdated, organizationsUpdated);

            return Ok(new
            {
                Success = true,
                Message = $"Password set successfully for all records",
                StudentsUpdated = studentsUpdated,
                OrganizationsUpdated = organizationsUpdated,
                TotalUpdated = studentsUpdated + organizationsUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting password for all Students and Organizations");
            return StatusCode(500, new 
            { 
                Success = false, 
                Message = $"An error occurred while setting passwords: {ex.Message}" 
            });
        }
    }

    /// <summary>
    /// Utility endpoint to fix orphaned projects and reassign students to valid projects.
    /// This is a destructive operation that will:
    /// - Change student ProjectId assignments
    /// - Update ProjectBoards associations
    /// - Delete orphaned projects and project modules
    /// 
    /// WARNING: This operation cannot be undone. Ensure you have a database backup before running.
    /// </summary>
    [HttpPost("change-projects")]
    public async Task<ActionResult<object>> ChangeProjects()
    {
        // Valid OrganizationId -> ProjectId mappings
        var validProjectMappings = new Dictionary<int, int>
        {
            { 12, 19 },
            { 3, 3 },
            { 14, 63 },
            { 23, 67 },
            { 25, 81 },
            { 29, 79 },
            { 31, 80 }
        };

        var validProjectIds = validProjectMappings.Values.ToHashSet();
        var validOrganizationIds = validProjectMappings.Keys.ToHashSet();

        IDbContextTransaction? transaction = null;
        try
        {
            _logger.LogWarning("Starting change-projects utility - DESTRUCTIVE OPERATION");
            
            // Start transaction
            transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation("Transaction started");

            // Get all valid organization IDs from database
            var existingOrgIds = await _context.Organizations
                .Where(o => validOrganizationIds.Contains(o.Id))
                .Select(o => o.Id)
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} valid organizations in database", existingOrgIds.Count);

            // Get all valid project IDs from database
            var existingProjectIds = await _context.Projects
                .Where(p => validProjectIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} valid projects in database", existingProjectIds.Count);

            // Get available project IDs from the valid list (that exist in DB)
            var availableProjectIds = validProjectIds.Where(pid => existingProjectIds.Contains(pid)).ToList();
            
            if (availableProjectIds.Count == 0)
            {
                _logger.LogError("No valid projects found in database. Cannot proceed.");
                await transaction.RollbackAsync();
                return BadRequest(new 
                { 
                    Success = false, 
                    Message = "No valid projects found in database. Cannot proceed." 
                });
            }

            _logger.LogInformation("Available project IDs for assignment: {ProjectIds}", string.Join(", ", availableProjectIds));

            var random = new Random();
            var stats = new ChangeProjectsStats();

            // Step 1: Identify students with ProjectId pointing to projects with invalid OrganizationId
            var allStudents = await _context.Students
                .Include(s => s.Project)
                .ToListAsync();

            // Helper function to check if a project is invalid
            bool IsProjectInvalid(Project? project)
            {
                if (project == null) return true;
                if (!project.OrganizationId.HasValue) return true;
                return !validOrganizationIds.Contains(project.OrganizationId.Value);
            }

            var studentsWithInvalidProjects = allStudents
                .Where(s => s.ProjectId.HasValue && IsProjectInvalid(s.Project))
                .ToList();

            _logger.LogInformation("Found {Count} students with invalid project assignments", studentsWithInvalidProjects.Count);

            // Step 2: Identify boards that have at least one student with invalid ProjectId
            var boardsWithInvalidStudents = studentsWithInvalidProjects
                .Where(s => !string.IsNullOrWhiteSpace(s.BoardId))
                .Select(s => s.BoardId!)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} unique boards with at least one invalid student", boardsWithInvalidStudents.Count);

            // Step 2a: Process students WITH BoardId - assign same ProjectId to ALL students in same board
            // This ensures data integrity: all students in same board have same ProjectId
            foreach (var boardId in boardsWithInvalidStudents)
            {
                // Get ALL students in this board (not just those with invalid projects)
                var allStudentsInBoard = allStudents
                    .Where(s => s.BoardId == boardId)
                    .ToList();
                
                // Randomly select a ProjectId for this board
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                
                _logger.LogInformation("Assigning ProjectId {ProjectId} to board {BoardId} ({StudentCount} total students in board)", 
                    selectedProjectId, boardId, allStudentsInBoard.Count);

                // Update ALL students in this board to have the same ProjectId
                foreach (var student in allStudentsInBoard)
                {
                    var wasUpdated = student.ProjectId != selectedProjectId;
                    student.ProjectId = selectedProjectId;
                    student.UpdatedAt = DateTime.UtcNow;
                    
                    if (wasUpdated)
                    {
                        stats.StudentsUpdatedWithBoard++;
                    }
                }

                // Update ProjectBoards table to match
                var projectBoard = await _context.ProjectBoards.FindAsync(boardId);
                if (projectBoard != null)
                {
                    projectBoard.ProjectId = selectedProjectId;
                    projectBoard.UpdatedAt = DateTime.UtcNow;
                    stats.ProjectBoardsUpdated++;
                    _logger.LogInformation("Updated ProjectBoard {BoardId} to ProjectId {ProjectId}", boardId, selectedProjectId);
                }
                else
                {
                    _logger.LogWarning("ProjectBoard {BoardId} not found in database", boardId);
                }
            }

            // Step 2b: Process students WITHOUT BoardId but WITH invalid ProjectId
            // Exclude students that were already processed in Step 2a (those with BoardId)
            var studentsWithoutBoard = studentsWithInvalidProjects
                .Where(s => string.IsNullOrWhiteSpace(s.BoardId))
                .ToList();

            _logger.LogInformation("Found {Count} students without BoardId but with invalid ProjectId", studentsWithoutBoard.Count);

            foreach (var student in studentsWithoutBoard)
            {
                // Randomly select a ProjectId
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                
                student.ProjectId = selectedProjectId;
                student.UpdatedAt = DateTime.UtcNow;
                stats.StudentsUpdatedWithoutBoard++;
                
                _logger.LogInformation("Assigned ProjectId {ProjectId} to student {StudentId} (no board)", 
                    selectedProjectId, student.Id);
            }

            // Step 3: Identify all orphaned projects (projects with OrganizationId not in valid list OR null)
            var orphanedProjects = await _context.Projects
                .Where(p => p.OrganizationId == null || !validOrganizationIds.Contains(p.OrganizationId.Value))
                .Select(p => p.Id)
                .ToListAsync();

            _logger.LogInformation("Found {Count} orphaned projects to delete", orphanedProjects.Count);
            stats.OrphanedProjectsFound = orphanedProjects.Count;

            // Step 4: Handle JoinRequests that reference orphaned projects
            // JoinRequests.ProjectId is NOT NULL, so we need to delete them before deleting projects
            var orphanedJoinRequests = await _context.JoinRequests
                .Where(jr => orphanedProjects.Contains(jr.ProjectId))
                .ToListAsync();

            if (orphanedJoinRequests.Any())
            {
                _context.JoinRequests.RemoveRange(orphanedJoinRequests);
                stats.JoinRequestsDeleted = orphanedJoinRequests.Count;
                _logger.LogInformation("Deleted {Count} join requests referencing orphaned projects", orphanedJoinRequests.Count);
            }

            // Step 5: Delete orphaned project modules first (due to foreign key constraints)
            var orphanedModules = await _context.ProjectModules
                .Where(pm => pm.ProjectId.HasValue && orphanedProjects.Contains(pm.ProjectId.Value))
                .ToListAsync();

            if (orphanedModules.Any())
            {
                _context.ProjectModules.RemoveRange(orphanedModules);
                stats.ProjectModulesDeleted = orphanedModules.Count;
                _logger.LogInformation("Deleted {Count} orphaned project modules", orphanedModules.Count);
            }

            // Step 6: Delete orphaned projects (cascade will handle other related data)
            var projectsToDelete = await _context.Projects
                .Where(p => orphanedProjects.Contains(p.Id))
                .ToListAsync();

            if (projectsToDelete.Any())
            {
                _context.Projects.RemoveRange(projectsToDelete);
                stats.ProjectsDeleted = projectsToDelete.Count;
                _logger.LogInformation("Deleted {Count} orphaned projects", projectsToDelete.Count);
            }

            // Step 7: Additional cleanup - Find any remaining orphaned ProjectIds in Students, ProjectBoards, ProjectModules
            // that don't exist in Projects table anymore
            var allExistingProjectIds = await _context.Projects.Select(p => p.Id).ToListAsync();
            
            // Fix students with non-existent ProjectIds
            var studentsWithNonExistentProjects = await _context.Students
                .Where(s => s.ProjectId.HasValue && !allExistingProjectIds.Contains(s.ProjectId.Value))
                .ToListAsync();

            foreach (var student in studentsWithNonExistentProjects)
            {
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                student.ProjectId = selectedProjectId;
                student.UpdatedAt = DateTime.UtcNow;
                stats.StudentsFixedNonExistent++;
                _logger.LogInformation("Fixed student {StudentId} with non-existent ProjectId, assigned {ProjectId}", 
                    student.Id, selectedProjectId);
            }

            // Fix ProjectBoards with non-existent ProjectIds
            var boardsWithNonExistentProjects = await _context.ProjectBoards
                .Where(pb => !allExistingProjectIds.Contains(pb.ProjectId))
                .ToListAsync();

            foreach (var board in boardsWithNonExistentProjects)
            {
                var selectedProjectId = availableProjectIds[random.Next(availableProjectIds.Count)];
                board.ProjectId = selectedProjectId;
                board.UpdatedAt = DateTime.UtcNow;
                stats.ProjectBoardsFixedNonExistent++;
                _logger.LogInformation("Fixed ProjectBoard {BoardId} with non-existent ProjectId, assigned {ProjectId}", 
                    board.Id, selectedProjectId);
            }

            // Fix ProjectModules with non-existent ProjectIds
            var modulesWithNonExistentProjects = await _context.ProjectModules
                .Where(pm => pm.ProjectId.HasValue && !allExistingProjectIds.Contains(pm.ProjectId.Value))
                .ToListAsync();

            if (modulesWithNonExistentProjects.Any())
            {
                _context.ProjectModules.RemoveRange(modulesWithNonExistentProjects);
                stats.ProjectModulesDeletedNonExistent = modulesWithNonExistentProjects.Count;
                _logger.LogInformation("Deleted {Count} project modules with non-existent ProjectIds", 
                    modulesWithNonExistentProjects.Count);
            }

            // Save all changes
            await _context.SaveChangesAsync();
            _logger.LogInformation("All changes saved successfully");

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogWarning("Transaction committed - change-projects operation completed successfully");

            return Ok(new
            {
                Success = true,
                Message = "Projects changed successfully",
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change-projects operation");
            
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("Transaction rolled back due to error");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during transaction rollback");
                }
            }

            return StatusCode(500, new 
            { 
                Success = false, 
                Message = $"An error occurred while changing projects: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Get OpenAI account details (email, organization, etc.) using the configured API key
    /// </summary>
    [HttpGet("openai-account-details")]
    public async Task<ActionResult<object>> GetOpenAIAccountDetails()
    {
        try
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured");
                return BadRequest(new { Success = false, Message = "OpenAI API key not configured" });
            }

            var baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
            _logger.LogInformation("Querying OpenAI account details from {BaseUrl}/me", baseUrl);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync($"{baseUrl}/me");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                return StatusCode((int)response.StatusCode, new
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}",
                    Error = errorContent
                });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var accountInfo = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Extract relevant fields
            var result = new Dictionary<string, object?>();
            if (accountInfo.TryGetProperty("id", out var idProp))
                result["id"] = idProp.GetString();
            if (accountInfo.TryGetProperty("email", out var emailProp))
                result["email"] = emailProp.GetString();
            if (accountInfo.TryGetProperty("name", out var nameProp))
                result["name"] = nameProp.GetString();
            if (accountInfo.TryGetProperty("object", out var objectProp))
                result["object"] = objectProp.GetString();
            if (accountInfo.TryGetProperty("picture", out var pictureProp))
                result["picture"] = pictureProp.GetString();
            if (accountInfo.TryGetProperty("created", out var createdProp))
                result["created"] = createdProp.GetInt64();
            if (accountInfo.TryGetProperty("organization_id", out var orgIdProp))
                result["organization_id"] = orgIdProp.GetString();
            if (accountInfo.TryGetProperty("organization", out var orgProp))
            {
                var orgDict = new Dictionary<string, object?>();
                if (orgProp.TryGetProperty("id", out var orgId))
                    orgDict["id"] = orgId.GetString();
                if (orgProp.TryGetProperty("name", out var orgName))
                    orgDict["name"] = orgName.GetString();
                if (orgProp.TryGetProperty("slug", out var orgSlug))
                    orgDict["slug"] = orgSlug.GetString();
                if (orgDict.Any())
                    result["organization"] = orgDict;
            }

            _logger.LogInformation("Successfully retrieved OpenAI account details for email: {Email}", result.ContainsKey("email") ? result["email"] : "unknown");

            return Ok(new
            {
                Success = true,
                Message = "OpenAI account details retrieved successfully",
                AccountDetails = result,
                RawResponse = responseContent // Include raw response for debugging
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OpenAI account details");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred while retrieving OpenAI account details: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Test endpoint to deploy backend to Railway
    /// Simulates the backend deployment process during board creation
    /// Returns the backend API URL
    /// </summary>
    [HttpPost("deploy-backend")]
    public async Task<ActionResult<object>> DeployBackend([FromBody] DeployBackendRequest? request = null)
    {
        try
        {
            var programmingLanguage = request?.ProgrammingLanguage ?? "C#";
            var testGuid = Guid.NewGuid().ToString("N")[..16]; // Short GUID for unique naming
            var repoName = $"test-{testGuid}";
            
            _logger.LogInformation("[UTILITY] Starting backend deployment test with repo name: {RepoName}, language: {Language}", repoName, programmingLanguage);

            var railwayApiToken = _configuration["Railway:ApiToken"];
            var railwayApiUrl = _configuration["Railway:ApiUrl"];
            var githubToken = _configuration["GitHub:AccessToken"];

            if (string.IsNullOrWhiteSpace(railwayApiToken) || railwayApiToken == "your-railway-api-token-here" || string.IsNullOrWhiteSpace(railwayApiUrl))
            {
                return BadRequest(new { Success = false, Message = "Railway API configuration is missing" });
            }

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                return BadRequest(new { Success = false, Message = "GitHub access token is missing" });
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", railwayApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");

            // Step 1: Create Railway project
            var railwayHostName = $"WebApi_{repoName}";
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(railwayHostName.ToLowerInvariant(), @"[^a-z0-9-_]", "-");
            
            _logger.LogInformation("[UTILITY] Creating Railway project: {ProjectName}", sanitizedName);
            
            var createProjectMutation = new
            {
                query = @"
                    mutation CreateProject($name: String!) {
                        projectCreate(input: { name: $name }) {
                            id
                            name
                        }
                    }",
                variables = new { name = sanitizedName }
            };

            var projectRequestBody = JsonSerializer.Serialize(createProjectMutation);
            var projectContent = new StringContent(projectRequestBody, System.Text.Encoding.UTF8, "application/json");
            var projectResponse = await httpClient.PostAsync(railwayApiUrl, projectContent);
            var projectResponseContent = await projectResponse.Content.ReadAsStringAsync();

            if (!projectResponse.IsSuccessStatusCode)
            {
                _logger.LogError("[UTILITY] Failed to create Railway project: {StatusCode} - {Error}", projectResponse.StatusCode, projectResponseContent);
                return StatusCode(500, new { Success = false, Message = "Failed to create Railway project", Error = projectResponseContent });
            }

            var projectDoc = JsonDocument.Parse(projectResponseContent);
            string? projectId = null;
            if (projectDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                dataObj.TryGetProperty("projectCreate", out var projectObj) &&
                projectObj.TryGetProperty("id", out var idProp))
            {
                projectId = idProp.GetString();
            }

            if (string.IsNullOrEmpty(projectId))
            {
                return StatusCode(500, new { Success = false, Message = "Failed to get Railway project ID" });
            }

            _logger.LogInformation("[UTILITY] Railway project created: {ProjectId}", projectId);

            // Step 2: Get environment ID
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
                variables = new { projectId }
            };

            var envQueryBody = JsonSerializer.Serialize(getProjectQuery);
            var envQueryContent = new StringContent(envQueryBody, System.Text.Encoding.UTF8, "application/json");
            var envQueryResponse = await httpClient.PostAsync(railwayApiUrl, envQueryContent);
            var envQueryResponseContent = await envQueryResponse.Content.ReadAsStringAsync();

            string? environmentId = null;
            if (envQueryResponse.IsSuccessStatusCode)
            {
                var envDoc = JsonDocument.Parse(envQueryResponseContent);
                if (envDoc.RootElement.TryGetProperty("data", out var envDataObj) &&
                    envDataObj.TryGetProperty("project", out var envProjectObj) &&
                    envProjectObj.TryGetProperty("environments", out var envEnvironmentsProp) &&
                    envEnvironmentsProp.TryGetProperty("edges", out var envEdgesProp))
                {
                    var edges = envEdgesProp.EnumerateArray().ToList();
                    if (edges.Count > 0 && edges[0].TryGetProperty("node", out var nodeProp) && nodeProp.TryGetProperty("id", out var envIdProp))
                    {
                        environmentId = envIdProp.GetString();
                    }
                }
            }

            if (string.IsNullOrEmpty(environmentId))
            {
                return StatusCode(500, new { Success = false, Message = "Failed to get Railway environment ID" });
            }

            // Step 3: Create Railway service
            _logger.LogInformation("[UTILITY] Creating Railway service in project {ProjectId}", projectId);
            
            var createServiceMutation = new
            {
                query = @"
                    mutation CreateService($projectId: String!, $name: String) {
                        serviceCreate(input: { projectId: $projectId, name: $name }) {
                            id
                            name
                        }
                    }",
                variables = new { projectId, name = sanitizedName }
            };

            var serviceRequestBody = JsonSerializer.Serialize(createServiceMutation);
            var serviceContent = new StringContent(serviceRequestBody, System.Text.Encoding.UTF8, "application/json");
            var serviceResponse = await httpClient.PostAsync(railwayApiUrl, serviceContent);
            var serviceResponseContent = await serviceResponse.Content.ReadAsStringAsync();

            if (!serviceResponse.IsSuccessStatusCode)
            {
                _logger.LogError("[UTILITY] Failed to create Railway service: {StatusCode} - {Error}", serviceResponse.StatusCode, serviceResponseContent);
                return StatusCode(500, new { Success = false, Message = "Failed to create Railway service", Error = serviceResponseContent });
            }

            var serviceDoc = JsonDocument.Parse(serviceResponseContent);
            string? railwayServiceId = null;
            if (serviceDoc.RootElement.TryGetProperty("data", out var serviceDataObj) &&
                serviceDataObj.TryGetProperty("serviceCreate", out var serviceObj) &&
                serviceObj.TryGetProperty("id", out var serviceIdProp))
            {
                railwayServiceId = serviceIdProp.GetString();
            }

            if (string.IsNullOrEmpty(railwayServiceId))
            {
                return StatusCode(500, new { Success = false, Message = "Failed to get Railway service ID" });
            }

            _logger.LogInformation("[UTILITY] Railway service created: {ServiceId}", railwayServiceId);

            // Step 4: Create GitHub repository with backend files
            _logger.LogInformation("[UTILITY] Creating GitHub repository: {RepoName}", repoName);
            
            var githubRequest = new CreateRepositoryRequest
            {
                Name = repoName,
                Description = $"Test backend deployment repository",
                IsPrivate = false,
                Collaborators = new List<string>(), // No collaborators for test
                ProjectTitle = "Test Backend Deployment",
                ProgrammingLanguage = programmingLanguage
            };

            var githubResponse = await _gitHubService.CreateRepositoryAsync(githubRequest);

            if (!githubResponse.Success || string.IsNullOrEmpty(githubResponse.RepositoryUrl))
            {
                return StatusCode(500, new { Success = false, Message = "Failed to create GitHub repository", Error = githubResponse.ErrorMessage });
            }

            var repositoryUrl = githubResponse.RepositoryUrl;
            _logger.LogInformation("[UTILITY] GitHub repository created: {RepositoryUrl}", repositoryUrl);

            // Step 5: Add Railway secrets to GitHub
            var uri = new Uri(repositoryUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length >= 2)
            {
                var owner = pathParts[0];
                var repoNameFromUrl = pathParts[1];
                
                await _gitHubService.CreateOrUpdateRepositorySecretAsync(owner, repoNameFromUrl, "RAILWAY_TOKEN", railwayApiToken, githubToken);
                await _gitHubService.CreateOrUpdateRepositorySecretAsync(owner, repoNameFromUrl, "RAILWAY_SERVICE_ID", railwayServiceId, githubToken);
                _logger.LogInformation("[UTILITY] Added Railway secrets to GitHub repository");
            }

            // Step 6: Connect GitHub repository to Railway
            // This allows Railway to know about the service, but we'll use GitHub Actions for actual deployment
            // Railway's auto-build may fail (since we don't have nixpacks.toml), but that's okay - GitHub Actions will deploy
            _logger.LogInformation("[UTILITY] Connecting GitHub repository to Railway service");
            
            var repoOwner = pathParts[0];
            var repoNameForRailway = pathParts[1].Replace(".git", "");
            var repoFullName = $"{repoOwner}/{repoNameForRailway}";

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
                variables = new { id = railwayServiceId, repo = repoFullName, branch = "main" }
            };

            var connectBody = JsonSerializer.Serialize(connectRepoMutation);
            var connectContent = new StringContent(connectBody, System.Text.Encoding.UTF8, "application/json");
            var connectResponse = await httpClient.PostAsync(railwayApiUrl, connectContent);
            var connectResponseContent = await connectResponse.Content.ReadAsStringAsync();

            if (connectResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("[UTILITY] ✅ Connected GitHub repository to Railway");
                _logger.LogInformation("[UTILITY] Note: Railway may attempt auto-build, but GitHub Actions will handle actual deployment");
            }
            else
            {
                _logger.LogWarning("[UTILITY] ⚠️ Failed to connect repository to Railway: {StatusCode} - {Error}", connectResponse.StatusCode, connectResponseContent);
                _logger.LogInformation("[UTILITY] GitHub Actions workflow will still deploy via Railway CLI");
            }

            // Step 6b: Trigger GitHub Actions workflow to deploy to Railway
            // This ensures the workflow runs immediately after repo creation
            _logger.LogInformation("[UTILITY] Triggering GitHub Actions workflow to deploy backend to Railway");
            
            // Wait a moment for the workflow file to be fully indexed by GitHub
            await Task.Delay(2000);
            
            // Trigger the backend deployment workflow
            var workflowTriggered = await _gitHubService.TriggerWorkflowDispatchAsync(
                repoOwner, 
                repoNameForRailway, 
                "deploy-backend.yml", 
                githubToken);
            
            if (workflowTriggered)
            {
                _logger.LogInformation("[UTILITY] ✅ Successfully triggered GitHub Actions workflow for backend deployment");
                _logger.LogInformation("[UTILITY] Check workflow status: https://github.com/{Owner}/{Repo}/actions", repoOwner, repoNameForRailway);
            }
            else
            {
                _logger.LogWarning("[UTILITY] ⚠️ Failed to trigger workflow via API, but it should run automatically on push");
                _logger.LogInformation("[UTILITY] The workflow will run automatically when backend files are pushed (on: push trigger)");
                _logger.LogInformation("[UTILITY] Check workflow status: https://github.com/{Owner}/{Repo}/actions", repoOwner, repoNameForRailway);
            }

            // Step 7: Set Railway build settings
            var targetPort = GetDefaultPortForLanguage(programmingLanguage);
            // Language-specific build variables
            var languageSpecificVariables = programmingLanguage?.ToLowerInvariant() switch
            {
                "c#" or "csharp" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "cd backend && dotnet restore Backend.csproj && dotnet publish Backend.csproj -c Release -o /app/publish" },
                    new { name = "RAILWAY_START_COMMAND", value = "dotnet /app/publish/Backend.dll" },
                    new { name = "NIXPACKS_CSHARP_SDK_VERSION", value = "8.0" },
                    new { name = "PORT", value = "8080" }
                },
                "python" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "cd backend && pip install -r requirements.txt" },
                    new { name = "RAILWAY_START_COMMAND", value = "cd backend && uvicorn main:app --host 0.0.0.0 --port $PORT" },
                    new { name = "NIXPACKS_PYTHON_VERSION", value = "3.11" },
                    new { name = "PORT", value = "8080" }
                },
                "nodejs" or "node.js" or "node" => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "cd backend && npm install" },
                    new { name = "RAILWAY_START_COMMAND", value = "cd backend && npm start" },
                    new { name = "PORT", value = "8080" }
                },
                _ => new[]
                {
                    new { name = "RAILWAY_BUILD_COMMAND", value = "cd backend && dotnet restore Backend.csproj && dotnet publish Backend.csproj -c Release -o /app/publish" },
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
            
            // Combine language-specific variables with Maven variables
            var buildVariables = languageSpecificVariables.Concat(mavenVariables).ToArray();

            foreach (var variable in buildVariables)
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
                    variables = new { projectId, environmentId, serviceId = railwayServiceId, name = variable.name, value = variable.value }
                };

                var varBody = JsonSerializer.Serialize(setVarMutation);
                var varContent = new StringContent(varBody, System.Text.Encoding.UTF8, "application/json");
                await httpClient.PostAsync(railwayApiUrl, varContent);
            }

            _logger.LogInformation("[UTILITY] Set Railway build environment variables");

            // Step 8: Create Railway domain
            var createDomainMutation = new
            {
                query = @"
                    mutation CreateDomain($serviceId: String!, $environmentId: String!) {
                        serviceDomainCreate(input: { serviceId: $serviceId, environmentId: $environmentId }) {
                            id
                            domain
                        }
                    }",
                variables = new { serviceId = railwayServiceId, environmentId }
            };

            var domainBody = JsonSerializer.Serialize(createDomainMutation);
            var domainContent = new StringContent(domainBody, System.Text.Encoding.UTF8, "application/json");
            var domainResponse = await httpClient.PostAsync(railwayApiUrl, domainContent);
            var domainResponseContent = await domainResponse.Content.ReadAsStringAsync();

            string? backendUrl = null;
            if (domainResponse.IsSuccessStatusCode)
            {
                var domainDoc = JsonDocument.Parse(domainResponseContent);
                if (domainDoc.RootElement.TryGetProperty("data", out var domainDataObj) &&
                    domainDataObj.TryGetProperty("serviceDomainCreate", out var domainObj) &&
                    domainObj.TryGetProperty("domain", out var domainProp))
                {
                    var domain = domainProp.GetString();
                    if (!string.IsNullOrEmpty(domain))
                    {
                        backendUrl = domain.StartsWith("http") ? domain : $"https://{domain}";
                    }
                }
            }

            if (string.IsNullOrEmpty(backendUrl))
            {
                _logger.LogWarning("[UTILITY] Domain creation may have failed, but continuing");
                backendUrl = $"https://railway.app/project/{projectId}";
            }

            _logger.LogInformation("[UTILITY] ✅ Backend deployment test completed. Backend URL: {BackendUrl}", backendUrl);

            return Ok(new
            {
                Success = true,
                Message = "Backend deployment test completed",
                RepositoryUrl = repositoryUrl,
                BackendUrl = backendUrl,
                RailwayServiceId = railwayServiceId,
                RailwayProjectId = projectId,
                ProgrammingLanguage = programmingLanguage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UTILITY] Error during backend deployment test");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error during backend deployment: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Utility endpoint to add workflow file to an existing backend repository
    /// </summary>
    [HttpPost("add-backend-workflow")]
    public async Task<ActionResult<object>> AddBackendWorkflow([FromBody] AddWorkflowRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            {
                return BadRequest(new { Success = false, Message = "Repository URL is required" });
            }

            if (string.IsNullOrWhiteSpace(request.ProgrammingLanguage))
            {
                return BadRequest(new { Success = false, Message = "Programming language is required (e.g., 'c#', 'python', 'nodejs')" });
            }

            _logger.LogInformation("[UTILITY] Adding workflow file to repository: {RepositoryUrl}", request.RepositoryUrl);

            // Parse repository URL
            var uri = new Uri(request.RepositoryUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                return BadRequest(new { Success = false, Message = "Invalid repository URL format" });
            }

            var owner = pathParts[0];
            var repoName = pathParts[1].Replace(".git", "");

            var githubToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(githubToken))
            {
                return StatusCode(500, new { Success = false, Message = "GitHub access token not configured" });
            }

            // Generate workflow content
            var workflowContent = GenerateRailwayDeploymentWorkflowAtRoot(request.ProgrammingLanguage);

            // Add workflow file to repository
            var success = await _gitHubService.UpdateFileAsync(
                owner, 
                repoName, 
                ".github/workflows/deploy-backend.yml", 
                workflowContent, 
                "Add Railway deployment workflow for manual triggers",
                githubToken);

            if (success)
            {
                return Ok(new
                {
                    Success = true,
                    Message = "Workflow file added successfully",
                    RepositoryUrl = request.RepositoryUrl,
                    WorkflowPath = ".github/workflows/deploy-backend.yml",
                    WorkflowUrl = $"https://github.com/{owner}/{repoName}/actions"
                });
            }
            else
            {
                return StatusCode(500, new { Success = false, Message = "Failed to add workflow file" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UTILITY] Error adding workflow file: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Force GitHub to send a new collaborator invitation email (e.g. when the user never approved the original).
    /// Cancels any pending invitation for the user, then re-adds them so GitHub sends a fresh email.
    /// </summary>
    [HttpPost("github/resend-invitation")]
    public async Task<ActionResult<object>> ResendGitHubInvitation([FromBody] ResendGitHubInvitationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.GitHubUsername))
            {
                return BadRequest(new { Success = false, Message = "GitHubUsername is required (e.g. oferlav)" });
            }

            string owner;
            string repo;
            const string defaultOwner = "skill-in-projects";

            if (!string.IsNullOrWhiteSpace(request.BoardId) && !string.IsNullOrWhiteSpace(request.RepoType))
            {
                // BoardId + RepoType: frontend repo = {boardId}, backend repo = backend_{boardId}
                var boardId = request.BoardId.Trim();
                var repoType = request.RepoType.Trim().ToLowerInvariant();
                owner = defaultOwner;
                repo = repoType == "frontend"
                    ? boardId
                    : (repoType == "backend" ? "backend_" + boardId : boardId);
                if (repoType != "frontend" && repoType != "backend")
                {
                    return BadRequest(new { Success = false, Message = "RepoType must be 'Frontend' or 'Backend' when using BoardId." });
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.RepositoryUrl))
            {
                var uri = new Uri(request.RepositoryUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = "RepositoryUrl must be a valid GitHub URL (e.g. https://github.com/skill-in-projects/697e00d2f9cb8d26a7271afa for frontend or .../backend_697e00d2f9cb8d26a7271afa for backend)" });
                }
                owner = pathParts[0];
                repo = pathParts[1].Replace(".git", "");
            }
            else if (!string.IsNullOrWhiteSpace(request.Owner) && !string.IsNullOrWhiteSpace(request.Repository))
            {
                owner = request.Owner.Trim();
                repo = request.Repository.Trim();
            }
            else
            {
                return BadRequest(new { Success = false, Message = "Provide one of: (1) BoardId + RepoType (e.g. BoardId: 697e00d2f9cb8d26a7271afa, RepoType: Frontend), (2) RepositoryUrl, or (3) Owner + Repository." });
            }

            var accessToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                return StatusCode(500, new { Success = false, Message = "GitHub access token not configured" });
            }

            _logger.LogInformation("[UTILITY] Resending GitHub invitation for {User} to {Owner}/{Repo}", request.GitHubUsername, owner, repo);

            var result = await _gitHubService.ResendCollaboratorInvitationAsync(owner, repo, request.GitHubUsername.Trim(), accessToken);

            return Ok(new
            {
                Success = result.Success,
                Message = result.Message,
                InvitationDeleted = result.InvitationDeleted,
                NewInvitationSent = result.NewInvitationSent,
                Repository = $"{owner}/{repo}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UTILITY] Error resending GitHub invitation: {Message}", ex.Message);
            return StatusCode(500, new { Success = false, Message = $"Error: {ex.Message}" });
        }
    }

    private string GenerateRailwayDeploymentWorkflowAtRoot(string programmingLanguage)
    {
        // Build commands vary by programming language (files are at root, no backend/ directory)
        var buildCommands = programmingLanguage?.ToLowerInvariant() switch
        {
            "c#" or "csharp" => @"      - name: Build .NET application
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./out",
            "python" => @"      - name: Install Python dependencies
        run: |
          pip install -r requirements.txt",
            "nodejs" or "node.js" or "node" => @"      - name: Install Node.js dependencies
        run: |
          npm install",
            "java" => @"      - name: Build Java application
        run: |
          mvn clean package || gradle build || echo 'Build system not detected'",
            _ => @"      - name: Build .NET application (default)
        run: |
          dotnet restore Backend.csproj
          dotnet publish Backend.csproj -c Release -o ./out"
        };

        return $@"name: Deploy Backend to Railway

on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup Railway CLI
        uses: bervProject/setup-railway@v2.0.0
        with:
          railway_token: ${{{{ secrets.RAILWAY_TOKEN }}}}
      
{buildCommands}
      
      - name: Deploy to Railway
        env:
          RAILWAY_SERVICE_ID: ${{{{ secrets.RAILWAY_SERVICE_ID }}}}
        run: |
          railway up --service $RAILWAY_SERVICE_ID --detach
";
    }

    /// <summary>
    /// Test endpoint to deploy frontend to GitHub Pages
    /// Simulates the frontend deployment process during board creation
    /// Returns the GitHub Pages URL
    /// </summary>
    [HttpPost("deploy-frontend")]
    public async Task<ActionResult<object>> DeployFrontend([FromBody] DeployFrontendRequest? request = null)
    {
        try
        {
            var testGuid = Guid.NewGuid().ToString("N")[..16]; // Short GUID for unique naming
            var repoName = $"test-{testGuid}";
            
            _logger.LogInformation("[UTILITY] Starting frontend deployment test with repo name: {RepoName}", repoName);

            var githubToken = _configuration["GitHub:AccessToken"];

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                return BadRequest(new { Success = false, Message = "GitHub access token is missing" });
            }

            // Create GitHub repository with frontend files
            var githubRequest = new CreateRepositoryRequest
            {
                Name = repoName,
                Description = $"Test frontend deployment repository",
                IsPrivate = false,
                Collaborators = new List<string>(), // No collaborators for test
                ProjectTitle = request?.ProjectTitle ?? "Test Frontend Deployment"
            };

            var githubResponse = await _gitHubService.CreateRepositoryAsync(githubRequest);

            if (!githubResponse.Success || string.IsNullOrEmpty(githubResponse.RepositoryUrl))
            {
                return StatusCode(500, new { Success = false, Message = "Failed to create GitHub repository", Error = githubResponse.ErrorMessage });
            }

            var repositoryUrl = githubResponse.RepositoryUrl;
            var pagesUrl = githubResponse.GitHubPagesUrl;

            _logger.LogInformation("[UTILITY] ✅ Frontend deployment test completed. Repository: {RepositoryUrl}, Pages URL: {PagesUrl}", repositoryUrl, pagesUrl);

            return Ok(new
            {
                Success = true,
                Message = "Frontend deployment test completed",
                RepositoryUrl = repositoryUrl,
                PagesUrl = pagesUrl,
                Note = "GitHub Pages will be enabled automatically by the workflow on first push"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UTILITY] Error during frontend deployment test");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Error during frontend deployment: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    private int GetDefaultPortForLanguage(string? programmingLanguage)
    {
        if (string.IsNullOrEmpty(programmingLanguage))
            return 8080;

        return programmingLanguage.ToLowerInvariant() switch
        {
            "c#" or "csharp" => 8080,
            "python" => 8080, // Match PORT environment variable (8080)
            "nodejs" or "node.js" or "node" => 8080, // Match PORT environment variable (8080)
            "java" => 8080,
            _ => 8080
        };
    }

    /// <summary>
    /// Utility endpoint to delete all Neon databases in the account.
    /// WARNING: This is a destructive operation that permanently deletes all databases.
    /// Requires confirmation=true in the request body to proceed.
    /// </summary>
    [HttpPost("neon/delete-all-databases")]
    public async Task<ActionResult<object>> DeleteAllNeonDatabases([FromBody] DeleteAllNeonDatabasesRequest request)
    {
        try
        {
            if (!request.Confirmation)
            {
                return BadRequest(new 
                { 
                    Success = false, 
                    Message = "Confirmation required. Set 'confirmation' to true in the request body to proceed with deletion." 
                });
            }

            var neonApiKey = _configuration["Neon:ApiKey"];
            var neonBaseUrl = _configuration["Neon:BaseUrl"];

            if (string.IsNullOrWhiteSpace(neonApiKey) || neonApiKey == "your-neon-api-key-here")
            {
                _logger.LogWarning("Neon API key not configured");
                return BadRequest(new { Success = false, Message = "Neon API key is not configured" });
            }

            if (string.IsNullOrWhiteSpace(neonBaseUrl))
            {
                _logger.LogWarning("Neon base URL not configured");
                return BadRequest(new { Success = false, Message = "Neon base URL is not configured" });
            }

            _logger.LogWarning("🗑️ [NEON] Starting deletion of all databases. This is a destructive operation!");

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", neonApiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var stats = new DeleteAllNeonDatabasesStats();
            var errors = new List<string>();

            // Step 1: List all projects (with pagination support and infinite loop protection)
            var projects = new List<(string Id, string Name)>();
            string? cursor = null;
            string? previousCursor = null;
            int pageCount = 0;
            const int maxPages = 100; // Safety limit to prevent infinite loops
            int projectsInLastPage = 0;
            
            do
            {
                pageCount++;
                
                // Safety check: prevent infinite loops
                if (pageCount > maxPages)
                {
                    _logger.LogWarning("⚠️ [NEON] Reached maximum page limit ({MaxPages}). Stopping pagination to prevent infinite loop.", maxPages);
                    break;
                }
                
                // Safety check: detect if cursor is not changing (infinite loop)
                if (!string.IsNullOrEmpty(cursor) && cursor == previousCursor)
                {
                    _logger.LogWarning("⚠️ [NEON] Cursor unchanged between iterations (possible infinite loop). Stopping pagination. Cursor: {Cursor}", cursor);
                    break;
                }
                
                previousCursor = cursor;
                
                var projectsApiUrl = string.IsNullOrEmpty(cursor) 
                    ? $"{neonBaseUrl}/projects" 
                    : $"{neonBaseUrl}/projects?cursor={Uri.EscapeDataString(cursor)}";
                    
                _logger.LogInformation("📋 [NEON] Listing projects page {Page} from: {Url}", pageCount, projectsApiUrl);

                var projectsResponse = await httpClient.GetAsync(projectsApiUrl);
                if (!projectsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await projectsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("❌ [NEON] Failed to list projects (page {Page}): {StatusCode} - {Error}", pageCount, projectsResponse.StatusCode, errorContent);
                    if (projects.Count == 0)
                    {
                        // If we haven't gotten any projects yet, fail the operation
                        return StatusCode((int)projectsResponse.StatusCode, new
                        {
                            Success = false,
                            Message = $"Failed to list projects: {projectsResponse.StatusCode}",
                            Error = errorContent
                        });
                    }
                    // If we have some projects, continue with what we have
                    break;
                }

                var projectsContent = await projectsResponse.Content.ReadAsStringAsync();
                var projectsDoc = JsonDocument.Parse(projectsContent);
                
                // Track projects count before this page
                var projectsBeforePage = projects.Count;
                
                // Parse projects from this page
                if (projectsDoc.RootElement.TryGetProperty("projects", out var projectsProp) && projectsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var projectElement in projectsProp.EnumerateArray())
                    {
                        if (projectElement.TryGetProperty("id", out var projectIdProp) && 
                            projectElement.TryGetProperty("name", out var projectNameProp))
                        {
                            var projectId = projectIdProp.GetString();
                            var projectName = projectNameProp.GetString() ?? "Unknown";
                            if (!string.IsNullOrEmpty(projectId))
                            {
                                projects.Add((projectId, projectName));
                            }
                        }
                    }
                }
                
                // Calculate how many projects we got in this page
                projectsInLastPage = projects.Count - projectsBeforePage;
                _logger.LogInformation("📋 [NEON] Page {Page}: Found {Count} projects (total so far: {Total})", pageCount, projectsInLastPage, projects.Count);
                
                // Safety check: if we got 0 projects on this page, stop pagination
                if (projectsInLastPage == 0 && pageCount > 1)
                {
                    _logger.LogInformation("📋 [NEON] No projects in page {Page}. Stopping pagination.", pageCount);
                    break;
                }
                
                // Check for pagination cursor
                cursor = null;
                if (projectsDoc.RootElement.TryGetProperty("pagination", out var paginationProp))
                {
                    if (paginationProp.TryGetProperty("cursor", out var cursorProp) && cursorProp.ValueKind == JsonValueKind.String)
                    {
                        var cursorValue = cursorProp.GetString();
                        if (!string.IsNullOrEmpty(cursorValue))
                        {
                            cursor = cursorValue;
                            _logger.LogInformation("📋 [NEON] Found pagination cursor for next page: {Cursor}", cursor);
                        }
                    }
                }
                else
                {
                    // If there's no pagination object, assume no more pages
                    _logger.LogInformation("📋 [NEON] No pagination object in response. Assuming all projects retrieved.");
                    break;
                }
            } while (!string.IsNullOrEmpty(cursor));

            _logger.LogInformation("📋 [NEON] Found {Count} projects", projects.Count);
            stats.ProjectsFound = projects.Count;

            // Step 2: For each project, list branches and databases
            foreach (var (projectId, projectName) in projects)
            {
                _logger.LogInformation("🔍 [NEON] Processing project: {ProjectName} ({ProjectId})", projectName, projectId);

                // Step 2.1: Remove quota limitations to allow deletion
                try
                {
                    _logger.LogInformation("🔓 [NEON] Removing quota limitations for project: {ProjectName} ({ProjectId})", projectName, projectId);

                    // Update project settings to remove quota limits by setting very high values
                    var updateProjectRequest = new
                    {
                        project = new
                        {
                            settings = new
                            {
                                quota = new
                                {
                                    active_time_seconds = 31536000,      // 1 year (effectively unlimited)
                                    compute_time_seconds = 31536000,     // 1 year (effectively unlimited)
                                    written_data_bytes = 1000000000000L,  // 1 TB (effectively unlimited)
                                    data_transfer_bytes = 1000000000000L // 1 TB (effectively unlimited)
                                }
                            }
                        }
                    };

                    var updateRequestBody = JsonSerializer.Serialize(updateProjectRequest);
                    var updateContent = new StringContent(updateRequestBody, System.Text.Encoding.UTF8, "application/json");

                    var updateProjectUrl = $"{neonBaseUrl}/projects/{Uri.EscapeDataString(projectId)}";
                    _logger.LogInformation("🔓 [NEON] Updating project quota: PATCH {Url}", updateProjectUrl);

                    var updateResponse = await httpClient.PatchAsync(updateProjectUrl, updateContent);

                    if (updateResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("✅ [NEON] Successfully removed quota limitations for project: {ProjectName}", projectName);
                        // Wait a moment for the quota update to take effect
                        await Task.Delay(1000);
                    }
                    else
                    {
                        var errorContent = await updateResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("⚠️ [NEON] Failed to update quota for project {ProjectName} ({ProjectId}): {StatusCode} - {Error}. Continuing with deletion attempt anyway.",
                            projectName, projectId, updateResponse.StatusCode, errorContent);
                    }
                }
                catch (Exception quotaEx)
                {
                    _logger.LogWarning(quotaEx, "⚠️ [NEON] Error removing quota limitations for project {ProjectName} ({ProjectId}). Continuing with deletion attempt anyway.",
                        projectName, projectId);
                }

                // Step 2.2: List branches in this project
                var branchesApiUrl = $"{neonBaseUrl}/projects/{projectId}/branches";
                var branchesResponse = await httpClient.GetAsync(branchesApiUrl);

                if (!branchesResponse.IsSuccessStatusCode)
                {
                    var errorContent = await branchesResponse.Content.ReadAsStringAsync();
                    var errorMsg = $"Failed to list branches for project {projectName} ({projectId}): {branchesResponse.StatusCode}";
                    _logger.LogError("❌ [NEON] {Error}", errorMsg);
                    errors.Add(errorMsg);
                    stats.Errors++;
                    continue;
                }

                var branchesContent = await branchesResponse.Content.ReadAsStringAsync();
                var branchesDoc = JsonDocument.Parse(branchesContent);

                var branches = new List<(string Id, string Name)>();
                if (branchesDoc.RootElement.TryGetProperty("branches", out var branchesProp) && branchesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var branchElement in branchesProp.EnumerateArray())
                    {
                        if (branchElement.TryGetProperty("id", out var branchIdProp) && 
                            branchElement.TryGetProperty("name", out var branchNameProp))
                        {
                            var branchId = branchIdProp.GetString();
                            var branchName = branchNameProp.GetString() ?? "Unknown";
                            if (!string.IsNullOrEmpty(branchId))
                            {
                                branches.Add((branchId, branchName));
                            }
                        }
                    }
                }

                _logger.LogInformation("🌿 [NEON] Found {Count} branches in project {ProjectName}", branches.Count, projectName);
                stats.BranchesFound += branches.Count;

                // Step 3: For each branch, list and delete databases
                foreach (var (branchId, branchName) in branches)
                {
                    _logger.LogInformation("📦 [NEON] Processing branch: {BranchName} ({BranchId}) in project {ProjectName}", branchName, branchId, projectName);

                    // List databases in this branch
                    var databasesApiUrl = $"{neonBaseUrl}/projects/{projectId}/branches/{Uri.EscapeDataString(branchId)}/databases";
                    var databasesResponse = await httpClient.GetAsync(databasesApiUrl);

                    if (!databasesResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await databasesResponse.Content.ReadAsStringAsync();
                        var errorMsg = $"Failed to list databases for branch {branchName} ({branchId}) in project {projectName}: {databasesResponse.StatusCode}";
                        _logger.LogError("❌ [NEON] {Error}", errorMsg);
                        errors.Add(errorMsg);
                        stats.Errors++;
                        continue;
                    }

                    var databasesContent = await databasesResponse.Content.ReadAsStringAsync();
                    var databasesDoc = JsonDocument.Parse(databasesContent);

                    var databases = new List<string>();
                    if (databasesDoc.RootElement.TryGetProperty("databases", out var databasesProp) && databasesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var dbElement in databasesProp.EnumerateArray())
                        {
                            if (dbElement.TryGetProperty("name", out var dbNameProp))
                            {
                                var dbName = dbNameProp.GetString();
                                if (!string.IsNullOrEmpty(dbName))
                                {
                                    // Skip reserved database names if requested
                                    if (request.SkipReserved && (dbName == "neondb" || dbName == "postgres" || dbName == "template0" || dbName == "template1"))
                                    {
                                        _logger.LogInformation("⏭️ [NEON] Skipping reserved database: {DbName}", dbName);
                                        stats.DatabasesSkipped++;
                                        continue;
                                    }
                                    databases.Add(dbName);
                                }
                            }
                        }
                    }

                    _logger.LogInformation("📦 [NEON] Found {Count} databases in branch {BranchName}", databases.Count, branchName);
                    stats.DatabasesFound += databases.Count;

                    // Step 4: Delete each database
                    foreach (var dbName in databases)
                    {
                        var deleteApiUrl = $"{neonBaseUrl}/projects/{projectId}/branches/{Uri.EscapeDataString(branchId)}/databases/{Uri.EscapeDataString(dbName)}";
                        _logger.LogWarning("🗑️ [NEON] Deleting database: {DbName} from branch {BranchName} in project {ProjectName}", dbName, branchName, projectName);

                        var deleteResponse = await httpClient.DeleteAsync(deleteApiUrl);

                        if (deleteResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("✅ [NEON] Successfully deleted database: {DbName}", dbName);
                            stats.DatabasesDeleted++;
                        }
                        else
                        {
                            var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                            var errorMsg = $"Failed to delete database {dbName} from branch {branchName} in project {projectName}: {deleteResponse.StatusCode} - {errorContent}";
                            _logger.LogError("❌ [NEON] {Error}", errorMsg);
                            errors.Add(errorMsg);
                            stats.Errors++;
                        }

                        // Add a small delay between deletions to avoid rate limiting
                        await Task.Delay(500);
                    }
                }
            }

            _logger.LogWarning("✅ [NEON] Deletion process completed. Databases deleted: {Deleted}, Errors: {Errors}", stats.DatabasesDeleted, stats.Errors);

            return Ok(new
            {
                Success = true,
                Message = $"Deletion process completed. {stats.DatabasesDeleted} database(s) deleted.",
                Stats = stats,
                Errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [NEON] Error during deletion of all databases");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Returns the TrelloBoardJson content for a project (utility to display stored board template).
    /// GET api/Utilities/trello/project/{projectId}/board-json
    /// </summary>
    [HttpGet("trello/project/{projectId:int}/board-json")]
    public async Task<ActionResult<object>> GetProjectTrelloBoardJson(int projectId)
    {
        if (projectId <= 0)
            return BadRequest(new { Success = false, Message = "ProjectId must be greater than 0." });

        var project = await _context.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.Title, p.TrelloBoardJson })
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null)
            return NotFound(new { Success = false, Message = $"Project {projectId} not found." });

        if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
            return Ok(new
            {
                projectId = project.Id,
                projectTitle = project.Title,
                hasTrelloBoardJson = false,
                trelloBoardJson = (string?)null,
                length = 0,
                Message = "Project has no TrelloBoardJson stored. Use POST api/Utilities/trello/store-board-json first."
            });

        object? parsed = null;
        try
        {
            parsed = JsonSerializer.Deserialize<object>(project.TrelloBoardJson);
        }
        catch
        {
            // Return raw string if not valid JSON
        }

        return Ok(new
        {
            projectId = project.Id,
            projectTitle = project.Title,
            hasTrelloBoardJson = true,
            trelloBoardJson = parsed ?? (object)project.TrelloBoardJson,
            length = project.TrelloBoardJson.Length
        });
    }

    /// <summary>
    /// Invite one or more members to an existing Trello board by email (e.g. to add PM to a board created before the allowBillableGuest fix).
    /// POST api/Utilities/trello/invite-to-board
    /// </summary>
    [HttpPost("trello/invite-to-board")]
    public async Task<ActionResult<object>> TrelloInviteToBoard([FromBody] TrelloInviteToBoardRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { Success = false, Message = "BoardId is required." });
        var emails = request.Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList() ?? new List<string>();
        if (emails.Count == 0)
            return BadRequest(new { Success = false, Message = "At least one email is required in Emails." });
        var invited = new List<string>();
        var failed = new List<object>();
        foreach (var email in emails)
        {
            var (success, error) = await _trelloService.InviteMemberToBoardByEmailAsync(request.BoardId.Trim(), email!.Trim());
            if (success)
                invited.Add(email.Trim());
            else
                failed.Add(new { email = email.Trim(), error = error ?? "Unknown error" });
        }
        return Ok(new
        {
            Success = failed.Count == 0,
            BoardId = request.BoardId,
            Invited = invited,
            Failed = failed,
            Message = failed.Count == 0
                ? $"Invited {invited.Count} member(s) to the board."
                : $"Invited {invited.Count} member(s); {failed.Count} failed."
        });
    }

    /// <summary>
    /// Generates Trello board creation JSON using the AI service (same flow as POST api/Boards/use/create) and stores it in Projects.TrelloBoardJson.
    /// When Trello:UseDBProjectBoard is true, board create will use this saved JSON instead of calling AI.
    /// </summary>
    [HttpPost("trello/store-board-json")]
    public async Task<ActionResult<object>> StoreTrelloBoardJson([FromBody] StoreTrelloBoardJsonRequest request)
    {
        try
        {
            if (request == null || request.ProjectId <= 0)
            {
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
            {
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            }

            var projectLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:ProjectLengthInWeeks", 12);
            var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);

            List<RoleInfo> roleGroups;
            List<StudentInfo> studentsForAi;

            if (request.StudentIds != null && request.StudentIds.Any())
            {
                var students = await _context.Students
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .Where(s => request.StudentIds.Contains(s.Id) && s.IsAvailable)
                    .ToListAsync();

                if (students.Count != request.StudentIds.Count)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "One or more students not found or not available.",
                        Hint = "To generate a board JSON template for all roles (PM, Backend, Frontend, UI/UX, Marketing), omit studentIds or send studentIds: []."
                    });
                }

                roleGroups = students
                    .Where(s => s.StudentRoles != null)
                    .SelectMany(s => s.StudentRoles!)
                    .Where(sr => sr?.Role != null)
                    .GroupBy(sr => new { sr!.RoleId, sr.Role!.Name })
                    .Select(g => new RoleInfo
                    {
                        RoleId = g.Key.RoleId,
                        RoleName = g.Key.Name,
                        StudentCount = g.Count()
                    })
                    .ToList();

                studentsForAi = students.Select(s => new StudentInfo
                {
                    Id = s.Id,
                    Name = $"{s.FirstName} {s.LastName}",
                    Email = s.Email,
                    Roles = s.StudentRoles?.Where(sr => sr?.Role != null).Select(sr => sr!.Role!.Name).ToList() ?? new List<string>()
                }).ToList();
            }
            else
            {
                // Generate template for ALL roles (FE, BE, UI/UX, PM, Marketing) - no studentIds
                var allRoleNames = new[] { "Product Manager", "Backend Developer", "Frontend Developer", "UI/UX Designer", "Marketing" };
                var roles = await _context.Roles
                    .Where(r => r.IsActive && allRoleNames.Contains(r.Name))
                    .ToListAsync();

                roleGroups = roles.Select(r => new RoleInfo { RoleId = r.Id, RoleName = r.Name, StudentCount = 1 }).ToList();
                studentsForAi = roles.Select((r, i) => new StudentInfo
                {
                    Id = -(i + 1),
                    Name = $"Template {r.Name}",
                    Email = $"template-{r.Id}@template.local",
                    Roles = new List<string> { r.Name }
                }).ToList();

                if (roleGroups.Count == 0)
                {
                    return BadRequest(new { Success = false, Message = "No template roles found in database. Ensure Roles exist for: Product Manager, Backend Developer, Frontend Developer, UI/UX Designer, Marketing." });
                }

                _logger.LogInformation("Generating Trello board JSON template for all roles: {Roles}", string.Join(", ", roleGroups.Select(r => r.RoleName)));
            }

            var projectModules = await _context.ProjectModules
                .Where(pm => pm.ProjectId == request.ProjectId && pm.ModuleType != 3)
                .OrderBy(pm => pm.Sequence)
                .Select(pm => new ProjectModuleInfo
                {
                    Id = pm.Id,
                    Title = pm.Title,
                    Description = pm.Description
                })
                .ToListAsync();

            var sprintPlanRequest = new SprintPlanningRequest
            {
                ProjectId = request.ProjectId,
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                StartDate = DateTime.UtcNow,
                SystemDesign = project.SystemDesign,
                TeamRoles = roleGroups,
                ProjectModules = projectModules,
                Students = studentsForAi
            };

            SprintPlanningResponse? sprintPlanResponse;
            if (_testingConfig.Value.SkipAIService)
            {
                return BadRequest(new { Success = false, Message = "Testing:SkipAIService is true. Set it to false to generate Trello board JSON via AI." });
            }

            sprintPlanResponse = await _aiService.GenerateSprintPlanAsync(sprintPlanRequest);
            if (sprintPlanResponse == null || sprintPlanResponse.SprintPlan == null)
            {
                _logger.LogWarning("AI returned null sprint plan for project {ProjectId}", request.ProjectId);
                return StatusCode(500, new { Success = false, Message = "AI service did not return a valid sprint plan." });
            }

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

            var teamMembers = request.StudentIds != null && request.StudentIds.Any()
                ? (await _context.Students
                    .Include(s => s.StudentRoles)
                    .ThenInclude(sr => sr.Role)
                    .Where(s => request.StudentIds.Contains(s.Id))
                    .ToListAsync())
                    .Select(s => new TrelloTeamMember
                    {
                        Email = s.Email,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        RoleId = s.StudentRoles?.FirstOrDefault()?.RoleId ?? 0,
                        RoleName = s.StudentRoles?.FirstOrDefault()?.Role?.Name ?? "Team Member"
                    }).ToList()
                : roleGroups.Select(r => new TrelloTeamMember
                {
                    Email = $"template-{r.RoleId}@template.local",
                    FirstName = "Template",
                    LastName = r.RoleName,
                    RoleId = r.RoleId,
                    RoleName = r.RoleName
                }).ToList();

            var trelloRequest = new TrelloProjectCreationRequest
            {
                ProjectId = request.ProjectId,
                ProjectTitle = project.Title,
                ProjectDescription = project.Description,
                StudentEmails = teamMembers.Select(m => m.Email).ToList(),
                ProjectLengthWeeks = projectLengthWeeks,
                SprintLengthWeeks = sprintLengthWeeks,
                TeamMembers = teamMembers,
                SprintPlan = trelloSprintPlan
            };

            var json = JsonSerializer.Serialize(trelloRequest);
            project.TrelloBoardJson = json;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stored TrelloBoardJson for project {ProjectId} ({Length} chars)", request.ProjectId, json.Length);
            return Ok(new
            {
                Success = true,
                Message = $"Trello board JSON stored for project {request.ProjectId}. Use POST api/Boards/use/create with ProjectId and StudentIds to create the board (when Trello:UseDBProjectBoard is true). Cards for roles not in the team are filtered out automatically.",
                ProjectId = request.ProjectId,
                JsonLength = json.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing TrelloBoardJson for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Override a sprint (Trello list) in the stored Projects.TrelloBoardJson: set Description and Checklist for the target sprint, then save JSON back to DB.
    /// Optionally uses AI to parse/normalize checklist text into Trello-friendly items.
    /// Use CheckListText (one string with newlines) to avoid JSON escaping: in JSON use \n; or use POST trello/sprint-override-form for literal line breaks.
    /// </summary>
    [HttpPost("trello/sprint-override")]
    public async Task<ActionResult<object>> TrelloSprintOverride([FromBody] SprintOverrideRequest? request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "Request body is required. For checklist with line breaks use checkListText (escape newlines as \\n in JSON) or POST api/Utilities/trello/sprint-override-form with form-data."
                });
            }
            if (request.ProjectId <= 0)
            {
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });
            }
            if (request.SprintNumber < 0)
            {
                return BadRequest(new { Success = false, Message = "SprintNumber must be 0 or greater (0 = new list before all others)." });
            }

            _logger.LogInformation("[SprintOverride] Request: ProjectId={ProjectId}, SprintNumber={SprintNumber}, CheckListText length={CheckListTextLen}, CheckList count={CheckListCount}, RoleName={RoleName}, Description length={DescLen}",
                request.ProjectId, request.SprintNumber,
                request.CheckListText?.Length ?? 0,
                request.CheckList?.Count ?? 0,
                request.RoleName ?? "(null)",
                request.Description?.Length ?? 0);

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
            {
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            }
            if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
            {
                return BadRequest(new { Success = false, Message = "Project has no TrelloBoardJson. Use POST api/Utilities/trello/store-board-json first." });
            }

            TrelloProjectCreationRequest? trelloRequest;
            try
            {
                trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TrelloBoardJson for project {ProjectId}", request.ProjectId);
                return BadRequest(new { Success = false, Message = "Invalid TrelloBoardJson in project." });
            }

            if (trelloRequest!.SprintPlan == null)
                trelloRequest.SprintPlan = new TrelloSprintPlan();
            if (trelloRequest.SprintPlan.Lists == null)
                trelloRequest.SprintPlan.Lists = new List<TrelloList>();

            TrelloList? list;
            if (request.SprintNumber == 0)
            {
                list = trelloRequest.SprintPlan.Lists.FirstOrDefault(l =>
                    l.Position == 0 ||
                    string.Equals(l.Name, "Sprint 0", StringComparison.OrdinalIgnoreCase));
                if (list == null)
                {
                    var boardName = trelloRequest.SprintPlan.Lists.FirstOrDefault()?.BoardName ?? "";
                    list = new TrelloList
                    {
                        Name = "Sprint 0",
                        BoardName = boardName,
                        Position = 0,
                        Description = request.Description ?? "",
                        ChecklistItems = new List<string>()
                    };
                    trelloRequest.SprintPlan.Lists.Insert(0, list);
                    _logger.LogInformation("[SprintOverride] Created new list 'Sprint 0' at index 0 (before all others).");
                }
            }
            else
            {
                if (!trelloRequest.SprintPlan.Lists.Any())
                {
                    return BadRequest(new { Success = false, Message = "TrelloBoardJson has no Lists. Use SprintNumber=0 to create the first list." });
                }
                list = trelloRequest.SprintPlan.Lists.FirstOrDefault(l =>
                    l.Position == request.SprintNumber ||
                    string.Equals(l.Name, $"Sprint {request.SprintNumber}", StringComparison.OrdinalIgnoreCase));
                if (list == null)
                {
                    list = trelloRequest.SprintPlan.Lists.ElementAtOrDefault(request.SprintNumber - 1);
                }
                if (list == null)
                {
                    return NotFound(new { Success = false, Message = $"Sprint {request.SprintNumber} not found in TrelloBoardJson (Lists count: {trelloRequest.SprintPlan.Lists.Count})." });
                }
            }

            _logger.LogInformation("[SprintOverride] Resolved list: Name={ListName}, Position={Position}", list.Name, list.Position);

            List<string>? checklistItems = null;
            if (request.CheckList != null && request.CheckList.Any())
            {
                checklistItems = new List<string>(request.CheckList);
                _logger.LogInformation("[SprintOverride] Built checklist from CheckList: {Count} items (first: {First})", checklistItems.Count, checklistItems.FirstOrDefault() ?? "(none)");
            }
            else if (!string.IsNullOrWhiteSpace(request.CheckListText))
            {
                // Split by "[ ]" so each "[ ]" starts a new check item; each item is formatted as "[ ] " + text so "[ ]" comes first
                var segments = request.CheckListText!
                    .Split(new[] { "[ ]" }, StringSplitOptions.None)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
                checklistItems = segments.Select(s => "[ ] " + s).ToList();
                _logger.LogInformation("[SprintOverride] Built checklist from CheckListText (split by '[ ]'): {Count} items (first: {First})", checklistItems.Count, checklistItems.FirstOrDefault() ?? "(none)");
            }
            if (checklistItems == null || !checklistItems.Any())
            {
                _logger.LogWarning("[SprintOverride] No checklist items to apply (CheckListText empty/null and CheckList empty/null or empty).");
            }
            if (checklistItems != null && checklistItems.Any())
            {
                var useAi = request.UseAiToParseCheckList ?? true;
                if (useAi && checklistItems.Count == 1 && (checklistItems[0].Length > 150 || checklistItems[0].Contains('\n')))
                {
                    var parsed = await ParseChecklistWithAiAsync(checklistItems[0]);
                    if (parsed != null && parsed.Any())
                    {
                        checklistItems = parsed;
                        _logger.LogInformation("AI parsed checklist into {Count} items for Sprint {SprintNumber}", checklistItems.Count, request.SprintNumber);
                    }
                }
                else if (useAi && checklistItems.Any(s => s.Length > 100))
                {
                    var normalized = await NormalizeChecklistWithAiAsync(checklistItems);
                    if (normalized != null && normalized.Any())
                    {
                        checklistItems = normalized;
                        _logger.LogInformation("AI normalized checklist to {Count} Trello-friendly items for Sprint {SprintNumber}", checklistItems.Count, request.SprintNumber);
                    }
                }
            }

            var hasChecklist = checklistItems != null && checklistItems.Any();
            var roleNameProvided = !string.IsNullOrWhiteSpace(request.RoleName);
            int cardsUpdated = 0;

            _logger.LogInformation("[SprintOverride] hasChecklist={HasChecklist}, roleNameProvided={RoleNameProvided}, updating {Target}", hasChecklist, roleNameProvided, roleNameProvided ? "cards" : "list");

            if (roleNameProvided)
            {
                if (trelloRequest.SprintPlan.Cards == null)
                    trelloRequest.SprintPlan.Cards = new List<TrelloCard>();
                var cards = trelloRequest.SprintPlan.Cards;
                var matchingCards = cards.Where(c =>
                    string.Equals(c.ListName, list.Name, StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(c.RoleName, request.RoleName, StringComparison.OrdinalIgnoreCase) ||
                     (c.Labels != null && c.Labels.Any(l => string.Equals(l, request.RoleName, StringComparison.OrdinalIgnoreCase))))).ToList();
                _logger.LogInformation("[SprintOverride] Matching cards for ListName={ListName}, RoleName={RoleName}: {Count} of {Total}, IsNewCard={IsNewCard}", list.Name, request.RoleName, matchingCards.Count, cards.Count, request.IsNewCard);

                if (request.IsNewCard)
                {
                    // Create a new card without overriding existing; place it after the last same-role card.
                    var roleLetter = char.ToUpperInvariant((request.RoleName ?? "X").Trim().FirstOrDefault());
                    if (roleLetter == '\0') roleLetter = 'X';
                    var baseId = $"{request.SprintNumber}-{roleLetter}";
                    var sameRoleIds = cards.Where(c => c.CardId != null && (c.CardId == baseId || c.CardId.StartsWith(baseId + "-", StringComparison.Ordinal))).Select(c => c.CardId!).ToList();
                    var cardId = sameRoleIds.Any() ? $"{baseId}-{sameRoleIds.Count + 1}" : baseId;
                    var newCard = new TrelloCard
                    {
                        CardId = !string.IsNullOrEmpty(request.CardId) ? request.CardId : cardId,
                        ListName = list.Name,
                        RoleName = request.RoleName!,
                        Name = !string.IsNullOrEmpty(request.Name) ? request.Name : $"{request.RoleName} - Sprint {request.SprintNumber}",
                        Description = request.Description ?? string.Empty,
                        ChecklistItems = checklistItems ?? new List<string>(),
                        Labels = new List<string> { request.RoleName! },
                        ModuleId = request.ModuleId ?? string.Empty,
                        Dependencies = request.Dependencies != null && request.Dependencies.Count > 0 ? new List<string>(request.Dependencies) : new List<string>()
                    };
                    int insertIndex;
                    if (matchingCards.Any())
                    {
                        var lastSameRole = matchingCards.Last();
                        insertIndex = cards.IndexOf(lastSameRole) + 1;
                        cards.Insert(insertIndex, newCard);
                        _logger.LogInformation("[SprintOverride] IsNewCard=true: inserted new card after same-role card at index {Index}.", insertIndex);
                    }
                    else
                    {
                        // No same-role card in this list; append after last card in this list, or at end.
                        var inListIndices = cards.Select((c, i) => (c, i)).Where(x => string.Equals(x.c.ListName, list.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                        insertIndex = inListIndices.Any() ? inListIndices.Last().i + 1 : cards.Count;
                        cards.Insert(insertIndex, newCard);
                        _logger.LogInformation("[SprintOverride] IsNewCard=true: no same-role card in list; inserted new card at index {Index}.", insertIndex);
                    }
                    cardsUpdated = 1;
                }
                else if (matchingCards.Any())
                {
                    // If multiple cards for same role in same sprint: keep one, remove the rest, then update the kept one.
                    if (matchingCards.Count > 1)
                    {
                        var toKeep = matchingCards[0];
                        var toRemove = matchingCards.Skip(1).ToList();
                        foreach (var card in toRemove)
                            cards.Remove(card);
                        _logger.LogInformation("[SprintOverride] Removed {Count} duplicate card(s) for role '{RoleName}' in Sprint {SprintNumber}; keeping one.", toRemove.Count, request.RoleName, request.SprintNumber);
                        matchingCards = new List<TrelloCard> { toKeep };
                    }
                    var cardToUpdate = matchingCards[0];
                    if (!string.IsNullOrEmpty(request.Name))
                        cardToUpdate.Name = request.Name;
                    if (request.Description != null)
                        cardToUpdate.Description = request.Description;
                    if (checklistItems != null)
                        cardToUpdate.ChecklistItems = new List<string>(checklistItems);
                    if (!string.IsNullOrEmpty(request.CardId))
                        cardToUpdate.CardId = request.CardId;
                    cardToUpdate.ModuleId = request.ModuleId ?? string.Empty;
                    if (request.Dependencies != null && request.Dependencies.Count > 0)
                        cardToUpdate.Dependencies = new List<string>(request.Dependencies);
                    cardsUpdated = 1;
                    _logger.LogInformation("[SprintOverride] Updated 1 card with RoleName/label '{RoleName}' in Sprint {SprintNumber}", request.RoleName, request.SprintNumber);
                }
                else
                {
                    var roleLetter = char.ToUpperInvariant((request.RoleName ?? "X").Trim().FirstOrDefault());
                    if (roleLetter == '\0') roleLetter = 'X';
                    var cardId = $"{request.SprintNumber}-{roleLetter}";
                    var newCard = new TrelloCard
                    {
                        CardId = !string.IsNullOrEmpty(request.CardId) ? request.CardId : cardId,
                        ListName = list.Name,
                        RoleName = request.RoleName!,
                        Name = !string.IsNullOrEmpty(request.Name) ? request.Name : $"{request.RoleName} - Sprint {request.SprintNumber}",
                        Description = request.Description ?? string.Empty,
                        ChecklistItems = checklistItems ?? new List<string>(),
                        Labels = new List<string> { request.RoleName! },
                        ModuleId = request.ModuleId ?? string.Empty,
                        Dependencies = request.Dependencies != null && request.Dependencies.Count > 0 ? new List<string>(request.Dependencies) : new List<string>()
                    };
                    cards.Add(newCard);
                    cardsUpdated = 1;
                    _logger.LogInformation("[SprintOverride] No card for role '{RoleName}' in Sprint {SprintNumber}; added new card.", request.RoleName, request.SprintNumber);
                }
            }
            else
            {
                if (request.Description != null)
                {
                    list.Description = request.Description;
                    _logger.LogInformation("[SprintOverride] Set list Description for Sprint {SprintNumber} (list: {ListName})", request.SprintNumber, list.Name);
                }
                if (checklistItems != null)
                {
                    list.ChecklistItems = checklistItems;
                    _logger.LogInformation("[SprintOverride] Set list ChecklistItems for Sprint {SprintNumber} (list: {ListName}), count={Count}", request.SprintNumber, list.Name, checklistItems.Count);
                }
            }

            if (request.OrderInBoard is int orderInBoard && trelloRequest.SprintPlan.Lists.Count > 0)
            {
                var lists = trelloRequest.SprintPlan.Lists;
                var currentIndex = lists.IndexOf(list);
                if (currentIndex >= 0)
                {
                    var targetIndex = Math.Clamp(orderInBoard, 0, lists.Count - 1);
                    if (currentIndex != targetIndex)
                    {
                        lists.RemoveAt(currentIndex);
                        lists.Insert(targetIndex, list);
                        _logger.LogInformation("[SprintOverride] Moved list '{ListName}' to 0-based index {TargetIndex} (OrderInBoard={OrderInBoard}).", list.Name, targetIndex, orderInBoard);
                    }
                    for (var i = 0; i < lists.Count; i++)
                        lists[i].Position = i;
                }
            }

            var newJson = JsonSerializer.Serialize(trelloRequest);
            _logger.LogInformation("[SprintOverride] Serialized TrelloBoardJson length: {Len} (before save)", newJson.Length);
            project.TrelloBoardJson = newJson;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("[SprintOverride] SaveChangesAsync completed. Sprint override saved for project {ProjectId}, sprint {SprintNumber}", request.ProjectId, request.SprintNumber);
            return Ok(new
            {
                Success = true,
                Message = roleNameProvided
                    ? $"Sprint {request.SprintNumber}: updated {cardsUpdated} card(s) with role/label '{request.RoleName}'."
                    : $"Sprint {request.SprintNumber} updated in TrelloBoardJson.",
                ProjectId = request.ProjectId,
                SprintNumber = request.SprintNumber,
                ListName = list.Name,
                RoleName = request.RoleName,
                CardsUpdated = roleNameProvided ? cardsUpdated : (int?)null,
                NameUpdated = !string.IsNullOrEmpty(request.Name),
                DescriptionUpdated = request.Description != null,
                ChecklistUpdated = hasChecklist,
                ChecklistItemCount = checklistItems?.Count ?? (roleNameProvided ? null : list.ChecklistItems?.Count)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in sprint-override for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Same as sprint-override but accepts application/x-www-form-urlencoded or multipart/form-data.
    /// Use this when CheckListText contains literal line breaks (e.g. from a textarea); no JSON escaping needed.
    /// </summary>
    [HttpPost("trello/sprint-override-form")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<ActionResult<object>> TrelloSprintOverrideForm([FromForm] SprintOverrideFormRequest? form)
    {
        if (form == null)
        {
            return BadRequest(new { Success = false, Message = "Form data is required." });
        }
        var dependenciesList = (List<string>?)null;
        if (!string.IsNullOrWhiteSpace(form.Dependencies))
        {
            dependenciesList = form.Dependencies!
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (dependenciesList.Count == 0)
                dependenciesList = null;
        }
        var request = new SprintOverrideRequest
        {
            ProjectId = form.ProjectId,
            SprintNumber = form.SprintNumber,
            Name = form.Name,
            Description = form.Description,
            CheckListText = form.CheckListText,
            RoleName = form.RoleName,
            UseAiToParseCheckList = form.UseAiToParseCheckList,
            OrderInBoard = null,
            IsNewCard = form.IsNewCard,
            CardId = form.CardId,
            ModuleId = form.ModuleId,
            Dependencies = dependenciesList
        };
        var result = await TrelloSprintOverride(request);
        if (result.Result is not OkObjectResult okResult || okResult.Value == null)
            return result;
        var body = okResult.Value;
        var successProp = body.GetType().GetProperty("Success");
        if (successProp?.GetValue(body) is not true)
            return result;

        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == form.ProjectId);
            if (project == null || string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                return result;
            var trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            if (trelloRequest?.SprintPlan?.Lists == null || trelloRequest.SprintPlan.Lists.Count == 0)
                return result;
            var lists = trelloRequest.SprintPlan.Lists;
            var canonicalNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7", "Bugs" };
            var used = new HashSet<TrelloList>();
            var ordered = new List<TrelloList>();
            foreach (var name in canonicalNames)
            {
                // Match "Sprint N", "SprintN", and normalized (trim/collapse spaces) so list order works regardless of spacing
                var nameNorm = NormalizeListName(name);
                var found = lists.FirstOrDefault(l =>
                    string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeListName(l.Name), nameNorm, StringComparison.OrdinalIgnoreCase) ||
                    (name.StartsWith("Sprint ", StringComparison.OrdinalIgnoreCase) && name.Length > 7 &&
                     string.Equals(l.Name, "Sprint" + name.Substring(7).Trim(), StringComparison.OrdinalIgnoreCase)));
                if (found != null && !used.Contains(found))
                {
                    used.Add(found);
                    ordered.Add(found);
                }
            }
            foreach (var l in lists)
                if (!used.Contains(l))
                    ordered.Add(l);
            trelloRequest.SprintPlan.Lists = ordered;
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = i;

            // Reorder cards: by list order (Sprint 1..7, Bugs), then within each list by CardId so role order is identical in every sprint
            var cards = trelloRequest.SprintPlan.Cards ?? new List<TrelloCard>();
            var orderedListNames = ordered.Select(l => l.Name).ToList();
            var reorderedCards = new List<TrelloCard>();
            var placed = new HashSet<TrelloCard>();
            foreach (var listName in orderedListNames)
            {
                var cardsInList = cards.Where(c => string.Equals(c.ListName, listName, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var c in cardsInList) placed.Add(c);
                cardsInList.Sort((a, b) => string.Compare(a.CardId ?? "", b.CardId ?? "", StringComparison.OrdinalIgnoreCase));
                reorderedCards.AddRange(cardsInList);
            }
            // Append any cards that didn't match a list (e.g. null ListName or typo) so we don't drop them
            foreach (var c in cards)
                if (!placed.Contains(c))
                    reorderedCards.Add(c);
            trelloRequest.SprintPlan.Cards = reorderedCards;

            project.TrelloBoardJson = JsonSerializer.Serialize(trelloRequest);
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("[SprintOverrideForm] Project {ProjectId}: lists reordered to canonical order after override.", form.ProjectId);
            var existingMessage = body.GetType().GetProperty("Message")?.GetValue(body) as string ?? "Sprint override saved.";
            return Ok(new
            {
                Success = true,
                Message = existingMessage + " Lists reordered to canonical order (Sprint 1..7, Bugs). Cards reordered by list then by CardId (same role order in every sprint).",
                ProjectId = form.ProjectId,
                SprintNumber = form.SprintNumber,
                ListName = body.GetType().GetProperty("ListName")?.GetValue(body),
                RoleName = form.RoleName,
                CardsUpdated = body.GetType().GetProperty("CardsUpdated")?.GetValue(body),
                NameUpdated = !string.IsNullOrEmpty(form.Name),
                DescriptionUpdated = form.Description != null,
                ChecklistUpdated = !string.IsNullOrWhiteSpace(form.CheckListText),
                ListOrder = ordered.Select(l => l.Name).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SprintOverrideForm] Failed to reorder lists for project {ProjectId}; override was saved.", form.ProjectId);
            return result;
        }
    }

    /// <summary>
    /// Move a sprint list by name to a target 0-based index and rename it.
    /// Example: move "Sprint 5" to index 1 and rename to "Sprint 2" so it becomes the second list on the board.
    /// </summary>
    [HttpPost("trello/set-sprint-index-in-board")]
    public async Task<ActionResult<object>> SetSprintIndexInBoard([FromBody] SetSprintIndexInBoardRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { Success = false, Message = "Request body is required." });
            if (request.ProjectId <= 0)
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });
            if (string.IsNullOrWhiteSpace(request.SourceListName))
                return BadRequest(new { Success = false, Message = "SourceListName is required." });
            if (string.IsNullOrWhiteSpace(request.TargetListName))
                return BadRequest(new { Success = false, Message = "TargetListName is required." });
            if (request.TargetListIndex < 0)
                return BadRequest(new { Success = false, Message = "TargetListIndex must be 0 or greater." });

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                return BadRequest(new { Success = false, Message = "Project has no TrelloBoardJson. Use POST api/Utilities/trello/store-board-json first." });

            TrelloProjectCreationRequest? trelloRequest;
            try
            {
                trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TrelloBoardJson for project {ProjectId}", request.ProjectId);
                return BadRequest(new { Success = false, Message = "Invalid TrelloBoardJson in project." });
            }

            if (trelloRequest!.SprintPlan == null)
                trelloRequest.SprintPlan = new TrelloSprintPlan();
            if (trelloRequest.SprintPlan.Lists == null)
                trelloRequest.SprintPlan.Lists = new List<TrelloList>();

            var lists = trelloRequest.SprintPlan.Lists;
            var listA = lists.FirstOrDefault(l => string.Equals(l.Name, request.SourceListName!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (listA == null)
                return NotFound(new { Success = false, Message = $"List '{request.SourceListName}' not found in TrelloBoardJson. Available: {string.Join(", ", lists.Select(l => l.Name))}" });

            var sourceName = listA.Name;
            var targetName = request.TargetListName!.Trim();
            var sourceIndex = lists.IndexOf(listA);
            var targetIndex = Math.Clamp(request.TargetListIndex, 0, lists.Count);
            var listB = lists.FirstOrDefault(l => !ReferenceEquals(l, listA) && string.Equals(l.Name, targetName, StringComparison.OrdinalIgnoreCase));
            var cards = trelloRequest.SprintPlan.Cards ?? new List<TrelloCard>();

            if (listB != null)
            {
                // Swap: target name already exists; swap names, positions, and CardIds so list name and CardId stay in sync.
                var sourceSprintNum = ParseSprintNumberFromListName(sourceName);
                var targetSprintNum = ParseSprintNumberFromListName(targetName);
                var cardIdSwapMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (sourceSprintNum.HasValue && targetSprintNum.HasValue)
                {
                    var sourcePrefix = $"{sourceSprintNum.Value}-";
                    var targetPrefix = $"{targetSprintNum.Value}-";
                    var cardsA = cards.Where(c => string.Equals(c.ListName, sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
                    var cardsB = cards.Where(c => string.Equals(c.ListName, targetName, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var c in cardsA) c.ListName = targetName;
                    foreach (var c in cardsB) c.ListName = sourceName;
                    // A's cards: CardId "5-*" -> "2-*"
                    foreach (var c in cardsA.Where(c => !string.IsNullOrEmpty(c.CardId) && c.CardId.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        var newId = targetPrefix + c.CardId!.Substring(sourcePrefix.Length);
                        cardIdSwapMap[c.CardId] = newId;
                        c.CardId = newId;
                    }
                    // B's cards: CardId "2-*" -> "5-*"
                    foreach (var c in cardsB.Where(c => !string.IsNullOrEmpty(c.CardId) && c.CardId.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        var newId = sourcePrefix + c.CardId!.Substring(targetPrefix.Length);
                        cardIdSwapMap[c.CardId] = newId;
                        c.CardId = newId;
                    }
                    // Update Dependencies across all cards
                    if (cardIdSwapMap.Count > 0)
                    {
                        foreach (var c in cards.Where(c => c.Dependencies != null && c.Dependencies.Count > 0))
                        {
                            for (var i = 0; i < c.Dependencies.Count; i++)
                                if (cardIdSwapMap.TryGetValue(c.Dependencies[i], out var newDep))
                                    c.Dependencies[i] = newDep;
                        }
                        _logger.LogInformation("[SetSprintIndexInBoard] Swapped CardIds and updated Dependencies ({Count} mappings).", cardIdSwapMap.Count);
                    }
                }
                else
                {
                    // Fallback: only update ListName (no CardId swap if list names don't parse as Sprint N)
                    foreach (var c in cards.Where(c => string.Equals(c.ListName, sourceName, StringComparison.OrdinalIgnoreCase)))
                        c.ListName = targetName;
                    if (targetSprintNum.HasValue)
                    {
                        var prefix = $"{targetSprintNum.Value}-";
                        foreach (var c in cards.Where(c => c.CardId != null && c.CardId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                            c.ListName = sourceName;
                    }
                }

                lists.Remove(listA);
                lists.Remove(listB);
                if (sourceIndex > targetIndex)
                {
                    lists.Insert(sourceIndex, listB);
                    lists.Insert(targetIndex, listA);
                }
                else
                {
                    lists.Insert(targetIndex, listA);
                    lists.Insert(sourceIndex, listB);
                }
                listA.Name = targetName;
                listB.Name = sourceName;
                _logger.LogInformation("[SetSprintIndexInBoard] Swapped list '{Source}' with '{Target}' (moved to index {TargetIndex}, other to index {SourceIndex}).", sourceName, targetName, targetIndex, sourceIndex);
            }
            else
            {
                // Simple move: no list with target name
                lists.Remove(listA);
                lists.Insert(targetIndex, listA);
                listA.Name = targetName;
                foreach (var c in cards.Where(c => string.Equals(c.ListName, sourceName, StringComparison.OrdinalIgnoreCase)))
                    c.ListName = targetName;
                _logger.LogInformation("[SetSprintIndexInBoard] Moved list '{Source}' to index {TargetIndex} and renamed to '{Target}'.", sourceName, targetIndex, targetName);
            }

            // Ensure "Sprint 1" is at index 0 (user expectation: Sprint 1 stays first)
            var sprint1 = lists.FirstOrDefault(l => string.Equals(l.Name, "Sprint 1", StringComparison.OrdinalIgnoreCase));
            if (sprint1 != null)
            {
                var idx = lists.IndexOf(sprint1);
                if (idx > 0)
                {
                    lists.RemoveAt(idx);
                    lists.Insert(0, sprint1);
                    _logger.LogInformation("[SetSprintIndexInBoard] Moved 'Sprint 1' to index 0.");
                }
            }

            for (var i = 0; i < lists.Count; i++)
                lists[i].Position = i;

            var newJson = JsonSerializer.Serialize(trelloRequest);
            project.TrelloBoardJson = newJson;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var finalIndex = lists.IndexOf(listA);
            return Ok(new
            {
                Success = true,
                Message = listB != null
                    ? $"List '{sourceName}' swapped with '{targetName}' (now at index {finalIndex}); Sprint 1 at index 0."
                    : $"List '{sourceName}' moved to index {finalIndex} and renamed to '{listA.Name}'; Sprint 1 at index 0.",
                ProjectId = request.ProjectId,
                SourceListName = sourceName,
                TargetListIndex = finalIndex,
                TargetListName = listA.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in set-sprint-index-in-board for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Revert a previous set-sprint-index-in-board: move the list at RevertListIndex back to RevertToIndex and rename to RevertToName; cards belonging to that list (by CardId prefix) get ListName = RevertToName. NOT_TO_USE in normal flow.
    /// </summary>
    [HttpPost("trello/revert-sprint-index-in-board-NOT_TO_USE")]
    public async Task<ActionResult<object>> RevertSprintIndexInBoard([FromBody] RevertSprintIndexInBoardRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { Success = false, Message = "Request body is required." });
            if (request.ProjectId <= 0)
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });
            if (string.IsNullOrWhiteSpace(request.RevertToName))
                return BadRequest(new { Success = false, Message = "RevertToName is required (e.g. original list name like 'Sprint 5')." });
            if (request.RevertListIndex < 0)
                return BadRequest(new { Success = false, Message = "RevertListIndex must be 0 or greater." });
            if (request.RevertToIndex < 0)
                return BadRequest(new { Success = false, Message = "RevertToIndex must be 0 or greater." });

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                return BadRequest(new { Success = false, Message = "Project has no TrelloBoardJson." });

            TrelloProjectCreationRequest? trelloRequest;
            try
            {
                trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TrelloBoardJson for project {ProjectId}", request.ProjectId);
                return BadRequest(new { Success = false, Message = "Invalid TrelloBoardJson in project." });
            }

            if (trelloRequest!.SprintPlan?.Lists == null || trelloRequest.SprintPlan.Lists.Count == 0)
                return BadRequest(new { Success = false, Message = "No lists in TrelloBoardJson." });

            var lists = trelloRequest.SprintPlan.Lists;
            if (request.RevertListIndex >= lists.Count)
                return BadRequest(new { Success = false, Message = $"RevertListIndex {request.RevertListIndex} is out of range (lists count: {lists.Count})." });

            var list = lists[request.RevertListIndex];
            var previousName = list.Name;
            list.Name = request.RevertToName.Trim();
            lists.RemoveAt(request.RevertListIndex);
            var insertIndex = Math.Clamp(request.RevertToIndex, 0, lists.Count);
            lists.Insert(insertIndex, list);
            for (var i = 0; i < lists.Count; i++)
                lists[i].Position = i;

            var sprintNum = ParseSprintNumberFromListName(request.RevertToName);
            if (trelloRequest.SprintPlan.Cards != null && sprintNum.HasValue)
            {
                var prefix = $"{sprintNum.Value}-";
                var cardsToUpdate = trelloRequest.SprintPlan.Cards.Where(c =>
                    c.CardId != null && c.CardId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.ListName, previousName, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var card in cardsToUpdate)
                    card.ListName = list.Name;
                _logger.LogInformation("[RevertSprintIndexInBoard] Reverted list '{Previous}' at index {From} to '{To}' at index {ToIndex}, updated {Count} card(s).", previousName, request.RevertListIndex, list.Name, insertIndex, cardsToUpdate.Count);
            }

            var newJson = JsonSerializer.Serialize(trelloRequest);
            project.TrelloBoardJson = newJson;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"List reverted from index {request.RevertListIndex} to index {insertIndex} and renamed to '{list.Name}'.",
                ProjectId = request.ProjectId,
                RevertListIndex = request.RevertListIndex,
                RevertToIndex = insertIndex,
                RevertToName = list.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in revert-sprint-index-in-board for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Perfect revert: fix duplicate "Sprint 2" (rename second one to "Sprint 5", fix 5-* cards), then reorder lists to canonical order (Sprint 1, 2, 3, 4, 5, 6, 7, Bugs). No lists or cards are ever deleted—only reorder and rename.
    /// </summary>
    [HttpPost("trello/perfect-revert-sprint-board")]
    public async Task<ActionResult<object>> PerfectRevertSprintBoard([FromBody] PerfectRevertSprintBoardRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { Success = false, Message = "Request body is required." });
            if (request.ProjectId <= 0)
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                return BadRequest(new { Success = false, Message = "Project has no TrelloBoardJson." });

            TrelloProjectCreationRequest? trelloRequest;
            try
            {
                trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TrelloBoardJson for project {ProjectId}", request.ProjectId);
                return BadRequest(new { Success = false, Message = "Invalid TrelloBoardJson in project." });
            }

            if (trelloRequest!.SprintPlan?.Lists == null || trelloRequest.SprintPlan.Lists.Count == 0)
                return BadRequest(new { Success = false, Message = "No lists in TrelloBoardJson." });

            var lists = trelloRequest.SprintPlan.Lists;
            var cards = trelloRequest.SprintPlan.Cards ?? new List<TrelloCard>();

            // 1) Fix duplicate "Sprint 2": if two lists named "Sprint 2", rename the second (by current order) to "Sprint 5" and fix cards
            var sprint2Lists = lists.Where(l => string.Equals(l.Name, "Sprint 2", StringComparison.OrdinalIgnoreCase)).ToList();
            if (sprint2Lists.Count >= 2)
            {
                var secondSprint2 = sprint2Lists[1];
                secondSprint2.Name = "Sprint 5";
                var cards5 = cards.Where(c => c.CardId != null && c.CardId.StartsWith("5-", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.ListName, "Sprint 2", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var card in cards5)
                    card.ListName = "Sprint 5";
                _logger.LogInformation("[PerfectRevertSprintBoard] Renamed second 'Sprint 2' list to 'Sprint 5', updated {Count} card(s).", cards5.Count);
            }

            // 2) Canonical order: Sprint 1, Sprint 2, Sprint 3, Sprint 4, Sprint 5, Sprint 6, Sprint 7, Bugs — then any other lists (no deletion)
            var canonicalNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7", "Bugs" };
            var used = new HashSet<TrelloList>();
            var ordered = new List<TrelloList>();
            foreach (var name in canonicalNames)
            {
                var found = lists.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                if (found != null && !used.Contains(found))
                {
                    used.Add(found);
                    ordered.Add(found);
                }
            }
            foreach (var l in lists)
                if (!used.Contains(l))
                    ordered.Add(l);
            trelloRequest.SprintPlan.Lists = ordered;
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = i;

            // 3) Normalize card ListName by CardId (e.g. "2-B" -> "Sprint 2") so every card points to the correct list name
            foreach (var card in cards)
            {
                if (string.IsNullOrEmpty(card.CardId)) continue;
                var num = ParseSprintNumberFromCardId(card.CardId);
                if (num.HasValue && num.Value >= 1 && num.Value <= 7)
                {
                    var expectedList = $"Sprint {num.Value}";
                    if (!string.Equals(card.ListName, expectedList, StringComparison.OrdinalIgnoreCase))
                        card.ListName = expectedList;
                }
            }

            var newJson = JsonSerializer.Serialize(trelloRequest);
            project.TrelloBoardJson = newJson;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var orderSummary = string.Join(", ", ordered.Select(l => l.Name));
            _logger.LogInformation("[PerfectRevertSprintBoard] Project {ProjectId}: lists reordered to [{Order}]. No lists or cards deleted.", request.ProjectId, orderSummary);
            return Ok(new
            {
                Success = true,
                Message = "Lists reordered to Sprint 1, 2, 3, 4, 5, 6, 7, Bugs. No data deleted.",
                ProjectId = request.ProjectId,
                ListOrder = ordered.Select(l => l.Name).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in perfect-revert-sprint-board for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Reorder lists in TrelloBoardJson so array order and Position match left-to-right: Sprint 1, Sprint 2, ..., Sprint 7, then Bugs last. Any other list names are appended after Bugs. No cards or list names changed.
    /// </summary>
    [HttpPost("trello/arrange-board-order")]
    public async Task<ActionResult<object>> ArrangeBoardOrder([FromBody] ArrangeBoardOrderRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { Success = false, Message = "Request body is required." });
            if (request.ProjectId <= 0)
                return BadRequest(new { Success = false, Message = "ProjectId is required and must be greater than 0." });

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
            if (project == null)
                return NotFound(new { Success = false, Message = $"Project {request.ProjectId} not found." });
            if (string.IsNullOrWhiteSpace(project.TrelloBoardJson))
                return BadRequest(new { Success = false, Message = "Project has no TrelloBoardJson." });

            TrelloProjectCreationRequest? trelloRequest;
            try
            {
                trelloRequest = JsonSerializer.Deserialize<TrelloProjectCreationRequest>(project.TrelloBoardJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize TrelloBoardJson for project {ProjectId}", request.ProjectId);
                return BadRequest(new { Success = false, Message = "Invalid TrelloBoardJson in project." });
            }

            if (trelloRequest!.SprintPlan?.Lists == null || trelloRequest.SprintPlan.Lists.Count == 0)
                return BadRequest(new { Success = false, Message = "No lists in TrelloBoardJson." });

            var lists = trelloRequest.SprintPlan.Lists;
            var canonicalNames = new[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", "Sprint 7", "Bugs" };
            var used = new HashSet<TrelloList>();
            var ordered = new List<TrelloList>();
            foreach (var name in canonicalNames)
            {
                var found = lists.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                if (found != null && !used.Contains(found))
                {
                    used.Add(found);
                    ordered.Add(found);
                }
            }
            foreach (var l in lists)
                if (!used.Contains(l))
                    ordered.Add(l);
            trelloRequest.SprintPlan.Lists = ordered;
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = i;

            var newJson = JsonSerializer.Serialize(trelloRequest);
            project.TrelloBoardJson = newJson;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var orderSummary = string.Join(", ", ordered.Select(l => l.Name));
            _logger.LogInformation("[ArrangeBoardOrder] Project {ProjectId}: lists ordered by name [{Order}].", request.ProjectId, orderSummary);
            return Ok(new
            {
                Success = true,
                Message = "Lists reordered by sprint name (Sprint 1..7, Bugs last).",
                ProjectId = request.ProjectId,
                ListOrder = ordered.Select(l => l.Name).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in arrange-board-order for project {ProjectId}", request?.ProjectId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    /// <summary>Parse leading sprint number from CardId like "5-B" or "1-P-2". Returns null if not matched.</summary>
    private static int? ParseSprintNumberFromCardId(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return null;
        var first = cardId.Trim();
        var dash = first.IndexOf('-');
        if (dash <= 0) return null;
        return int.TryParse(first.Substring(0, dash), out var n) ? n : null;
    }

    /// <summary>Parse sprint number from list name like "Sprint 5" or "Sprint 1". Returns null if not matched.</summary>
    /// <summary>Trim and collapse multiple spaces to one for list name matching (e.g. "Sprint  1" -> "Sprint 1").</summary>
    private static string NormalizeListName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
    }

    private static int? ParseSprintNumberFromListName(string? listName)
    {
        if (string.IsNullOrWhiteSpace(listName)) return null;
        var trimmed = listName.Trim();
        if (trimmed.Length < 7 || !trimmed.StartsWith("Sprint ", StringComparison.OrdinalIgnoreCase)) return null;
        var numPart = trimmed.Substring(6).Trim();
        return int.TryParse(numPart, out var n) ? n : null;
    }

    // PromptType: SprintPlanning — Keep (inline; small single-block prompt for checklist parsing)
    private async Task<List<string>?> ParseChecklistWithAiAsync(string text)
    {
        var prompt = $@"Convert the following into a JSON array of short checklist item names suitable for Trello (each under 100 characters).
Return ONLY a valid JSON array of strings, e.g. [""Item 1"", ""Item 2""]. No other text.

Text:
{text}";
        var response = await _aiService.GenerateTextResponseAsync(prompt);
        if (string.IsNullOrWhiteSpace(response)) return null;
        try
        {
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```")) cleaned = cleaned.Replace("```json", "").Replace("```", "").Trim();
            var list = JsonSerializer.Deserialize<List<string>>(cleaned);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().Length > 100 ? s.Trim().Substring(0, 97) + "..." : s.Trim()).ToList();
        }
        catch { return null; }
    }

    // PromptType: SprintPlanning — Keep (inline; small single-block prompt for checklist normalization)
    private async Task<List<string>?> NormalizeChecklistWithAiAsync(List<string> items)
    {
        var input = JsonSerializer.Serialize(items);
        var prompt = $@"Normalize these checklist items into a JSON array of short Trello check item names (each under 100 characters). Keep the same meaning and order. Return ONLY a valid JSON array of strings.

Items: {input}";
        var response = await _aiService.GenerateTextResponseAsync(prompt);
        if (string.IsNullOrWhiteSpace(response)) return null;
        try
        {
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```")) cleaned = cleaned.Replace("```json", "").Replace("```", "").Trim();
            var list = JsonSerializer.Deserialize<List<string>>(cleaned);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().Length > 100 ? s.Trim().Substring(0, 97) + "..." : s.Trim()).ToList();
        }
        catch { return null; }
    }

    /// <summary>
    /// Utility endpoint to remove all guests from ALL Trello boards in the workspace.
    /// This identifies guests by comparing board members with workspace members.
    /// Anyone on a board who is NOT a workspace member is considered a guest.
    /// WARNING: This is a destructive operation that will remove guests from all boards.
    /// </summary>
    [HttpPost("trello/remove-all-guests")]
    public async Task<ActionResult<object>> RemoveAllTrelloGuests([FromBody] RemoveAllGuestsRequest? request = null)
    {
        try
        {
            var confirmation = request?.Confirmation ?? false;
            if (!confirmation)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "Confirmation required. Set 'confirmation' to true in the request body to proceed with removing all guests."
                });
            }

            _logger.LogWarning("🗑️ [TRELLO] Starting removal of all guests from all boards. This is a destructive operation!");

            var trelloApiKey = _configuration["Trello:ApiKey"];
            var trelloApiToken = _configuration["Trello:ApiToken"];

            if (string.IsNullOrWhiteSpace(trelloApiKey) || string.IsNullOrWhiteSpace(trelloApiToken))
            {
                return BadRequest(new { Success = false, Message = "Trello API credentials not configured" });
            }

            using var httpClient = _httpClientFactory.CreateClient();
            var stats = new RemoveAllGuestsStats();
            var errors = new List<string>();

            // Step 1: Get all boards in the workspace
            _logger.LogInformation("📋 [TRELLO] Getting all boards");
            var boardsResult = await _trelloService.ListAllBoardsAsync();
            var boardsJson = JsonSerializer.Serialize(boardsResult);
            var boardsElement = JsonSerializer.Deserialize<JsonElement>(boardsJson);

            var boards = new List<(string Id, string Name)>();
            if (boardsElement.TryGetProperty("Boards", out var boardsProp) && boardsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var board in boardsProp.EnumerateArray())
                {
                    var boardId = board.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
                    var boardName = board.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
                    if (!string.IsNullOrEmpty(boardId) && !string.IsNullOrEmpty(boardName))
                    {
                        boards.Add((boardId, boardName));
                    }
                }
            }

            _logger.LogInformation("📋 [TRELLO] Found {Count} boards", boards.Count);
            stats.TotalBoards = boards.Count;

            // Step 2: Get all workspace members
            _logger.LogInformation("👥 [TRELLO] Getting workspace members");
            var workspaceMembers = new HashSet<string>(); // Set of member IDs who are workspace members

            // Get user's organizations
            var orgsUrl = $"https://api.trello.com/1/members/me/organizations?key={trelloApiKey}&token={trelloApiToken}";
            var orgsResponse = await httpClient.GetAsync(orgsUrl);

            if (orgsResponse.IsSuccessStatusCode)
            {
                var orgsContent = await orgsResponse.Content.ReadAsStringAsync();
                var orgsData = JsonSerializer.Deserialize<JsonElement[]>(orgsContent);

                foreach (var org in orgsData ?? Array.Empty<JsonElement>())
                {
                    var orgId = org.TryGetProperty("id", out var orgIdProp) ? orgIdProp.GetString() : null;
                    if (string.IsNullOrEmpty(orgId))
                        continue;

                    // Get members of this organization/workspace
                    var orgMembersUrl = $"https://api.trello.com/1/organizations/{orgId}/members?key={trelloApiKey}&token={trelloApiToken}";
                    var orgMembersResponse = await httpClient.GetAsync(orgMembersUrl);

                    if (orgMembersResponse.IsSuccessStatusCode)
                    {
                        var orgMembersContent = await orgMembersResponse.Content.ReadAsStringAsync();
                        var orgMembersData = JsonSerializer.Deserialize<JsonElement[]>(orgMembersContent);

                        foreach (var member in orgMembersData ?? Array.Empty<JsonElement>())
                        {
                            var memberId = member.TryGetProperty("id", out var memberIdProp) ? memberIdProp.GetString() : null;
                            if (!string.IsNullOrEmpty(memberId))
                            {
                                workspaceMembers.Add(memberId);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("👥 [TRELLO] Found {Count} workspace members", workspaceMembers.Count);
            stats.WorkspaceMembersCount = workspaceMembers.Count;

            // Step 3: For each board, get members and remove guests
            foreach (var (boardId, boardName) in boards)
            {
                try
                {
                    _logger.LogInformation("🔍 [TRELLO] Processing board: {BoardName} ({BoardId})", boardName, boardId);

                    // Get board members
                    var boardMembersUrl = $"https://api.trello.com/1/boards/{boardId}/members?key={trelloApiKey}&token={trelloApiToken}";
                    var boardMembersResponse = await httpClient.GetAsync(boardMembersUrl);

                    if (!boardMembersResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await boardMembersResponse.Content.ReadAsStringAsync();
                        var errorMsg = $"Failed to get members for board {boardName} ({boardId}): {boardMembersResponse.StatusCode} - {errorContent}";
                        _logger.LogError("❌ [TRELLO] {Error}", errorMsg);
                        errors.Add(errorMsg);
                        stats.Errors++;
                        continue;
                    }

                    var boardMembersContent = await boardMembersResponse.Content.ReadAsStringAsync();
                    var boardMembersData = JsonSerializer.Deserialize<JsonElement[]>(boardMembersContent);

                    var guestsToRemove = new List<(string MemberId, string Email, string FullName)>();

                    foreach (var member in boardMembersData ?? Array.Empty<JsonElement>())
                    {
                        var memberId = member.TryGetProperty("id", out var memberIdProp) ? memberIdProp.GetString() : null;
                        var email = member.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                        var fullName = member.TryGetProperty("fullName", out var nameProp) ? nameProp.GetString() : null;

                        if (string.IsNullOrEmpty(memberId))
                            continue;

                        // If member is NOT in workspace members set, they are a guest
                        if (!workspaceMembers.Contains(memberId))
                        {
                            guestsToRemove.Add((memberId, email ?? "unknown", fullName ?? "unknown"));
                        }
                    }

                    _logger.LogInformation("👤 [TRELLO] Found {Count} guests on board {BoardName}", guestsToRemove.Count, boardName);
                    stats.TotalGuestsFound += guestsToRemove.Count;

                    // Step 4: Remove each guest from the board
                    foreach (var (memberId, email, fullName) in guestsToRemove)
                    {
                        try
                        {
                            var removeUrl = $"https://api.trello.com/1/boards/{boardId}/members/{memberId}?key={trelloApiKey}&token={trelloApiToken}";
                            _logger.LogWarning("🗑️ [TRELLO] Removing guest {FullName} ({Email}) from board {BoardName}", fullName, email, boardName);

                            var removeResponse = await httpClient.DeleteAsync(removeUrl);

                            if (removeResponse.IsSuccessStatusCode)
                            {
                                _logger.LogInformation("✅ [TRELLO] Successfully removed guest {FullName} ({Email}) from board {BoardName}", fullName, email, boardName);
                                stats.GuestsRemoved++;
                            }
                            else
                            {
                                var errorContent = await removeResponse.Content.ReadAsStringAsync();
                                var errorMsg = $"Failed to remove guest {fullName} ({email}) from board {boardName}: {removeResponse.StatusCode} - {errorContent}";
                                _logger.LogError("❌ [TRELLO] {Error}", errorMsg);
                                errors.Add(errorMsg);
                                stats.Errors++;
                            }

                            // Small delay to avoid rate limiting
                            await Task.Delay(200);
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Error removing guest {fullName} ({email}) from board {boardName}: {ex.Message}";
                            _logger.LogError(ex, "❌ [TRELLO] {Error}", errorMsg);
                            errors.Add(errorMsg);
                            stats.Errors++;
                        }
                    }

                    stats.BoardsProcessed++;
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error processing board {boardName} ({boardId}): {ex.Message}";
                    _logger.LogError(ex, "❌ [TRELLO] {Error}", errorMsg);
                    errors.Add(errorMsg);
                    stats.Errors++;
                }
            }

            _logger.LogWarning("✅ [TRELLO] Guest removal process completed. Guests removed: {Removed}, Errors: {Errors}", stats.GuestsRemoved, stats.Errors);

            return Ok(new
            {
                Success = true,
                Message = $"Guest removal process completed. {stats.GuestsRemoved} guest(s) removed from {stats.BoardsProcessed} board(s).",
                Stats = stats,
                Errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRELLO] Error during guest removal process");
            return StatusCode(500, new
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}",
                Error = ex.ToString()
            });
        }
    }
}

public class SetPasswordForAllRequest
{
    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

public class ChangeProjectsStats
{
    public int StudentsUpdatedWithBoard { get; set; }
    public int StudentsUpdatedWithoutBoard { get; set; }
    public int StudentsFixedNonExistent { get; set; }
    public int ProjectBoardsUpdated { get; set; }
    public int ProjectBoardsFixedNonExistent { get; set; }
    public int OrphanedProjectsFound { get; set; }
    public int ProjectsDeleted { get; set; }
    public int ProjectModulesDeleted { get; set; }
    public int ProjectModulesDeletedNonExistent { get; set; }
    public int JoinRequestsDeleted { get; set; }
    
    public int TotalStudentsUpdated => StudentsUpdatedWithBoard + StudentsUpdatedWithoutBoard + StudentsFixedNonExistent;
    public int TotalProjectBoardsUpdated => ProjectBoardsUpdated + ProjectBoardsFixedNonExistent;
    public int TotalProjectsDeleted => ProjectsDeleted;
    public int TotalProjectModulesDeleted => ProjectModulesDeleted + ProjectModulesDeletedNonExistent;
}

public class DeployBackendRequest
{
    public string? ProgrammingLanguage { get; set; }
}

public class DeployFrontendRequest
{
    public string? ProjectTitle { get; set; }
}

public class AddWorkflowRequest
{
    [Required]
    public string RepositoryUrl { get; set; } = string.Empty;
    
    [Required]
    public string ProgrammingLanguage { get; set; } = string.Empty;
}

public class DeleteAllNeonDatabasesRequest
{
    /// <summary>
    /// Must be set to true to confirm deletion. This is a safety measure.
    /// </summary>
    [Required]
    public bool Confirmation { get; set; } = false;

    /// <summary>
    /// If true, skips deletion of reserved database names (neondb, postgres, template0, template1)
    /// </summary>
    public bool SkipReserved { get; set; } = true;
}public class DeleteAllNeonDatabasesStats
{
    public int ProjectsFound { get; set; }
    public int BranchesFound { get; set; }
    public int DatabasesFound { get; set; }
    public int DatabasesDeleted { get; set; }
    public int DatabasesSkipped { get; set; }
    public int Errors { get; set; }
}

public class StoreTrelloBoardJsonRequest
{
    public int ProjectId { get; set; }
    public List<int> StudentIds { get; set; } = new();
}

/// <summary>Request for POST /api/Utilities/trello/set-sprint-index-in-board</summary>
public class SetSprintIndexInBoardRequest
{
    public int ProjectId { get; set; }
    /// <summary>Current name of the list (sprint) to move, e.g. "Sprint 5".</summary>
    public string? SourceListName { get; set; }
    /// <summary>0-based index where the list should be placed (0 = first list).</summary>
    public int TargetListIndex { get; set; }
    /// <summary>New name for the list after moving, e.g. "Sprint 2".</summary>
    public string? TargetListName { get; set; }
}

/// <summary>Request for POST /api/Utilities/trello/revert-sprint-index-in-board-NOT_TO_USE</summary>
public class RevertSprintIndexInBoardRequest
{
    public int ProjectId { get; set; }
    /// <summary>Current 0-based index of the list to revert (the list that was moved).</summary>
    public int RevertListIndex { get; set; }
    /// <summary>Name to restore for that list, e.g. "Sprint 5".</summary>
    public string? RevertToName { get; set; }
    /// <summary>0-based index to move the list back to.</summary>
    public int RevertToIndex { get; set; }
}

/// <summary>Request for POST /api/Utilities/trello/perfect-revert-sprint-board</summary>
public class PerfectRevertSprintBoardRequest
{
    public int ProjectId { get; set; }
}

/// <summary>Request for POST /api/Utilities/trello/arrange-board-order</summary>
public class ArrangeBoardOrderRequest
{
    public int ProjectId { get; set; }
}

/// <summary>Request for POST /api/Utilities/trello/sprint-override</summary>
public class SprintOverrideRequest
{
    public int ProjectId { get; set; }
    public int SprintNumber { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Checklist as array of items (use CheckListText for one string with line breaks).</summary>
    public List<string>? CheckList { get; set; }
    /// <summary>Checklist as a single string: items separated by newlines. In JSON use \n; when sending form-data, literal line breaks are allowed.</summary>
    public string? CheckListText { get; set; }
    /// <summary>When set, only cards in the sprint that have this role as a label (Labels or RoleName) are updated; otherwise the list (sprint) is updated.</summary>
    public string? RoleName { get; set; }
    /// <summary>When true (default), use AI to parse/normalize checklist text into Trello-friendly items.</summary>
    public bool? UseAiToParseCheckList { get; set; }
    /// <summary>0-based position on the board (0 = first list). When set, the sprint list is moved to this index after update. Ignored when using sprint-override-form (lists are reordered to canonical at end).</summary>
    public int? OrderInBoard { get; set; }
    /// <summary>When true, create a new card for the role without overriding existing cards; new card is placed after the last same-role card. Default false.</summary>
    public bool IsNewCard { get; set; }
    /// <summary>If non-empty, override the card's CardId (e.g. "1-B"). Leave null/empty to keep existing.</summary>
    public string? CardId { get; set; }
    /// <summary>Override the card's ModuleId. Null or empty string sets it to "" (clears it).</summary>
    public string? ModuleId { get; set; }
    /// <summary>If non-null and non-empty, override the card's Dependencies (e.g. ["1-P"]). Leave null/empty to keep existing.</summary>
    public List<string>? Dependencies { get; set; }
}

/// <summary>Form request for POST /api/Utilities/trello/sprint-override-form (allows literal newlines in CheckListText). Lists are always reordered to canonical order (Sprint 1..7, Bugs) at the end; OrderInBoard is ignored.</summary>
public class SprintOverrideFormRequest
{
    public int ProjectId { get; set; }
    public int SprintNumber { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CheckListText { get; set; }
    public string? RoleName { get; set; }
    public bool? UseAiToParseCheckList { get; set; }
    /// <summary>Ignored: sprint-override-form always reorders lists to canonical order at the end. Kept for API compatibility.</summary>
    public int? OrderInBoard { get; set; }
    /// <summary>When true, create a new card for the role without overriding existing cards; new card is placed after the last same-role card. Default false.</summary>
    public bool IsNewCard { get; set; }
    /// <summary>If non-empty, override the card's CardId (e.g. "1-B"). Leave empty to keep existing.</summary>
    public string? CardId { get; set; }
    /// <summary>Override the card's ModuleId. Null or empty string sets it to "" (clears it).</summary>
    public string? ModuleId { get; set; }
    /// <summary>If non-empty, override the card's Dependencies (comma-separated card IDs, e.g. "1-P" or "2-B, 2-U"). Leave empty to keep existing.</summary>
    public string? Dependencies { get; set; }
}

public class RemoveAllGuestsRequest
{
    /// <summary>
    /// Must be set to true to confirm removal. This is a safety measure.
    /// </summary>
    [Required]
    public bool Confirmation { get; set; } = false;
}

public class RemoveAllGuestsStats
{
    public int TotalBoards { get; set; }
    public int BoardsProcessed { get; set; }
    public int WorkspaceMembersCount { get; set; }
    public int TotalGuestsFound { get; set; }
    public int GuestsRemoved { get; set; }
    public int Errors { get; set; }
}

/// <summary>Request for POST api/Utilities/github/resend-invitation</summary>
public class ResendGitHubInvitationRequest
{
    /// <summary>GitHub username to resend the invite to (e.g. oferlav).</summary>
    public string? GitHubUsername { get; set; }

    /// <summary>Full repo URL. For frontend use .../697e00d2f9cb8d26a7271afa; for backend use .../backend_697e00d2f9cb8d26a7271afa. Use this OR (Owner + Repository) OR (BoardId + RepoType).</summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>Repo owner (e.g. skill-in-projects). Use with Repository if not using RepositoryUrl or BoardId.</summary>
    public string? Owner { get; set; }

    /// <summary>Repo name without .git. For frontend = board ID (e.g. 697e00d2f9cb8d26a7271afa); for backend = backend_{boardId}. Use with Owner, or use BoardId + RepoType instead.</summary>
    public string? Repository { get; set; }

    /// <summary>Trello board ID (e.g. 697e00d2f9cb8d26a7271afa). Use with RepoType to target frontend or backend repo.</summary>
    public string? BoardId { get; set; }

    /// <summary>Must be "Frontend" or "Backend" when using BoardId. Frontend repo = BoardId; backend repo = backend_{BoardId}.</summary>
    public string? RepoType { get; set; }
}

/// <summary>Request for POST api/Utilities/trello/invite-to-board</summary>
public class TrelloInviteToBoardRequest
{
    /// <summary>Trello board ID (e.g. 698f522ddffd39fa9da1a6a7).</summary>
    public string? BoardId { get; set; }

    /// <summary>Email addresses to invite to the board (e.g. PM who was not added before allowBillableGuest fix).</summary>
    public List<string>? Emails { get; set; }
}
