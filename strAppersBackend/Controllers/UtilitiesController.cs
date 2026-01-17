using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
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

    public UtilitiesController(
        ApplicationDbContext context, 
        ILogger<UtilitiesController> logger, 
        IPasswordHasherService passwordHasher, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IGitHubService gitHubService)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _gitHubService = gitHubService;
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
