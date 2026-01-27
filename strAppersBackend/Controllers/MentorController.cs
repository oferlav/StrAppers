using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using Npgsql;

namespace strAppersBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MentorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MentorController> _logger;
        private readonly ITrelloService _trelloService;
        private readonly IGitHubService _githubService;
        private readonly IMentorIntentService _intentService;
        private readonly ICodeReviewAgent _codeReviewAgent;
        private readonly PromptConfig _promptConfig;
        private readonly TrelloConfig _trelloConfig;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAIService _aiService;
        private readonly DeploymentsConfig _deploymentsConfig;
        private readonly IOptions<TestingConfig> _testingConfig;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public MentorController(
            ApplicationDbContext context,
            ILogger<MentorController> logger,
            ITrelloService trelloService,
            IGitHubService githubService,
            IMentorIntentService intentService,
            ICodeReviewAgent codeReviewAgent,
            IOptions<PromptConfig> promptConfig,
            IOptions<TrelloConfig> trelloConfig,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IAIService aiService,
            IOptions<DeploymentsConfig> deploymentsConfig,
            IOptions<TestingConfig> testingConfig,
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _trelloService = trelloService;
            _githubService = githubService;
            _intentService = intentService;
            _codeReviewAgent = codeReviewAgent;
            _promptConfig = promptConfig.Value;
            _trelloConfig = trelloConfig.Value;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _aiService = aiService;
            _deploymentsConfig = deploymentsConfig.Value;
            _testingConfig = testingConfig;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Get mentor context for a specific student and sprint
        /// Gathers all necessary information to construct a context-aware prompt for the mentor chatbot
        /// </summary>
        [HttpGet("use/get-mentor-context/{studentId}/{sprintId}")]
        public async Task<ActionResult<object>> GetMentorContext(int studentId, int sprintId)
        {
            try
            {
                _logger.LogInformation("Getting mentor context for StudentId: {StudentId}, SprintId: {SprintId}", studentId, sprintId);

                // A. Fetch User Profile
                var student = await _context.Students
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .Include(s => s.ProgrammingLanguage)
                    .Include(s => s.ProjectBoard)
                        .ThenInclude(pb => pb.Project)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    _logger.LogWarning("Student {StudentId} not found", studentId);
                    return NotFound(new { Success = false, Message = $"Student with ID {studentId} not found" });
                }

                if (string.IsNullOrEmpty(student.BoardId))
                {
                    _logger.LogWarning("Student {StudentId} does not have a board assigned", studentId);
                    return BadRequest(new { Success = false, Message = $"Student {studentId} does not have a Trello board assigned" });
                }

                var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
                var roleName = activeRole?.Role?.Name ?? "Team Member";
                var programmingLanguage = student.ProgrammingLanguage?.Name ?? "Not specified";
                var project = student.ProjectBoard?.Project;

                if (project == null)
                {
                    _logger.LogWarning("Project not found for student {StudentId}", studentId);
                    return BadRequest(new { Success = false, Message = $"Project not found for student {studentId}" });
                }

                // Get Trello board lists to find the sprint list
                var listsResult = await GetBoardListsAsync(student.BoardId);
                
                string? sprintListId = null;
                string? sprintListName = null;
                
                // Special handling for sprintId=0 (Bugs sprint)
                if (sprintId == 0)
                {
                    // Look for "Bugs" list
                    foreach (var listObj in listsResult)
                    {
                        var listJson = JsonSerializer.Serialize(listObj);
                        var listElement = JsonSerializer.Deserialize<JsonElement>(listJson);
                        
                        var name = listElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                        var id = listElement.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";
                        
                        if (!string.IsNullOrEmpty(name) && name.Equals("Bugs", StringComparison.OrdinalIgnoreCase))
                        {
                            sprintListId = id;
                            sprintListName = name;
                            break;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(sprintListId))
                    {
                        _logger.LogWarning("Bugs list not found for board {BoardId}", student.BoardId);
                        return BadRequest(new { Success = false, Message = "Bugs list not found on Trello board" });
                    }
                }
                else
                {
                    // Find the sprint list by matching sprint number
                    foreach (var listObj in listsResult)
                    {
                        var listJson = JsonSerializer.Serialize(listObj);
                        var listElement = JsonSerializer.Deserialize<JsonElement>(listJson);
                        
                        var name = listElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                        var id = listElement.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";
                        
                        // Sprint lists are typically named "Sprint 1", "Sprint 2", etc.
                        if (!string.IsNullOrEmpty(name) && 
                            (name.Equals($"Sprint {sprintId}", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals($"Sprint{sprintId}", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains($"Sprint {sprintId}", StringComparison.OrdinalIgnoreCase)))
                        {
                            sprintListId = id;
                            sprintListName = name;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(sprintListId))
                    {
                        _logger.LogWarning("Sprint {SprintId} list not found for board {BoardId}", sprintId, student.BoardId);
                        return BadRequest(new { Success = false, Message = $"Sprint {sprintId} not found on Trello board" });
                    }
                }

                // B. Fetch Current Trello Tasks for current sprint (filtered by role label)
                var userTasksResult = await _trelloService.GetCardsAndListsByLabelAsync(student.BoardId, roleName);
                var userTasksJson = JsonSerializer.Serialize(userTasksResult);
                var userTasksElement = JsonSerializer.Deserialize<JsonElement>(userTasksJson);

                var userTasks = new List<object>();
                if (userTasksElement.TryGetProperty("Cards", out var cardsProp) && cardsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var card in cardsProp.EnumerateArray())
                    {
                        var cardListId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : "";
                        // Only include cards from the current sprint list
                        if (cardListId == sprintListId)
                        {
                            var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";
                            
                            // C. Fetch Trello card custom fields
                            var customFields = await GetTrelloCardCustomFieldsAsync(cardId, student.BoardId);
                            
                            // Get checklist items
                            var checklistItems = new List<string>();
                            if (card.TryGetProperty("Checklists", out var checklistsProp) && checklistsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var checklist in checklistsProp.EnumerateArray())
                                {
                                    if (checklist.TryGetProperty("CheckItems", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in itemsProp.EnumerateArray())
                                        {
                                            var itemName = item.TryGetProperty("Name", out var itemNameProp) ? itemNameProp.GetString() : "";
                                            if (!string.IsNullOrEmpty(itemName))
                                            {
                                                checklistItems.Add(itemName);
                                            }
                                        }
                                    }
                                }
                            }

                            userTasks.Add(new
                            {
                                Id = cardId,
                                Name = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "",
                                Description = card.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                                Closed = card.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false,
                                DueDate = card.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : null,
                                CustomFields = customFields,
                                ChecklistItems = checklistItems
                            });
                        }
                    }
                }

                // D. Fetch Project Module Description (for each task's ModuleId)
                var moduleDescriptions = new Dictionary<string, string>();
                foreach (var task in userTasks)
                {
                    var taskObj = JsonSerializer.Serialize(task);
                    var taskElement = JsonSerializer.Deserialize<JsonElement>(taskObj);
                    if (taskElement.TryGetProperty("CustomFields", out var cfProp) && cfProp.ValueKind == JsonValueKind.Object)
                    {
                        if (cfProp.TryGetProperty("ModuleId", out var moduleIdProp))
                        {
                            var moduleIdStr = "";
                            
                            // Handle different JSON value types
                            if (moduleIdProp.ValueKind == JsonValueKind.String)
                            {
                                moduleIdStr = moduleIdProp.GetString() ?? "";
                            }
                            else if (moduleIdProp.ValueKind == JsonValueKind.Number)
                            {
                                moduleIdStr = moduleIdProp.GetRawText();
                            }
                            
                            // Clean up the string - remove quotes and whitespace
                            if (!string.IsNullOrEmpty(moduleIdStr))
                            {
                                // Remove surrounding quotes if present (handles "\"12345\"" case)
                                moduleIdStr = moduleIdStr.Trim().Trim('"').Trim('\'').Trim();
                                
                                // Try to parse as integer
                                if (int.TryParse(moduleIdStr, out int moduleId))
                                {
                                    _logger.LogDebug("Looking up module with ID {ModuleId} for project {ProjectId}", moduleId, project.Id);
                                    
                                    var module = await _context.ProjectModules
                                        .FirstOrDefaultAsync(pm => pm.Id == moduleId && pm.ProjectId == project.Id);
                                    
                                    if (module != null)
                                    {
                                        if (!string.IsNullOrEmpty(module.Description))
                                        {
                                            moduleDescriptions[moduleIdStr] = module.Description;
                                            _logger.LogDebug("Found module description for ModuleId {ModuleId}: {Description}", 
                                                moduleId, module.Description.Substring(0, Math.Min(50, module.Description.Length)));
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Module {ModuleId} found but has no description", moduleId);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Module {ModuleId} not found for project {ProjectId}", moduleId, project.Id);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to parse ModuleId as integer: '{ModuleIdStr}' (original value)", moduleIdStr);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No ModuleId custom field found in task custom fields");
                        }
                    }
                }

                // E. Fetch GitHub Repository Files - using role-based repository selection
                var githubFiles = new List<string>();
                object? githubCommitSummary = null;
                
                // Get appropriate repository URL(s) based on role
                var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                
                // Determine which repos to fetch based on role
                var reposToFetch = new List<(string Url, string Type)>();
                if (!string.IsNullOrEmpty(frontendRepoUrl))
                {
                    reposToFetch.Add((frontendRepoUrl, "frontend"));
                }
                if (!string.IsNullOrEmpty(backendRepoUrl))
                {
                    reposToFetch.Add((backendRepoUrl, "backend"));
                }
                
                if (reposToFetch.Any())
                {
                    try
                    {
                        var allFiles = new List<string>();
                        var allCommitSummaries = new List<object>();
                        
                        // Fetch files and commits from each repository
                        foreach (var (repoUrl, repoType) in reposToFetch)
                        {
                            // Extract repo name from GitHub URL (format: https://github.com/owner/repo)
                            var repoName = repoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/');
                            var repoFiles = await GetGitHubRepositoryFilesAsync(repoName);
                            
                            // Prefix files with repo type for clarity
                            if (isFullstack)
                            {
                                allFiles.AddRange(repoFiles.Select(f => $"[{repoType.ToUpper()}] {f}"));
                            }
                            else
                            {
                                allFiles.AddRange(repoFiles);
                            }
                            
                            // Fetch GitHub commit summary for developer roles only
                            var isDeveloperRole = !string.IsNullOrEmpty(roleName) && 
                                (roleName.Contains("Developer", StringComparison.OrdinalIgnoreCase) || 
                                 roleName.Contains("Programmer", StringComparison.OrdinalIgnoreCase) ||
                                 roleName.Contains("Engineer", StringComparison.OrdinalIgnoreCase));
                            
                            if (isDeveloperRole)
                            {
                                var commitSummary = await GetGitHubCommitSummaryAsync(repoUrl, student.GithubUser);
                                if (commitSummary != null)
                                {
                                    allCommitSummaries.Add(commitSummary);
                                }
                            }
                        }
                        
                        githubFiles = allFiles;
                        
                        // Combine commit summaries for fullstack developers
                        if (isFullstack && allCommitSummaries.Any())
                        {
                            githubCommitSummary = CombineCommitSummaries(allCommitSummaries);
                        }
                        else if (allCommitSummaries.Any())
                        {
                            githubCommitSummary = allCommitSummaries.First();
                        }
                    }
                    catch (Exception ex)
                    {
                        var repoList = string.Join(", ", reposToFetch.Select(r => r.Url));
                        _logger.LogWarning(ex, "Failed to fetch GitHub files for repos: {Repos}", repoList);
                    }
                }
                else
                {
                    _logger.LogWarning("No GitHub repository URLs found for student {StudentId} with role {Role}", studentId, roleName);
                }

                // F. Fetch Team Members (names and roles)
                var teamMembers = new List<object>();
                if (!string.IsNullOrEmpty(student.BoardId))
                {
                    var teamStudents = await _context.Students
                        .Include(s => s.StudentRoles)
                            .ThenInclude(sr => sr.Role)
                        .Where(s => s.BoardId == student.BoardId && s.Id != studentId)
                        .ToListAsync();

                    foreach (var teamStudent in teamStudents)
                    {
                        var teamRole = teamStudent.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
                        teamMembers.Add(new
                        {
                            Id = teamStudent.Id,
                            FirstName = teamStudent.FirstName,
                            LastName = teamStudent.LastName,
                            RoleName = teamRole?.Role?.Name ?? "Team Member"
                        });
                    }
                }

                // G. Fetch Team Member Tasks for current sprint
                var teamMemberTasks = new List<object>();
                foreach (var teamMember in teamMembers)
                {
                    var memberObj = JsonSerializer.Serialize(teamMember);
                    var memberElement = JsonSerializer.Deserialize<JsonElement>(memberObj);
                    var memberRoleName = memberElement.TryGetProperty("RoleName", out var roleProp) ? roleProp.GetString() : "";
                    
                    if (!string.IsNullOrEmpty(memberRoleName))
                    {
                        var memberTasksResult = await _trelloService.GetCardsAndListsByLabelAsync(student.BoardId, memberRoleName);
                        var memberTasksJson = JsonSerializer.Serialize(memberTasksResult);
                        var memberTasksElement = JsonSerializer.Deserialize<JsonElement>(memberTasksJson);

                        if (memberTasksElement.TryGetProperty("Cards", out var memberCardsProp) && memberCardsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var card in memberCardsProp.EnumerateArray())
                            {
                                var cardListId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : "";
                                // Only include cards from the current sprint list
                                if (cardListId == sprintListId)
                                {
                                    teamMemberTasks.Add(new
                                    {
                                        TeamMemberFirstName = memberElement.TryGetProperty("FirstName", out var fnProp) ? fnProp.GetString() : "",
                                        TeamMemberRoleName = memberRoleName,
                                        TaskName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "",
                                        IsClosed = card.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false
                                    });
                                }
                            }
                        }
                    }
                }

                // Construct the formatted prompt
                var currentTaskDetails = FormatTaskDetails(userTasks);
                var moduleDescription = "No current tasks";
                if (userTasks.Count > 0)
                {
                    var firstTaskJson = JsonSerializer.Serialize(userTasks[0]);
                    var firstTaskElement = JsonSerializer.Deserialize<JsonElement>(firstTaskJson);
                    moduleDescription = GetModuleDescriptionForFirstTask(firstTaskElement, moduleDescriptions);
                }
                
                var teamMembersList = FormatTeamMembers(teamMembers);
                var teamMemberTasksList = FormatTeamMemberTasks(teamMemberTasks);
                var githubFilesList = string.Join("\n", githubFiles);

                // Format sprint identifier for prompt (show "Bugs" when sprintId=0)
                var sprintIdentifier = sprintId == 0 ? "Bugs" : sprintId.ToString();
                var sprintDisplayName = sprintId == 0 ? "Bugs" : (sprintListName ?? $"Sprint {sprintId}");
                
                var formattedPrompt = string.Format(
                    _promptConfig.Mentor.UserPromptTemplate,
                    student.FirstName,                    // {0} - User's first name
                    roleName,                              // {1} - Role
                    sprintIdentifier,                      // {2} - Sprint number (or "Bugs" for sprintId=0)
                    sprintDisplayName,                     // {3} - Module name (using sprint name as module context)
                    currentTaskDetails,                    // {4} - Task name/description
                    programmingLanguage,                   // {5} - Programming language
                    FormatTaskDetails(userTasks),          // {6} - Current task details
                    moduleDescription,                     // {7} - Project module description
                    teamMembersList,                       // {8} - Team members
                    teamMemberTasksList,                   // {9} - Team member tasks
                    githubFilesList,                       // {10} - GitHub files
                    ""                                     // {11} - User question (empty for context gathering)
                );

                return Ok(new
                {
                    Success = true,
                    Context = new
                    {
                        StudentId = studentId,
                        SprintId = sprintId,
                        UserProfile = new
                        {
                            FirstName = student.FirstName,
                            Role = roleName,
                            ProgrammingLanguage = programmingLanguage
                        },
                        CurrentTasks = userTasks,
                        ModuleDescriptions = moduleDescriptions,
                        TeamMembers = teamMembers,
                        TeamMemberTasks = teamMemberTasks,
                        GitHubFiles = githubFiles,
                        GitHubCommitSummary = githubCommitSummary,
                        NextTeamMeeting = new
                        {
                            Time = student.ProjectBoard?.NextMeetingTime,
                            Url = student.ProjectBoard?.NextMeetingUrl
                        },
                        DatabasePassword = student.ProjectBoard?.DBPassword
                    },
                    SystemPrompt = _promptConfig.Mentor.SystemPrompt,
                    UserPrompt = formattedPrompt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor context for StudentId: {StudentId}, SprintId: {SprintId}", studentId, sprintId);
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get the structured system prompt that is sent to the AI service
        /// Returns the base system prompt with Platform Context & Vision and Knowledge Limitations prepended
        /// Response includes formatted versions with proper line breaks for readability
        /// </summary>
        [HttpGet("use/system-prompt")]
        public ActionResult<object> GetSystemPrompt()
        {
            try
            {
                var baseSystemPrompt = _promptConfig.Mentor.SystemPrompt;
                var platformContext = GetPlatformContextAndLimitations();
                var fullSystemPrompt = platformContext + baseSystemPrompt;

                // Return the prompts - JSON strings preserve \n characters which will be rendered as line breaks
                // when the JSON is parsed and displayed
                return Ok(new
                {
                    Success = true,
                    SystemPrompt = fullSystemPrompt,
                    PlatformContextAndLimitations = platformContext,
                    BaseSystemPrompt = baseSystemPrompt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system prompt");
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
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

        /// <summary>
        /// Get GitHub commit summary (recent commits with file changes summary) for mentor context
        /// Optimized to save tokens by summarizing instead of including full diffs
        /// Supports multiple repositories for fullstack developers
        /// </summary>
        private async Task<object?> GetGitHubCommitSummaryAsync(string? githubRepoUrl, string? githubUsername)
        {
            if (string.IsNullOrEmpty(githubRepoUrl) || string.IsNullOrEmpty(githubUsername))
            {
                return null;
            }

            try
            {
                // Parse GitHub repo URL
                var repoParts = githubRepoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/').Split('/');
                if (repoParts.Length < 2)
                {
                    return null;
                }

                var owner = repoParts[0];
                var repo = repoParts[1];

                // Get GitHub access token for API calls
                var accessToken = _configuration["GitHub:AccessToken"];

                // Get recent commits (limit to 5 for token efficiency)
                var recentCommits = await _githubService.GetRecentCommitsAsync(owner, repo, githubUsername, 5, accessToken);
                if (!recentCommits.Any())
                {
                    return new
                    {
                        HasCommits = false,
                        CommitCount = 0,
                        RecentCommits = new List<object>()
                    };
                }

                // Build summary of commits with file changes (without full diffs to save tokens)
                var commitSummaries = new List<object>();
                var allFilesChanged = new HashSet<string>();

                foreach (var commit in recentCommits.Take(5))
                {
                    var diff = await _githubService.GetCommitDiffAsync(owner, repo, commit.Sha, accessToken);
                    if (diff?.FileChanges != null && diff.FileChanges.Any())
                    {
                        var filesInCommit = diff.FileChanges
                            .Where(f => f.Status != "removed" && (f.Additions > 0 || f.Deletions > 0))
                            .Select(f => Path.GetFileName(f.FilePath))
                            .Distinct()
                            .ToList();

                        foreach (var file in filesInCommit)
                        {
                            allFilesChanged.Add(file);
                        }

                        commitSummaries.Add(new
                        {
                            Sha = commit.Sha.Substring(0, Math.Min(7, commit.Sha.Length)),
                            Message = commit.Message,
                            Date = commit.CommitDate.ToString("yyyy-MM-dd HH:mm"),
                            FilesChanged = filesInCommit,
                            TotalAdditions = diff.TotalAdditions,
                            TotalDeletions = diff.TotalDeletions
                        });
                    }
                }

                return new
                {
                    HasCommits = true,
                    CommitCount = recentCommits.Count,
                    RecentCommits = commitSummaries,
                    AllFilesChanged = allFilesChanged.ToList(),
                    RepositoryUrl = githubRepoUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch GitHub commit summary for repo {Repo}", githubRepoUrl);
                return null;
            }
        }

        /// <summary>
        /// Combines commit summaries from multiple repositories (for fullstack developers)
        /// </summary>
        private object CombineCommitSummaries(List<object> commitSummaries)
        {
            try
            {
                var combinedCommits = new List<object>();
                var allFilesChanged = new HashSet<string>();
                var totalCommitCount = 0;

                foreach (var summary in commitSummaries)
                {
                    if (summary is System.Text.Json.JsonElement jsonElement)
                    {
                        if (jsonElement.TryGetProperty("RecentCommits", out var commitsProp) && 
                            commitsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var commit in commitsProp.EnumerateArray())
                            {
                                combinedCommits.Add(commit);
                            }
                        }

                        if (jsonElement.TryGetProperty("AllFilesChanged", out var filesProp) && 
                            filesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var file in filesProp.EnumerateArray())
                            {
                                if (file.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    allFilesChanged.Add(file.GetString() ?? "");
                                }
                            }
                        }

                        if (jsonElement.TryGetProperty("CommitCount", out var countProp))
                        {
                            totalCommitCount += countProp.GetInt32();
                        }
                    }
                }

                // Sort commits by date (most recent first) - simplified for now
                // In a real implementation, you'd parse and sort by actual commit date

                return new
                {
                    HasCommits = combinedCommits.Any(),
                    CommitCount = totalCommitCount,
                    RecentCommits = combinedCommits.Take(10).ToList(), // Limit to 10 most recent across all repos
                    AllFilesChanged = allFilesChanged.ToList(),
                    RepositoryCount = commitSummaries.Count,
                    Message = "Combined commits from multiple repositories"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error combining commit summaries: {Message}", ex.Message);
                return commitSummaries.FirstOrDefault() ?? new { HasCommits = false, CommitCount = 0 };
            }
        }

        /// <summary>
        /// Get Trello card custom fields (Priority, Status, Risk, Effort, ModuleId)
        /// </summary>
        private async Task<Dictionary<string, string>> GetTrelloCardCustomFieldsAsync(string cardId, string boardId)
        {
            var customFields = new Dictionary<string, string>();
            
            try
            {
                using var httpClient = new HttpClient();
                
                // Step 1: Get board custom field definitions to map idCustomField to name
                var boardCustomFieldsUrl = $"https://api.trello.com/1/boards/{boardId}/customFields?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var boardFieldsResponse = await httpClient.GetAsync(boardCustomFieldsUrl);
                
                var customFieldNameMap = new Dictionary<string, string>();
                
                if (boardFieldsResponse.IsSuccessStatusCode)
                {
                    var boardFieldsContent = await boardFieldsResponse.Content.ReadAsStringAsync();
                    var boardFieldsData = JsonSerializer.Deserialize<JsonElement[]>(boardFieldsContent);
                    
                    foreach (var fieldDef in boardFieldsData ?? Array.Empty<JsonElement>())
                    {
                        var fieldId = fieldDef.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";
                        var fieldName = fieldDef.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                        
                        if (!string.IsNullOrEmpty(fieldId) && !string.IsNullOrEmpty(fieldName))
                        {
                            customFieldNameMap[fieldId] = fieldName;
                        }
                    }
                }
                
                // Step 2: Get card custom field items
                var customFieldsUrl = $"https://api.trello.com/1/cards/{cardId}/customFieldItems?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                var response = await httpClient.GetAsync(customFieldsUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Custom field items response for card {CardId}: {Content}", cardId, content);
                    
                    var customFieldsData = JsonSerializer.Deserialize<JsonElement[]>(content);
                    
                    foreach (var field in customFieldsData ?? Array.Empty<JsonElement>())
                    {
                        var idCustomField = field.TryGetProperty("idCustomField", out var idProp) ? idProp.GetString() : "";
                        
                        // Get field name from the mapping we created
                        var fieldName = !string.IsNullOrEmpty(idCustomField) && customFieldNameMap.TryGetValue(idCustomField, out var name) 
                            ? name 
                            : idCustomField ?? "Unknown";
                        
                        var fieldValue = "";
                        
                        // Handle different custom field types
                        if (field.TryGetProperty("value", out var valueProp))
                        {
                            if (valueProp.ValueKind == JsonValueKind.String)
                            {
                                fieldValue = valueProp.GetString() ?? "";
                                // Handle double-encoded strings (e.g., "\"12345\"")
                                if (fieldValue.StartsWith("\"") && fieldValue.EndsWith("\""))
                                {
                                    try
                                    {
                                        // Try to unescape JSON string
                                        fieldValue = JsonSerializer.Deserialize<string>(fieldValue) ?? fieldValue;
                                    }
                                    catch
                                    {
                                        // If deserialization fails, just remove surrounding quotes
                                        fieldValue = fieldValue.Trim('"');
                                    }
                                }
                            }
                            else if (valueProp.ValueKind == JsonValueKind.Object)
                            {
                                // For dropdown/select fields - check for text property
                                if (valueProp.TryGetProperty("text", out var textProp))
                                {
                                    fieldValue = textProp.GetString() ?? "";
                                }
                                // For number fields stored as object
                                else if (valueProp.TryGetProperty("number", out var numberProp))
                                {
                                    fieldValue = numberProp.GetRawText();
                                }
                                // For date fields
                                else if (valueProp.TryGetProperty("date", out var dateProp))
                                {
                                    fieldValue = dateProp.GetString() ?? "";
                                }
                                // For checkbox fields
                                else if (valueProp.TryGetProperty("checked", out var checkedProp))
                                {
                                    fieldValue = checkedProp.GetBoolean().ToString();
                                }
                            }
                            else if (valueProp.ValueKind == JsonValueKind.Number)
                            {
                                fieldValue = valueProp.GetRawText();
                            }
                            else if (valueProp.ValueKind == JsonValueKind.True || valueProp.ValueKind == JsonValueKind.False)
                            {
                                fieldValue = valueProp.GetBoolean().ToString();
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(fieldValue))
                        {
                            customFields[fieldName] = fieldValue;
                            _logger.LogDebug("Found custom field: {FieldName} = {FieldValue}", fieldName, fieldValue);
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to fetch custom fields for card {CardId}. Status: {Status}, Response: {Error}", 
                        cardId, response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch custom fields for card {CardId}", cardId);
            }
            
            return customFields;
        }

        /// <summary>
        /// Get GitHub repository files list
        /// </summary>
        private async Task<List<string>> GetGitHubRepositoryFilesAsync(string repoName)
        {
            var files = new List<string>();
            
            try
            {
                // Extract owner and repo name from repo string (format: "owner/repo" or just "repo")
                string owner = "skill-in"; // Default owner, adjust if needed
                string repo = repoName;
                
                if (repoName.Contains('/'))
                {
                    var parts = repoName.Split('/');
                    owner = parts[0];
                    repo = parts[1];
                }

                var accessToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("GitHub access token not configured");
                    return files;
                }

                // Get repository tree (files)
                var treeUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/main?recursive=1";
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await httpClient.GetAsync(treeUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Try master branch
                    treeUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/master?recursive=1";
                    response = await httpClient.GetAsync(treeUrl);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var treeData = JsonSerializer.Deserialize<JsonElement>(content);
                    
                    if (treeData.TryGetProperty("tree", out var treeProp) && treeProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in treeProp.EnumerateArray())
                        {
                            var path = item.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : "";
                            var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "";
                            
                            // Only include files, not directories
                            if (type == "blob" && !string.IsNullOrEmpty(path))
                            {
                                files.Add(path);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch GitHub files for repo {RepoName}", repoName);
            }
            
            return files;
        }

        /// <summary>
        /// Get Trello board lists
        /// </summary>
        private async Task<List<object>> GetBoardListsAsync(string boardId)
        {
            try
            {
                var listsUrl = $"https://api.trello.com/1/boards/{boardId}/lists?key={_trelloConfig.ApiKey}&token={_trelloConfig.ApiToken}";
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(listsUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var listsData = JsonSerializer.Deserialize<JsonElement[]>(content);
                    
                    return listsData?.Select(l => new
                    {
                        Id = l.TryGetProperty("id", out var idProp) ? idProp.GetString() : "",
                        Name = l.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "",
                        Closed = l.TryGetProperty("closed", out var closedProp) ? closedProp.GetBoolean() : false
                    }).Cast<object>().ToList() ?? new List<object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch board lists for board {BoardId}", boardId);
            }
            
            return new List<object>();
        }

        private string FormatTaskDetails(List<object> tasks)
        {
            if (tasks.Count == 0) return "No tasks assigned";
            
            var details = new List<string>();
            foreach (var task in tasks)
            {
                var taskJson = JsonSerializer.Serialize(task);
                var taskElement = JsonSerializer.Deserialize<JsonElement>(taskJson);
                
                var name = taskElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                var description = taskElement.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "";
                var closed = taskElement.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false;
                var dueDate = taskElement.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : null;
                
                // Format due date to be human-readable and check if overdue
                string? formattedDueDate = null;
                bool isOverdue = false;
                if (!string.IsNullOrEmpty(dueDate))
                {
                    if (DateTime.TryParse(dueDate, out var parsedDate))
                    {
                        var now = DateTime.UtcNow.Date;
                        var dueDateOnly = parsedDate.Date;
                        isOverdue = dueDateOnly < now && !closed;
                        formattedDueDate = parsedDate.ToString("MMMM d, yyyy"); // e.g., "December 19, 2025"
                        if (isOverdue)
                        {
                            formattedDueDate += " (OVERDUE)";
                        }
                    }
                    else
                    {
                        formattedDueDate = dueDate; // Fallback to raw string if parsing fails
                    }
                }
                
                var customFieldsJson = taskElement.TryGetProperty("CustomFields", out var cfProp) ? cfProp.ToString() : "{}";
                var customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsJson) ?? new Dictionary<string, string>();
                
                var checklistItems = new List<string>();
                if (taskElement.TryGetProperty("ChecklistItems", out var checklistProp) && checklistProp.ValueKind == JsonValueKind.Array)
                {
                    checklistItems = checklistProp.EnumerateArray()
                        .Select(item => item.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }
                
                var taskDetail = $"- Task: {name}\n  Status: {(closed ? "Completed" : "In Progress")}\n  Description: {description ?? "No description"}";
                
                if (!string.IsNullOrEmpty(formattedDueDate))
                {
                    taskDetail += $"\n  Due Date: {formattedDueDate}";
                }
                
                if (customFields.Any())
                {
                    taskDetail += $"\n  Custom Fields: {string.Join(", ", customFields.Select(kv => $"{kv.Key}={kv.Value}"))}";
                }
                
                if (checklistItems.Any())
                {
                    taskDetail += $"\n  Task Breakdown:\n{string.Join("\n", checklistItems.Select(item => $"    - {item}"))}";
                }
                
                details.Add(taskDetail);
            }
            
            return string.Join("\n\n", details);
        }

        private string GetModuleDescriptionForFirstTask(JsonElement firstTask, Dictionary<string, string> moduleDescriptions)
        {
            if (firstTask.ValueKind == JsonValueKind.Undefined || firstTask.ValueKind == JsonValueKind.Null)
            {
                return "No module description available";
            }
            
            if (firstTask.TryGetProperty("CustomFields", out var cfProp) && cfProp.ValueKind == JsonValueKind.Object)
            {
                if (cfProp.TryGetProperty("ModuleId", out var moduleIdProp))
                {
                    var moduleIdStr = "";
                    if (moduleIdProp.ValueKind == JsonValueKind.String)
                    {
                        moduleIdStr = moduleIdProp.GetString() ?? "";
                    }
                    else if (moduleIdProp.ValueKind == JsonValueKind.Number)
                    {
                        moduleIdStr = moduleIdProp.GetRawText();
                    }
                    
                    if (!string.IsNullOrEmpty(moduleIdStr))
                    {
                        moduleIdStr = moduleIdStr.Trim().Trim('"').Trim('\'').Trim();
                        if (!string.IsNullOrEmpty(moduleIdStr) && moduleDescriptions.TryGetValue(moduleIdStr, out var description))
                        {
                            return description;
                        }
                    }
                }
            }
            
            return "No module description available for current task";
        }

        private string FormatTeamMembers(List<object> teamMembers)
        {
            if (teamMembers.Count == 0) return "No other team members";
            
            var members = new List<string>();
            foreach (var member in teamMembers)
            {
                var memberJson = JsonSerializer.Serialize(member);
                var memberElement = JsonSerializer.Deserialize<JsonElement>(memberJson);
                
                var firstName = memberElement.TryGetProperty("FirstName", out var fnProp) ? fnProp.GetString() : "";
                var lastName = memberElement.TryGetProperty("LastName", out var lnProp) ? lnProp.GetString() : "";
                var roleName = memberElement.TryGetProperty("RoleName", out var roleProp) ? roleProp.GetString() : "";
                
                members.Add($"- {firstName} {lastName} ({roleName})");
            }
            
            return string.Join("\n", members);
        }

        private string FormatTeamMemberTasks(List<object> teamMemberTasks)
        {
            if (teamMemberTasks.Count == 0) return "No other team member tasks in current sprint";
            
            var tasks = new List<string>();
            foreach (var task in teamMemberTasks)
            {
                var taskJson = JsonSerializer.Serialize(task);
                var taskElement = JsonSerializer.Deserialize<JsonElement>(taskJson);
                
                var firstName = taskElement.TryGetProperty("TeamMemberFirstName", out var fnProp) ? fnProp.GetString() : "";
                var roleName = taskElement.TryGetProperty("TeamMemberRoleName", out var roleProp) ? roleProp.GetString() : "";
                var taskName = taskElement.TryGetProperty("TaskName", out var tnProp) ? tnProp.GetString() : "";
                var isClosed = taskElement.TryGetProperty("IsClosed", out var closedProp) ? closedProp.GetBoolean() : false;
                
                tasks.Add($"- {firstName} ({roleName}): {taskName} [{(isClosed ? "Completed" : "In Progress")}]");
            }
            
            return string.Join("\n", tasks);
        }

        /// <summary>
        /// Get the Platform Context & Vision and Knowledge Limitations content to prepend to system prompts
        /// </summary>
        private string GetPlatformContextAndLimitations()
        {
            return @"Platform Context & Vision:
You are the Lead Mentor and Architect on [Skill-In], the only professional ecosystem designed to bridge the gap between academic learning and industry-level employment.
The Mission: This platform is the new standard for hands-on engineering. We do not provide 'tutorials' or 'sandboxes.' We provide Real Projects using an elite infrastructure stack: GitHub for version control, Railway for cloud deployment, and Neon Postgres for production databases.
How the System Works:
• True Professional Experience: Juniors are placed in high-fidelity environments where they must navigate real-world complexity (CORS, Environment Variables, API Contracts, and Deployment Pipelines).
• The Team Dynamic: Most projects are collaborative, featuring distinct roles like Backend Developer, Frontend Developer, UI/UX, and PM. You oversee the interdependencies between these repos and individuals.
• The Scouting Edge: Every action the user takes—from commit quality to how they resolve architectural conflicts—is analyzed. This is a stage where their skills are exposed to potential employers. We are the 'Scouting Ground' for the next generation of tech talent.
Your Role as the Mentor:
1. Lead by Context: You are the only entity with a 'Global View' of all repositories (API and Client) and the Trello roadmap. You know when a Backend push will break a Frontend fetch.
2. Facilitate, Don't Hand-hold: Do not provide expertise unless asked, and even then, guide them toward the 'Environmental Truth.'
3. The Professional Standard: Remind users that they are building a verifiable portfolio. If they take shortcuts, they hurt their chances of being scouted. If they collaborate effectively and solve integration gaps, they prove they are 'Job Ready.'
4. Emphasize the 'New Way': If a user feels lost, remind them that this 'lostness' is exactly what professional engineering feels like. Navigating this complexity is the only way to gain the 'Valid Experience' that employers actually value today.

Knowledge Limitations & Operational Boundaries:
Your intelligence is strictly tethered to the Current Project Context and the user's Assigned Role. You are a project-specific Lead Architect, not a general-purpose AI.
1. The 'Need to Know' Filter:
• In-Scope: Technical guidance regarding the project's specific Tech Stack (C#/.NET, JS/HTML, Neon Postgres, Railway, GitHub), architectural decisions, Trello card requirements, and cross-team integration.
• Out-of-Scope: Anything unrelated to the current project. This includes general trivia, homework help, unrelated coding snippets, political/social discussions, or advice on other technologies not used in this specific project.
2. Handling Out-of-Scope Queries: If a user asks a question that does not directly impact the completion of their current Trello tasks or the stability of the project infrastructure, you must decline to answer.
• Your Response Strategy: Do not say 'I don't know' in a way that suggests technical incompetence. Instead, respond as a professional Lead Architect who is focused purely on the deadline and the project.
• Example Response: 'That's outside the scope of our current sprint. Let's stay focused on getting the [Task Name] deployed to Railway. We don't have time for distractions if we want this project to be scout-ready.'
3. Role-Specific Blindness:
• If the Backend Developer asks for advice on CSS styling, redirect them: 'That's a frontend concern. Check the UI/UX design cards or sync with the Frontend dev. My focus for you is the API integrity.'
• If a user asks for 'Best practices for Python' in a .NET project, you do not know Python for the purposes of this conversation. Your expertise is locked to the Project's System Design.
4. No External LLM Assistance: If the user asks you to 'Act like ChatGPT' or 'Explain a concept from scratch' that is easily Googleable, challenge them to find the answer in the context of the code. Your value is not in explaining what a variable is, but where that specific variable lives in their repository.

";
        }

        private string BuildEnhancedSystemPrompt(string baseSystemPrompt, JsonElement contextData)
        {
            // Prepend Platform Context & Vision and Knowledge Limitations to the system prompt
            var platformContext = GetPlatformContextAndLimitations();
            var enhancedBasePrompt = platformContext + baseSystemPrompt;

            if (contextData.ValueKind == JsonValueKind.Undefined || contextData.ValueKind == JsonValueKind.Null)
            {
                return enhancedBasePrompt;
            }

            var contextParts = new List<string>();

            // Extract GitHub commit summary from context (if available)
            string? githubContextInfo = null;
            if (contextData.TryGetProperty("GitHubCommitSummary", out var githubSummaryProp) && 
                githubSummaryProp.ValueKind != JsonValueKind.Null && 
                githubSummaryProp.ValueKind != JsonValueKind.Undefined)
            {
                githubContextInfo = FormatGitHubCommitSummary(githubSummaryProp);
            }

            // Extract user profile and check if it's a developer role
            bool isDeveloperRole = false;
            string roleName = "";
            if (contextData.TryGetProperty("UserProfile", out var userProfile))
            {
                var firstName = userProfile.TryGetProperty("FirstName", out var fnProp) ? fnProp.GetString() : "";
                roleName = userProfile.TryGetProperty("Role", out var roleProp) ? roleProp.GetString() ?? "" : "";
                var programmingLanguage = userProfile.TryGetProperty("ProgrammingLanguage", out var langProp) ? langProp.GetString() : "";

                // Check if role is a developer role (contains "Developer" or "Full Stack" or "Frontend" or "Backend")
                if (!string.IsNullOrEmpty(roleName))
                {
                    var roleLower = roleName.ToLowerInvariant();
                    isDeveloperRole = roleLower.Contains("developer") || 
                                     roleLower.Contains("full stack") || 
                                     roleLower.Contains("frontend") || 
                                     roleLower.Contains("backend");
                }

                if (!string.IsNullOrEmpty(firstName))
                {
                    contextParts.Add($"You are mentoring {firstName}");
                    if (!string.IsNullOrEmpty(roleName))
                    {
                        contextParts.Add($"who is a {roleName}");
                    }
                    if (!string.IsNullOrEmpty(programmingLanguage) && programmingLanguage != "Not specified")
                    {
                        contextParts.Add($"working with {programmingLanguage}");
                    }
                }
            }

            // Extract current tasks with details
            if (contextData.TryGetProperty("CurrentTasks", out var tasksProp) && tasksProp.ValueKind == JsonValueKind.Array && tasksProp.GetArrayLength() > 0)
            {
                var taskCount = tasksProp.GetArrayLength();
                var taskDetails = new List<string>();
                
                foreach (var task in tasksProp.EnumerateArray())
                {
                    var taskName = task.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                    var taskDesc = task.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "";
                    var dueDate = task.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : null;
                    
                    // Format due date to be human-readable and check if overdue
                    string? formattedDueDate = null;
                    bool isOverdue = false;
                    if (!string.IsNullOrEmpty(dueDate))
                    {
                        if (DateTime.TryParse(dueDate, out var parsedDate))
                        {
                            var now = DateTime.UtcNow.Date;
                            var dueDateOnly = parsedDate.Date;
                            var isClosed = task.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false;
                            isOverdue = dueDateOnly < now && !isClosed;
                            formattedDueDate = parsedDate.ToString("MMMM d, yyyy"); // e.g., "December 19, 2025"
                            if (isOverdue)
                            {
                                formattedDueDate += " (OVERDUE)";
                            }
                        }
                        else
                        {
                            formattedDueDate = dueDate; // Fallback to raw string if parsing fails
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(taskName))
                    {
                        var taskInfo = $"Task: {taskName}";
                        if (!string.IsNullOrEmpty(taskDesc) && taskDesc.Length > 0)
                        {
                            // Truncate long descriptions to save tokens (keep first 200 chars)
                            var shortDesc = taskDesc.Length > 200 ? taskDesc.Substring(0, 200) + "..." : taskDesc;
                            taskInfo += $" - {shortDesc}";
                        }
                        if (!string.IsNullOrEmpty(formattedDueDate))
                        {
                            taskInfo += $" (Due: {formattedDueDate})";
                        }
                        taskDetails.Add(taskInfo);
                    }
                }
                
                if (taskDetails.Any())
                {
                    contextParts.Add($"Currently working on {taskCount} task{(taskCount > 1 ? "s" : "")} in this sprint:");
                    contextParts.Add(string.Join(" | ", taskDetails));
                }
                else
                {
                    contextParts.Add($"Currently working on {taskCount} task{(taskCount > 1 ? "s" : "")} in this sprint");
                }
            }

            // Extract team members with actual names and roles
            string? teamMembersInfo = null;
            if (contextData.TryGetProperty("TeamMembers", out var teamProp) && teamProp.ValueKind == JsonValueKind.Array && teamProp.GetArrayLength() > 0)
            {
                var teamMembersList = new List<object>();
                foreach (var member in teamProp.EnumerateArray())
                {
                    teamMembersList.Add(member);
                }
                
                var formattedTeamMembers = FormatTeamMembers(teamMembersList);
                var teamCount = teamProp.GetArrayLength();
                contextParts.Add($"Working with {teamCount} team member{(teamCount > 1 ? "s" : "")}");
                teamMembersInfo = formattedTeamMembers;
            }

            // Extract team member tasks with actual task information
            string? teamMemberTasksInfo = null;
            if (contextData.TryGetProperty("TeamMemberTasks", out var teamTasksProp) && teamTasksProp.ValueKind == JsonValueKind.Array && teamTasksProp.GetArrayLength() > 0)
            {
                var teamMemberTasksList = new List<object>();
                foreach (var task in teamTasksProp.EnumerateArray())
                {
                    teamMemberTasksList.Add(task);
                }
                
                var formattedTeamMemberTasks = FormatTeamMemberTasks(teamMemberTasksList);
                teamMemberTasksInfo = formattedTeamMemberTasks;
            }

            var contextInfo = contextParts.Any() ? string.Join(". ", contextParts) + "." : "";
            
            // Add current date context for deadline calculations
            var currentDate = DateTime.UtcNow;
            contextInfo += $"\n\nCURRENT DATE: {currentDate:MMMM d, yyyy} (Use this as the reference point for calculating future dates)";
            
            // Add team members list separately with proper formatting
            if (!string.IsNullOrEmpty(teamMembersInfo))
            {
                contextInfo += $"\n\nTEAM MEMBERS:\n{teamMembersInfo}";
            }
            
            // Add team member tasks separately with proper formatting
            if (!string.IsNullOrEmpty(teamMemberTasksInfo))
            {
                contextInfo += $"\n\nTEAM MEMBER TASKS (Current Sprint):\n{teamMemberTasksInfo}";
            }
            
            // Extract next team meeting information
            if (contextData.TryGetProperty("NextTeamMeeting", out var meetingProp) && meetingProp.ValueKind == JsonValueKind.Object)
            {
                DateTime? meetingTime = null;
                if (meetingProp.TryGetProperty("Time", out var timeProp) && timeProp.ValueKind != JsonValueKind.Null)
                {
                    // Handle DateTime serialized as string (ISO 8601 format)
                    if (timeProp.ValueKind == JsonValueKind.String)
                    {
                        var timeString = timeProp.GetString();
                        if (!string.IsNullOrEmpty(timeString) && DateTime.TryParse(timeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTime))
                        {
                            meetingTime = parsedTime;
                        }
                    }
                }
                
                var meetingUrl = meetingProp.TryGetProperty("Url", out var urlProp) && urlProp.ValueKind != JsonValueKind.Null 
                    ? urlProp.GetString() 
                    : null;
                
                var currentTime = DateTime.UtcNow;
                
                if (meetingTime.HasValue)
                {
                    // Check if the meeting time is in the past
                    bool isMeetingInPast = meetingTime.Value < currentTime;
                    
                    if (isMeetingInPast)
                    {
                        // Meeting time is in the past - inform mentor that it has passed and no new meeting is scheduled
                        var formattedTime = meetingTime.Value.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'");
                        contextInfo += $"\n\nNEXT TEAM MEETING:\nThe previous meeting was scheduled for {formattedTime}, but this time has already passed. No new team meeting has been scheduled. The Product Manager is responsible for scheduling team meetings.";
                    }
                    else
                    {
                        // Meeting is in the future - check if it has a URL (fully scheduled)
                        if (!string.IsNullOrEmpty(meetingUrl))
                        {
                            // Format the meeting time (convert UTC to readable format)
                            var formattedTime = meetingTime.Value.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'");
                            contextInfo += $"\n\nNEXT TEAM MEETING:\nScheduled for: {formattedTime}";
                            contextInfo += $"\nMeeting URL: {meetingUrl}";
                        }
                        else
                        {
                            // Future time but no URL - not fully scheduled
                            var formattedTime = meetingTime.Value.ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'");
                            contextInfo += $"\n\nNEXT TEAM MEETING:\nA meeting time ({formattedTime}) exists but no meeting URL is available, indicating the meeting is not fully scheduled. The Product Manager is responsible for scheduling team meetings.";
                        }
                    }
                }
                else
                {
                    // No meeting time exists - no meeting is scheduled
                    contextInfo += $"\n\nNEXT TEAM MEETING:\nNo team meeting is currently scheduled. The Product Manager is responsible for scheduling team meetings.";
                }
            }

            // Extract database password from context (if available)
            string? databasePassword = null;
            if (contextData.TryGetProperty("DatabasePassword", out var dbPasswordProp) &&
                dbPasswordProp.ValueKind != JsonValueKind.Null &&
                dbPasswordProp.ValueKind != JsonValueKind.Undefined)
            {
                databasePassword = dbPasswordProp.GetString();
            }
            
            // Build GitHub context info from commit summary (if available and developer role)
            string githubContextSection = "";
            if (isDeveloperRole && !string.IsNullOrEmpty(githubContextInfo))
            {
                var githubTemplate = _promptConfig.Mentor.EnhancedPrompt.GitHubContextTemplate ?? 
                    "GITHUB REPOSITORY STATUS:\n{0}\n\nUse this information to provide accurate, context-aware responses about the student's code and repository activity.";
                githubContextSection = $"\n\n{string.Format(githubTemplate, githubContextInfo)}";
            }

            // Add database connection information if password is available
            string databaseInfoSection = "";
            if (!string.IsNullOrEmpty(databasePassword))
            {
                databaseInfoSection = $"\n\nDATABASE CONNECTION INFORMATION:\n" +
                    $"The project has a Neon PostgreSQL database. " +
                    $"⚠️ CRITICAL: If the user asks for database connection details, you MUST extract the EXACT connection string from the README content (if available in the context above). " +
                    $"The README contains the complete connection string with the isolated database role. " +
                    $"Parse the connection string and provide EXACT values - DO NOT use placeholders or generic formats. " +
                    $"ONLY if the README is not available, use the following information:\n" +
                    $"- Database Password: {databasePassword}\n" +
                    $"- Note: This password is for the isolated database role. " +
                    $"The full connection string with exact database name (starts with 'AppDB_'), username, host, and port is in the README. " +
                    $"ALWAYS prioritize and extract EXACT values from the README connection string.";
            }
            
            // Only add GitHub capabilities for developer roles (from configuration)
            string capabilitiesInfo = "";
            if (isDeveloperRole)
            {
                capabilitiesInfo = _promptConfig.Mentor.EnhancedPrompt.DeveloperCapabilitiesInfo;
            }
            else if (!string.IsNullOrEmpty(roleName))
            {
                capabilitiesInfo = string.Format(_promptConfig.Mentor.EnhancedPrompt.NonDeveloperCapabilitiesInfo, roleName);
            }

            if (!string.IsNullOrEmpty(contextInfo))
            {
                var capabilitiesSection = !string.IsNullOrEmpty(capabilitiesInfo) ? $"\n\n{capabilitiesInfo}" : "";
                return $"{enhancedBasePrompt}\n\nCURRENT CONTEXT:\n{contextInfo}{githubContextSection}{databaseInfoSection}{capabilitiesSection}\n\n{_promptConfig.Mentor.EnhancedPrompt.ContextReminder}";
            }

            // Even without context, add capability information if available
            if (!string.IsNullOrEmpty(capabilitiesInfo))
            {
                return $"{enhancedBasePrompt}\n\n{capabilitiesInfo}";
            }
            
            return enhancedBasePrompt;
        }

        /// <summary>
        /// Format GitHub commit summary for prompt inclusion (token-efficient)
        /// </summary>
        private string FormatGitHubCommitSummary(JsonElement githubSummary)
        {
            try
            {
                var hasCommits = githubSummary.TryGetProperty("HasCommits", out var hasCommitsProp) && 
                                hasCommitsProp.GetBoolean();
                
                if (!hasCommits)
                {
                    return "No commits found in repository yet. The student needs to commit and push their code.";
                }

                var commitCount = githubSummary.TryGetProperty("CommitCount", out var countProp) ? 
                                 countProp.GetInt32() : 0;
                var repoUrl = githubSummary.TryGetProperty("RepositoryUrl", out var urlProp) ? 
                             urlProp.GetString() : "repository";

                var sb = new StringBuilder();
                sb.AppendLine($"Repository: {repoUrl}");
                sb.AppendLine($"Total commits: {commitCount}");

                if (githubSummary.TryGetProperty("RecentCommits", out var commitsProp) && 
                    commitsProp.ValueKind == JsonValueKind.Array)
                {
                    var commits = commitsProp.EnumerateArray().Take(3).ToList(); // Limit to 3 most recent for token efficiency
                    if (commits.Any())
                    {
                        sb.AppendLine("\nRecent commits:");
                        foreach (var commit in commits)
                        {
                            var sha = commit.TryGetProperty("Sha", out var shaProp) ? shaProp.GetString() : "";
                            var message = commit.TryGetProperty("Message", out var msgProp) ? msgProp.GetString() : "";
                            var date = commit.TryGetProperty("Date", out var dateProp) ? dateProp.GetString() : "";
                            var additions = commit.TryGetProperty("TotalAdditions", out var addProp) ? addProp.GetInt32() : 0;
                            var deletions = commit.TryGetProperty("TotalDeletions", out var delProp) ? delProp.GetInt32() : 0;

                            if (commit.TryGetProperty("FilesChanged", out var filesProp) && 
                                filesProp.ValueKind == JsonValueKind.Array)
                            {
                                var files = filesProp.EnumerateArray()
                                    .Select(f => f.GetString())
                                    .Where(f => !string.IsNullOrEmpty(f))
                                    .Take(3) // Limit files per commit
                                    .ToList();
                                
                                var filesText = files.Any() ? string.Join(", ", files) : "no files";
                                if (filesProp.GetArrayLength() > 3)
                                {
                                    filesText += $" (+{filesProp.GetArrayLength() - 3} more)";
                                }
                                
                                sb.AppendLine($"- {sha}: {message} ({date}) - Files: {filesText} (+{additions}/-{deletions})");
                            }
                            else
                            {
                                sb.AppendLine($"- {sha}: {message} ({date}) (+{additions}/-{deletions})");
                            }
                        }
                    }
                }

                if (githubSummary.TryGetProperty("AllFilesChanged", out var allFilesProp) && 
                    allFilesProp.ValueKind == JsonValueKind.Array)
                {
                    var allFiles = allFilesProp.EnumerateArray()
                        .Select(f => f.GetString())
                        .Where(f => !string.IsNullOrEmpty(f))
                        .Take(10) // Limit to 10 files for token efficiency
                        .ToList();
                    
                    if (allFiles.Any())
                    {
                        sb.AppendLine($"\nFiles with recent changes: {string.Join(", ", allFiles)}");
                        if (allFilesProp.GetArrayLength() > 10)
                        {
                            sb.AppendLine($"(+{allFilesProp.GetArrayLength() - 10} more files)");
                        }
                    }
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error formatting GitHub commit summary");
                return "GitHub repository information is available, but details could not be retrieved.";
            }
        }

        /// <summary>
        /// Get all available AI models
        /// </summary>
        [HttpGet("use/get-models")]
        public async Task<ActionResult<object>> GetModels()
        {
            try
            {
                _logger.LogInformation("Getting all available AI models");

                var models = await _context.AIModels
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Provider)
                    .ThenBy(m => m.Name)
                    .Select(m => new
                    {
                        m.Id,
                        m.Name,
                        m.Provider,
                        m.BaseUrl,
                        m.ApiVersion,
                        m.MaxTokens,
                        m.DefaultTemperature,
                        m.Description,
                        m.IsActive
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Success = true,
                    Models = models,
                    Count = models.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI models");
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
            }
        }


        /// <summary>
        /// Webhook endpoint for Railway build failure notifications
        /// Receives deployment failure events from Railway and processes them
        /// </summary>
        [HttpPost("railway-webhook")]
        [Consumes("application/json")]
        public async Task<ActionResult> HandleRailwayWebhook([FromBody] RailwayWebhookPayload payload)
        {
            try
            {
                var serviceName = payload.Resource?.Service?.Name;
                var deploymentId = payload.Resource?.Deployment?.Id;
                var deploymentStatus = payload.Details?.Status;
                var webhookType = payload.Type;
                
                // Log full webhook payload for debugging
                var fullPayloadJson = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                _logger.LogDebug("Full Railway webhook payload: {Payload}", fullPayloadJson);
                
                _logger.LogInformation("Railway webhook received. Type: {Type}, Service: {ServiceName}, Deployment: {DeploymentId}, Status: {Status}", 
                    webhookType, serviceName, deploymentId, deploymentStatus);
                
                // Extract boardId from service name (webapi-{boardId} or webapi_{boardId})
                // IMPORTANT: Take everything after the prefix, not just the first part after splitting
                // This handles boardIds that may contain hyphens or underscores
                string? boardId = null;
                if (!string.IsNullOrEmpty(serviceName))
                {
                    var serviceNameLower = serviceName.ToLowerInvariant();
                    if (serviceNameLower.StartsWith("webapi-"))
                    {
                        // Take everything after "webapi-"
                        boardId = serviceNameLower.Substring(7); // "webapi-".Length = 7
                    }
                    else if (serviceNameLower.StartsWith("webapi_"))
                    {
                        // Take everything after "webapi_"
                        boardId = serviceNameLower.Substring(7); // "webapi_".Length = 7
                    }
                }
                
                if (string.IsNullOrEmpty(boardId))
                {
                    _logger.LogWarning("Could not extract boardId from service name: {ServiceName}. Webhook may be for non-tenant service.", serviceName);
                    return Ok(new { Success = true, Message = "Webhook received but boardId not found" });
                }
                
                // Log extracted boardId for debugging
                _logger.LogInformation("Extracted boardId from service name: ServiceName={ServiceName}, BoardId={BoardId}", 
                    serviceName, boardId);
                
                // Verify that ProjectBoard exists before trying to insert/update BoardState
                // This prevents foreign key constraint violations
                // Note: ProjectBoard should exist regardless of SkipTrelloApi setting
                var projectBoard = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == boardId);
                
                if (projectBoard == null)
                {
                    // This is an error - ProjectBoard should exist whether SkipTrelloApi is true or false
                    // Check for potential timing issues by looking for recently created boards
                    var recentBoards = await _context.ProjectBoards
                        .Where(pb => pb.CreatedAt >= DateTime.UtcNow.AddMinutes(-5))
                        .OrderByDescending(pb => pb.CreatedAt)
                        .Select(pb => new { pb.Id, pb.CreatedAt })
                        .Take(10)
                        .ToListAsync();
                    
                    _logger.LogError("ProjectBoard with BoardId {BoardId} does not exist. " +
                        "This may be a timing issue (webhook arrived before board creation completed) or the board creation failed. " +
                        "SkipTrelloApi setting: {SkipTrelloApi}. " +
                        "Recent boards created in last 5 minutes: {RecentBoardCount}. " +
                        "Recent board IDs: {RecentBoardIds}. " +
                        "Skipping BoardState update to avoid foreign key constraint violation.", 
                        boardId, 
                        _testingConfig.Value.SkipTrelloApi,
                        recentBoards.Count,
                        string.Join(", ", recentBoards.Select(b => $"{b.Id} (created: {b.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC)")));
                    
                    // Also check if there's a board with similar ID (case sensitivity issue?)
                    var similarBoards = await _context.ProjectBoards
                        .Where(pb => pb.Id.ToLower() == boardId.ToLower() || pb.Id.Contains(boardId) || boardId.Contains(pb.Id))
                        .Select(pb => new { pb.Id, pb.CreatedAt })
                        .Take(5)
                        .ToListAsync();
                    
                    if (similarBoards.Any())
                    {
                        _logger.LogWarning("Found {Count} boards with similar IDs to {BoardId}: {SimilarIds}", 
                            similarBoards.Count, boardId, string.Join(", ", similarBoards.Select(b => b.Id)));
                    }
                    
                    return Ok(new { Success = true, Message = $"ProjectBoard {boardId} not found, skipping BoardState update" });
                }
                
                // Log ProjectBoard details for timing analysis
                _logger.LogInformation("ProjectBoard found: BoardId={BoardId}, CreatedAt={CreatedAt}, UpdatedAt={UpdatedAt}, " +
                    "TimeSinceCreation={TimeSinceCreation} seconds", 
                    projectBoard.Id, 
                    projectBoard.CreatedAt, 
                    projectBoard.UpdatedAt,
                    (DateTime.UtcNow - projectBoard.CreatedAt).TotalSeconds);
                
                // Determine build status from webhook type and details
                // Handle Railway webhook event types:
                // - "Deployment.Deployed" = SUCCESS
                // - "Deployment.Failed" = FAILED
                // - "Deployment.Crashed" = FAILED (runtime crash)
                // - "Deployment.OomKilled" = FAILED (out of memory)
                // - "VolumeAlert.Triggered" = ignore (not a deployment event)
                // - "Monitor.Triggered" = ignore (not a deployment event)
                string? buildStatus = null;
                
                // Only process deployment-related events
                if (webhookType != null && webhookType.StartsWith("Deployment.", StringComparison.OrdinalIgnoreCase))
                {
                    if (webhookType.Equals("Deployment.Deployed", StringComparison.OrdinalIgnoreCase) || 
                        deploymentStatus?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true ||
                        deploymentStatus?.Equals("DEPLOYED", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        buildStatus = "SUCCESS";
                    }
                    else if (webhookType.Equals("Deployment.Failed", StringComparison.OrdinalIgnoreCase) ||
                             webhookType.Equals("Deployment.Crashed", StringComparison.OrdinalIgnoreCase) ||
                             webhookType.Equals("Deployment.OomKilled", StringComparison.OrdinalIgnoreCase) ||
                             deploymentStatus?.Equals("FAILED", StringComparison.OrdinalIgnoreCase) == true ||
                             deploymentStatus?.Equals("CRASHED", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        buildStatus = "FAILED";
                    }
                }
                
                // Skip non-deployment events (VolumeAlert, Monitor, etc.)
                if (string.IsNullOrEmpty(webhookType) || !webhookType.StartsWith("Deployment.", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping non-deployment webhook event: {Type}", webhookType ?? "null");
                    return Ok(new { Success = true, Message = "Webhook received but not a deployment event", EventType = webhookType });
                }
                
                // Log warning if we couldn't determine build status for a deployment event
                if (buildStatus == null)
                {
                    _logger.LogWarning("Could not determine build status for deployment event: {Type}, Status: {Status}", 
                        webhookType, deploymentStatus);
                }
                
                // For failed deployments, we'll fetch build logs using Railway's buildLogs GraphQL query
                // Railway webhooks may not include detailed error information (error, errorMessage, etc. may be null)
                // For detailed runtime error logs (File, Line, StackTrace), we need the runtime error logging endpoint
                string? buildOutput = null;
                string? errorMessage = null;
                string? errorFile = null;
                int? errorLine = null;
                string? stackTrace = null;
                
                // For failed deployments, try to get build logs from Railway API and extract error info from webhook payload
                if (buildStatus == "FAILED")
                {
                    // Try to fetch build logs from Railway API using buildLogs query
                    try
                    {
                        buildOutput = await GetRailwayDeploymentOutputAsync(deploymentId);
                        if (!string.IsNullOrEmpty(buildOutput))
                        {
                            _logger.LogInformation("Retrieved raw build logs for failed deployment {DeploymentId}: {LogLength} characters (parsing with AI)", 
                                deploymentId, buildOutput.Length);
                            _logger.LogInformation("First 500 chars of buildOutput: {FirstChars}", 
                                buildOutput.Length > 500 ? buildOutput.Substring(0, 500) : buildOutput);
                            _logger.LogInformation("Last 500 chars of buildOutput: {LastChars}", 
                                buildOutput.Length > 500 ? buildOutput.Substring(buildOutput.Length - 500) : buildOutput);
                            
                            // Parse build output using AI service to extract error details (only if configured)
                            if (_deploymentsConfig.BuildErrors.SendToAISummary)
                            {
                                try
                                {
                                    var parsedOutput = await _aiService.ParseBuildOutputAsync(buildOutput);
                                if (parsedOutput != null)
                                {
                                    // Populate error fields from AI parsing
                                    if (!string.IsNullOrEmpty(parsedOutput.File))
                                    {
                                        errorFile = parsedOutput.File;
                                    }
                                    if (parsedOutput.Line.HasValue)
                                    {
                                        errorLine = parsedOutput.Line.Value;
                                    }
                                    if (!string.IsNullOrEmpty(parsedOutput.StackTrace))
                                    {
                                        stackTrace = parsedOutput.StackTrace;
                                    }
                                    if (!string.IsNullOrEmpty(parsedOutput.LatestErrorSummary))
                                    {
                                        // Store the AI-generated error summary
                                        // This will be stored in LatestErrorSummary field
                                        _logger.LogInformation("AI parsed build output: File={File}, Line={Line}, HasSummary={HasSummary}", 
                                            parsedOutput.File, parsedOutput.Line, !string.IsNullOrEmpty(parsedOutput.LatestErrorSummary));
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("AI parsing returned null for deployment {DeploymentId}", deploymentId);
                                }
                            }
                                catch (Exception aiEx)
                                {
                                    _logger.LogError(aiEx, "Failed to parse build output with AI for deployment {DeploymentId}", deploymentId);
                                    // Continue without AI parsing - we still have the raw logs
                                }
                            }
                            else
                            {
                                _logger.LogInformation("AI summarization skipped for build errors (DeploymentsConfig.BuildErrors.SendToAISummary = false)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve build logs for deployment {DeploymentId}", deploymentId);
                    }
                    
                    // Check if webhook payload contains any error information
                    if (payload.Details != null)
                    {
                        // Priority: ErrorMessage > Error > Message > Reason > Status
                        if (!string.IsNullOrEmpty(payload.Details.ErrorMessage))
                        {
                            errorMessage = payload.Details.ErrorMessage;
                        }
                        else if (!string.IsNullOrEmpty(payload.Details.Error))
                        {
                            errorMessage = payload.Details.Error;
                        }
                        else if (!string.IsNullOrEmpty(payload.Details.Message))
                        {
                            errorMessage = payload.Details.Message;
                        }
                        else if (!string.IsNullOrEmpty(payload.Details.Reason))
                        {
                            errorMessage = payload.Details.Reason;
                        }
                        
                        // If no direct error message, construct from available data
                        if (string.IsNullOrEmpty(errorMessage))
                        {
                            var errorParts = new List<string>();
                            
                            // Include webhook type for context (Deployment.crashed, Deployment.failed, etc.)
                            if (!string.IsNullOrEmpty(webhookType))
                            {
                                errorParts.Add($"Event: {webhookType}");
                            }
                            
                            if (!string.IsNullOrEmpty(payload.Details.Status))
                            {
                                errorParts.Add($"Status: {payload.Details.Status}");
                            }
                            
                            if (!string.IsNullOrEmpty(payload.Details.CommitHash))
                            {
                                errorParts.Add($"Commit: {payload.Details.CommitHash.Substring(0, Math.Min(7, payload.Details.CommitHash.Length))}");
                            }
                            
                            if (!string.IsNullOrEmpty(payload.Details.Branch))
                            {
                                errorParts.Add($"Branch: {payload.Details.Branch}");
                            }
                            
                            if (errorParts.Any())
                            {
                                errorMessage = string.Join(" | ", errorParts);
                            }
                        }
                    }
                    
                    // If we have severity info, include it
                    if (!string.IsNullOrEmpty(payload.Severity))
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage) 
                            ? $"Severity: {payload.Severity}" 
                            : $"{errorMessage} | Severity: {payload.Severity}";
                    }
                    
                    // Add Railway dashboard link for detailed logs
                    var projectId = payload.Resource?.Project?.Id;
                    var serviceId = payload.Resource?.Service?.Id;
                    var dashboardUrl = !string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(serviceId)
                        ? $"https://railway.app/project/{projectId}/service/{serviceId}/deployment/{deploymentId}"
                        : !string.IsNullOrEmpty(deploymentId)
                            ? $"https://railway.app/deployment/{deploymentId}"
                            : "https://railway.app";
                    
                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessage = $"Deployment failed ({webhookType}). Check Railway dashboard for detailed logs: {dashboardUrl}";
                    }
                    else
                    {
                        errorMessage = $"{errorMessage} | Logs: {dashboardUrl}";
                    }
                    
                    // Note: File, Line, StackTrace will be populated when PHP error logging endpoint is implemented
                    _logger.LogInformation("Extracted error info from webhook: ErrorMessage={ErrorMessage}", errorMessage);
                    _logger.LogWarning("Note: Detailed error info (File, Line, StackTrace) not available from Railway webhook/API. " +
                        "Implement PHP error logging endpoint to capture runtime errors.");
                }
                
                // Create or update BoardState record using PostgreSQL upsert (INSERT ... ON CONFLICT DO UPDATE)
                // This handles race conditions atomically - if two webhooks arrive simultaneously, only one record will exist
                var timestamp = payload.Timestamp != default ? payload.Timestamp : DateTime.UtcNow;
                var createdAt = DateTime.UtcNow;
                var updatedAt = DateTime.UtcNow;
                
                // Parse build output with AI if we have it and status is FAILED (only if configured)
                string? latestErrorSummary = null;
                if (buildStatus == "FAILED" && !string.IsNullOrEmpty(buildOutput) && _deploymentsConfig.BuildErrors.SendToAISummary)
                {
                    try
                    {
                        var parsedOutput = await _aiService.ParseBuildOutputAsync(buildOutput);
                        if (parsedOutput != null && !string.IsNullOrEmpty(parsedOutput.LatestErrorSummary))
                        {
                            latestErrorSummary = parsedOutput.LatestErrorSummary;
                            // Also update errorFile, errorLine, stackTrace if not already set
                            if (string.IsNullOrEmpty(errorFile) && !string.IsNullOrEmpty(parsedOutput.File))
                            {
                                errorFile = parsedOutput.File;
                            }
                            if (!errorLine.HasValue && parsedOutput.Line.HasValue)
                            {
                                errorLine = parsedOutput.Line.Value;
                            }
                            if (string.IsNullOrEmpty(stackTrace) && !string.IsNullOrEmpty(parsedOutput.StackTrace))
                            {
                                stackTrace = parsedOutput.StackTrace;
                            }
                        }
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogError(aiEx, "Failed to parse build output with AI for BoardId {BoardId}", boardId);
                        // Continue without AI parsing
                    }
                }
                
                // Prepare values for upsert
                var errorMsgValue = buildStatus == "FAILED" ? errorMessage : null;
                var errorFileValue = buildStatus == "FAILED" ? errorFile : null;
                var errorLineValue = buildStatus == "FAILED" ? errorLine : null;
                var errorStackTraceValue = buildStatus == "FAILED" ? stackTrace : null;
                var latestErrorSummaryValue = buildStatus == "FAILED" ? latestErrorSummary : null;
                
                // Extract commit information from webhook payload
                var latestCommitId = payload.Details?.CommitHash; // Full commit hash (SHA)
                var latestCommitDescription = payload.Details?.CommitMessage; // Commit message
                
                // Log what we're about to save
                if (!string.IsNullOrEmpty(buildOutput))
                {
                    _logger.LogInformation("Saving buildOutput to database: Length={Length}, First 200 chars: {FirstChars}", 
                        buildOutput.Length, buildOutput.Length > 200 ? buildOutput.Substring(0, 200) : buildOutput);
                }
                
                // Use parameterized SQL for atomic upsert to prevent race conditions
                // Using FormattableString for safe parameterization
                // For Railway records, GithubBranch is always NULL, so we use empty string '' as sentinel value
                // This ensures ON CONFLICT works correctly (NULL != NULL in PostgreSQL, but '' == '')
                // Railway records should be unique per board (one record per board), so using '' ensures proper conflict detection
                var railwayGithubBranch = ""; // Use empty string instead of NULL for Railway records
                FormattableString sql = $@"
                    INSERT INTO ""BoardStates"" (
                        ""BoardId"", ""Source"", ""Webhook"", ""ServiceName"", 
                        ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", 
                        ""File"", ""Line"", ""StackTrace"", ""LatestErrorSummary"", ""Timestamp"", 
                        ""LatestCommitId"", ""LatestCommitDescription"", ""DevRole"", ""GithubBranch"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        {boardId}, {"Railway"}, {true}, {serviceName}, 
                        {buildStatus}, {buildOutput}, {errorMsgValue}, 
                        {errorFileValue}, {errorLineValue}, {errorStackTraceValue}, {latestErrorSummaryValue}, 
                        {timestamp}, {latestCommitId}, {latestCommitDescription}, {"Backend"}, {railwayGithubBranch},
                        {createdAt}, {updatedAt}
                    )
                    ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                    DO UPDATE SET
                        ""ServiceName"" = EXCLUDED.""ServiceName"",
                        ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                        ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                        ""Timestamp"" = EXCLUDED.""Timestamp"",
                        ""LatestCommitId"" = EXCLUDED.""LatestCommitId"",
                        ""LatestCommitDescription"" = EXCLUDED.""LatestCommitDescription"",
                        ""DevRole"" = EXCLUDED.""DevRole"",
                        ""UpdatedAt"" = EXCLUDED.""UpdatedAt"",
                        ""ErrorMessage"" = CASE 
                            WHEN EXCLUDED.""LastBuildStatus"" = 'FAILED' THEN EXCLUDED.""ErrorMessage""
                            ELSE NULL 
                        END,
                        ""File"" = CASE 
                            WHEN EXCLUDED.""LastBuildStatus"" = 'FAILED' THEN EXCLUDED.""File""
                            ELSE NULL 
                        END,
                        ""Line"" = CASE 
                            WHEN EXCLUDED.""LastBuildStatus"" = 'FAILED' THEN EXCLUDED.""Line""
                            ELSE NULL 
                        END,
                        ""StackTrace"" = CASE 
                            WHEN EXCLUDED.""LastBuildStatus"" = 'FAILED' THEN EXCLUDED.""StackTrace""
                            ELSE NULL 
                        END,
                        ""LatestErrorSummary"" = CASE 
                            WHEN EXCLUDED.""LastBuildStatus"" = 'FAILED' THEN EXCLUDED.""LatestErrorSummary""
                            ELSE NULL 
                        END
                    ";
                
                // Log before upsert to help diagnose duplicate issues
                // Railway records use empty string '' for GithubBranch (not NULL) to ensure ON CONFLICT works
                var existingState = await _context.BoardStates
                    .FirstOrDefaultAsync(bs => bs.BoardId == boardId && 
                                               bs.Source == "Railway" && 
                                               bs.Webhook == true &&
                                               (bs.GithubBranch == "" || bs.GithubBranch == null)); // Check both for backward compatibility
                
                if (existingState != null)
                {
                    _logger.LogInformation("Updating existing BoardState: BoardId={BoardId}, Source={Source}, CurrentStatus={CurrentStatus}, NewStatus={NewStatus}", 
                        boardId, "Railway", existingState.LastBuildStatus, buildStatus);
                }
                else
                {
                    _logger.LogInformation("Creating new BoardState: BoardId={BoardId}, Source={Source}, Status={Status}", 
                        boardId, "Railway", buildStatus);
                }
                
                await _context.Database.ExecuteSqlInterpolatedAsync(sql);
                
                // Retrieve the updated record for logging
                // Railway records use empty string '' for GithubBranch (not NULL) to ensure ON CONFLICT works
                var boardState = await _context.BoardStates
                    .FirstOrDefaultAsync(bs => bs.BoardId == boardId && 
                                               bs.Source == "Railway" && 
                                               bs.Webhook == true &&
                                               (bs.GithubBranch == "" || bs.GithubBranch == null)); // Check both for backward compatibility
                
                _logger.LogInformation("BoardState upsert completed for BoardId: {BoardId}, Status: {Status}, Deployment: {DeploymentId}", 
                    boardId, buildStatus, deploymentId);
                
                if (boardState != null)
                {
                    _logger.LogInformation("BoardState fields - LastBuildOutput: {HasOutput}, ErrorMessage: {HasError}, File: {File}, Line: {Line}, StackTrace: {HasTrace}", 
                        !string.IsNullOrEmpty(boardState.LastBuildOutput), 
                        !string.IsNullOrEmpty(boardState.ErrorMessage),
                        boardState.File,
                        boardState.Line,
                        !string.IsNullOrEmpty(boardState.StackTrace));
                    
                    // Log what was actually saved to verify it matches what we sent
                    if (!string.IsNullOrEmpty(boardState.LastBuildOutput))
                    {
                        _logger.LogInformation("LastBuildOutput saved to DB: Length={Length}, First 200 chars: {FirstChars}, Last 200 chars: {LastChars}", 
                            boardState.LastBuildOutput.Length,
                            boardState.LastBuildOutput.Length > 200 ? boardState.LastBuildOutput.Substring(0, 200) : boardState.LastBuildOutput,
                            boardState.LastBuildOutput.Length > 200 ? boardState.LastBuildOutput.Substring(boardState.LastBuildOutput.Length - 200) : boardState.LastBuildOutput);
                        
                        // Count lines in saved output
                        var lineCount = boardState.LastBuildOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                        _logger.LogInformation("LastBuildOutput contains {LineCount} lines", lineCount);
                    }
                }
                else
                {
                    _logger.LogWarning("BoardState record not found after upsert for BoardId: {BoardId}, Source: Railway", boardId);
                }
                
                if (buildStatus == "FAILED")
                {
                    _logger.LogWarning("Build failure detected: Service={ServiceName}, Branch={Branch}, Commit={CommitHash}, Author={CommitAuthor}", 
                        serviceName, payload.Details?.Branch, payload.Details?.CommitHash, payload.Details?.CommitAuthor);
                }
                
                return Ok(new { Success = true, Message = "Webhook received and processed", BoardId = boardId, BuildStatus = buildStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Railway webhook");
                // Return 200 to prevent Railway from retrying (or 500 if you want retries)
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
        
        /// <summary>
        /// Helper class to return deployment information
        /// </summary>
        private class DeploymentInfo
        {
            public string? BuildOutput { get; set; }
        }
        
        /// <summary>
        /// Helper class to return parsed error details
        /// </summary>
        private class ErrorDetails
        {
            public string? ErrorMessage { get; set; }
            public string? File { get; set; }
            public int? Line { get; set; }
            public string? StackTrace { get; set; }
        }

        /// <summary>
        /// Endpoint to receive runtime errors from generated backend services
        /// Populates BoardStates table with File, Line, StackTrace, LatestErrorSummary
        /// </summary>
        [HttpPost("runtime-error")]
        public async Task<ActionResult> LogRuntimeError([FromBody] RuntimeErrorRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { Success = false, Message = "Request body is required" });
                }

                // Log error even if BoardId is missing (for debugging)
                if (string.IsNullOrWhiteSpace(request.BoardId))
                {
                    _logger.LogWarning("Received runtime error with missing BoardId. File: {File}, Line: {Line}, Message: {Message}, RequestPath: {RequestPath}, RequestMethod: {RequestMethod}", 
                        request.File, request.Line, request.Message, request.RequestPath, request.RequestMethod);
                    _logger.LogWarning("Full request payload: BoardId='{BoardId}', Message='{Message}', ExceptionType='{ExceptionType}'", 
                        request.BoardId ?? "NULL", request.Message ?? "NULL", request.ExceptionType ?? "NULL");
                    return BadRequest(new { Success = false, Message = "BoardId is required to store error in BoardStates table" });
                }

                // Validate that BoardId exists in ProjectBoards table (foreign key constraint)
                var boardExists = await _context.ProjectBoards
                    .AnyAsync(pb => pb.Id == request.BoardId);
                
                if (!boardExists)
                {
                    _logger.LogWarning("Runtime error received for non-existent BoardId: {BoardId}. Skipping log entry.", request.BoardId);
                    return BadRequest(new { Success = false, Message = $"BoardId {request.BoardId} does not exist in ProjectBoards table" });
                }

                _logger.LogInformation("Received runtime error for BoardId: {BoardId}, File: {File}, Line: {Line}", 
                    request.BoardId, request.File, request.Line);

                // If file or line is missing, try to parse from stack trace using AI
                var file = request.File;
                var line = request.Line;
                
                if ((string.IsNullOrWhiteSpace(file) || line == null) && !string.IsNullOrWhiteSpace(request.StackTrace))
                {
                    _logger.LogInformation("File or line missing, attempting AI parsing of stack trace for BoardId: {BoardId}", request.BoardId);
                    
                    try
                    {
                        var parsedOutput = await _aiService.ParseBuildOutputAsync(request.StackTrace);
                        if (parsedOutput != null)
                        {
                            if (string.IsNullOrWhiteSpace(file) && !string.IsNullOrWhiteSpace(parsedOutput.File))
                            {
                                file = parsedOutput.File;
                                _logger.LogInformation("AI parsed file from stack trace: {File}", file);
                            }
                            
                            if (line == null && parsedOutput.Line.HasValue)
                            {
                                line = parsedOutput.Line.Value;
                                _logger.LogInformation("AI parsed line from stack trace: {Line}", line);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("AI parsing returned null for BoardId: {BoardId}", request.BoardId);
                        }
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogError(aiEx, "Error calling AI service to parse stack trace for BoardId: {BoardId}", request.BoardId);
                        // Continue with original values (null/empty)
                    }
                }

                // Prepare error output
                var errorOutput = !string.IsNullOrEmpty(request.StackTrace)
                    ? $"[RUNTIME ERROR] {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
                      $"File: {file ?? "Unknown"}\n" +
                      $"Line: {line?.ToString() ?? "Unknown"}\n" +
                      $"Message: {request.Message}\n" +
                      $"Exception Type: {request.ExceptionType ?? "Unknown"}\n" +
                      $"Request Path: {request.RequestPath ?? "Unknown"}\n" +
                      $"Request Method: {request.RequestMethod ?? "Unknown"}\n" +
                      $"StackTrace:\n{request.StackTrace}"
                    : null;

                // Use PostgreSQL upsert (INSERT ... ON CONFLICT DO UPDATE) to handle race conditions
                // Unique constraint is on (BoardId, Source, Webhook)
                // Runtime errors use: BoardId, Source="RuntimeError", Webhook=false
                // Ensure timestamp is UTC (PostgreSQL requires UTC for timestamp with time zone)
                var timestamp = request.Timestamp != default 
                    ? (request.Timestamp.Kind == DateTimeKind.Utc 
                        ? request.Timestamp 
                        : request.Timestamp.ToUniversalTime()) 
                    : DateTime.UtcNow;
                var createdAt = DateTime.UtcNow;
                var updatedAt = DateTime.UtcNow;
                var source = "RuntimeError";
                var webhook = false; // Runtime errors are NOT from webhooks
                var buildStatus = "FAILED";

                // Check if record exists for logging
                var existingState = await _context.BoardStates
                    .FirstOrDefaultAsync(bs => bs.BoardId == request.BoardId && 
                                               bs.Source == source && 
                                               bs.Webhook == webhook);

                if (existingState != null)
                {
                    _logger.LogInformation("Updating existing runtime error BoardState: BoardId={BoardId}, Source={Source}, Webhook={Webhook}", 
                        request.BoardId, source, webhook);
                }
                else
                {
                    _logger.LogInformation("Creating new runtime error BoardState: BoardId={BoardId}, Source={Source}, Webhook={Webhook}", 
                        request.BoardId, source, webhook);
                }

                // Use FormattableString for safe parameterization
                FormattableString sql = $@"
                    INSERT INTO ""BoardStates"" (
                        ""BoardId"", ""Source"", ""Webhook"", 
                        ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", 
                        ""File"", ""Line"", ""StackTrace"", ""LatestErrorSummary"", ""Timestamp"", 
                        ""RequestUrl"", ""RequestMethod"", ""GithubBranch"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        {request.BoardId}, {source}, {webhook}, 
                        {buildStatus}, {errorOutput}, {request.Message}, 
                        {file}, {line}, {request.StackTrace}, {request.Message}, {timestamp}, 
                        {request.RequestPath}, {request.RequestMethod}, {(string?)null},
                        {createdAt}, {updatedAt}
                    )
                    ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                    DO UPDATE SET
                        ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                        ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                        ""ErrorMessage"" = EXCLUDED.""ErrorMessage"",
                        ""File"" = EXCLUDED.""File"",
                        ""Line"" = EXCLUDED.""Line"",
                        ""StackTrace"" = EXCLUDED.""StackTrace"",
                        ""LatestErrorSummary"" = EXCLUDED.""LatestErrorSummary"",
                        ""Timestamp"" = EXCLUDED.""Timestamp"",
                        ""RequestUrl"" = EXCLUDED.""RequestUrl"",
                        ""RequestMethod"" = EXCLUDED.""RequestMethod"",
                        ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
                    ";

                await _context.Database.ExecuteSqlInterpolatedAsync(sql);

                _logger.LogInformation("Successfully logged runtime error for BoardId: {BoardId}", request.BoardId);
                return Ok(new { Success = true, Message = "Runtime error logged successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging runtime error for BoardId: {BoardId}", request?.BoardId);
                return StatusCode(500, new { Success = false, Message = $"Error logging runtime error: {ex.Message}" });
            }
        }

        public class RuntimeErrorRequest
        {
            public string BoardId { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public string? File { get; set; }
            public int? Line { get; set; }
            public string? StackTrace { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? ExceptionType { get; set; }
            public string? RequestPath { get; set; }
            public string? RequestMethod { get; set; }
            public string? UserAgent { get; set; }
            public RuntimeErrorInnerException? InnerException { get; set; }
        }

        public class RuntimeErrorInnerException
        {
            public string? Message { get; set; }
            public string? Type { get; set; }
            public string? StackTrace { get; set; }
        }

        /// <summary>
        /// Endpoint to receive frontend runtime errors and success logs from GitHub Pages
        /// Populates BoardStates table with Source="GithubPages" and Webhook=false
        /// </summary>
        [HttpPost("runtime-error-frontend")]
        public async Task<ActionResult> LogFrontendRuntimeError([FromBody] FrontendLogRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest(new { Success = false, Message = "BoardId is required" });
                }

                // Validate that BoardId exists in ProjectBoards table (foreign key constraint)
                var boardExists = await _context.ProjectBoards
                    .AnyAsync(pb => pb.Id == request.BoardId);
                
                if (!boardExists)
                {
                    _logger.LogWarning("Frontend log received for non-existent BoardId: {BoardId}. Skipping log entry.", request.BoardId);
                    return BadRequest(new { Success = false, Message = $"BoardId {request.BoardId} does not exist in ProjectBoards table" });
                }

                _logger.LogInformation("Received frontend log for BoardId: {BoardId}, Type: {Type}, File: {File}, Line: {Line}", 
                    request.BoardId, request.Type, request.File, request.Line);

                var timestamp = request.Timestamp != default ? request.Timestamp : DateTime.UtcNow;
                var createdAt = DateTime.UtcNow;
                var updatedAt = DateTime.UtcNow;
                var source = "GithubPages";
                var webhook = false; // Frontend logs are NOT from webhooks

                string? buildStatus = null;
                string? errorOutput = null;
                string? errorMessage = null;
                string? latestErrorSummary = null;

                // Handle different log types
                if (request.Type == "FRONTEND_SUCCESS")
                {
                    buildStatus = "SUCCESS";
                    _logger.LogInformation("Frontend success log for BoardId: {BoardId}", request.BoardId);
                }
                else if (request.Type == "FRONTEND_RUNTIME" || request.Type == "FRONTEND_PROMISE_REJECTION")
                {
                    buildStatus = "FAILED";
                    errorMessage = request.Message;
                    
                    // Prepare error output
                    errorOutput = $"[FRONTEND {request.Type}] {timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
                                  $"File: {request.File ?? "Unknown"}\n" +
                                  $"Line: {request.Line?.ToString() ?? "Unknown"}\n" +
                                  $"Column: {request.Column?.ToString() ?? "Unknown"}\n" +
                                  $"Message: {request.Message}\n" +
                                  $"StackTrace:\n{request.Stack ?? "N/A"}";
                    
                    latestErrorSummary = request.Message;
                    
                    _logger.LogWarning("Frontend error log for BoardId: {BoardId}, Type: {Type}, Message: {Message}", 
                        request.BoardId, request.Type, request.Message);
                }
                else
                {
                    return BadRequest(new { Success = false, Message = $"Unknown log type: {request.Type}" });
                }

                // Use empty string instead of NULL for GithubBranch to ensure ON CONFLICT works correctly
                // PostgreSQL treats NULL values as DISTINCT in unique constraints, so multiple NULLs don't conflict
                // Empty string ensures proper conflict detection (same approach as Railway records)
                var githubPagesGithubBranch = ""; // Use empty string instead of NULL for GithubPages records
                
                // Check if record exists for logging
                // GithubPages records use empty string '' for GithubBranch (not NULL) to ensure ON CONFLICT works
                var existingState = await _context.BoardStates
                    .FirstOrDefaultAsync(bs => bs.BoardId == request.BoardId && 
                                               bs.Source == source && 
                                               bs.Webhook == webhook &&
                                               (bs.GithubBranch == "" || bs.GithubBranch == null)); // Check both for backward compatibility

                if (existingState != null)
                {
                    _logger.LogInformation("Updating existing frontend log BoardState: BoardId={BoardId}, Source={Source}, Webhook={Webhook}, Type={Type}", 
                        request.BoardId, source, webhook, request.Type);
                }
                else
                {
                    _logger.LogInformation("Creating new frontend log BoardState: BoardId={BoardId}, Source={Source}, Webhook={Webhook}, Type={Type}", 
                        request.BoardId, source, webhook, request.Type);
                }

                // Use FormattableString for safe parameterization
                FormattableString sql = $@"
                    INSERT INTO ""BoardStates"" (
                        ""BoardId"", ""Source"", ""Webhook"", 
                        ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", 
                        ""File"", ""Line"", ""StackTrace"", ""LatestErrorSummary"", ""Timestamp"", 
                        ""DevRole"", ""GithubBranch"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        {request.BoardId}, {source}, {webhook}, 
                        {buildStatus}, {errorOutput}, {errorMessage}, 
                        {request.File}, {request.Line}, {request.Stack}, {latestErrorSummary}, {timestamp}, 
                        {"Frontend"}, {githubPagesGithubBranch},
                        {createdAt}, {updatedAt}
                    )
                    ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                    DO UPDATE SET
                        ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                        ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                        ""ErrorMessage"" = EXCLUDED.""ErrorMessage"",
                        ""File"" = EXCLUDED.""File"",
                        ""Line"" = EXCLUDED.""Line"",
                        ""StackTrace"" = EXCLUDED.""StackTrace"",
                        ""LatestErrorSummary"" = EXCLUDED.""LatestErrorSummary"",
                        ""Timestamp"" = EXCLUDED.""Timestamp"",
                        ""DevRole"" = EXCLUDED.""DevRole"",
                        ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
                    ";

                await _context.Database.ExecuteSqlInterpolatedAsync(sql);

                _logger.LogInformation("Successfully logged frontend log for BoardId: {BoardId}, Type: {Type}", request.BoardId, request.Type);
                return Ok(new { Success = true, Message = "Frontend log recorded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging frontend log for BoardId: {BoardId}", request?.BoardId);
                return StatusCode(500, new { Success = false, Message = $"Error logging frontend log: {ex.Message}" });
            }
        }

        public class FrontendLogRequest
        {
            public string BoardId { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty; // FRONTEND_SUCCESS, FRONTEND_RUNTIME, FRONTEND_PROMISE_REJECTION
            public DateTime Timestamp { get; set; }
            public string? File { get; set; }
            public int? Line { get; set; }
            public int? Column { get; set; }
            public string? Stack { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// Updates BoardStates table with GitHub Pages deployment status
        /// Called after deployment and from webhooks to track Pages build status
        /// </summary>
        private async Task UpdateGithubPagesBoardStateAsync(string boardId, string owner, string repositoryName, string accessToken)
        {
            try
            {
                _logger.LogInformation("[GithubPages] Updating BoardState for BoardId: {BoardId}, Repo: {Owner}/{Repo}", boardId, owner, repositoryName);

                // Get detailed status information from GitHub Pages API
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://api.github.com/repos/{owner}/{repositoryName}/pages");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);
                string? buildStatus = null;
                string? statusMessage = null;

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var pagesInfo = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseContent, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var status = pagesInfo.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                    
                    // Map GitHub Pages status to build status
                    if (status == "built")
                    {
                        buildStatus = "SUCCESS";
                        statusMessage = "GitHub Pages deployment completed successfully";
                    }
                    else if (status == "building" || status == "queued")
                    {
                        buildStatus = "IN_PROGRESS";
                        statusMessage = $"GitHub Pages deployment {status}";
                    }
                    else if (status == "errored")
                    {
                        buildStatus = "FAILED";
                        statusMessage = "GitHub Pages deployment failed";
                    }
                    else if (status != null)
                    {
                        buildStatus = "UNKNOWN";
                        statusMessage = $"GitHub Pages status: {status}";
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    buildStatus = "NOT_ENABLED";
                    statusMessage = "GitHub Pages is not enabled for this repository";
                }

                // Only update if we got a status
                if (buildStatus != null)
                {
                    var source = "GithubPages";
                    var webhook = false; // API-triggered, not webhook
                    var githubPagesGithubBranch = ""; // Use empty string instead of NULL
                    var timestamp = DateTime.UtcNow;
                    var createdAt = DateTime.UtcNow;
                    var updatedAt = DateTime.UtcNow;

                    FormattableString sql = $@"
                        INSERT INTO ""BoardStates"" (
                            ""BoardId"", ""Source"", ""Webhook"", 
                            ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", 
                            ""Timestamp"", 
                            ""DevRole"", ""GithubBranch"",
                            ""CreatedAt"", ""UpdatedAt""
                        ) VALUES (
                            {boardId}, {source}, {webhook}, 
                            {buildStatus}, {statusMessage}, NULL, 
                            {timestamp}, 
                            {"Frontend"}, {githubPagesGithubBranch},
                            {createdAt}, {updatedAt}
                        )
                        ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                        DO UPDATE SET
                            ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                            ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                            ""Timestamp"" = EXCLUDED.""Timestamp"",
                            ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
                    ";

                    await _context.Database.ExecuteSqlInterpolatedAsync(sql);
                    _logger.LogInformation("[GithubPages] ✅ Updated BoardState for BoardId: {BoardId}, Status: {Status}", boardId, buildStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GithubPages] ❌ Error updating BoardState for BoardId: {BoardId}", boardId);
            }
        }
        
        /// <summary>
        /// Helper method to get Railway deployment information (build output)
        /// </summary>
        private async Task<DeploymentInfo> GetRailwayDeploymentInfoAsync(string deploymentId)
        {
            var result = new DeploymentInfo();
            result.BuildOutput = await GetRailwayDeploymentOutputAsync(deploymentId);
            return result;
        }
        
        /// <summary>
        /// Helper method to process log entries from Railway GraphQL response
        /// </summary>
        private void ProcessLogEntries(System.Text.Json.JsonElement logEntriesProp, string logType, 
            List<string> allLogEntries, List<string> errorLogEntries, List<string> warningLogEntries)
        {
            foreach (var logEntry in logEntriesProp.EnumerateArray())
            {
                var message = logEntry.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString()?.ToUpperInvariant() : "";
                var timestamp = logEntry.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : "";
                
                if (!string.IsNullOrEmpty(message))
                {
                    var logLine = $"[{logType}] [{severity}] {timestamp}: {message}";
                    allLogEntries.Add(logLine);
                    
                    // Check for error patterns in message content (not just severity)
                    // Support all 7 languages: PHP, Python, C#, Java, Go, Ruby, Node.js
                    var isError = severity == "ERROR" || severity == "FATAL" || 
                        // C# errors
                        message.Contains("error CS", StringComparison.OrdinalIgnoreCase) ||
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"error\s+CS\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"/app/.*\.cs\(\d+,\d+\):\s*error", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // Java errors
                        System.Text.RegularExpressions.Regex.IsMatch(message, @".*\.java:\d+:\s*error:", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // Go errors
                        System.Text.RegularExpressions.Regex.IsMatch(message, @".*\.go:\d+:\d*:\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // Ruby errors
                        System.Text.RegularExpressions.Regex.IsMatch(message, @".*\.rb:\d+:", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        message.Contains("SyntaxError", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("NameError", StringComparison.OrdinalIgnoreCase) ||
                        // Python errors
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"File\s+[""'].*\.py[""'],\s+line\s+\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // PHP errors
                        message.Contains("Parse error", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("Fatal error", StringComparison.OrdinalIgnoreCase) ||
                        // Node.js/TypeScript errors
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"error\s+TS\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        System.Text.RegularExpressions.Regex.IsMatch(message, @".*\.(ts|tsx|js)\(\d+,\d+\):\s*error", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // Generic errors
                        message.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("Build failed", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase);
                    
                    var isWarning = severity == "WARNING" || severity == "WARN" || 
                        // C# warnings
                        message.Contains("warning CS", StringComparison.OrdinalIgnoreCase) ||
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"warning\s+CS\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // Java warnings
                        System.Text.RegularExpressions.Regex.IsMatch(message, @".*\.java:\d+:\s*warning:", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // TypeScript warnings
                        System.Text.RegularExpressions.Regex.IsMatch(message, @"warning\s+TS\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        // PHP warnings
                        message.Contains("Warning:", StringComparison.OrdinalIgnoreCase);
                    
                    // Prioritize error and warning logs
                    if (isError)
                    {
                        errorLogEntries.Add(logLine);
                    }
                    else if (isWarning)
                    {
                        warningLogEntries.Add(logLine);
                    }
                }
            }
        }
        
        /// <summary>
        /// Helper method to get Railway deployment build output using buildLogs and deploymentLogs GraphQL queries
        /// Uses both buildLogs (build-time) and deploymentLogs (runtime) root queries to capture all errors
        /// Returns ALL logs without filtering to ensure no errors are missed
        /// </summary>
        private async Task<string?> GetRailwayDeploymentOutputAsync(string deploymentId)
        {
            try
            {
                var railwayApiToken = _configuration["Railway:ApiToken"];
                var railwayApiUrl = _configuration["Railway:ApiUrl"] ?? "https://backboard.railway.app/graphql/v2";
                
                if (string.IsNullOrWhiteSpace(railwayApiToken) || railwayApiToken == "your-railway-api-token-here")
                {
                    _logger.LogWarning("Railway API token not configured, cannot retrieve deployment output");
                    return null;
                }
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");
                
                // Step 1: Query deployment status (for logging purposes)
                // Note: Railway's Deployment type only has id and status fields - no error message fields available
                try
                {
                    var deploymentQuery = new
                    {
                        query = @"
                            query GetDeploymentStatus($deploymentId: String!) {
                                deployment(id: $deploymentId) {
                                    id
                                    status
                                }
                            }",
                        variables = new { deploymentId = deploymentId }
                    };
                    
                    var deploymentQueryBody = System.Text.Json.JsonSerializer.Serialize(deploymentQuery);
                    var deploymentQueryContent = new StringContent(deploymentQueryBody, System.Text.Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Querying Railway API for deployment status for deployment {DeploymentId}", deploymentId);
                    
                    var deploymentResponse = await httpClient.PostAsync(railwayApiUrl, deploymentQueryContent);
                    var deploymentResponseContent = await deploymentResponse.Content.ReadAsStringAsync();
                    
                    if (deploymentResponse.IsSuccessStatusCode)
                    {
                        using var deploymentDoc = System.Text.Json.JsonDocument.Parse(deploymentResponseContent);
                        
                        if (deploymentDoc.RootElement.TryGetProperty("data", out var deploymentData) &&
                            deploymentData.TryGetProperty("deployment", out var deploymentObj))
                        {
                            if (deploymentObj.TryGetProperty("status", out var statusProp))
                            {
                                var status = statusProp.GetString();
                                _logger.LogInformation("Deployment status: {Status}", status);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query deployment status, continuing with log queries");
                }
                
                // Step 2: Try querying with filter="ERROR" to get error logs directly (bypasses truncation)
                // Also try filtering by message content (ERROR, FAILED, BUILD FAILURE, etc.)
                var errorFilteredLogs = new List<System.Text.Json.JsonElement>();
                try
                {
                    // Try severity-based filter first
                    var errorFilterQuery = new
                    {
                        query = @"
                            query GetErrorLogs($deploymentId: String!) {
                                buildLogs: buildLogs(deploymentId: $deploymentId, filter: ""ERROR"", limit: 200) {
                                    message
                                    severity
                                    timestamp
                                }
                                deploymentLogs: deploymentLogs(deploymentId: $deploymentId, filter: ""ERROR"", limit: 200) {
                                    message
                                    severity
                                    timestamp
                                }
                            }",
                        variables = new { deploymentId = deploymentId }
                    };
                    
                    var errorFilterQueryBody = System.Text.Json.JsonSerializer.Serialize(errorFilterQuery);
                    var errorFilterQueryContent = new StringContent(errorFilterQueryBody, System.Text.Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Querying Railway API for ERROR-filtered logs (severity filter) for deployment {DeploymentId}", deploymentId);
                    
                    var errorFilterResponse = await httpClient.PostAsync(railwayApiUrl, errorFilterQueryContent);
                    var errorFilterResponseContent = await errorFilterResponse.Content.ReadAsStringAsync();
                    
                    if (errorFilterResponse.IsSuccessStatusCode)
                    {
                        using var errorFilterDoc = System.Text.Json.JsonDocument.Parse(errorFilterResponseContent);
                        if (errorFilterDoc.RootElement.TryGetProperty("data", out var errorFilterData))
                        {
                            if (errorFilterData.TryGetProperty("buildLogs", out var errorBuildLogsProp) && 
                                errorBuildLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var logEntry in errorBuildLogsProp.EnumerateArray())
                                {
                                    var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                        logEntry.GetRawText());
                                    errorFilteredLogs.Add(cloned);
                                }
                                _logger.LogInformation("Retrieved {Count} ERROR-filtered buildLogs (severity)", errorBuildLogsProp.GetArrayLength());
                            }
                            
                            if (errorFilterData.TryGetProperty("deploymentLogs", out var errorDeployLogsProp) && 
                                errorDeployLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var logEntry in errorDeployLogsProp.EnumerateArray())
                                {
                                    var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                        logEntry.GetRawText());
                                    errorFilteredLogs.Add(cloned);
                                }
                                _logger.LogInformation("Retrieved {Count} ERROR-filtered deploymentLogs (severity)", errorDeployLogsProp.GetArrayLength());
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ERROR severity filter query failed: StatusCode={StatusCode}, Response={Response}", 
                            errorFilterResponse.StatusCode, errorFilterResponseContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query ERROR-filtered logs, continuing with regular log queries");
                }
                
                // Step 3: Query both buildLogs and deploymentLogs to capture build-time and runtime errors
                // Try to use pageInfo-based pagination with 'after' parameter and pageInfo
                const int pageSize = 500; // Use smaller page size for pagination
                var allBuildLogs = new List<System.Text.Json.JsonElement>();
                var allDeploymentLogs = new List<System.Text.Json.JsonElement>();
                
                // Try pagination with pageInfo first
                string? buildLogsCursor = null;
                string? deploymentLogsCursor = null;
                bool buildLogsHasMore = true;
                bool deploymentLogsHasMore = true;
                int buildLogsPageNumber = 1;
                int deploymentLogsPageNumber = 1;
                
                // Pagination loop for buildLogs
                while (buildLogsHasMore)
                {
                    var buildLogsQuery = new
                    {
                        query = $@"
                            query GetBuildLogs($deploymentId: String!, $after: String) {{
                                buildLogs: buildLogs(deploymentId: $deploymentId, limit: {pageSize}, after: $after) {{
                                    message
                                    severity
                                    timestamp
                                    pageInfo {{
                                        hasNextPage
                                        endCursor
                                    }}
                                }}
                            }}",
                        variables = new { 
                            deploymentId = deploymentId,
                            after = buildLogsCursor
                        }
                    };
                    
                    var queryBody = System.Text.Json.JsonSerializer.Serialize(buildLogsQuery);
                    var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Querying Railway API for buildLogs page {PageNumber} for deployment {DeploymentId} with limit {Limit}, after cursor: {Cursor}", 
                        buildLogsPageNumber, deploymentId, pageSize, buildLogsCursor ?? "none (first page)");
                    _logger.LogDebug("Railway GraphQL buildLogs pagination Query: {Query}", queryBody);
                    
                    var response = await httpClient.PostAsync(railwayApiUrl, queryContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    _logger.LogInformation("Railway API buildLogs pagination query response: StatusCode={StatusCode}, ResponseLength={Length}", 
                        response.StatusCode, responseContent != null ? responseContent.Length : 0);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Railway API buildLogs pagination query failed: StatusCode={StatusCode}, Response={Response}", 
                            response.StatusCode, responseContent);
                        // If pagination fails, break and try fallback
                        break;
                    }
                    
                    System.Text.Json.JsonDocument? responseDoc = null;
                    try
                    {
                        responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                        
                        // Check for GraphQL errors
                        if (responseDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                        {
                            var errors = errorsProp.EnumerateArray().ToList();
                            bool hasPaginationError = false;
                            foreach (var error in errors)
                            {
                                var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                _logger.LogWarning("GraphQL error in buildLogs pagination query: {Error}", errorMsg);
                                if (errorMsg != null && (errorMsg.Contains("after", StringComparison.OrdinalIgnoreCase) || 
                                    errorMsg.Contains("pageInfo", StringComparison.OrdinalIgnoreCase)))
                                {
                                    hasPaginationError = true;
                                }
                            }
                            
                            if (hasPaginationError)
                            {
                                _logger.LogInformation("Railway doesn't support pagination (after/pageInfo). Will try fallback without pagination.");
                                responseDoc.Dispose();
                                break; // Exit pagination loop, will use fallback
                            }
                            else
                            {
                                return null; // Other error
                            }
                        }
                        
                        // Extract logs and pageInfo
                        if (responseDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                            dataObj.TryGetProperty("buildLogs", out var buildLogsProp))
                        {
                            // Check if buildLogs is an array (direct) or has pageInfo (connection pattern)
                            if (buildLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                // Direct array - no pagination support
                                _logger.LogInformation("buildLogs returned as direct array (no pageInfo). Pagination not supported.");
                                var arrayLength = buildLogsProp.GetArrayLength();
                                foreach (var logEntry in buildLogsProp.EnumerateArray())
                                {
                                    var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                        logEntry.GetRawText());
                                    allBuildLogs.Add(cloned);
                                }
                                _logger.LogInformation("Retrieved {Count} buildLogs entries from direct array", arrayLength);
                                buildLogsHasMore = false; // No more pages
                            }
                            else if (buildLogsProp.TryGetProperty("pageInfo", out var pageInfoProp))
                            {
                                // Connection pattern with pageInfo
                                _logger.LogInformation("buildLogs uses connection pattern with pageInfo - pagination supported!");
                                
                                // Extract pageInfo
                                if (pageInfoProp.TryGetProperty("hasNextPage", out var hasNextPageProp))
                                {
                                    buildLogsHasMore = hasNextPageProp.GetBoolean();
                                }
                                if (pageInfoProp.TryGetProperty("endCursor", out var endCursorProp))
                                {
                                    buildLogsCursor = endCursorProp.GetString();
                                }
                                
                                // Extract log entries (could be in 'edges' or direct array)
                                if (buildLogsProp.TryGetProperty("edges", out var edgesProp) && 
                                    edgesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    // GraphQL connection pattern (edges/node)
                                    foreach (var edge in edgesProp.EnumerateArray())
                                    {
                                        if (edge.TryGetProperty("node", out var nodeProp))
                                        {
                                            var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                nodeProp.GetRawText());
                                            allBuildLogs.Add(cloned);
                                        }
                                    }
                                    _logger.LogInformation("Retrieved {Count} buildLogs entries from edges (page {PageNumber}). hasNextPage: {HasNext}, endCursor: {Cursor}", 
                                        edgesProp.GetArrayLength(), buildLogsPageNumber, buildLogsHasMore, buildLogsCursor ?? "none");
                                }
                                else if (buildLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    // Logs might be directly in buildLogs array with pageInfo as a sibling
                                    // This is unusual but possible
                                    foreach (var logEntry in buildLogsProp.EnumerateArray())
                                    {
                                        if (!logEntry.TryGetProperty("pageInfo", out _)) // Skip pageInfo entries
                                        {
                                            var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                logEntry.GetRawText());
                                            allBuildLogs.Add(cloned);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // No pageInfo, no edges - might be direct array or different structure
                                _logger.LogWarning("buildLogs response structure unclear - no pageInfo, no edges, not an array. ValueKind: {ValueKind}", 
                                    buildLogsProp.ValueKind);
                                var rawText = buildLogsProp.GetRawText();
                                _logger.LogDebug("buildLogs raw structure (first 500 chars): {Structure}", 
                                    rawText.Length > 500 ? rawText.Substring(0, 500) : rawText);
                                buildLogsHasMore = false;
                            }
                        }
                    }
                    finally
                    {
                        responseDoc?.Dispose();
                    }
                    
                    buildLogsPageNumber++;
                    
                    // Safety limit
                    if (buildLogsPageNumber > 20)
                    {
                        _logger.LogWarning("Reached maximum page limit (20 pages) for buildLogs. Stopping pagination.");
                        break;
                    }
                }
                
                // Pagination loop for deploymentLogs (same pattern)
                while (deploymentLogsHasMore)
                {
                    var deploymentLogsQuery = new
                    {
                        query = $@"
                            query GetDeploymentLogs($deploymentId: String!, $after: String) {{
                                deploymentLogs: deploymentLogs(deploymentId: $deploymentId, limit: {pageSize}, after: $after) {{
                                    message
                                    severity
                                    timestamp
                                    pageInfo {{
                                        hasNextPage
                                        endCursor
                                    }}
                                }}
                            }}",
                        variables = new { 
                            deploymentId = deploymentId,
                            after = deploymentLogsCursor
                        }
                    };
                    
                    var queryBody = System.Text.Json.JsonSerializer.Serialize(deploymentLogsQuery);
                    var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Querying Railway API for deploymentLogs page {PageNumber} for deployment {DeploymentId} with limit {Limit}, after cursor: {Cursor}", 
                        deploymentLogsPageNumber, deploymentId, pageSize, deploymentLogsCursor ?? "none (first page)");
                    
                    var response = await httpClient.PostAsync(railwayApiUrl, queryContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Railway API deploymentLogs pagination query failed: StatusCode={StatusCode}", response.StatusCode);
                        break;
                    }
                    
                    System.Text.Json.JsonDocument? responseDoc = null;
                    try
                    {
                        responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                        
                        if (responseDoc.RootElement.TryGetProperty("errors", out var errorsProp))
                        {
                            var errors = errorsProp.EnumerateArray().ToList();
                            bool hasPaginationError = false;
                            foreach (var error in errors)
                            {
                                var errorMsg = error.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                                if (errorMsg != null && (errorMsg.Contains("after", StringComparison.OrdinalIgnoreCase) || 
                                    errorMsg.Contains("pageInfo", StringComparison.OrdinalIgnoreCase)))
                                {
                                    hasPaginationError = true;
                                }
                            }
                            
                            if (hasPaginationError)
                            {
                                _logger.LogInformation("Railway doesn't support pagination for deploymentLogs. Stopping pagination.");
                                break;
                            }
                        }
                        
                        if (responseDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                            dataObj.TryGetProperty("deploymentLogs", out var deploymentLogsProp))
                        {
                            if (deploymentLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var arrayLength = deploymentLogsProp.GetArrayLength();
                                foreach (var logEntry in deploymentLogsProp.EnumerateArray())
                                {
                                    var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                        logEntry.GetRawText());
                                    allDeploymentLogs.Add(cloned);
                                }
                                _logger.LogInformation("Retrieved {Count} deploymentLogs entries from direct array", arrayLength);
                                deploymentLogsHasMore = false;
                            }
                            else if (deploymentLogsProp.TryGetProperty("pageInfo", out var pageInfoProp))
                            {
                                if (pageInfoProp.TryGetProperty("hasNextPage", out var hasNextPageProp))
                                {
                                    deploymentLogsHasMore = hasNextPageProp.GetBoolean();
                                }
                                if (pageInfoProp.TryGetProperty("endCursor", out var endCursorProp))
                                {
                                    deploymentLogsCursor = endCursorProp.GetString();
                                }
                                
                                if (deploymentLogsProp.TryGetProperty("edges", out var edgesProp) && 
                                    edgesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var edge in edgesProp.EnumerateArray())
                                    {
                                        if (edge.TryGetProperty("node", out var nodeProp))
                                        {
                                            var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                nodeProp.GetRawText());
                                            allDeploymentLogs.Add(cloned);
                                        }
                                    }
                                    _logger.LogInformation("Retrieved {Count} deploymentLogs entries from edges (page {PageNumber}). hasNextPage: {HasNext}", 
                                        edgesProp.GetArrayLength(), deploymentLogsPageNumber, deploymentLogsHasMore);
                                }
                            }
                            else
                            {
                                deploymentLogsHasMore = false;
                            }
                        }
                    }
                    finally
                    {
                        responseDoc?.Dispose();
                    }
                    
                    deploymentLogsPageNumber++;
                    
                    if (deploymentLogsPageNumber > 20)
                    {
                        _logger.LogWarning("Reached maximum page limit (20 pages) for deploymentLogs. Stopping pagination.");
                        break;
                    }
                }
                
                // If pagination didn't work or returned no results, try fallback without pagination
                if (allBuildLogs.Count == 0 && allDeploymentLogs.Count == 0)
                {
                    _logger.LogInformation("Pagination returned no results. Trying fallback query without pagination...");
                    const int maxLimit = 5000;
                    var fallbackQuery = new
                    {
                        query = $@"
                            query GetLogs($deploymentId: String!) {{
                                buildLogs: buildLogs(deploymentId: $deploymentId, limit: {maxLimit}) {{
                                    message
                                    severity
                                    timestamp
                                }}
                                deploymentLogs: deploymentLogs(deploymentId: $deploymentId, limit: {maxLimit}) {{
                                    message
                                    severity
                                    timestamp
                                }}
                            }}",
                        variables = new { deploymentId = deploymentId }
                    };
                    
                    var queryBody = System.Text.Json.JsonSerializer.Serialize(fallbackQuery);
                    var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Querying Railway API for build and deployment logs (fallback, no pagination) for deployment {DeploymentId} with limit {Limit}", 
                        deploymentId, maxLimit);
                    
                    var response = await httpClient.PostAsync(railwayApiUrl, queryContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        System.Text.Json.JsonDocument? responseDoc = null;
                        try
                        {
                            responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                            
                            if (responseDoc.RootElement.TryGetProperty("data", out var dataObj))
                            {
                                if (dataObj.TryGetProperty("buildLogs", out var buildLogsProp) && 
                                    buildLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var logEntry in buildLogsProp.EnumerateArray())
                                    {
                                        var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                            logEntry.GetRawText());
                                        allBuildLogs.Add(cloned);
                                    }
                                }
                                
                                if (dataObj.TryGetProperty("deploymentLogs", out var deploymentLogsProp) && 
                                    deploymentLogsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var logEntry in deploymentLogsProp.EnumerateArray())
                                    {
                                        var cloned = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                            logEntry.GetRawText());
                                        allDeploymentLogs.Add(cloned);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            responseDoc?.Dispose();
                        }
                    }
                }
                
                // Log summary of retrieved logs
                _logger.LogInformation("=== SUMMARY: TOTAL LOGS RETRIEVED (BEFORE FILTERING) ===");
                _logger.LogInformation("Total buildLogs: {BuildCount}, Total deploymentLogs: {DeployCount}, Combined Total: {TotalCount}", 
                    allBuildLogs.Count, allDeploymentLogs.Count, allBuildLogs.Count + allDeploymentLogs.Count);
                
                // Count by severity BEFORE filtering
                var buildLogsBySeverity = new Dictionary<string, int>();
                foreach (var logEntry in allBuildLogs)
                {
                    var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() ?? "unknown" : "unknown";
                    buildLogsBySeverity[severity] = buildLogsBySeverity.GetValueOrDefault(severity, 0) + 1;
                }
                _logger.LogInformation("buildLogs severity breakdown (BEFORE filtering): {SeverityCounts}", 
                    string.Join(", ", buildLogsBySeverity.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                
                var deploymentLogsBySeverity = new Dictionary<string, int>();
                foreach (var logEntry in allDeploymentLogs)
                {
                    var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() ?? "unknown" : "unknown";
                    deploymentLogsBySeverity[severity] = deploymentLogsBySeverity.GetValueOrDefault(severity, 0) + 1;
                }
                _logger.LogInformation("deploymentLogs severity breakdown (BEFORE filtering): {SeverityCounts}", 
                    string.Join(", ", deploymentLogsBySeverity.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                
                if (allBuildLogs.Count == 0 && allDeploymentLogs.Count == 0)
                {
                    _logger.LogWarning("No logs found for deployment {DeploymentId}", deploymentId);
                    return null;
                }
                
                // Extract logs from both buildLogs and deploymentLogs - NO FILTERING, INCLUDE EVERYTHING
                var logEntries = new List<(string Severity, string Timestamp, string Message)>();
                
                // Process all build logs - NO FILTERING, INCLUDE EVERYTHING FROM API
                foreach (var logEntry in allBuildLogs)
                {
                    var message = logEntry.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                    var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() : "";
                    var timestamp = logEntry.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : "";
                    
                    // Include ALL logs, even if message is empty
                    logEntries.Add((severity ?? "", timestamp ?? "", message ?? ""));
                }
                
                // Process deployment logs - NO FILTERING, INCLUDE EVERYTHING FROM API
                foreach (var logEntry in allDeploymentLogs)
                {
                    var message = logEntry.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                    var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() : "";
                    var timestamp = logEntry.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : "";
                    
                    // Include ALL logs, even if message is empty
                    logEntries.Add((severity ?? "", timestamp ?? "", message ?? ""));
                }
                
                // === LOG ENTRIES BY SEVERITY (NO FILTERING - ALL LOGS FROM API) ===
                var totalWarnCount = logEntries.Count(e => e.Severity.Equals("warn", StringComparison.OrdinalIgnoreCase));
                var totalErrorCount = logEntries.Count(e => e.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
                var totalInfoCount = logEntries.Count(e => e.Severity.Equals("info", StringComparison.OrdinalIgnoreCase));
                var totalDebugCount = logEntries.Count(e => e.Severity.Equals("debug", StringComparison.OrdinalIgnoreCase));
                _logger.LogInformation("=== ALL LOGS FROM API (NO FILTERING) ===");
                _logger.LogInformation("warn: {WarnCount}, error: {ErrorCount}, info: {InfoCount}, debug: {DebugCount}, total: {Total}",
                    totalWarnCount, totalErrorCount, totalInfoCount, totalDebugCount, logEntries.Count);
                
                // Combine ERROR-filtered logs and all logs - NO FILTERING
                var allLogLines = new List<string>();
                
                // Add ERROR-filtered logs first (if any - these bypass truncation)
                if (errorFilteredLogs.Count > 0)
                {
                    allLogLines.Add("=== ERROR-FILTERED LOGS (from filter=ERROR query) ===");
                    foreach (var logEntry in errorFilteredLogs)
                    {
                        var message = logEntry.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                        var severity = logEntry.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() : "";
                        var timestamp = logEntry.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : "";
                        allLogLines.Add($"[{severity}] {timestamp}: {message ?? "(empty message)"}");
                    }
                    _logger.LogInformation("Including {Count} ERROR-filtered log entries", errorFilteredLogs.Count);
                }
                
                // Add all logs from regular query - NO FILTERING
                if (logEntries.Count > 0)
                {
                    if (errorFilteredLogs.Count > 0)
                    {
                        allLogLines.Add("=== ALL LOGS (from regular query) ===");
                    }
                    foreach (var (severity, timestamp, message) in logEntries)
                    {
                        var logLine = $"[{severity}] {timestamp}: {message ?? "(empty message)"}";
                        allLogLines.Add(logLine);
                    }
                }
                
                var combinedLogs = string.Join("\n", allLogLines);
                var totalLines = allLogLines.Count;
                _logger.LogInformation("Returning ALL logs for deployment {DeploymentId}: {TotalLines} lines (ERROR-filtered: {ErrorFilteredCount}, regular: {RegularCount}). Log length: {Length} chars", 
                    deploymentId, totalLines, errorFilteredLogs.Count, logEntries.Count, combinedLogs.Length);
                
                return combinedLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while retrieving deployment output for deployment {DeploymentId}", deploymentId);
            }
            
            return null;
        }

        /// <summary>
        /// Get mentor response using a specific AI model
        /// </summary>
        [HttpPost("use/respond/{aiModelName}")]
        public async Task<ActionResult<object>> GetMentorResponse(
            string aiModelName,
            [FromBody] MentorRequest request)
        {
            try
            {
                _logger.LogInformation("Getting mentor response for StudentId: {StudentId}, SprintId: {SprintId}, Model: {Model}", 
                    request.StudentId, request.SprintId, aiModelName);

                // Get the AI model from database
                var aiModel = await _context.AIModels
                    .FirstOrDefaultAsync(m => m.Name == aiModelName && m.IsActive);

                if (aiModel == null)
                {
                    _logger.LogWarning("AI model '{ModelName}' not found or not active", aiModelName);
                    return NotFound(new { Success = false, Message = $"AI model '{aiModelName}' not found or not active" });
                }

                // Get mentor intent
                var userQuestion = request.UserQuestion ?? "";
                var intent = await _intentService.DetectIntentAsync(userQuestion);

                // Handle based on intent
                return intent.Type switch
                {
                    "code_review" => await HandleCodeReviewIntent(request, intent, aiModel),
                    "github_help" => await HandleGitHubHelpIntent(request, intent),
                    _ => await HandleGeneralMentorResponse(request, intent, aiModel)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor response for StudentId: {StudentId}, SprintId: {SprintId}, Model: {Model}",
                    request.StudentId, request.SprintId, aiModelName);
                return StatusCode(500, new { Success = false, Message = "An error occurred while processing your request." });
            }
        }
        
        /// <summary>
        /// Parses error details from build output/logs
        /// Extracts ErrorMessage, File, Line, and StackTrace from common error formats
        /// </summary>
        private ErrorDetails ParseErrorFromBuildOutput(string buildOutput)
        {
            var errorDetails = new ErrorDetails();
            
            if (string.IsNullOrWhiteSpace(buildOutput))
            {
                return errorDetails;
            }
            
            try
            {
                var lines = buildOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Look for common error patterns
                foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                    
                    // PHP errors: "Parse error: syntax error, unexpected 'X' in /path/to/file.php on line Y"
                    var phpErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"(Parse error|Fatal error|Warning|Error):\s*(.+?)\s+in\s+(.+?)\s+on\s+line\s+(\d+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (phpErrorMatch.Success)
                    {
                        errorDetails.ErrorMessage = phpErrorMatch.Groups[1].Value + ": " + phpErrorMatch.Groups[2].Value.Trim();
                        errorDetails.File = phpErrorMatch.Groups[3].Value.Trim();
                        if (int.TryParse(phpErrorMatch.Groups[4].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        break;
                    }
                    
                    // Python errors: "File \"/path/to/file.py\", line X, in function\n    Error: message"
                    var pythonErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"File\s+[""'](.+?)[""'],\s+line\s+(\d+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (pythonErrorMatch.Success)
                    {
                        errorDetails.File = pythonErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(pythonErrorMatch.Groups[2].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        // Look for error message in next few lines
                        var currentIndex = Array.IndexOf(lines, line);
                        if (currentIndex >= 0 && currentIndex + 1 < lines.Length)
                        {
                            var errorTypeMatch = System.Text.RegularExpressions.Regex.Match(lines[currentIndex + 1].Trim(), 
                                @"(\w+Error|Exception):\s*(.+)");
                            if (errorTypeMatch.Success)
                            {
                                errorDetails.ErrorMessage = errorTypeMatch.Groups[1].Value + ": " + errorTypeMatch.Groups[2].Value.Trim();
                            }
                        }
                        break;
                    }
                    
                    // C# compile errors: "/path/to/file.cs(line,column): error CS####: message"
                    // Example: "/app/Controllers/TestController.cs(10,5): error CS1002: ; expected"
                    var csErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.cs)\((\d+),(\d+)\):\s*(error|warning)\s+(CS\d+):\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (csErrorMatch.Success)
                    {
                        errorDetails.File = csErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(csErrorMatch.Groups[2].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorCode = csErrorMatch.Groups[5].Value.Trim();
                        var errorMsg = csErrorMatch.Groups[6].Value.Trim();
                        errorDetails.ErrorMessage = $"error {errorCode}: {errorMsg}";
                        break; // Found C# error, stop looking
                    }
                    
                    // C# error alternative format: "error CS####: message [file(line,column)]"
                    var csErrorAltMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"(error|warning)\s+(CS\d+):\s*(.+?)(?:\s+\[([/\w\\\-\.]+\.cs)\((\d+),(\d+)\)\])?", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (csErrorAltMatch.Success && string.IsNullOrEmpty(errorDetails.ErrorMessage))
                    {
                        var errorCode = csErrorAltMatch.Groups[2].Value.Trim();
                        var errorMsg = csErrorAltMatch.Groups[3].Value.Trim();
                        errorDetails.ErrorMessage = $"error {errorCode}: {errorMsg}";
                        
                        if (csErrorAltMatch.Groups[4].Success && !string.IsNullOrEmpty(csErrorAltMatch.Groups[4].Value))
                        {
                            errorDetails.File = csErrorAltMatch.Groups[4].Value.Trim();
                            if (int.TryParse(csErrorAltMatch.Groups[5].Value, out var lineNum))
                            {
                                errorDetails.Line = lineNum;
                            }
                        }
                    }
                    
                    // Java compile errors: "/path/to/file.java:123: error: message" or "error: message\n    ^\n  location: file.java:123"
                    var javaErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.java):(\d+):\s*(error|warning):\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (javaErrorMatch.Success)
                    {
                        errorDetails.File = javaErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(javaErrorMatch.Groups[2].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorType = javaErrorMatch.Groups[3].Value.Trim();
                        var errorMsg = javaErrorMatch.Groups[4].Value.Trim();
                        errorDetails.ErrorMessage = $"{errorType}: {errorMsg}";
                        break;
                    }
                    
                    // Go compile errors: "/path/to/file.go:123:5: message" or "file.go:123: undefined: variable"
                    var goErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.go):(\d+)(?::(\d+))?:\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (goErrorMatch.Success)
                    {
                        errorDetails.File = goErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(goErrorMatch.Groups[2].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorMsg = goErrorMatch.Groups[4].Value.Trim();
                        errorDetails.ErrorMessage = errorMsg;
                        break;
                    }
                    
                    // Ruby errors: "/path/to/file.rb:123:in `method': message" or "SyntaxError: /path/to/file.rb:123: message"
                    var rubyErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.rb):(\d+)(?::in\s+[`'""](.+?)[`'""])?:\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (rubyErrorMatch.Success)
                    {
                        errorDetails.File = rubyErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(rubyErrorMatch.Groups[2].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorMsg = rubyErrorMatch.Groups[4].Value.Trim();
                        if (string.IsNullOrEmpty(errorMsg) && rubyErrorMatch.Groups.Count > 4)
                        {
                            errorMsg = trimmedLine; // Fallback to full line
                        }
                        errorDetails.ErrorMessage = errorMsg;
                        break;
                    }
                    
                    // Ruby syntax errors: "SyntaxError: /path/to/file.rb:123: message"
                    var rubySyntaxErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"(SyntaxError|NameError|NoMethodError|ArgumentError):\s*([/\w\\\-\.]+\.rb):(\d+):\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (rubySyntaxErrorMatch.Success && string.IsNullOrEmpty(errorDetails.ErrorMessage))
                    {
                        errorDetails.File = rubySyntaxErrorMatch.Groups[2].Value.Trim();
                        if (int.TryParse(rubySyntaxErrorMatch.Groups[3].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorType = rubySyntaxErrorMatch.Groups[1].Value.Trim();
                        var errorMsg = rubySyntaxErrorMatch.Groups[4].Value.Trim();
                        errorDetails.ErrorMessage = $"{errorType}: {errorMsg}";
                        break;
                    }
                    
                    // Node.js/JavaScript errors: "Error: message\n    at function (file:line:column)"
                    var jsErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"Error:\s*(.+?)(?:\n|$)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (jsErrorMatch.Success && string.IsNullOrEmpty(errorDetails.ErrorMessage))
                    {
                        errorDetails.ErrorMessage = "Error: " + jsErrorMatch.Groups[1].Value.Trim();
                    }
                    
                    // Node.js/TypeScript compile errors: "file.ts(123,5): error TS####: message"
                    var tsErrorMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.(ts|tsx))\((\d+),(\d+)\):\s*(error|warning)\s+(TS\d+):\s*(.+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (tsErrorMatch.Success && string.IsNullOrEmpty(errorDetails.ErrorMessage))
                    {
                        errorDetails.File = tsErrorMatch.Groups[1].Value.Trim();
                        if (int.TryParse(tsErrorMatch.Groups[3].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                        var errorCode = tsErrorMatch.Groups[6].Value.Trim();
                        var errorMsg = tsErrorMatch.Groups[7].Value.Trim();
                        errorDetails.ErrorMessage = $"error {errorCode}: {errorMsg}";
                        break;
                    }
                    
                    // File path with line number: "/path/to/file.ext:123" or "file.ext:123:"
                    // Support all language file extensions
                    var fileLineMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, 
                        @"([/\w\\\-\.]+\.(php|py|rb|js|ts|jsx|tsx|cs|java|go)):(\d+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (fileLineMatch.Success && string.IsNullOrEmpty(errorDetails.File))
                    {
                        errorDetails.File = fileLineMatch.Groups[1].Value.Trim();
                        if (int.TryParse(fileLineMatch.Groups[3].Value, out var lineNum))
                        {
                            errorDetails.Line = lineNum;
                        }
                    }
                }
                
                // Extract stack trace (look for "Traceback", "Stack trace", "at ", etc.)
                var stackTraceStart = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("Traceback", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Stack trace", StringComparison.OrdinalIgnoreCase) ||
                        (line.StartsWith("at ", StringComparison.OrdinalIgnoreCase) && stackTraceStart == -1))
                    {
                        stackTraceStart = i;
                        break;
                    }
                }
                
                if (stackTraceStart >= 0)
                {
                    var stackTraceLines = new List<string>();
                    for (int i = stackTraceStart; i < lines.Length && i < stackTraceStart + 50; i++) // Limit to 50 lines
                    {
                        stackTraceLines.Add(lines[i]);
                    }
                    errorDetails.StackTrace = string.Join("\n", stackTraceLines);
                }
                
                // If we have a file/line but no error message, try to extract from build output
                if (!string.IsNullOrEmpty(errorDetails.File) && string.IsNullOrEmpty(errorDetails.ErrorMessage))
                {
                    // Look for common error keywords near the file reference
                    foreach (var line in lines)
                    {
                        if (line.Contains(errorDetails.File, StringComparison.OrdinalIgnoreCase))
                        {
                            var errorKeywords = new[] { "error", "failed", "exception", "fatal", "syntax error", "parse error" };
                            foreach (var keyword in errorKeywords)
                            {
                                if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                {
                                    errorDetails.ErrorMessage = line.Trim();
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(errorDetails.ErrorMessage)) break;
                        }
                    }
                }
                
                // Fallback: if no specific error message found, use the last few lines of output
                if (string.IsNullOrEmpty(errorDetails.ErrorMessage) && lines.Length > 0)
                {
                    var lastLines = lines.Skip(Math.Max(0, lines.Length - 5))
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                    if (lastLines.Count > 0)
                    {
                        errorDetails.ErrorMessage = string.Join(" | ", lastLines);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing build output for error details");
                // Fallback to using build output as error message
                if (string.IsNullOrEmpty(errorDetails.ErrorMessage))
                {
                    errorDetails.ErrorMessage = buildOutput.Length > 500 ? buildOutput.Substring(0, 500) + "..." : buildOutput;
                }
            }
            
            return errorDetails;
        }

        /// <summary>
        /// Handle code review intent
        /// </summary>
        private async Task<ActionResult<object>> HandleCodeReviewIntent(MentorRequest request, MentorIntent intent, AIModel aiModel)
        {
            try
            {
                // Get student info
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                        .ThenInclude(pb => pb.Project)
                    .Include(s => s.ProgrammingLanguage)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null || string.IsNullOrEmpty(student.GithubUser))
                {
                    return BadRequest(new { Success = false, Message = "Student not found or GitHub username not set" });
                }

                // Get appropriate repository URL(s) based on role
                var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                
                // For code review, use the primary repo based on role (or backend as default)
                var githubRepoUrl = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl;
                if (string.IsNullOrEmpty(githubRepoUrl))
                {
                    return BadRequest(new { Success = false, Message = "GitHub repository URL not found for this project. Please ensure the repository is set up for your role." });
                }

                // Parse GitHub repo URL (format: https://github.com/owner/repo)
                var repoParts = githubRepoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/').Split('/');
                if (repoParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = "Invalid GitHub repository URL format" });
                }

                var owner = repoParts[0];
                var repo = repoParts[1];
                
                // Log which repo is being used for code review
                if (isFullstack)
                {
                    _logger.LogInformation("[CODE_REVIEW] Fullstack developer - reviewing backend repo: {RepoUrl}", githubRepoUrl);
                }
                else if (!string.IsNullOrEmpty(frontendRepoUrl) && githubRepoUrl == frontendRepoUrl)
                {
                    _logger.LogInformation("[CODE_REVIEW] Frontend developer - reviewing frontend repo: {RepoUrl}", githubRepoUrl);
                }
                else
                {
                    _logger.LogInformation("[CODE_REVIEW] Backend developer - reviewing backend repo: {RepoUrl}", githubRepoUrl);
                }

                // Get GitHub access token for API calls
                var accessToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("GitHub access token not configured. API calls may fail with 403 Forbidden.");
                }

                // Check if user asked specifically about diffs
                var userQuestion = request.UserQuestion ?? "";
                var isAskingAboutDiffs = userQuestion.Contains("diff", StringComparison.OrdinalIgnoreCase) || 
                                        userQuestion.Contains("diffs", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("changes", StringComparison.OrdinalIgnoreCase);

                // Check if this is a file selection response (user selecting from a previously shown file list)
                var isFileSelectionResponse = false;
                List<string>? previouslyShownFiles = null;
                try
                {
                    var lastAssistantMessage = await _context.MentorChatHistory
                        .Where(h => h.StudentId == request.StudentId && h.SprintId == request.SprintId && h.Role == "assistant")
                        .OrderByDescending(h => h.CreatedAt)
                        .FirstOrDefaultAsync();
                    
                    if (lastAssistantMessage != null && 
                        lastAssistantMessage.Message.Contains("Which file would you like me to review", StringComparison.OrdinalIgnoreCase))
                    {
                        isFileSelectionResponse = true;
                        _logger.LogInformation("Detected file selection response in HandleCodeReviewIntent: {UserQuestion}", userQuestion);
                        
                        // Extract file list from the previous message
                        // Format: "I found X files...\n\n1. file1\n2. file2\n\nWhich file..."
                        var messageLines = lastAssistantMessage.Message.Split('\n');
                        foreach (var line in messageLines)
                        {
                            var trimmedLine = line.Trim();
                            // Look for lines starting with numbers (1., 2., etc.)
                            if (trimmedLine.Length > 2 && char.IsDigit(trimmedLine[0]) && trimmedLine[1] == '.')
                            {
                                var fileName = trimmedLine.Substring(2).Trim();
                                if (!string.IsNullOrEmpty(fileName))
                                {
                                    if (previouslyShownFiles == null)
                                        previouslyShownFiles = new List<string>();
                                    previouslyShownFiles.Add(fileName);
                                }
                            }
                        }
                        
                        if (previouslyShownFiles != null && previouslyShownFiles.Any())
                        {
                            _logger.LogInformation("Extracted {Count} files from previous message: {Files}", 
                                previouslyShownFiles.Count, string.Join(", ", previouslyShownFiles));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking chat history for file selection in HandleCodeReviewIntent");
                }

                // Get recent commits first (don't rely on hasRecentCommits which might be too restrictive)
                _logger.LogInformation("Fetching commits for repo {Owner}/{Repo} with author filter: {Username}", owner, repo, student.GithubUser);
                var recentCommits = await _githubService.GetRecentCommitsAsync(owner, repo, student.GithubUser, 10, accessToken); // Get more commits to find one with actual changes
                _logger.LogInformation("Found {Count} commits with author filter '{Username}'", recentCommits.Count, student.GithubUser);
                
                // If no commits found, try fetching without author filter to see if commits exist at all
                if (!recentCommits.Any())
                {
                    _logger.LogWarning("No commits found with author filter '{Username}'. Attempting to fetch all commits without author filter to diagnose issue.", student.GithubUser);
                    try
                    {
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        }
                        var allCommitsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?per_page=5";
                        var allCommitsResponse = await httpClient.GetAsync(allCommitsUrl);
                        
                        if (allCommitsResponse.IsSuccessStatusCode)
                        {
                            var allCommitsContent = await allCommitsResponse.Content.ReadAsStringAsync();
                            var allCommitsData = JsonSerializer.Deserialize<List<JsonElement>>(allCommitsContent, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            
                            if (allCommitsData != null && allCommitsData.Any())
                            {
                                _logger.LogInformation("Found {Count} total commits in repository (without author filter). Checking authors...", allCommitsData.Count);
                                
                                // Log commit authors to help diagnose
                                foreach (var commitJson in allCommitsData.Take(5))
                                {
                                    string commitAuthor = "";
                                    string commitAuthorLogin = "";
                                    string commitSha = commitJson.TryGetProperty("sha", out var shaProp) ? shaProp.GetString() ?? "" : "";
                                    
                                    if (commitJson.TryGetProperty("commit", out var commitProp))
                                    {
                                        if (commitProp.TryGetProperty("author", out var authorProp))
                                        {
                                            commitAuthor = authorProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                                            commitAuthor = authorProp.TryGetProperty("email", out var emailProp) 
                                                ? commitAuthor + " <" + emailProp.GetString() + ">" 
                                                : commitAuthor;
                                        }
                                    }
                                    
                                    if (commitJson.TryGetProperty("author", out var authorJson))
                                    {
                                        commitAuthorLogin = authorJson.TryGetProperty("login", out var loginProp) ? loginProp.GetString() ?? "" : "";
                                    }
                                    
                                    _logger.LogInformation("Commit {Sha}: Author='{Author}', AuthorLogin='{AuthorLogin}', Expected='{Expected}'", 
                                        commitSha.Substring(0, Math.Min(7, commitSha.Length)), commitAuthor, commitAuthorLogin, student.GithubUser);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Repository exists but no commits found at all (even without author filter)");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to fetch commits without author filter. Status: {StatusCode}", allCommitsResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching commits without author filter for diagnosis");
                    }
                }
                
                // If no commits found with author filter, try fetching without filter as fallback
                // This handles cases where commits exist but author doesn't match the stored username
                if (!recentCommits.Any())
                {
                    _logger.LogWarning("No commits found with author filter '{Username}'. Attempting fallback: fetch all commits without author filter.", student.GithubUser);
                    try
                    {
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        }
                        var allCommitsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?per_page=10";
                        var allCommitsResponse = await httpClient.GetAsync(allCommitsUrl);
                        
                        if (allCommitsResponse.IsSuccessStatusCode)
                        {
                            var allCommitsContent = await allCommitsResponse.Content.ReadAsStringAsync();
                            var allCommitsData = JsonSerializer.Deserialize<List<JsonElement>>(allCommitsContent, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            
                            if (allCommitsData != null && allCommitsData.Any())
                            {
                                _logger.LogInformation("Fallback successful: Found {Count} commits without author filter. Converting to GitHubCommit objects...", allCommitsData.Count);
                                
                                // Convert all commits to GitHubCommit objects
                                var fallbackCommits = new List<GitHubCommit>();
                                foreach (var commitJson in allCommitsData)
                                {
                                    var sha = commitJson.TryGetProperty("sha", out var shaProp) ? shaProp.GetString() ?? "" : "";
                                    if (string.IsNullOrEmpty(sha)) continue;
                                    
                                    var htmlUrl = commitJson.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                                    string message = "";
                                    DateTime commitDate = DateTime.UtcNow;
                                    string author = "";
                                    
                                    if (commitJson.TryGetProperty("commit", out var commitProp))
                                    {
                                        message = commitProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                                        if (commitProp.TryGetProperty("author", out var authorProp))
                                        {
                                            author = authorProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                                            commitDate = authorProp.TryGetProperty("date", out var dateProp) 
                                                ? DateTime.Parse(dateProp.GetString() ?? "").ToUniversalTime() 
                                                : DateTime.UtcNow;
                                        }
                                    }
                                    
                                    if (commitJson.TryGetProperty("author", out var authorJson))
                                    {
                                        var authorLogin = authorJson.TryGetProperty("login", out var loginProp) ? loginProp.GetString() ?? "" : "";
                                        if (!string.IsNullOrEmpty(authorLogin))
                                        {
                                            author = authorLogin; // Prefer login over name
                                        }
                                    }
                                    
                                    fallbackCommits.Add(new GitHubCommit
                                    {
                                        Sha = sha,
                                        Message = message,
                                        CommitDate = commitDate,
                                        Author = author,
                                        Url = htmlUrl
                                    });
                                }
                                
                                if (fallbackCommits.Any())
                                {
                                    recentCommits = fallbackCommits;
                                    _logger.LogInformation("Using {Count} commits from fallback (without author filter). This indicates author mismatch: expected '{Expected}', found commits by various authors.", 
                                        recentCommits.Count, student.GithubUser);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Fallback also returned no commits. Repository may truly be empty or inaccessible.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Fallback failed: HTTP {StatusCode} when fetching commits without author filter", allCommitsResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in fallback commit fetching");
                    }
                }
                
                // If still no commits found after fallback, return error message
                if (!recentCommits.Any() && !isFileSelectionResponse)
                {
                    var responseMessage = isAskingAboutDiffs
                        ? $"I checked your GitHub repository ({githubRepoUrl}) and I don't see any diffs or commits yet. To see diffs, you'll need to:\n\n1. Make sure your code is committed and pushed to GitHub\n2. The commits should be made by your GitHub username: {student.GithubUser}\n\nWould you like help setting up Git and making your first commit? Just ask me \"How do I commit my code?\" or \"Help me with GitHub\" and I'll guide you through the process!"
                        : $"I don't see any commits in your GitHub repository ({githubRepoUrl}) yet. To review your code, you'll need to:\n\n1. Make sure your code is committed and pushed to GitHub\n2. The commits should be made by your GitHub username: {student.GithubUser}\n\nWould you like help setting up Git and making your first commit? Just ask me \"How do I commit my code?\" or \"Help me with GitHub\" and I'll guide you through the process!";
                    
                    return Ok(new
                    {
                        Success = true,
                        Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                        Response = responseMessage,
                        Intent = "code_review",
                        HasCommits = false
                    });
                }
                
                // If file selection but no commits, try fetching ALL commits (without author filter) as fallback
                // This handles cases where the username doesn't match the commit author
                if (!recentCommits.Any() && isFileSelectionResponse && previouslyShownFiles != null && previouslyShownFiles.Any())
                {
                    _logger.LogWarning("No commits found with author filter '{Username}' but file selection detected. Attempting to fetch all commits without author filter.", student.GithubUser);
                    
                    try
                    {
                        // Fetch all recent commits without author filter
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppers-Backend");
                        var allCommitsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?per_page=10";
                        var allCommitsResponse = await httpClient.GetAsync(allCommitsUrl);
                        
                        if (allCommitsResponse.IsSuccessStatusCode)
                        {
                            var allCommitsContent = await allCommitsResponse.Content.ReadAsStringAsync();
                            var allCommitsData = JsonSerializer.Deserialize<List<JsonElement>>(allCommitsContent, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            
                            if (allCommitsData != null && allCommitsData.Any())
                            {
                                // Convert to GitHubCommit objects and filter by files we're looking for
                                var tempCommits = new List<GitHubCommit>();
                                foreach (var commitJson in allCommitsData.Take(10))
                                {
                                    var sha = commitJson.TryGetProperty("sha", out var shaProp) ? shaProp.GetString() ?? "" : "";
                                    if (string.IsNullOrEmpty(sha)) continue;
                                    
                                    // Check if this commit contains any of the files we're looking for
                                    var diff = await _githubService.GetCommitDiffAsync(owner, repo, sha, accessToken);
                                    if (diff?.FileChanges != null)
                                    {
                                        var hasMatchingFile = diff.FileChanges.Any(f => 
                                            previouslyShownFiles.Any(pf => 
                                                f.FilePath.Contains(pf, StringComparison.OrdinalIgnoreCase) ||
                                                Path.GetFileName(f.FilePath).Equals(pf, StringComparison.OrdinalIgnoreCase) ||
                                                Path.GetFileNameWithoutExtension(f.FilePath).Equals(pf, StringComparison.OrdinalIgnoreCase)));
                                        
                                        if (hasMatchingFile)
                                        {
                                            // Parse commit details
                                            var htmlUrl = commitJson.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                                            string message = "";
                                            DateTime commitDate = DateTime.UtcNow;
                                            string author = "";
                                            
                                            if (commitJson.TryGetProperty("commit", out var commitProp))
                                            {
                                                message = commitProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                                                if (commitProp.TryGetProperty("author", out var authorProp))
                                                {
                                                    commitDate = authorProp.TryGetProperty("date", out var dateProp) 
                                                        ? DateTime.Parse(dateProp.GetString() ?? "").ToUniversalTime() 
                                                        : DateTime.UtcNow;
                                                }
                                            }
                                            
                                            if (commitJson.TryGetProperty("author", out var authorJson))
                                            {
                                                author = authorJson.TryGetProperty("login", out var loginProp) ? loginProp.GetString() ?? "" : "";
                                            }
                                            
                                            tempCommits.Add(new GitHubCommit
                                            {
                                                Sha = sha,
                                                Message = message,
                                                CommitDate = commitDate,
                                                Author = author,
                                                Url = htmlUrl
                                            });
                                        }
                                    }
                                }
                                
                                if (tempCommits.Any())
                                {
                                    recentCommits = tempCommits;
                                    _logger.LogInformation("Found {Count} commits containing previously shown files when fetching without author filter", recentCommits.Count);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch commits without author filter as fallback");
                    }
                }

                // Check if user wants to review a specific commit (e.g., "one commit before", "previous commit")
                var orderedCommits = recentCommits.OrderByDescending(c => c.CommitDate).ToList();
                int commitIndex = 0;
                
                var lowerQuestion = userQuestion.ToLowerInvariant();
                if (lowerQuestion.Contains("one commit before") || lowerQuestion.Contains("commit before") || 
                    lowerQuestion.Contains("previous commit") || lowerQuestion.Contains("earlier commit") ||
                    lowerQuestion.Contains("before that") || lowerQuestion.Contains("earlier"))
                {
                    commitIndex = 1; // Review the second most recent commit
                    _logger.LogInformation("User requested to review commit before the latest, using index {Index}", commitIndex);
                }

                // Find the first commit with actual code changes (skip rename-only commits)
                GitHubCommit? commitToReview = null;
                GitHubCommitDiff? commitDiff = null;
                
                for (int i = commitIndex; i < orderedCommits.Count; i++)
                {
                    var candidateCommit = orderedCommits[i];
                    var candidateDiff = await _githubService.GetCommitDiffAsync(owner, repo, candidateCommit.Sha, accessToken);
                    
                    if (candidateDiff == null)
                        continue;
                    
                    // Check if this commit has actual code changes (not just renames)
                    var hasActualChanges = candidateDiff.FileChanges != null && 
                                          candidateDiff.FileChanges.Any(f => 
                                              f.Status != "renamed" || (f.Additions > 0 || f.Deletions > 0));
                    
                    // Also check if total additions/deletions indicate actual code changes
                    var hasCodeChanges = candidateDiff.TotalAdditions > 0 || candidateDiff.TotalDeletions > 0;
                    
                    if (hasActualChanges && hasCodeChanges)
                    {
                        commitToReview = candidateCommit;
                        commitDiff = candidateDiff;
                        _logger.LogInformation("Found commit with actual code changes: {Sha} (index {Index})", candidateCommit.Sha, i);
                        break;
                    }
                }
                
                // If no commit with code changes found, use the most recent one
                if (commitToReview == null || commitDiff == null)
                {
                    commitToReview = orderedCommits[commitIndex];
                    commitDiff = await _githubService.GetCommitDiffAsync(owner, repo, commitToReview.Sha, accessToken);
                }
                
                var mostRecentCommit = commitToReview;

                if (commitDiff == null)
                {
                    _logger.LogWarning("Failed to fetch commit diff for commit {Sha} in repo {Owner}/{Repo}", mostRecentCommit.Sha, owner, repo);
                    return BadRequest(new { Success = false, Message = "Failed to fetch commit diff" });
                }

                // Validate that the diff actually has code changes
                if (commitDiff.FileChanges == null || !commitDiff.FileChanges.Any() || commitDiff.TotalFilesChanged == 0)
                {
                    _logger.LogWarning("Commit diff has no file changes for commit {Sha} in repo {Owner}/{Repo}", mostRecentCommit.Sha, owner, repo);
                    return Ok(new
                    {
                        Success = true,
                        Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                        Response = $"I checked your repository, but the commit '{mostRecentCommit.Message}' doesn't contain any code changes to review. Please make sure you've committed and pushed your actual code files.",
                        Intent = "code_review",
                        HasCommits = false
                    });
                }

                // Get all unique files from recent commits to show user if multiple files exist
                var allFilesInCommits = new HashSet<string>();
                
                // If we have previously shown files from chat history, add them to the set for matching
                if (previouslyShownFiles != null && previouslyShownFiles.Any())
                {
                    foreach (var file in previouslyShownFiles)
                    {
                        allFilesInCommits.Add(file);
                    }
                    _logger.LogInformation("Added {Count} previously shown files to matching set", previouslyShownFiles.Count);
                }
                
                foreach (var commit in orderedCommits.Take(5))
                {
                    var diff = await _githubService.GetCommitDiffAsync(owner, repo, commit.Sha, accessToken);
                    if (diff?.FileChanges != null)
                    {
                        foreach (var fileChange in diff.FileChanges)
                        {
                            if (!string.IsNullOrEmpty(fileChange.FilePath) && 
                                fileChange.Status != "removed" &&
                                (fileChange.Additions > 0 || fileChange.Deletions > 0))
                            {
                                allFilesInCommits.Add(fileChange.FilePath);
                            }
                        }
                    }
                }

                // If multiple files exist OR if this is a file selection response, check if user specified which one
                if (allFilesInCommits.Count > 1 || (isFileSelectionResponse && allFilesInCommits.Count > 0))
                {
                    // Check if user specified a file in their question (by name or number)
                    string? specifiedFilePath = null;
                    
                    // First check by number (e.g., "1", "2")
                    if (int.TryParse(userQuestion.Trim(), out int fileNumber) && fileNumber > 0 && fileNumber <= allFilesInCommits.Count)
                    {
                        specifiedFilePath = allFilesInCommits.ElementAt(fileNumber - 1);
                        _logger.LogInformation("User selected file by number {Number}: {FilePath}", fileNumber, specifiedFilePath);
                    }
                    else
                    {
                        // Check by file name (full path, filename, or filename without extension)
                        var userInput = userQuestion.Trim();
                        
                        // First, try matching against previously shown files (these are likely just filenames)
                        if (previouslyShownFiles != null && previouslyShownFiles.Any())
                        {
                            var matchedFile = previouslyShownFiles.FirstOrDefault(f => 
                                f.Equals(userInput, StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(f).Equals(userInput, StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileNameWithoutExtension(f).Equals(userInput, StringComparison.OrdinalIgnoreCase) ||
                                f.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                                userInput.Contains(f, StringComparison.OrdinalIgnoreCase));
                            
                            if (!string.IsNullOrEmpty(matchedFile))
                            {
                                // Now find the actual file path in allFilesInCommits that matches this filename
                                specifiedFilePath = allFilesInCommits.FirstOrDefault(f => 
                                    Path.GetFileName(f).Equals(Path.GetFileName(matchedFile), StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileNameWithoutExtension(matchedFile), StringComparison.OrdinalIgnoreCase) ||
                                    f.Contains(Path.GetFileName(matchedFile), StringComparison.OrdinalIgnoreCase));
                                
                                // If still not found, use the matched file itself (it might be the full path)
                                if (string.IsNullOrEmpty(specifiedFilePath))
                                {
                                    specifiedFilePath = matchedFile;
                                }
                                
                                _logger.LogInformation("Matched user input '{UserInput}' to previously shown file '{MatchedFile}', resolved to '{FilePath}'", 
                                    userInput, matchedFile, specifiedFilePath);
                            }
                        }
                        
                        // If not matched via previously shown files, try direct matching against allFilesInCommits
                        if (string.IsNullOrEmpty(specifiedFilePath))
                        {
                            // Try exact matches first
                            specifiedFilePath = allFilesInCommits.FirstOrDefault(f => 
                                f.Equals(userInput, StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(f).Equals(userInput, StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileNameWithoutExtension(f).Equals(userInput, StringComparison.OrdinalIgnoreCase));
                            
                            // If no exact match, try partial matches
                            if (string.IsNullOrEmpty(specifiedFilePath))
                            {
                                specifiedFilePath = allFilesInCommits.FirstOrDefault(f => 
                                    userInput.Contains(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase) ||
                                    userInput.Contains(Path.GetFileNameWithoutExtension(f), StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(f).Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileNameWithoutExtension(f).Contains(userInput, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            if (!string.IsNullOrEmpty(specifiedFilePath))
                            {
                                _logger.LogInformation("User selected file by name '{UserInput}': {FilePath}", userInput, specifiedFilePath);
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(specifiedFilePath))
                    {
                        // User didn't specify, show list and ask
                        var fileList = string.Join("\n", allFilesInCommits.Select((f, i) => $"{i + 1}. {f}"));
                        return Ok(new
                        {
                            Success = true,
                            Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                            Response = $"I found {allFilesInCommits.Count} files with changes in your recent commits:\n\n{fileList}\n\nWhich file would you like me to review? Just mention the file name or number.",
                            Intent = "code_review",
                            HasCommits = true,
                            Files = allFilesInCommits.ToList()
                        });
                    }
                    else
                    {
                        // User specified a file - find the commit that contains this file and filter
                        GitHubCommitDiff? fileCommitDiff = null;
                        GitHubCommit? fileCommit = null;
                        
                        foreach (var commit in orderedCommits)
                        {
                            var diff = await _githubService.GetCommitDiffAsync(owner, repo, commit.Sha, accessToken);
                            if (diff?.FileChanges != null)
                            {
                                var matchingFile = diff.FileChanges.FirstOrDefault(f => 
                                    f.FilePath.Equals(specifiedFilePath, StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileName(f.FilePath).Equals(Path.GetFileName(specifiedFilePath), StringComparison.OrdinalIgnoreCase) ||
                                    Path.GetFileNameWithoutExtension(f.FilePath).Equals(Path.GetFileNameWithoutExtension(specifiedFilePath), StringComparison.OrdinalIgnoreCase));
                                
                                if (matchingFile != null && (matchingFile.Additions > 0 || matchingFile.Deletions > 0))
                                {
                                    // Found the file in this commit - use this commit for review
                                    fileCommitDiff = diff;
                                    fileCommitDiff.FileChanges = diff.FileChanges
                                        .Where(f => f.FilePath.Equals(matchingFile.FilePath, StringComparison.OrdinalIgnoreCase))
                                        .ToList();
                                    fileCommitDiff.TotalFilesChanged = fileCommitDiff.FileChanges.Count;
                                    fileCommitDiff.TotalAdditions = fileCommitDiff.FileChanges.Sum(f => f.Additions);
                                    fileCommitDiff.TotalDeletions = fileCommitDiff.FileChanges.Sum(f => f.Deletions);
                                    fileCommit = commit;
                                    _logger.LogInformation("Found file {FilePath} in commit {Sha}, filtering review to this file", specifiedFilePath, commit.Sha);
                                    break;
                                }
                            }
                        }
                        
                        // Use the found commit diff, or fall back to filtering the current commitDiff
                        if (fileCommitDiff != null && fileCommit != null)
                        {
                            commitDiff = fileCommitDiff;
                            mostRecentCommit = fileCommit;
                            _logger.LogInformation("Using commit {Sha} containing file {FilePath}", fileCommit.Sha, specifiedFilePath);
                        }
                        else if (commitDiff != null && commitDiff.FileChanges != null)
                        {
                            // Fallback: filter current commitDiff
                            var filteredChanges = commitDiff.FileChanges
                                .Where(f => f.FilePath.Equals(specifiedFilePath, StringComparison.OrdinalIgnoreCase) ||
                                           Path.GetFileName(f.FilePath).Equals(Path.GetFileName(specifiedFilePath), StringComparison.OrdinalIgnoreCase) ||
                                           Path.GetFileNameWithoutExtension(f.FilePath).Equals(Path.GetFileNameWithoutExtension(specifiedFilePath), StringComparison.OrdinalIgnoreCase) ||
                                           f.FilePath.Contains(Path.GetFileName(specifiedFilePath), StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            if (filteredChanges.Any())
                            {
                                commitDiff.FileChanges = filteredChanges;
                                commitDiff.TotalFilesChanged = commitDiff.FileChanges.Count;
                                commitDiff.TotalAdditions = commitDiff.FileChanges.Sum(f => f.Additions);
                                commitDiff.TotalDeletions = commitDiff.FileChanges.Sum(f => f.Deletions);
                                _logger.LogInformation("Filtered current commit diff to file: {FilePath}", specifiedFilePath);
                            }
                            else
                            {
                                _logger.LogWarning("File {FilePath} not found in current commit diff, but will proceed with full diff", specifiedFilePath);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No commit diff available and file {FilePath} not found in any commit", specifiedFilePath);
                        }
                    }
                }

                // Get module description and task description from context
                var contextResult = await GetMentorContextInternal(request.StudentId, request.SprintId);
                string? moduleDescription = null;
                string? taskDescription = null;

                if (contextResult != null)
                {
                    var contextJson = JsonSerializer.Serialize(contextResult);
                    var contextElement = JsonSerializer.Deserialize<JsonElement>(contextJson);

                    if (contextElement.TryGetProperty("Context", out var ctxProp))
                    {
                        // Get module description from first task
                        if (ctxProp.TryGetProperty("CurrentTasks", out var tasksProp) && tasksProp.ValueKind == JsonValueKind.Array && tasksProp.GetArrayLength() > 0)
                        {
                            var firstTask = tasksProp[0];
                            if (firstTask.TryGetProperty("CustomFields", out var cfProp) && cfProp.ValueKind == JsonValueKind.Object)
                            {
                                if (cfProp.TryGetProperty("ModuleId", out var moduleIdProp))
                                {
                                    var moduleIdStr = "";
                                    if (moduleIdProp.ValueKind == JsonValueKind.String)
                                    {
                                        moduleIdStr = moduleIdProp.GetString() ?? "";
                                    }
                                    else if (moduleIdProp.ValueKind == JsonValueKind.Number)
                                    {
                                        moduleIdStr = moduleIdProp.GetRawText();
                                    }

                                    if (!string.IsNullOrEmpty(moduleIdStr))
                                    {
                                        moduleIdStr = moduleIdStr.Trim().Trim('"').Trim('\'').Trim();
                                        if (int.TryParse(moduleIdStr, out int moduleId))
                                        {
                                            var project = student.ProjectBoard?.Project;
                                            if (project != null)
                                            {
                                                var module = await _context.ProjectModules
                                                    .FirstOrDefaultAsync(pm => pm.Id == moduleId && pm.ProjectId == project.Id);
                                                moduleDescription = module?.Description;
                                            }
                                        }
                                    }
                                }
                            }

                            // Get task description
                            if (firstTask.TryGetProperty("Description", out var descProp))
                            {
                                taskDescription = descProp.GetString();
                            }
                            else if (firstTask.TryGetProperty("name", out var nameProp))
                            {
                                taskDescription = nameProp.GetString();
                            }
                        }

                        // Try to get module descriptions from context
                        if (ctxProp.TryGetProperty("ModuleDescriptions", out var moduleDescProp) && moduleDescProp.ValueKind == JsonValueKind.Object)
                        {
                            // Get first module description if available
                            foreach (var prop in moduleDescProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    moduleDescription = prop.Value.GetString();
                                    break;
                                }
                            }
                        }
                    }
                }

                var programmingLanguage = student.ProgrammingLanguage?.Name ?? "C#";

                // Final validation: ensure we have actual code to review before calling AI
                if (commitDiff == null || commitDiff.FileChanges == null || !commitDiff.FileChanges.Any() || commitDiff.TotalFilesChanged == 0)
                {
                    _logger.LogWarning("Attempted code review with no file changes for StudentId {StudentId}, Commit {Sha}", 
                        request.StudentId, mostRecentCommit?.Sha ?? "unknown");
                    return Ok(new
                    {
                        Success = true,
                        Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                        Response = $"I don't see any code changes in your repository to review. Please make sure your code is committed and pushed to GitHub, then ask me to review it again.",
                        Intent = "code_review",
                        HasCommits = false
                    });
                }

                // Build system prompt for code review from configuration
                var codeReviewSystemPrompt = _promptConfig.Mentor.CodeReview.ReviewSystemPrompt;

                // Perform code review
                var reviewResult = await _codeReviewAgent.ReviewCodeAsync(
                    commitDiff,
                    moduleDescription,
                    taskDescription,
                    programmingLanguage,
                    aiModel,
                    codeReviewSystemPrompt,
                    request.UserQuestion ?? ""
                );

                if (!reviewResult.Success)
                {
                    // If review failed, return the error message (which should be user-friendly)
                    return Ok(new
                    {
                        Success = true,
                        Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                        Response = reviewResult.ReviewText,
                        Intent = "code_review",
                        HasCommits = true
                    });
                }

                // Save user question and AI response to chat history
                var userMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "user",
                    Message = request.UserQuestion ?? "",
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(userMessage);

                var assistantMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "assistant",
                    Message = reviewResult.ReviewText,
                    AIModelName = aiModel.Name,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(assistantMessage);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                    Response = reviewResult.ReviewText,
                    Intent = "code_review",
                    HasCommits = true,
                    CommitInfo = new
                    {
                        mostRecentCommit.Sha,
                        mostRecentCommit.Message,
                        mostRecentCommit.CommitDate,
                        commitDiff.TotalFilesChanged,
                        commitDiff.TotalAdditions,
                        commitDiff.TotalDeletions
                    },
                    ReviewResult = reviewResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling code review intent for StudentId: {StudentId}", request.StudentId);
                return StatusCode(500, new { Success = false, Message = $"Error performing code review: {ex.Message}" });
            }
        }

        /// <summary>
        /// Handle GitHub help intent
        /// </summary>
        private async Task<ActionResult<object>> HandleGitHubHelpIntent(MentorRequest request, MentorIntent intent)
        {
            try
            {
                // Get student info
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return BadRequest(new { Success = false, Message = "Student not found" });
                }

                // Check if student has a developer role (Type 1: Full-stack Developer, Type 2: Frontend/Backend Developer)
                var isDeveloperRole = student.StudentRoles?
                    .Any(sr => sr.IsActive && (sr.Role?.Type == 1 || sr.Role?.Type == 2)) ?? false;

                // Get appropriate repository URL(s) based on role
                var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                
                // Use the primary repo for help text (backend for backend/fullstack, frontend for frontend)
                var githubRepoUrl = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl ?? "";
                var githubUser = student.GithubUser ?? "";

                var helpText = BuildGitHubHelpText(githubRepoUrl, githubUser, intent.Parameters, isDeveloperRole);

                // Save to chat history
                var userMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "user",
                    Message = request.UserQuestion ?? "",
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(userMessage);

                var assistantMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "assistant",
                    Message = helpText,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(assistantMessage);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Response = helpText,
                    Intent = "github_help"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling GitHub help intent for StudentId: {StudentId}", request.StudentId);
                return StatusCode(500, new { Success = false, Message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Handle general mentor response (original behavior)
        /// </summary>
        private async Task<ActionResult<object>> HandleGeneralMentorResponse(MentorRequest request, MentorIntent intent, AIModel aiModel)
        {
            try
            {
                // Get mentor context
                var contextResult = await GetMentorContextInternal(request.StudentId, request.SprintId);
                if (contextResult == null)
                {
                    return BadRequest(new { Success = false, Message = "Failed to get mentor context" });
                }

                var contextJson = JsonSerializer.Serialize(contextResult);
                var contextElement = JsonSerializer.Deserialize<JsonElement>(contextJson);

                var baseSystemPrompt = contextElement.TryGetProperty("SystemPrompt", out var sysProp) 
                    ? sysProp.GetString() ?? "" 
                    : "";
                var contextData = contextElement.TryGetProperty("Context", out var ctxProp) 
                    ? ctxProp 
                    : default(JsonElement);

                // Get user question early for use in sprint detection
                var userQuestion = request.UserQuestion ?? "";

                // Check if user is explicitly asking to view/switch to a different sprint (not just mentioning it)
                // Skip sprint detection entirely if user is asking for suggestions, advice, or feedback
                var lowerQuestion = userQuestion.ToLowerInvariant();
                
                // Check if user is asking for suggestions/advice - if so, skip sprint switch detection entirely
                var suggestionKeywords = new[] { "suggestion", "advice", "recommend", "suggest", "what should", "how to", "what do you think", "overcome", "issue", "problem", "what is your" };
                bool isAskingForSuggestion = suggestionKeywords.Any(keyword => lowerQuestion.Contains(keyword));
                
                int? mentionedSprint = null;
                bool isExplicitSprintRequest = false;
                
                // Only check for sprint switch if user is NOT asking for suggestions/advice
                if (!isAskingForSuggestion)
                {
                    // Very restrictive patterns - only direct requests to view/switch sprints
                    // Action words must be immediately before or after "sprint X"
                    var explicitSprintRequestPatterns = new[]
                    {
                        @"(?:show|view|see|switch|go to|change to|look at)\s+(?:me\s+)?(?:the\s+)?sprint\s*(\d+)",
                        @"sprint\s*(\d+)\s+(?:information|details|tasks|context|show|view|see)",
                        @"(?:tell\s+me|what\s+is|what\s+are)\s+(?:the\s+)?(?:tasks|information|details)\s+(?:in|for|about)\s+sprint\s*(\d+)"
                    };
                    
                    foreach (var pattern in explicitSprintRequestPatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(lowerQuestion, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var sprintValue = match.Groups[1].Value;
                            if (int.TryParse(sprintValue, out int sprintNum))
                            {
                                mentionedSprint = sprintNum;
                                isExplicitSprintRequest = true;
                                break;
                            }
                        }
                    }
                }
                
                // Only redirect if it's an explicit request to view/switch sprints, not just a mention
                if (isExplicitSprintRequest && mentionedSprint.HasValue && mentionedSprint.Value != request.SprintId)
                {
                    _logger.LogInformation("User explicitly requested Sprint {MentionedSprint} but context is Sprint {CurrentSprint}. Informing user to switch context.", 
                        mentionedSprint.Value, request.SprintId);
                    return Ok(new
                    {
                        Success = true,
                        Model = new { aiModel.Id, aiModel.Name, aiModel.Provider },
                        Response = $"I see you're asking about Sprint {mentionedSprint.Value}, but I'm currently viewing Sprint {request.SprintId}. To get information about Sprint {mentionedSprint.Value}, please switch your context to Sprint {mentionedSprint.Value} first, then ask me again.",
                        Intent = "general",
                        RequiresSprintSwitch = true,
                        RequestedSprint = mentionedSprint.Value,
                        CurrentSprint = request.SprintId
                    });
                }

                // Get chat history for this student/sprint combination (BEFORE saving current message)
                var chatHistoryLength = _promptConfig.Mentor.ChatHistoryLength;
                var rawChatHistory = await _context.MentorChatHistory
                    .Where(h => h.StudentId == request.StudentId && h.SprintId == request.SprintId)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(chatHistoryLength * 2) // Get last N pairs (user + assistant)
                    .OrderBy(h => h.CreatedAt) // Re-order chronologically
                    .Select(h => new ChatHistoryItem
                    {
                        Role = h.Role,
                        Message = h.Message
                    })
                    .ToListAsync();
                
                // Filter out database connection information from chat history to prevent using outdated credentials
                // This is critical because boards can be deleted and recreated with new connection strings
                var chatHistory = rawChatHistory.Select(h => new ChatHistoryItem
                {
                    Role = h.Role,
                    Message = FilterDatabaseConnectionInfo(h.Message)
                }).ToList();
                
                // Check if this is a new conversation (no chat history) to determine if greeting is appropriate
                var hasChatHistory = chatHistory.Any();

                // Get student GitHub information for context (reused in multiple places)
                var student = await _context.Students
                    .Include(s => s.ProjectBoard)
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);
                
                // Get GitHub access token for API calls
                var accessToken = _configuration["GitHub:AccessToken"];
                
                // Check if student has a developer role (Type 1: Full-stack Developer, Type 2: Frontend/Backend Developer)
                var isDeveloperRole = student?.StudentRoles?
                    .Any(sr => sr.IsActive && (sr.Role?.Type == 1 || sr.Role?.Type == 2)) ?? false;

                // Check if user is asking about repository access, diffs, or code changes
                var isAskingAboutRepo = userQuestion.Contains("diff", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("diffs", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("repository", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("repo", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("see", StringComparison.OrdinalIgnoreCase) && 
                                        (userQuestion.Contains("code", StringComparison.OrdinalIgnoreCase) || 
                                         userQuestion.Contains("changes", StringComparison.OrdinalIgnoreCase)) ||
                                        userQuestion.Contains("access", StringComparison.OrdinalIgnoreCase) && 
                                        userQuestion.Contains("repository", StringComparison.OrdinalIgnoreCase);

                // Check if developer is asking about connection strings, README, API, web, or parsing from GitHub
                var isAskingAboutConnectionString = isDeveloperRole && (
                                        userQuestion.Contains("connection string", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("parse", StringComparison.OrdinalIgnoreCase) && 
                                        (userQuestion.Contains("connection", StringComparison.OrdinalIgnoreCase) || 
                                         userQuestion.Contains("readme", StringComparison.OrdinalIgnoreCase) ||
                                         userQuestion.Contains("README", StringComparison.OrdinalIgnoreCase)) ||
                                        userQuestion.Contains("readme", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("README", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("read", StringComparison.OrdinalIgnoreCase) && 
                                        (userQuestion.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                                         userQuestion.Contains("readme", StringComparison.OrdinalIgnoreCase)) ||
                                        userQuestion.Contains("api", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("webapi", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("web api", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("swagger", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("endpoint", StringComparison.OrdinalIgnoreCase) ||
                                        userQuestion.Contains("railway", StringComparison.OrdinalIgnoreCase));
                
                // Check if user is asking to review code (even if intent wasn't detected)
                // This catches cases where intent detection failed but user clearly wants code review
                // Note: lowerQuestion is already declared earlier in the method
                var isAskingToReviewCode = (lowerQuestion.Contains("review") && 
                                          (lowerQuestion.Contains("code") || 
                                           lowerQuestion.Contains("my code") ||
                                           lowerQuestion.Contains("this file") ||
                                           lowerQuestion.Contains("the file") ||
                                           lowerQuestion.Contains("file") ||
                                           lowerQuestion.Contains("it") ||
                                           lowerQuestion.Contains("that"))) ||
                                          lowerQuestion.Contains("give feedback") ||
                                          lowerQuestion.Contains("provide feedback") ||
                                          lowerQuestion.Contains("feedback on") ||
                                          (lowerQuestion.Contains("please") && lowerQuestion.Contains("review")) ||
                                          (lowerQuestion.Contains("can you") && lowerQuestion.Contains("review"));
                
                // If asking to review code and is developer role, route to code review handler
                if (isAskingToReviewCode && isDeveloperRole)
                {
                    _logger.LogInformation("Detected code review request in HandleGeneralMentorResponse: {UserQuestion}. Routing to code review handler.", userQuestion);
                    // Force code_review intent and route to code review handler
                    intent.Type = "code_review";
                    intent.Confidence = 0.9;
                    return await HandleCodeReviewIntent(request, intent, aiModel);
                }
                
                // If asking to review code, check for commits and add explicit instruction
                string? codeReviewWarning = null;
                if (isAskingToReviewCode && student != null && isDeveloperRole)
                {
                    try
                    {
                        // Get appropriate repository URL based on role
                        var (frontendRepoUrl, backendRepoUrl, _) = GetRepositoryUrlsByRole(student);
                        var githubRepoUrl = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl;
                        
                        if (!string.IsNullOrEmpty(student.GithubUser) && !string.IsNullOrEmpty(githubRepoUrl))
                        {
                            var repoParts = githubRepoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/').Split('/');
                            
                            if (repoParts.Length >= 2)
                            {
                                var owner = repoParts[0];
                                var repo = repoParts[1];
                                
                                // Check for commits
                                var recentCommits = await _githubService.GetRecentCommitsAsync(owner, repo, student.GithubUser, 5, accessToken);
                                
                                if (!recentCommits.Any())
                                {
                                    // No commits found - add explicit instruction to be honest
                                    codeReviewWarning = "\n\n🚨 CRITICAL: The user asked you to review their code, but there are NO commits in their repository. You MUST respond honestly: \"I don't see any commits in your repository yet. Please make sure your code is committed and pushed to GitHub, then ask me to review it again.\" DO NOT generate fake reviews or claim you reviewed code when there are no commits.";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking commits for code review warning");
                    }
                }
                
                // Check if user is asking to repeat instructions (likely GitHub-related)
                var isAskingToRepeat = userQuestion.Contains("instruct again", StringComparison.OrdinalIgnoreCase) ||
                                      userQuestion.Contains("tell me again", StringComparison.OrdinalIgnoreCase) ||
                                      userQuestion.Contains("repeat", StringComparison.OrdinalIgnoreCase) ||
                                      userQuestion.Contains("show me again", StringComparison.OrdinalIgnoreCase);
                
                // If asking to repeat and is developer role, assume it's about GitHub/Git
                if (isAskingToRepeat && isDeveloperRole)
                {
                    isAskingAboutRepo = true;
                }

                // If asking about repository/diffs, check the repository and add info to context
                string? repositoryStatusInfo = null;
                string? readmeContent = null;
                if ((isAskingAboutRepo || isAskingAboutConnectionString) && student != null)
                {
                    try
                    {
                        // Get appropriate repository URL based on role
                        var (frontendRepoUrl, backendRepoUrl, _) = GetRepositoryUrlsByRole(student);
                        var githubRepoUrl = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl;
                        
                        if (!string.IsNullOrEmpty(student.GithubUser) && !string.IsNullOrEmpty(githubRepoUrl))
                        {
                            var repoParts = githubRepoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/').Split('/');
                            
                            if (repoParts.Length >= 2)
                            {
                                var owner = repoParts[0];
                                var repo = repoParts[1];
                                
                                // If asking about connection strings, README, API, or web-related topics, fetch README content
                                if (isAskingAboutConnectionString)
                                {
                                    _logger.LogInformation("Developer asking about connection string/README/API/web. Fetching README content from {Owner}/{Repo}", owner, repo);
                                    readmeContent = await _githubService.GetFileContentAsync(owner, repo, "README.md", accessToken);
                                    
                                    if (string.IsNullOrEmpty(readmeContent))
                                    {
                                        // Try README.txt or README
                                        readmeContent = await _githubService.GetFileContentAsync(owner, repo, "README.txt", accessToken) ??
                                                       await _githubService.GetFileContentAsync(owner, repo, "README", accessToken);
                                    }
                                    
                                    if (!string.IsNullOrEmpty(readmeContent))
                                    {
                                        _logger.LogInformation("Successfully fetched README content ({Length} characters)", readmeContent.Length);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("README file not found in repository {Owner}/{Repo}", owner, repo);
                                    }
                                }
                                
                                // Check for recent commits
                                var hasRecentCommits = await _githubService.HasRecentCommitsAsync(owner, repo, student.GithubUser, 168, accessToken);
                                var recentCommits = await _githubService.GetRecentCommitsAsync(owner, repo, student.GithubUser, 5, accessToken);
                                
                                if (hasRecentCommits && recentCommits.Any())
                                {
                                    var mostRecentCommit = recentCommits.OrderByDescending(c => c.CommitDate).First();
                                    repositoryStatusInfo = $"IMPORTANT: You DO have access to the GitHub repository ({githubRepoUrl}). " +
                                        $"I found {recentCommits.Count} recent commit(s) by {student.GithubUser}. " +
                                        $"The most recent commit is '{mostRecentCommit.Message}' from {mostRecentCommit.CommitDate:yyyy-MM-dd}. " +
                                        $"You can review code changes, see diffs, and analyze commits. " +
                                        $"When asked about diffs or repository access, you should check the repository and provide actual information.";
                                }
                                else
                                {
                                    repositoryStatusInfo = $"IMPORTANT: You DO have access to the GitHub repository ({githubRepoUrl}), " +
                                        $"but there are currently no recent commits by {student.GithubUser} to review. " +
                                        $"You can still access the repository to check for files and structure.";
                                    
                                    // If we have README content but no commits, still inform about repository access
                                    if (!string.IsNullOrEmpty(readmeContent))
                                    {
                                        repositoryStatusInfo += " However, the repository does contain files (README.md is available).";
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check repository status or fetch README content");
                        // Continue without repository info if check fails
                    }
                }
                
                var githubAccountInfo = "";
                // Only add GitHub information for developer roles
                if (student != null && isDeveloperRole)
                {
                    var githubUser = student.GithubUser ?? "";
                    var (frontendRepoUrl, backendRepoUrl, _) = GetRepositoryUrlsByRole(student);
                    var githubRepoUrl = !string.IsNullOrEmpty(backendRepoUrl) ? backendRepoUrl : frontendRepoUrl ?? "";
                    
                    // Always add GitHub info for developer roles, especially if asking about GitHub or repeating instructions
                    if (isAskingAboutRepo || isAskingToRepeat || !string.IsNullOrEmpty(githubUser) || !string.IsNullOrEmpty(githubRepoUrl))
                    {
                        githubAccountInfo = "\n\n⚠️ CRITICAL GITHUB INFORMATION (Developer Role) - MUST FOLLOW:\n";
                        githubAccountInfo += "The student ALREADY HAS a GitHub account and their project repository ALREADY EXISTS.\n";
                        githubAccountInfo += "⚠️ ABSOLUTE REQUIREMENT: When providing GitHub/Git instructions:\n";
                        githubAccountInfo += "1. NEVER include steps to create a GitHub account\n";
                        githubAccountInfo += "2. NEVER include steps to create a repository\n";
                        githubAccountInfo += "3. ALWAYS start by acknowledging their existing account and repository\n";
                        githubAccountInfo += "4. Focus ONLY on Git workflow: git init → git add → git commit → git push\n";
                        githubAccountInfo += "5. IGNORE any previous chat history that mentioned creating accounts or repositories\n";
                        githubAccountInfo += "6. ⚠️ CRITICAL FORMATTING: ALWAYS wrap ALL code snippets and commands in triple backticks (```bash\ncommand\n```)\n";
                        githubAccountInfo += "   - Even single commands like 'git init' MUST be wrapped: ```bash\ngit init\n```\n";
                        githubAccountInfo += "   - This ensures proper display in the frontend with copy buttons\n";
                        if (!string.IsNullOrEmpty(githubUser))
                        {
                            githubAccountInfo += $"\nTheir GitHub username: {githubUser}\n";
                        }
                        if (!string.IsNullOrEmpty(githubRepoUrl))
                        {
                            githubAccountInfo += $"Their repository URL: {githubRepoUrl}\n";
                        }
                        githubAccountInfo += "\nIf the user asks to 'instruct again' or 'repeat', provide ONLY the Git workflow steps (init, add, commit, push) - NOT account/repository creation. ALWAYS wrap all commands in triple backticks.";
                    }
                }
                else if (student != null && !isDeveloperRole)
                {
                    // For non-developer roles, add a note that GitHub assumptions don't apply
                    githubAccountInfo = "\n\nNOTE: This student is NOT in a developer role. Do NOT assume they have a GitHub account or repository unless explicitly mentioned in the context.";
                }

                // Build enhanced system prompt with context (but NOT the user question)
                var enhancedSystemPrompt = BuildEnhancedSystemPrompt(baseSystemPrompt, contextData);
                
                // Add instruction to skip greeting if there's existing chat history
                if (!hasChatHistory)
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\nNOTE: This is a new conversation. You may greet the user naturally if appropriate.";
                }
                else
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n⚠️ IMPORTANT: This is NOT a new conversation - there is existing chat history. Do NOT greet the user with phrases like \"Hi [name]! How can I help you today?\" or similar greetings. Go straight to answering their question based on the conversation context.\n\n" +
                        $"⚠️ CRITICAL: If the user asks about database connection details, IGNORE any database connection information from the chat history below. " +
                        $"Chat history may contain outdated information from deleted boards or old projects. " +
                        $"ALWAYS use the CURRENT connection information from the PROJECT INFORMATION (README) section above or from the current context, NOT from chat history.";
                }
                
                // Add GitHub account information
                if (!string.IsNullOrEmpty(githubAccountInfo))
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}{githubAccountInfo}";
                }
                
                // Add repository access information to system prompt if relevant
                if (!string.IsNullOrEmpty(repositoryStatusInfo))
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n{repositoryStatusInfo}";
                }

                // Extract WebApi URL and Swagger URL from README if available (for developer roles)
                string? webApiUrl = null;
                string? swaggerUrl = null;
                
                if (!string.IsNullOrEmpty(readmeContent))
                {
                    // Extract WebApi URL from README (format: **WebApi URL:** https://railway.app/project/...)
                    var webApiMatch = System.Text.RegularExpressions.Regex.Match(readmeContent, @"\*\*WebApi URL:\*\*\s*(https?://[^\s\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (webApiMatch.Success)
                    {
                        webApiUrl = webApiMatch.Groups[1].Value.Trim();
                    }
                    
                    // Extract Swagger URL from README (format: **Swagger API Tester URL:** https://...)
                    var swaggerMatch = System.Text.RegularExpressions.Regex.Match(readmeContent, @"\*\*Swagger API Tester URL:\*\*\s*(https?://[^\s\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (swaggerMatch.Success)
                    {
                        swaggerUrl = swaggerMatch.Groups[1].Value.Trim();
                    }
                }
                
                // Add README content if fetched (for connection string parsing, etc.)
                if (isDeveloperRole)
                {
                    // Check if README contains connection string
                    var hasConnectionStringInReadme = !string.IsNullOrEmpty(readmeContent) && 
                        (readmeContent.Contains("postgresql://", StringComparison.OrdinalIgnoreCase) || 
                         readmeContent.Contains("postgres://", StringComparison.OrdinalIgnoreCase) ||
                         readmeContent.Contains("Application DB Connection String", StringComparison.OrdinalIgnoreCase));
                    
                    if (!string.IsNullOrEmpty(readmeContent))
                    {
                        enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n=== PROJECT INFORMATION (FROM README) ===\n" +
                        $"{readmeContent}\n\n" +
                            $"=== END OF PROJECT INFORMATION ===\n\n";
                    }
                    
                    // Add connection string fallback if README doesn't have it
                    string? connectionStringFallback = null;
                    if (!hasConnectionStringInReadme && student.ProjectBoard != null)
                    {
                        var boardId = student.ProjectBoard.Id;
                        var dbPassword = student.ProjectBoard.DBPassword;
                        var neonProjectId = student.ProjectBoard.NeonProjectId; // Get the saved NeonProjectId (project-per-tenant)
                        var neonBranchId = student.ProjectBoard.NeonBranchId; // Get the saved NeonBranchId
                        
                        if (!string.IsNullOrEmpty(boardId) && !string.IsNullOrEmpty(dbPassword))
                        {
                            var dbName = $"AppDB_{boardId}";
                            var username = $"db_appdb_{boardId}_user";
                            
                            // Try to extract host from README first
                            string? hostInfo = null;
                            if (!string.IsNullOrEmpty(readmeContent))
                            {
                                var hostMatch = System.Text.RegularExpressions.Regex.Match(readmeContent, 
                                    @"@([a-zA-Z0-9\-\.]+\.neon\.tech|ep-[a-zA-Z0-9\-]+\.gwc\.azure\.neon\.tech):(\d+)", 
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (hostMatch.Success)
                                {
                                    hostInfo = $"{hostMatch.Groups[1].Value}:{hostMatch.Groups[2].Value}";
                                }
                            }
                            
                            // If host not found in README, try to retrieve it dynamically using NeonBranchId and NeonProjectId
                            if (string.IsNullOrEmpty(hostInfo) && !string.IsNullOrEmpty(neonBranchId) && !string.IsNullOrEmpty(neonProjectId))
                            {
                                try
                                {
                                    var neonApiKey = _configuration["Neon:ApiKey"];
                                    var neonBaseUrl = _configuration["Neon:BaseUrl"];
                                    var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";
                                    
                                    if (!string.IsNullOrEmpty(neonApiKey) && !string.IsNullOrEmpty(neonBaseUrl))
                                    {
                                        // Use the Neon API to get the connection string for this specific branch in the tenant's project
                                        var connectionUrl = $"{neonBaseUrl}/projects/{neonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(neonBranchId)}&pooled=false";
                                        
                                        using var httpClient = _httpClientFactory.CreateClient();
                                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);
                                        
                                        var connResponse = await httpClient.GetAsync(connectionUrl);
                                        if (connResponse.IsSuccessStatusCode)
                                        {
                                            var connContent = await connResponse.Content.ReadAsStringAsync();
                                            var connDoc = JsonDocument.Parse(connContent);
                                            if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
                                            {
                                                var fullConnectionString = uriProp.GetString();
                                                if (!string.IsNullOrEmpty(fullConnectionString))
                                                {
                                                    // Extract host and port from the connection string
                                                    var uri = new Uri(fullConnectionString);
                                                    hostInfo = $"{uri.Host}:{uri.Port}";
                                                    _logger.LogInformation("✅ [MENTOR] Successfully retrieved host from Neon API for branch '{BranchId}': {Host}", neonBranchId, hostInfo);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogWarning("⚠️ [MENTOR] Failed to retrieve connection string from Neon API for project '{ProjectId}', branch '{BranchId}': {StatusCode}", 
                                                neonProjectId, neonBranchId, connResponse.StatusCode);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [MENTOR] Neon API key or base URL not configured. Cannot retrieve host dynamically.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "⚠️ [MENTOR] Failed to dynamically retrieve host for project '{ProjectId}', branch '{BranchId}' from Neon API", neonProjectId, neonBranchId);
                                }
                            }
                            else if (string.IsNullOrEmpty(neonProjectId))
                            {
                                _logger.LogWarning("⚠️ [MENTOR] NeonProjectId not found in ProjectBoard. Cannot retrieve host dynamically. This may be an older board created before project-per-tenant model.");
                            }
                            
                            enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n=== DATABASE CONNECTION INFORMATION (FALLBACK - README MISSING/INCOMPLETE) ===\n" +
                                $"- Database Name: {dbName}\n" +
                                $"- Username: {username}\n" +
                                $"- Password: {dbPassword}\n";
                            
                            if (!string.IsNullOrEmpty(hostInfo))
                            {
                                var fullConnectionString = $"postgresql://{username}:{Uri.EscapeDataString(dbPassword)}@{hostInfo}/{dbName}?sslmode=require";
                                enhancedSystemPrompt += $"- Complete Connection String: {fullConnectionString}\n";
                            }
                            else
                            {
                                enhancedSystemPrompt += $"- Host/Port: Could not retrieve dynamically. Please check Neon dashboard for the host and port (typically port 5432) for branch '{neonBranchId}'.\n";
                            }
                            
                            enhancedSystemPrompt += $"=== END DATABASE CONNECTION INFORMATION ===\n\n";
                        }
                    }
                    
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}" +
                        $"🚨 ABSOLUTELY CRITICAL INSTRUCTIONS FOR DATABASE CONNECTION INFORMATION - READ CAREFULLY:\n" +
                        $"1. If the user asks about database connection details, you MUST extract the EXACT connection string from the PROJECT INFORMATION section above (README).\n" +
                        $"2. The connection string is in the format: postgresql://username:password@host:port/database?sslmode=require\n" +
                        $"3. 🚨 FORBIDDEN: You MUST NEVER use placeholders like <boardid>, <BoardId>, db_appdb_<boardid>_user, AppDB_<boardid>, or ANY other placeholder format.\n" +
                        $"4. 🚨 FORBIDDEN: You MUST NEVER say 'replace <boardid> with your board ID' or similar instructions.\n" +
                        $"5. 🚨 FORBIDDEN: You MUST NEVER use angle brackets < > or square brackets [ ] as placeholders.\n" +
                        $"6. ✅ REQUIRED: Extract and provide the COMPLETE, EXACT connection string from the README above.\n" +
                        $"7. ✅ REQUIRED: If the README shows a connection string like 'postgresql://db_appdb_695e42b42ddace5d23fb1f0e_user:password@host:5432/AppDB_695e42b42ddace5d23fb1f0e?sslmode=require', you MUST provide it EXACTLY as shown.\n" +
                        $"8. IMPORTANT: Database name starts with 'AppDB_' (capital A, capital D, capital B) followed by the actual board ID - it does NOT end with '_user'.\n" +
                        $"9. IMPORTANT: Username is 'db_appdb_' (lowercase) followed by the actual board ID and '_user' (lowercase).\n" +
                        $"10. IMPORTANT: PostgreSQL is CASE-SENSITIVE - preserve exact case for database names, usernames, and all values.\n" +
                        $"11. URL-decode the password if it contains encoded characters (e.g., %24 becomes $, %40 becomes @, %25 becomes %).\n" +
                        $"12. 🚨 EXAMPLE OF WHAT NOT TO DO: 'Database Name: AppDB_<boardid>' or 'Username: db_appdb_<boardid>_user' - THIS IS WRONG!\n" +
                        $"13. ✅ EXAMPLE OF WHAT TO DO: If README shows 'AppDB_695e42b42ddace5d23fb1f0e', provide 'AppDB_695e42b42ddace5d23fb1f0e' - EXACTLY as shown.\n" +
                        $"14. DO NOT use generic or default connection information (like neondb_owner) - ALWAYS use what's in the README.\n" +
                        $"15. IGNORE any database connection information from previous chat history - it may be from deleted boards or old projects.\n" +
                        $"16. If the README connection string is missing or incomplete, use the DATABASE CONNECTION INFORMATION (FALLBACK) section above if available.\n" +
                        $"For all other project information, use the content above to answer naturally.";
                }
                
                // Add Web API information for developer roles
                if (isDeveloperRole && (!string.IsNullOrEmpty(webApiUrl) || !string.IsNullOrEmpty(swaggerUrl)))
                {
                    var webApiInfo = "\n\n=== WEB API INFORMATION (Developer Role) ===\n";
                    webApiInfo += "This project has a Web API hosted on Railway:\n";
                    
                    if (!string.IsNullOrEmpty(webApiUrl))
                    {
                        webApiInfo += $"- WebApi URL: {webApiUrl}\n";
                    }
                    
                    if (!string.IsNullOrEmpty(swaggerUrl))
                    {
                        webApiInfo += $"- Swagger API Tester URL: {swaggerUrl}\n";
                        webApiInfo += "The Swagger URL provides an interactive interface to test and explore the API endpoints.\n";
                    }
                    
                    webApiInfo += "\nYou can reference these URLs when helping the developer with API-related questions, testing, or integration tasks.\n";
                    webApiInfo += "=== END WEB API INFORMATION ===\n";
                    
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}{webApiInfo}";
                }
                else if (isAskingAboutConnectionString && string.IsNullOrEmpty(readmeContent) && isDeveloperRole)
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n⚠️ NOTE: The user asked about connection strings or project configuration details, " +
                        $"but this information is not currently available in their project repository. " +
                        $"You should inform them that the requested information could not be found.";
                }
                
                // Add code review warning if user asked to review code but there are no commits
                if (!string.IsNullOrEmpty(codeReviewWarning))
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}{codeReviewWarning}";
                }
                var userMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "user",
                    Message = userQuestion,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(userMessage);
                await _context.SaveChangesAsync();

                // Call the appropriate AI API based on provider
                string aiResponse;
                int inputTokens = 0;
                int outputTokens = 0;
                try
                {
                    if (aiModel.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = await CallOpenAIAsync(aiModel, enhancedSystemPrompt, userQuestion, chatHistory);
                        aiResponse = result.Response;
                        inputTokens = result.InputTokens;
                        outputTokens = result.OutputTokens;
                    }
                    else if (aiModel.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = await CallAnthropicAsync(aiModel, enhancedSystemPrompt, userQuestion, chatHistory);
                        aiResponse = result.Response;
                        inputTokens = result.InputTokens;
                        outputTokens = result.OutputTokens;
                    }
                    else
                    {
                        return BadRequest(new { Success = false, Message = $"Unsupported AI provider: {aiModel.Provider}" });
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("billing") || ex.Message.Contains("credit"))
                {
                    _logger.LogWarning("AI API billing issue: {Error}", ex.Message);
                    return StatusCode(402, new { Success = false, Message = "AI API credits insufficient. Please check your API account billing." });
                }

                // Temporarily append token usage to response
                var responseWithTokens = $"{aiResponse}\n\n---\n[Token Usage: Input={inputTokens}, Output={outputTokens}, Total={inputTokens + outputTokens}]";

                // Save AI response to chat history (without token info)
                var assistantMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "assistant",
                    Message = aiResponse,
                    AIModelName = aiModel.Name,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MentorChatHistory.Add(assistantMessage);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Model = new
                    {
                        aiModel.Id,
                        aiModel.Name,
                        aiModel.Provider
                    },
                    Response = responseWithTokens,
                    Context = contextResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor response for StudentId: {StudentId}, SprintId: {SprintId}, Model: {Model}", 
                    request.StudentId, request.SprintId, aiModel?.Name ?? "Unknown");
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Internal method to get mentor context (extracted from GetMentorContext for reuse)
        /// </summary>
        private async Task<object?> GetMentorContextInternal(int studentId, int sprintId)
        {
            try
            {
                // Reuse the logic from GetMentorContext but return the context object
                var student = await _context.Students
                    .Include(s => s.StudentRoles)
                        .ThenInclude(sr => sr.Role)
                    .Include(s => s.ProgrammingLanguage)
                    .Include(s => s.ProjectBoard)
                        .ThenInclude(pb => pb.Project)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null || string.IsNullOrEmpty(student.BoardId))
                {
                    return null;
                }

                var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
                var roleName = activeRole?.Role?.Name ?? "Team Member";
                var programmingLanguage = student.ProgrammingLanguage?.Name ?? "Not specified";
                var project = student.ProjectBoard?.Project;

                if (project == null)
                {
                    return null;
                }

                // Get sprint list (or Bugs list if sprintId=0)
                var listsResult = await GetBoardListsAsync(student.BoardId);
                string? sprintListId = null;
                string? sprintListName = null;

                // Special handling for sprintId=0 (Bugs sprint)
                if (sprintId == 0)
                {
                    // Look for "Bugs" list
                    foreach (var listObj in listsResult)
                    {
                        var listJson = JsonSerializer.Serialize(listObj);
                        var listElement = JsonSerializer.Deserialize<JsonElement>(listJson);

                        var name = listElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                        var id = listElement.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";

                        if (!string.IsNullOrEmpty(name) && name.Equals("Bugs", StringComparison.OrdinalIgnoreCase))
                        {
                            sprintListId = id;
                            sprintListName = name;
                            break;
                        }
                    }
                }
                else
                {
                    // Find the sprint list by matching sprint number
                    foreach (var listObj in listsResult)
                    {
                        var listJson = JsonSerializer.Serialize(listObj);
                        var listElement = JsonSerializer.Deserialize<JsonElement>(listJson);

                        var name = listElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                        var id = listElement.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";

                        if (!string.IsNullOrEmpty(name) &&
                            (name.Equals($"Sprint {sprintId}", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals($"Sprint{sprintId}", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains($"Sprint {sprintId}", StringComparison.OrdinalIgnoreCase)))
                        {
                            sprintListId = id;
                            sprintListName = name;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(sprintListId))
                {
                    return null;
                }

                // Get user tasks
                var userTasksResult = await _trelloService.GetCardsAndListsByLabelAsync(student.BoardId, roleName);
                var userTasksJson = JsonSerializer.Serialize(userTasksResult);
                var userTasksElement = JsonSerializer.Deserialize<JsonElement>(userTasksJson);

                var userTasks = new List<object>();
                if (userTasksElement.TryGetProperty("Cards", out var cardsProp) && cardsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var card in cardsProp.EnumerateArray())
                    {
                        var cardListId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : "";
                        if (cardListId == sprintListId)
                        {
                            var cardId = card.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";
                            var customFields = await GetTrelloCardCustomFieldsAsync(cardId, student.BoardId);

                            var checklistItems = new List<string>();
                            if (card.TryGetProperty("Checklists", out var checklistsProp) && checklistsProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var checklist in checklistsProp.EnumerateArray())
                                {
                                    if (checklist.TryGetProperty("CheckItems", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in itemsProp.EnumerateArray())
                                        {
                                            var itemName = item.TryGetProperty("Name", out var itemNameProp) ? itemNameProp.GetString() : "";
                                            if (!string.IsNullOrEmpty(itemName))
                                            {
                                                checklistItems.Add(itemName);
                                            }
                                        }
                                    }
                                }
                            }

                            userTasks.Add(new
                            {
                                Id = cardId,
                                Name = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "",
                                Description = card.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                                Closed = card.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false,
                                DueDate = card.TryGetProperty("DueDate", out var dueProp) ? dueProp.GetString() : null,
                                CustomFields = customFields,
                                ChecklistItems = checklistItems
                            });
                        }
                    }
                }

                // Get module descriptions
                var moduleDescriptions = new Dictionary<string, string>();
                foreach (var task in userTasks)
                {
                    var taskObj = JsonSerializer.Serialize(task);
                    var taskElement = JsonSerializer.Deserialize<JsonElement>(taskObj);
                    if (taskElement.TryGetProperty("CustomFields", out var cfProp) && cfProp.ValueKind == JsonValueKind.Object)
                    {
                        if (cfProp.TryGetProperty("ModuleId", out var moduleIdProp))
                        {
                            var moduleIdStr = "";
                            if (moduleIdProp.ValueKind == JsonValueKind.String)
                            {
                                moduleIdStr = moduleIdProp.GetString() ?? "";
                            }
                            else if (moduleIdProp.ValueKind == JsonValueKind.Number)
                            {
                                moduleIdStr = moduleIdProp.GetRawText();
                            }

                            if (!string.IsNullOrEmpty(moduleIdStr))
                            {
                                moduleIdStr = moduleIdStr.Trim().Trim('"').Trim('\'').Trim();
                                if (int.TryParse(moduleIdStr, out int moduleId))
                                {
                                    var module = await _context.ProjectModules
                                        .FirstOrDefaultAsync(pm => pm.Id == moduleId && pm.ProjectId == project.Id);

                                    if (module != null && !string.IsNullOrEmpty(module.Description))
                                    {
                                        moduleDescriptions[moduleIdStr] = module.Description;
                                    }
                                }
                            }
                        }
                    }
                }

                // Get GitHub files - use role-based repository selection
                var githubFiles = new List<string>();
                object? githubCommitSummary = null;
                
                var (frontendRepoUrl, backendRepoUrl, isFullstack) = GetRepositoryUrlsByRole(student);
                
                // Determine which repos to fetch based on role
                var reposToFetch = new List<(string Url, string Type)>();
                if (!string.IsNullOrEmpty(frontendRepoUrl))
                {
                    reposToFetch.Add((frontendRepoUrl, "frontend"));
                }
                if (!string.IsNullOrEmpty(backendRepoUrl))
                {
                    reposToFetch.Add((backendRepoUrl, "backend"));
                }
                
                if (reposToFetch.Any())
                {
                    try
                    {
                        var allFiles = new List<string>();
                        var allCommitSummaries = new List<object>();
                        
                        foreach (var (repoUrl, repoType) in reposToFetch)
                        {
                            var repoName = repoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/');
                            var repoFiles = await GetGitHubRepositoryFilesAsync(repoName);
                            
                            if (isFullstack)
                            {
                                allFiles.AddRange(repoFiles.Select(f => $"[{repoType.ToUpper()}] {f}"));
                            }
                            else
                            {
                                allFiles.AddRange(repoFiles);
                            }
                            
                            var summary = await GetGitHubCommitSummaryAsync(repoUrl, student.GithubUser);
                            if (summary != null)
                            {
                                allCommitSummaries.Add(summary);
                            }
                        }
                        
                        githubFiles = allFiles;
                        
                        if (isFullstack && allCommitSummaries.Any())
                        {
                            githubCommitSummary = CombineCommitSummaries(allCommitSummaries);
                        }
                        else if (allCommitSummaries.Any())
                        {
                            githubCommitSummary = allCommitSummaries.First();
                        }
                    }
                    catch (Exception ex)
                    {
                        var repoList = string.Join(", ", reposToFetch.Select(r => r.Url));
                        _logger.LogWarning(ex, "Failed to fetch GitHub files for repos: {Repos}", repoList);
                    }
                }

                // Get team members
                var teamMembers = new List<object>();
                if (!string.IsNullOrEmpty(student.BoardId))
                {
                    var teamStudents = await _context.Students
                        .Include(s => s.StudentRoles)
                            .ThenInclude(sr => sr.Role)
                        .Where(s => s.BoardId == student.BoardId && s.Id != studentId)
                        .ToListAsync();

                    foreach (var teamStudent in teamStudents)
                    {
                        var teamRole = teamStudent.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
                        teamMembers.Add(new
                        {
                            Id = teamStudent.Id,
                            FirstName = teamStudent.FirstName,
                            LastName = teamStudent.LastName,
                            RoleName = teamRole?.Role?.Name ?? "Team Member"
                        });
                    }
                }

                // Get team member tasks
                var teamMemberTasks = new List<object>();
                foreach (var teamMember in teamMembers)
                {
                    var memberObj = JsonSerializer.Serialize(teamMember);
                    var memberElement = JsonSerializer.Deserialize<JsonElement>(memberObj);
                    var memberRoleName = memberElement.TryGetProperty("RoleName", out var roleProp) ? roleProp.GetString() : "";

                    if (!string.IsNullOrEmpty(memberRoleName))
                    {
                        var memberTasksResult = await _trelloService.GetCardsAndListsByLabelAsync(student.BoardId, memberRoleName);
                        var memberTasksJson = JsonSerializer.Serialize(memberTasksResult);
                        var memberTasksElement = JsonSerializer.Deserialize<JsonElement>(memberTasksJson);

                        if (memberTasksElement.TryGetProperty("Cards", out var memberCardsProp) && memberCardsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var card in memberCardsProp.EnumerateArray())
                            {
                                var cardListId = card.TryGetProperty("ListId", out var listIdProp) ? listIdProp.GetString() : "";
                                if (cardListId == sprintListId)
                                {
                                    teamMemberTasks.Add(new
                                    {
                                        TeamMemberFirstName = memberElement.TryGetProperty("FirstName", out var fnProp) ? fnProp.GetString() : "",
                                        TeamMemberRoleName = memberRoleName,
                                        TaskName = card.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "",
                                        IsClosed = card.TryGetProperty("Closed", out var closedProp) ? closedProp.GetBoolean() : false
                                    });
                                }
                            }
                        }
                    }
                }

                // Format data
                var currentTaskDetails = FormatTaskDetails(userTasks);
                var moduleDescription = "No current tasks";
                if (userTasks.Count > 0)
                {
                    var firstTaskJson = JsonSerializer.Serialize(userTasks[0]);
                    var firstTaskElement = JsonSerializer.Deserialize<JsonElement>(firstTaskJson);
                    moduleDescription = GetModuleDescriptionForFirstTask(firstTaskElement, moduleDescriptions);
                }
                
                var teamMembersList = FormatTeamMembers(teamMembers);
                var teamMemberTasksList = FormatTeamMemberTasks(teamMemberTasks);
                var githubFilesList = string.Join("\n", githubFiles);

                // Format sprint identifier for prompt (show "Bugs" when sprintId=0)
                var sprintIdentifier = sprintId == 0 ? "Bugs" : sprintId.ToString();
                var sprintDisplayName = sprintId == 0 ? "Bugs" : (sprintListName ?? $"Sprint {sprintId}");
                
                var formattedPrompt = string.Format(
                    _promptConfig.Mentor.UserPromptTemplate,
                    student.FirstName,
                    roleName,
                    sprintIdentifier,
                    sprintDisplayName,
                    currentTaskDetails,
                    programmingLanguage,
                    FormatTaskDetails(userTasks),
                    moduleDescription,
                    teamMembersList,
                    teamMemberTasksList,
                    githubFilesList,
                    "" // Placeholder for user question - will be replaced in the calling method
                );

                return new
                {
                    SystemPrompt = _promptConfig.Mentor.SystemPrompt,
                    UserPrompt = formattedPrompt,
                    Context = new
                    {
                        StudentId = studentId,
                        SprintId = sprintId,
                        UserProfile = new
                        {
                            FirstName = student.FirstName,
                            Role = roleName,
                            ProgrammingLanguage = programmingLanguage
                        },
                        CurrentTasks = userTasks,
                        ModuleDescriptions = moduleDescriptions,
                        TeamMembers = teamMembers,
                        TeamMemberTasks = teamMemberTasks,
                        GitHubFiles = githubFiles,
                        GitHubCommitSummary = githubCommitSummary,
                        NextTeamMeeting = new
                        {
                            Time = student.ProjectBoard?.NextMeetingTime,
                            Url = student.ProjectBoard?.NextMeetingUrl
                        },
                        DatabasePassword = student.ProjectBoard?.DBPassword
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor context internally");
                return null;
            }
        }

        /// <summary>
        /// Call OpenAI API
        /// </summary>
        private async Task<(string Response, int InputTokens, int OutputTokens)> CallOpenAIAsync(AIModel aiModel, string systemPrompt, string userPrompt, List<ChatHistoryItem>? chatHistory = null)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("OpenAI API key not configured");
                }

                var baseUrl = aiModel.BaseUrl ?? "https://api.openai.com/v1";
                var maxTokens = aiModel.MaxTokens ?? 16384;
                var temperature = aiModel.DefaultTemperature ?? 0.2;

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                // Build messages array with chat history
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // Add chat history messages
                if (chatHistory != null && chatHistory.Any())
                {
                    foreach (var historyItem in chatHistory)
                    {
                        var role = historyItem.Role == "assistant" ? "assistant" : "user";
                        messages.Add(new { role = role, content = historyItem.Message });
                    }
                }

                // Add current user prompt
                messages.Add(new { role = "user", content = userPrompt });

                var requestBody = new
                {
                    model = aiModel.Name,
                    messages = messages.ToArray(),
                    max_tokens = maxTokens,
                    temperature = temperature
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling OpenAI API with model {Model}", aiModel.Name);
                var response = await httpClient.PostAsync($"{baseUrl}/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new Exception($"OpenAI API error: {response.StatusCode}. {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAIResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                // Extract token usage
                int inputTokens = 0;
                int outputTokens = 0;
                if (openAIResponse.TryGetProperty("usage", out var usageProp))
                {
                    if (usageProp.TryGetProperty("prompt_tokens", out var promptTokensProp))
                    {
                        inputTokens = promptTokensProp.GetInt32();
                    }
                    if (usageProp.TryGetProperty("completion_tokens", out var completionTokensProp))
                    {
                        outputTokens = completionTokensProp.GetInt32();
                    }
                }

                if (openAIResponse.TryGetProperty("choices", out var choicesProp) && choicesProp.ValueKind == JsonValueKind.Array && choicesProp.GetArrayLength() > 0)
                {
                    var firstChoice = choicesProp[0];
                    if (firstChoice.TryGetProperty("message", out var messageProp))
                    {
                        if (messageProp.TryGetProperty("content", out var contentProp))
                        {
                            var responseText = contentProp.GetString() ?? "";
                            return (responseText, inputTokens, outputTokens);
                        }
                    }
                }

                throw new Exception("Failed to parse OpenAI response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                throw;
            }
        }

        /// <summary>
        /// Call Anthropic API
        /// </summary>
        private async Task<(string Response, int InputTokens, int OutputTokens)> CallAnthropicAsync(AIModel aiModel, string systemPrompt, string userPrompt, List<ChatHistoryItem>? chatHistory = null)
        {
            try
            {
                var apiKey = _configuration["Anthropic:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("Anthropic API key not configured");
                }

                var baseUrl = aiModel.BaseUrl ?? "https://api.anthropic.com/v1";
                var apiVersion = aiModel.ApiVersion ?? "2023-06-01";
                var maxTokens = aiModel.MaxTokens ?? 200000;
                var temperature = aiModel.DefaultTemperature ?? 0.3;

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", apiVersion);
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                // Build messages array with chat history
                var messages = new List<object>();

                // Add chat history messages
                if (chatHistory != null && chatHistory.Any())
                {
                    foreach (var historyItem in chatHistory)
                    {
                        var role = historyItem.Role == "assistant" ? "assistant" : "user";
                        messages.Add(new { role = role, content = historyItem.Message });
                    }
                }

                // Add current user prompt
                messages.Add(new { role = "user", content = userPrompt });

                var requestBody = new
                {
                    model = aiModel.Name,
                    max_tokens = maxTokens,
                    system = systemPrompt,
                    messages = messages.ToArray(),
                    temperature = temperature
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Anthropic API with model {Model}", aiModel.Name);
                var response = await httpClient.PostAsync($"{baseUrl}/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Anthropic API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new Exception($"Anthropic API error: {response.StatusCode}. {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var anthropicResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                // Extract token usage
                int inputTokens = 0;
                int outputTokens = 0;
                if (anthropicResponse.TryGetProperty("usage", out var usageProp))
                {
                    if (usageProp.TryGetProperty("input_tokens", out var inputTokensProp))
                    {
                        inputTokens = inputTokensProp.GetInt32();
                    }
                    if (usageProp.TryGetProperty("output_tokens", out var outputTokensProp))
                    {
                        outputTokens = outputTokensProp.GetInt32();
                    }
                }

                if (anthropicResponse.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Array && contentProp.GetArrayLength() > 0)
                {
                    var firstContent = contentProp[0];
                    if (firstContent.TryGetProperty("text", out var textProp))
                    {
                        var responseText = textProp.GetString() ?? "";
                        return (responseText, inputTokens, outputTokens);
                    }
                }

                throw new Exception("Failed to parse Anthropic response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Anthropic API");
                throw;
            }
        }

        /// <summary>
        /// Build GitHub help text based on user's question
        /// </summary>
        private string BuildGitHubHelpText(string? repoUrl, string? githubUser, Dictionary<string, object>? parameters, bool isDeveloperRole = true)
        {
            var sb = new StringBuilder();
            var operation = parameters?.TryGetValue("operation", out var op) == true ? op?.ToString()?.ToLower() : null;

            // Only assume GitHub account/repository exists for developer roles
            if (!isDeveloperRole)
            {
                sb.AppendLine("Here's a general guide to using GitHub:");
                sb.AppendLine();
                sb.AppendLine("**1. Create a GitHub Account:**");
                sb.AppendLine("Go to https://github.com and sign up for a free account.");
                sb.AppendLine();
                sb.AppendLine("**2. Create a New Repository:**");
                sb.AppendLine("1. Go to https://github.com/new");
                sb.AppendLine("2. Name your repository");
                sb.AppendLine("3. Choose public or private");
                sb.AppendLine("4. Click 'Create repository'");
                sb.AppendLine();
                sb.AppendLine("**3. Initialize Git in your project:**");
                sb.AppendLine("```bash");
                sb.AppendLine("cd your-project-folder");
                sb.AppendLine("git init");
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("**4. Add your files:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git add .");
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("**5. Create your first commit:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git commit -m \"Initial commit\"");
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("**6. Connect to your GitHub repository:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO.git");
                sb.AppendLine("git branch -M main");
                sb.AppendLine("git push -u origin main");
                sb.AppendLine("```");
                sb.AppendLine();
                return sb.ToString();
            }

            // For developer roles, assume GitHub account and repository exist
            var hasGitHubAccount = !string.IsNullOrEmpty(githubUser);
            var hasRepository = !string.IsNullOrEmpty(repoUrl);

            if (hasGitHubAccount && hasRepository)
            {
                sb.AppendLine($"Great! I can see you already have a GitHub account (`{githubUser}`) and your project repository is set up at: {repoUrl}");
                sb.AppendLine();
                sb.AppendLine("Here's how to get your code into that repository:");
                sb.AppendLine();
            }
            else if (hasGitHubAccount)
            {
                sb.AppendLine($"I can see you have a GitHub account (`{githubUser}`). Here's how to get your code on GitHub:");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Here's a step-by-step guide to get your code on GitHub:");
                sb.AppendLine();
            }

            if (operation == "setup" || string.IsNullOrEmpty(operation))
            {
                if (!hasGitHubAccount)
                {
                    sb.AppendLine("**1. Create a GitHub Account:**");
                    sb.AppendLine("Go to https://github.com and sign up for a free account.");
                    sb.AppendLine();
                }

                if (!hasRepository && hasGitHubAccount)
                {
                    sb.AppendLine("**1. Create a New Repository:**");
                    sb.AppendLine("1. Go to https://github.com/new");
                    sb.AppendLine("2. Name your repository");
                    sb.AppendLine("3. Choose public or private");
                    sb.AppendLine("4. Click 'Create repository'");
                    sb.AppendLine();
                }

                var stepNum = 1;
                if (hasGitHubAccount && hasRepository)
                {
                    stepNum = 1;
                }
                else if (hasGitHubAccount || hasRepository)
                {
                    stepNum = 2;
                }
                else
                {
                    stepNum = 3;
                }

                sb.AppendLine($"**{stepNum}. Initialize Git in your project:**");
                sb.AppendLine("```bash");
                sb.AppendLine("cd your-project-folder");
                sb.AppendLine("git init");
                sb.AppendLine("```");
                sb.AppendLine();

                stepNum++;
                sb.AppendLine($"**{stepNum}. Add your files:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git add .");
                sb.AppendLine("# Or add specific files: git add file1.cs file2.cs");
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (operation == "commit" || string.IsNullOrEmpty(operation))
            {
                var stepNum = hasGitHubAccount && hasRepository ? 3 : 
                             (hasGitHubAccount || hasRepository ? 3 : 4);
                
                sb.AppendLine($"**{stepNum}. Create your first commit:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git commit -m \"Initial commit: Add project files\"");
                sb.AppendLine("# Use a descriptive message about what you're committing");
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(repoUrl))
            {
                var stepNum = hasGitHubAccount && hasRepository ? 4 : 
                             (hasGitHubAccount || hasRepository ? 4 : 5);
                
                sb.AppendLine($"**{stepNum}. Connect to your GitHub repository:**");
                sb.AppendLine("```bash");
                // Check if repoUrl already ends with .git
                var remoteUrl = repoUrl.EndsWith(".git") ? repoUrl : $"{repoUrl}.git";
                sb.AppendLine($"git remote add origin {remoteUrl}");
                sb.AppendLine("# If you already have a remote, use: git remote set-url origin <url>");
                sb.AppendLine("```");
                sb.AppendLine();

                stepNum++;
                sb.AppendLine($"**{stepNum}. Push your code:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git branch -M main  # Rename branch to 'main' (if needed)");
                sb.AppendLine("git push -u origin main");
                sb.AppendLine("```");
                sb.AppendLine();
            }
            else if (hasGitHubAccount)
            {
                var stepNum = 4;
                sb.AppendLine($"**{stepNum}. Connect to your GitHub repository:**");
                sb.AppendLine("First, create a repository on GitHub, then:");
                sb.AppendLine("```bash");
                sb.AppendLine($"git remote add origin https://github.com/{githubUser}/YOUR_REPO_NAME.git");
                sb.AppendLine("git branch -M main");
                sb.AppendLine("git push -u origin main");
                sb.AppendLine("```");
                sb.AppendLine();
            }
            else
            {
                var stepNum = 4;
                sb.AppendLine($"**{stepNum}. Connect to your GitHub repository:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO.git");
                sb.AppendLine("git branch -M main");
                sb.AppendLine("git push -u origin main");
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (operation == "push" || string.IsNullOrEmpty(operation))
            {
                sb.AppendLine("**For future updates:**");
                sb.AppendLine("```bash");
                sb.AppendLine("git add .                    # Stage your changes");
                sb.AppendLine("git commit -m \"Your message\"  # Commit with a message");
                sb.AppendLine("git push                      # Push to GitHub");
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(githubUser))
            {
                sb.AppendLine($"**Note:** Make sure you're committing with your GitHub username: `{githubUser}`");
                sb.AppendLine("You can configure this with:");
                sb.AppendLine("```bash");
                sb.AppendLine($"git config user.name \"{githubUser}\"");
                sb.AppendLine("git config user.email \"your-email@example.com\"");
                sb.AppendLine("```");
            }

            sb.AppendLine();
            sb.AppendLine("Once you've pushed your code, I'll be able to review it! Just ask me to \"review my code\" after you've made a commit.");

            return sb.ToString();
        }

        /// <summary>
        /// Creates a GitHub branch for a sprint following the branch naming convention
        /// </summary>
        [HttpPost("use/github-branch")]
        public async Task<ActionResult<CreateGitHubBranchResponse>> CreateGitHubBranch([FromBody] CreateGitHubBranchRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body is required");
                }

                if (request.SprintNumber < 0 || request.SprintNumber > 20)
                {
                    return BadRequest("Sprint number must be between 0 and 20 (0 for Bugs branch)");
                }

                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(b => b.Id == request.BoardId);

                if (board == null)
                {
                    return NotFound($"Project board with BoardId {request.BoardId} not found");
                }

                var githubUrl = request.IsBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;

                if (string.IsNullOrEmpty(githubUrl))
                {
                    return BadRequest($"GitHub {(request.IsBackend ? "backend" : "frontend")} URL not found for board {request.BoardId}");
                }

                var uri = new Uri(githubUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest($"Invalid GitHub URL format: {githubUrl}");
                }

                var owner = pathParts[0];
                var repo = pathParts[1];

                string branchName;
                if (request.SprintNumber == 0)
                {
                    // Special case: sprintNumber 0 creates a "Bugs" branch
                    branchName = "Bugs";
                }
                else
                {
                    // Normal case: use sprint number and repo type letter
                    var patternKey = request.IsBackend ? "GitHub:BranchNamingPatterns:Backend" : "GitHub:BranchNamingPatterns:Frontend";
                    var pattern = _configuration[patternKey] ?? (request.IsBackend ? "^([1-9]|1[0-9]|20)-B$" : "^([1-9]|1[0-9]|20)-F$");

                    var repoTypeLetter = request.IsBackend ? "B" : "F";
                    branchName = $"{request.SprintNumber}-{repoTypeLetter}";
                }

                var accessToken = _configuration["GitHub:AccessToken"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    return StatusCode(500, "GitHub access token is not configured");
                }

                // Check for unmerged branches (all branches excluding main)
                _logger.LogInformation("🔍 [GITHUB BRANCH] Checking for unmerged branches in {Owner}/{Repo}", owner, repo);
                
                // Get all branches from the repository
                var branchesUrl = $"https://api.github.com/repos/{owner}/{repo}/branches";
                using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
                var branchesRequest = new HttpRequestMessage(HttpMethod.Get, branchesUrl);
                branchesRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                branchesRequest.Headers.Add("User-Agent", "StrAppersBackend/1.0");
                branchesRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                
                var branchesResponse = await httpClient.SendAsync(branchesRequest);
                var unmergedBranches = new List<string>();
                
                if (branchesResponse.IsSuccessStatusCode)
                {
                    var branchesContent = await branchesResponse.Content.ReadAsStringAsync();
                    var branchesData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(branchesContent, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    // Filter out "main" branch - all other branches are considered unmerged
                    foreach (var branch in branchesData ?? Array.Empty<System.Text.Json.JsonElement>())
                    {
                        var branchNameFromApi = branch.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                        if (!string.IsNullOrEmpty(branchNameFromApi) && 
                            !branchNameFromApi.Equals("main", StringComparison.OrdinalIgnoreCase) &&
                            !branchNameFromApi.Equals("master", StringComparison.OrdinalIgnoreCase))
                        {
                            unmergedBranches.Add(branchNameFromApi);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ [GITHUB BRANCH] Failed to get branches list. Status: {StatusCode}", branchesResponse.StatusCode);
                }
                
                if (unmergedBranches.Count > 0)
                {
                    var branchesList = string.Join(", ", unmergedBranches);
                    
                    _logger.LogWarning("❌ [GITHUB BRANCH] Cannot create branch '{BranchName}'. Found {Count} unmerged branch(es): {Branches}", 
                        branchName, unmergedBranches.Count, branchesList);
                    
                    return BadRequest(new CreateGitHubBranchResponse
                    {
                        Success = false,
                        ErrorMessage = $"Cannot create branch '{branchName}'. There are {unmergedBranches.Count} unmerged branch(es): {branchesList}. Please merge or delete the existing branches before creating a new branch.",
                        StatusCode = 400
                    });
                }

                _logger.LogInformation("✅ [GITHUB BRANCH] No unmerged branches found. Creating branch '{BranchName}' in {Owner}/{Repo} for sprint {SprintNumber}",
                    branchName, owner, repo, request.SprintNumber);

                var branchResponse = await _githubService.CreateBranchAsync(owner, repo, branchName, "main", accessToken);

                if (branchResponse.Success)
                {
                    return Ok(new CreateGitHubBranchResponse
                    {
                        Success = true,
                        BranchUrl = branchResponse.BranchUrl,
                        BranchName = branchName,
                        GitHubResponse = branchResponse.GitHubResponse,
                        StatusCode = branchResponse.StatusCode
                    });
                }
                else
                {
                    _logger.LogError("❌ [GITHUB BRANCH] Failed to create branch: {Error}", branchResponse.ErrorMessage);
                    return StatusCode(branchResponse.StatusCode > 0 ? branchResponse.StatusCode : 500, new CreateGitHubBranchResponse
                    {
                        Success = false,
                        ErrorMessage = branchResponse.ErrorMessage,
                        StatusCode = branchResponse.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GITHUB BRANCH] Error creating GitHub branch: {Message}", ex.Message);
                return StatusCode(500, new CreateGitHubBranchResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
        }
    }

    /// <summary>
    /// Validate backend configuration for a board
    /// Validates GitHub repository, Railway service, database connection, and code structure
    /// </summary>
    [HttpGet("use/validate-backend")]
    public async Task<ActionResult<object>> ValidateBackend([FromQuery] string boardId, [FromQuery] string? branch = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(boardId))
            {
                return BadRequest(new { Success = false, Message = "boardId query parameter is required" });
            }

            var branchName = branch ?? "main";
            _logger.LogInformation("🔍 [VALIDATION] Starting backend validation for BoardId: {BoardId}, Branch: {Branch}", boardId, branchName);

            // Get board from database
            var board = await _context.ProjectBoards
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (board == null)
            {
                return NotFound(new { Success = false, Message = $"Board with ID {boardId} not found" });
            }

            var validationResult = new
            {
                boardId = boardId,
                timestamp = DateTime.UtcNow,
                github = new { },
                railway = new { },
                database = new { },
                codeStructure = new { },
                overall = new { valid = false, issues = new List<string>() }
            };

            var issues = new List<string>();
            var githubValid = false;
            var railwayValid = false;
            var databaseValid = false;
            var codeStructureValid = false;

            // 1. GitHub Repository Validation
            var githubResult = await ValidateGitHubBackendAsync(board, boardId, branchName);
            githubValid = githubResult.Valid;
            if (!githubValid)
            {
                issues.AddRange(githubResult.Issues);
            }

            // 2. Railway Service Validation
            var railwayResult = await ValidateRailwayBackendAsync(board, boardId);
            railwayValid = railwayResult.Valid;
            if (!railwayValid)
            {
                issues.AddRange(railwayResult.Issues);
            }

            // 3. Database Connection Validation
            var databaseResult = await ValidateDatabaseBackendAsync(board, boardId);
            databaseValid = databaseResult.Valid;
            if (!databaseValid)
            {
                issues.AddRange(databaseResult.Issues);
            }

            // 4. Code Structure Validation (middleware, CORS, etc.)
            var codeResult = await ValidateCodeStructureBackendAsync(board, boardId, branchName);
            codeStructureValid = codeResult.Valid;
            if (!codeStructureValid)
            {
                issues.AddRange(codeResult.Issues);
            }

            // 5. Build Status Validation (Railway)
            var buildStatusResult = await ValidateBuildStatusBackendAsync(board, boardId);
            var buildStatusValid = buildStatusResult.Valid;
            if (!buildStatusValid)
            {
                issues.AddRange(buildStatusResult.Issues);
            }

            var overallValid = githubValid && railwayValid && databaseValid && codeStructureValid && buildStatusValid;

            // Record validation result in BoardStates (both success and failure)
            try
            {
                var status = overallValid ? "SUCCESS" : "FAILED";
                var output = overallValid 
                    ? $"[BACKEND VALIDATION SUCCESS] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\nBranch: {branchName}\nAll validations passed."
                    : $"[BACKEND VALIDATION FAILED] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\nBranch: {branchName}\nIssues:\n{string.Join("\n", issues.Select((issue, idx) => $"{idx + 1}. {issue}"))}";
                
                var timestamp = DateTime.UtcNow;
                var createdAt = DateTime.UtcNow;
                var updatedAt = DateTime.UtcNow;
                var source = "PR-BackendValidation";
                var webhook = false;
                var errorMessage = overallValid ? null : string.Join("; ", issues);
                var devRole = DetermineDevRole(source, branchName);

                FormattableString sql = $@"
                    INSERT INTO ""BoardStates"" (
                        ""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"",
                        ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", ""Timestamp"", 
                        ""DevRole"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        {boardId}, {source}, {webhook}, {branchName},
                        {status}, {output}, {errorMessage}, {timestamp},
                        {devRole},
                        {createdAt}, {updatedAt}
                    )
                    ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                    DO UPDATE SET
                        ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                        ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                        ""ErrorMessage"" = EXCLUDED.""ErrorMessage"",
                        ""Timestamp"" = EXCLUDED.""Timestamp"",
                        ""DevRole"" = EXCLUDED.""DevRole"",
                        ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
                ";

                await _context.Database.ExecuteSqlInterpolatedAsync(sql);
                _logger.LogInformation("✅ [VALIDATION] Recorded backend validation {Status} for BoardId: {BoardId}, Branch: {Branch}", status, boardId, branchName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [VALIDATION] Failed to record backend validation result for BoardId: {BoardId}", boardId);
            }

            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                Branch = branchName,
                Timestamp = DateTime.UtcNow,
                Overall = new
                {
                    Valid = overallValid,
                    Issues = issues
                },
                GitHub = githubResult,
                Railway = railwayResult,
                Database = databaseResult,
                CodeStructure = codeResult,
                BuildStatus = buildStatusResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [VALIDATION] Error validating backend for BoardId: {BoardId}", boardId);
            return StatusCode(500, new { Success = false, Message = $"Error validating backend: {ex.Message}" });
        }
    }

    /// <summary>
    /// Validate frontend configuration for a board
    /// Validates GitHub repository, config.js, API URL configuration, and deployment
    /// </summary>
    [HttpGet("use/validate-frontend")]
    public async Task<ActionResult<object>> ValidateFrontend([FromQuery] string boardId, [FromQuery] string? branch = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(boardId))
            {
                return BadRequest(new { Success = false, Message = "boardId query parameter is required" });
            }

            var branchName = branch ?? "main";
            _logger.LogInformation("🔍 [VALIDATION] Starting frontend validation for BoardId: {BoardId}, Branch: {Branch}", boardId, branchName);

            // Get board from database
            var board = await _context.ProjectBoards
                .FirstOrDefaultAsync(pb => pb.Id == boardId);

            if (board == null)
            {
                return NotFound(new { Success = false, Message = $"Board with ID {boardId} not found" });
            }

            var issues = new List<string>();
            var githubValid = false;
            var configValid = false;
            var deploymentValid = false;

            // 1. GitHub Repository Validation
            var githubResult = await ValidateGitHubFrontendAsync(board, boardId, branchName);
            githubValid = githubResult.Valid;
            if (!githubValid)
            {
                issues.AddRange(githubResult.Issues);
            }

            // 2. Config.js Validation
            var configResult = await ValidateFrontendConfigAsync(board, boardId, branchName);
            configValid = configResult.Valid;
            if (!configValid)
            {
                issues.AddRange(configResult.Issues);
            }

            // 3. Deployment Validation
            var deploymentResult = await ValidateFrontendDeploymentAsync(board, boardId);
            deploymentValid = deploymentResult.Valid;
            if (!deploymentValid)
            {
                issues.AddRange(deploymentResult.Issues);
            }

            // 4. Build Status Validation (GitHub Pages - for future implementation)
            var buildStatusResult = await ValidateBuildStatusFrontendAsync(board, boardId);
            var buildStatusValid = buildStatusResult.Valid;
            if (!buildStatusValid)
            {
                issues.AddRange(buildStatusResult.Issues);
            }

            var overallValid = githubValid && configValid && deploymentValid && buildStatusValid;

            // Record validation result in BoardStates (both success and failure)
            try
            {
                var status = overallValid ? "SUCCESS" : "FAILED";
                var output = overallValid 
                    ? $"[FRONTEND VALIDATION SUCCESS] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\nBranch: {branchName}\nAll validations passed."
                    : $"[FRONTEND VALIDATION FAILED] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\nBranch: {branchName}\nIssues:\n{string.Join("\n", issues.Select((issue, idx) => $"{idx + 1}. {issue}"))}";
                
                var timestamp = DateTime.UtcNow;
                var createdAt = DateTime.UtcNow;
                var updatedAt = DateTime.UtcNow;
                var source = "PR-FrontendValidation";
                var webhook = false;
                var errorMessage = overallValid ? null : string.Join("; ", issues);
                var devRole = DetermineDevRole(source, branchName);

                FormattableString sql = $@"
                    INSERT INTO ""BoardStates"" (
                        ""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"",
                        ""LastBuildStatus"", ""LastBuildOutput"", ""ErrorMessage"", ""Timestamp"", 
                        ""DevRole"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        {boardId}, {source}, {webhook}, {branchName},
                        {status}, {output}, {errorMessage}, {timestamp},
                        {devRole},
                        {createdAt}, {updatedAt}
                    )
                    ON CONFLICT (""BoardId"", ""Source"", ""Webhook"", ""GithubBranch"") 
                    DO UPDATE SET
                        ""LastBuildStatus"" = EXCLUDED.""LastBuildStatus"",
                        ""LastBuildOutput"" = EXCLUDED.""LastBuildOutput"",
                        ""ErrorMessage"" = EXCLUDED.""ErrorMessage"",
                        ""Timestamp"" = EXCLUDED.""Timestamp"",
                        ""DevRole"" = EXCLUDED.""DevRole"",
                        ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
                ";

                await _context.Database.ExecuteSqlInterpolatedAsync(sql);
                _logger.LogInformation("✅ [VALIDATION] Recorded frontend validation {Status} for BoardId: {BoardId}, Branch: {Branch}", status, boardId, branchName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [VALIDATION] Failed to record frontend validation result for BoardId: {BoardId}", boardId);
            }

            return Ok(new
            {
                Success = true,
                BoardId = boardId,
                Branch = branchName,
                Timestamp = DateTime.UtcNow,
                Overall = new
                {
                    Valid = overallValid,
                    Issues = issues
                },
                GitHub = githubResult,
                Config = configResult,
                Deployment = deploymentResult,
                BuildStatus = buildStatusResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [VALIDATION] Error validating frontend for BoardId: {BoardId}", boardId);
            return StatusCode(500, new { Success = false, Message = $"Error validating frontend: {ex.Message}" });
        }
    }

    /// <summary>
    /// Request model for mentor response endpoint
    /// </summary>
    public class MentorRequest
    {
        public int StudentId { get; set; }
        public int SprintId { get; set; }
        public string UserQuestion { get; set; } = string.Empty;
    }

    #region Validation Helper Methods

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateGitHubBackendAsync(ProjectBoard board, string boardId, string branch = "main")
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.GithubBackendUrl))
        {
            issues.Add("GithubBackendUrl is not set in ProjectBoards table");
            return (false, issues, details);
        }

        try
        {
            // Parse GitHub URL
            var uri = new Uri(board.GithubBackendUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                issues.Add($"Invalid GitHub backend URL format: {board.GithubBackendUrl}");
                return (false, issues, details);
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");

            details["Owner"] = owner;
            details["Repository"] = repo;
            details["Url"] = board.GithubBackendUrl;

            // Check if repository exists via GitHub API
            var accessToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                issues.Add("GitHub access token not configured");
                return (false, issues, details);
            }

            // Try to get repository info
            // Use the named HttpClient "DeploymentController" which has SSL certificate validation configured for GitHub
            using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var repoResponse = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}");
            if (!repoResponse.IsSuccessStatusCode)
            {
                issues.Add($"GitHub repository not found or not accessible: {board.GithubBackendUrl} (Status: {repoResponse.StatusCode})");
                details["RepositoryExists"] = false;
                return (false, issues, details);
            }

            details["RepositoryExists"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating GitHub backend for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating GitHub repository: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateRailwayBackendAsync(ProjectBoard board, string boardId)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        var railwayApiToken = _configuration["Railway:ApiToken"];
        var railwayApiUrl = _configuration["Railway:ApiUrl"] ?? "https://backboard.railway.app/graphql/v2";
        var railwayProjectId = _configuration["Railway:SharedProjectId"];

        if (string.IsNullOrEmpty(railwayApiToken) || string.IsNullOrEmpty(railwayProjectId))
        {
            issues.Add("Railway API token or project ID not configured");
            return (false, issues, details);
        }

        try
        {
            // Use the named HttpClient "DeploymentController" which has SSL certificate validation configured
            using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayApiToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Derive service name from boardId
            var serviceNamePattern = $"webapi-{boardId.ToLowerInvariant()}";
            var sanitizedPattern = System.Text.RegularExpressions.Regex.Replace(serviceNamePattern, @"[^a-z0-9-_]", "-");

            details["ExpectedServiceName"] = sanitizedPattern;

            // Query Railway API for services in the project
            var queryServices = new
            {
                query = @"
                    query GetProjectServices($projectId: String!) {
                        project(id: $projectId) {
                            id
                            services {
                                edges {
                                    node {
                                        id
                                        name
                                    }
                                }
                            }
                        }
                    }",
                variables = new { projectId = railwayProjectId }
            };

            var queryBody = System.Text.Json.JsonSerializer.Serialize(queryServices);
            var queryContent = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json");
            var queryResponse = await httpClient.PostAsync(railwayApiUrl, queryContent);

            if (!queryResponse.IsSuccessStatusCode)
            {
                issues.Add($"Failed to query Railway API: {queryResponse.StatusCode}");
                return (false, issues, details);
            }

            var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
            var queryDoc = System.Text.Json.JsonDocument.Parse(queryResponseContent);

            string? serviceId = null;
            string? serviceName = null;

            if (queryDoc.RootElement.TryGetProperty("data", out var dataObj) &&
                dataObj.TryGetProperty("project", out var projectObj) &&
                projectObj.TryGetProperty("services", out var servicesObj) &&
                servicesObj.TryGetProperty("edges", out var edgesProp))
            {
                var edges = edgesProp.EnumerateArray().ToList();
                foreach (var edge in edges)
                {
                    if (edge.TryGetProperty("node", out var nodeProp))
                    {
                        var name = nodeProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        if (!string.IsNullOrEmpty(name) && 
                            (name.Equals(sanitizedPattern, StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith($"webapi-{boardId}", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith($"webapi_{boardId}", StringComparison.OrdinalIgnoreCase)))
                        {
                            serviceId = nodeProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            serviceName = name;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(serviceId))
            {
                issues.Add($"Railway service not found with name pattern: {sanitizedPattern}");
                details["ServiceExists"] = false;
                return (false, issues, details);
            }

            details["ServiceExists"] = true;
            details["ServiceId"] = serviceId;
            details["ServiceName"] = serviceName;

            // Query service for environment variables and deployment status
            var queryService = new
            {
                query = @"
                    query GetService($serviceId: String!) {
                        service(id: $serviceId) {
                            id
                            name
                            deployments(first: 1) {
                                edges {
                                    node {
                                        id
                                        status
                                        url
                                    }
                                }
                            }
                        }
                    }",
                variables = new { serviceId = serviceId }
            };

            var serviceQueryBody = System.Text.Json.JsonSerializer.Serialize(queryService);
            var serviceQueryContent = new StringContent(serviceQueryBody, System.Text.Encoding.UTF8, "application/json");
            var serviceQueryResponse = await httpClient.PostAsync(railwayApiUrl, serviceQueryContent);

            if (serviceQueryResponse.IsSuccessStatusCode)
            {
                var serviceResponseContent = await serviceQueryResponse.Content.ReadAsStringAsync();
                var serviceDoc = System.Text.Json.JsonDocument.Parse(serviceResponseContent);

                if (serviceDoc.RootElement.TryGetProperty("data", out var serviceDataObj) &&
                    serviceDataObj.TryGetProperty("service", out var serviceObj))
                {
                    // Check deployments
                    if (serviceObj.TryGetProperty("deployments", out var deploymentsObj) &&
                        deploymentsObj.TryGetProperty("edges", out var deploymentEdges))
                    {
                        var deploymentList = deploymentEdges.EnumerateArray().ToList();
                        if (deploymentList.Count > 0 && deploymentList[0].TryGetProperty("node", out var deploymentNode))
                        {
                            var deploymentStatus = deploymentNode.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                            var deploymentUrl = deploymentNode.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

                            details["LatestDeploymentStatus"] = deploymentStatus ?? "UNKNOWN";
                            details["LatestDeploymentUrl"] = deploymentUrl;

                            if (deploymentStatus != "SUCCESS" && deploymentStatus != "ACTIVE")
                            {
                                issues.Add($"Latest deployment status is {deploymentStatus}, expected SUCCESS or ACTIVE");
                            }

                            // Compare with WebApiUrl from ProjectBoards
                            if (!string.IsNullOrEmpty(board.WebApiUrl) && !string.IsNullOrEmpty(deploymentUrl))
                            {
                                var urlMatches = board.WebApiUrl.Equals(deploymentUrl, StringComparison.OrdinalIgnoreCase) ||
                                                 board.WebApiUrl.Contains(deploymentUrl, StringComparison.OrdinalIgnoreCase) ||
                                                 deploymentUrl.Contains(board.WebApiUrl, StringComparison.OrdinalIgnoreCase);
                                
                                details["WebApiUrlMatches"] = urlMatches;
                                details["ProjectBoardsWebApiUrl"] = board.WebApiUrl;
                                
                                if (!urlMatches)
                                {
                                    issues.Add($"Railway service URL ({deploymentUrl}) does not match WebApiUrl in ProjectBoards ({board.WebApiUrl})");
                                }
                            }
                        }
                    }
                }
            }

            // Query environment variables
            var envQuery = new
            {
                query = @"
                    query GetServiceVariables($serviceId: String!) {
                        service(id: $serviceId) {
                            variables {
                                name
                            }
                        }
                    }",
                variables = new { serviceId = serviceId }
            };

            var envQueryBody = System.Text.Json.JsonSerializer.Serialize(envQuery);
            var envQueryContent = new StringContent(envQueryBody, System.Text.Encoding.UTF8, "application/json");
            var envQueryResponse = await httpClient.PostAsync(railwayApiUrl, envQueryContent);

            if (envQueryResponse.IsSuccessStatusCode)
            {
                var envResponseContent = await envQueryResponse.Content.ReadAsStringAsync();
                var envDoc = System.Text.Json.JsonDocument.Parse(envResponseContent);

                var envVars = new Dictionary<string, bool>();
                if (envDoc.RootElement.TryGetProperty("data", out var envDataObj) &&
                    envDataObj.TryGetProperty("service", out var serviceObj) &&
                    serviceObj.TryGetProperty("variables", out var variablesProp))
                {
                    var variables = variablesProp.EnumerateArray().ToList();
                    var varNames = variables.Select(v => v.TryGetProperty("name", out var n) ? n.GetString() : null)
                                           .Where(n => !string.IsNullOrEmpty(n))
                                           .ToList();

                    envVars["DATABASE_URL"] = varNames.Contains("DATABASE_URL");
                    envVars["PORT"] = varNames.Contains("PORT");
                    envVars["RUNTIME_ERROR_ENDPOINT_URL"] = varNames.Contains("RUNTIME_ERROR_ENDPOINT_URL");

                    details["EnvironmentVariables"] = envVars;

                    if (!envVars["DATABASE_URL"])
                    {
                        issues.Add("DATABASE_URL environment variable is not set in Railway service");
                    }
                    if (!envVars["PORT"])
                    {
                        issues.Add("PORT environment variable is not set in Railway service");
                    }
                    if (!envVars["RUNTIME_ERROR_ENDPOINT_URL"])
                    {
                        issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not set in Railway service");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Railway backend for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating Railway service: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateDatabaseBackendAsync(ProjectBoard board, string boardId)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.NeonProjectId) || string.IsNullOrEmpty(board.NeonBranchId) || string.IsNullOrEmpty(board.DBPassword))
        {
            issues.Add("NeonProjectId, NeonBranchId, or DBPassword is missing in ProjectBoards table");
            details["HasNeonProjectId"] = !string.IsNullOrEmpty(board.NeonProjectId);
            details["HasNeonBranchId"] = !string.IsNullOrEmpty(board.NeonBranchId);
            details["HasDBPassword"] = !string.IsNullOrEmpty(board.DBPassword);
            return (false, issues, details);
        }

        try
        {
            var dbName = $"AppDB_{boardId}";
            var neonApiKey = _configuration["Neon:ApiKey"];
            var neonBaseUrl = _configuration["Neon:BaseUrl"];
            var neonDefaultOwnerName = _configuration["Neon:DefaultOwnerName"] ?? "neondb_owner";

            if (string.IsNullOrEmpty(neonApiKey) || string.IsNullOrEmpty(neonBaseUrl))
            {
                issues.Add("Neon API key or base URL not configured");
                return (false, issues, details);
            }

            // Get connection string from Neon API
            var connectionUrl = $"{neonBaseUrl}/projects/{board.NeonProjectId}/connection_uri?database_name={Uri.EscapeDataString(dbName)}&role_name={Uri.EscapeDataString(neonDefaultOwnerName)}&branch_id={Uri.EscapeDataString(board.NeonBranchId)}&pooled=false";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", neonApiKey);

            var connResponse = await httpClient.GetAsync(connectionUrl);
            if (!connResponse.IsSuccessStatusCode)
            {
                issues.Add($"Failed to get database connection string from Neon API: {connResponse.StatusCode}");
                return (false, issues, details);
            }

            var connContent = await connResponse.Content.ReadAsStringAsync();
            var connDoc = System.Text.Json.JsonDocument.Parse(connContent);

            string? connectionString = null;
            if (connDoc.RootElement.TryGetProperty("uri", out var uriProp))
            {
                connectionString = uriProp.GetString();
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                issues.Add("Database connection string is empty or invalid");
                return (false, issues, details);
            }

            details["ConnectionStringRetrieved"] = true;
            details["DatabaseName"] = dbName;

            // Validate connection string format (cold validation - no actual connection)
            // Just verify it's a valid PostgreSQL connection string format
            try
            {
                // First, try to parse as URI (for postgresql:// URLs)
                if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) || 
                    connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    // Try parsing as URI first
                    if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
                    {
                        // URI parsing succeeded - basic format is valid
                        details["ConnectionStringFormatValid"] = true;
                        details["ConnectionStringHost"] = uri.Host;
                        details["ConnectionStringDatabase"] = uri.AbsolutePath.TrimStart('/');
                        details["ConnectionStringUsername"] = uri.UserInfo?.Split(':')[0];
                        
                        // Verify database name matches
                        var dbNameFromUri = uri.AbsolutePath.TrimStart('/');
                        if (!string.IsNullOrEmpty(dbNameFromUri) && dbNameFromUri != dbName)
                        {
                            issues.Add($"Connection string database name ({dbNameFromUri}) does not match expected database name ({dbName})");
                        }
                    }
                    else
                    {
                        // URI parsing failed, try NpgsqlConnectionStringBuilder as fallback
                        var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                        details["ConnectionStringFormatValid"] = true;
                        details["ConnectionStringHost"] = connBuilder.Host;
                        details["ConnectionStringDatabase"] = connBuilder.Database;
                        details["ConnectionStringUsername"] = connBuilder.Username;
                        
                        if (!string.IsNullOrEmpty(connBuilder.Database) && connBuilder.Database != dbName)
                        {
                            issues.Add($"Connection string database name ({connBuilder.Database}) does not match expected database name ({dbName})");
                        }
                    }
                }
                else
                {
                    // Not a URL format, try NpgsqlConnectionStringBuilder
                    var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                    details["ConnectionStringFormatValid"] = true;
                    details["ConnectionStringHost"] = connBuilder.Host;
                    details["ConnectionStringDatabase"] = connBuilder.Database;
                    details["ConnectionStringUsername"] = connBuilder.Username;
                    
                    if (!string.IsNullOrEmpty(connBuilder.Database) && connBuilder.Database != dbName)
                    {
                        issues.Add($"Connection string database name ({connBuilder.Database}) does not match expected database name ({dbName})");
                    }
                }
            }
            catch (Exception ex)
            {
                // If parsing fails, check if it's a known issue (like unsupported query parameters)
                // Neon connection strings may have query parameters that Npgsql doesn't recognize
                // but the connection string itself is still valid
                if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) || 
                    connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    // For PostgreSQL URLs, if URI parsing works, consider it valid even if Npgsql fails
                    if (Uri.TryCreate(connectionString, UriKind.Absolute, out _))
                    {
                        details["ConnectionStringFormatValid"] = true;
                        details["ConnectionStringWarning"] = "Connection string parsed as URI but Npgsql parsing failed (may have unsupported query parameters)";
                    }
                    else
                    {
                        issues.Add($"Database connection string format is invalid: {ex.Message}");
                        details["ConnectionStringFormatValid"] = false;
                        details["ConnectionStringError"] = ex.Message;
                    }
                }
                else
                {
                    issues.Add($"Database connection string format is invalid: {ex.Message}");
                    details["ConnectionStringFormatValid"] = false;
                    details["ConnectionStringError"] = ex.Message;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating database backend for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating database: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateCodeStructureBackendAsync(ProjectBoard board, string boardId, string branch = "main")
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.GithubBackendUrl))
        {
            issues.Add("GithubBackendUrl is not set - cannot validate code structure");
            return (false, issues, details);
        }

        try
        {
            // Parse GitHub URL
            var uri = new Uri(board.GithubBackendUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                issues.Add("Invalid GitHub backend URL format");
                return (false, issues, details);
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");
            var accessToken = _configuration["GitHub:AccessToken"];

            if (string.IsNullOrEmpty(accessToken))
            {
                issues.Add("GitHub access token not configured");
                return (false, issues, details);
            }

            // Check for nixpacks.toml at root
            var nixpacksContent = await _githubService.GetFileContentAsync(owner, repo, "nixpacks.toml", accessToken, branch);
            details["NixpacksTomlExists"] = !string.IsNullOrEmpty(nixpacksContent);
            
            // Pre-load Program.cs to check if it's C# (needed for both nixpacks check and later validation)
            var programCs = await _githubService.GetFileContentAsync(owner, repo, "backend/Program.cs", accessToken, branch) ??
                           await _githubService.GetFileContentAsync(owner, repo, "Program.cs", accessToken, branch);
            var isCSharp = !string.IsNullOrEmpty(programCs);
            
            if (string.IsNullOrEmpty(nixpacksContent))
            {
                issues.Add("nixpacks.toml file not found at repository root");
            }
            else
            {
                // Check if this is a Node.js project (PORT binding is handled in app.js, not nixpacks.toml)
                var appJs = await _githubService.GetFileContentAsync(owner, repo, "backend/app.js", accessToken, branch) ??
                           await _githubService.GetFileContentAsync(owner, repo, "app.js", accessToken, branch);
                var isNodeJs = !string.IsNullOrEmpty(appJs);
                
                // Check if this is a Java project (PORT binding is handled in application.properties/yml, not nixpacks.toml)
                var applicationJava = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/Application.java", accessToken, branch) ??
                                     await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/Application.java", accessToken, branch);
                var pomXml = await _githubService.GetFileContentAsync(owner, repo, "backend/pom.xml", accessToken, branch) ??
                            await _githubService.GetFileContentAsync(owner, repo, "pom.xml", accessToken, branch);
                var isJava = !string.IsNullOrEmpty(applicationJava) || !string.IsNullOrEmpty(pomXml);
                
                // Check if this is a Go project (PORT binding is handled in main.go, not nixpacks.toml)
                var mainGo = await _githubService.GetFileContentAsync(owner, repo, "backend/main.go", accessToken, branch) ??
                            await _githubService.GetFileContentAsync(owner, repo, "main.go", accessToken, branch);
                var isGo = !string.IsNullOrEmpty(mainGo);
                
                // Check if this is a Ruby project (PORT binding is handled in app.rb/config.ru, not nixpacks.toml)
                var appRb = await _githubService.GetFileContentAsync(owner, repo, "backend/app.rb", accessToken, branch) ??
                           await _githubService.GetFileContentAsync(owner, repo, "app.rb", accessToken, branch);
                var configRu = await _githubService.GetFileContentAsync(owner, repo, "backend/config.ru", accessToken, branch) ??
                              await _githubService.GetFileContentAsync(owner, repo, "config.ru", accessToken, branch);
                var isRuby = !string.IsNullOrEmpty(appRb) || !string.IsNullOrEmpty(configRu);
                
                // For Node.js, PORT binding is handled in app.js, so skip nixpacks.toml PORT check
                // For Java, PORT binding is handled in application.properties/yml, so skip nixpacks.toml PORT check
                // For C#, PORT binding is handled in Program.cs, so skip nixpacks.toml PORT check
                // For Go, PORT binding is handled in main.go, so skip nixpacks.toml PORT check
                // For Ruby, PORT binding is handled in app.rb/config.ru, so skip nixpacks.toml PORT check
                // For other languages (Python, etc.), PORT should be in nixpacks.toml
                if (!isNodeJs && !isJava && !isCSharp && !isGo && !isRuby)
                {
                    var hasPortBinding = nixpacksContent.Contains("0.0.0.0") || nixpacksContent.Contains("$PORT");
                    details["NixpacksHasPortBinding"] = hasPortBinding;
                    if (!hasPortBinding)
                    {
                        issues.Add("nixpacks.toml does not bind to 0.0.0.0 or use PORT environment variable");
                    }
                }
                else
                {
                    details["NixpacksHasPortBinding"] = true; // Node.js/Java/C#/Go/Ruby handle PORT in their own config files
                }
            }

            // Check for middleware registration (language-specific)
            // This is a simplified check - we'd need to know the programming language
            // For now, check common files
            // programCs is already loaded above
            
            if (!string.IsNullOrEmpty(programCs))
            {
                // C# backend
                var hasMiddleware = programCs.Contains("UseMiddleware") && programCs.Contains("GlobalExceptionHandlerMiddleware");
                var hasCors = programCs.Contains("UseCors") && programCs.Contains("AllowAll");
                var hasPortBinding = programCs.Contains("UseUrls") && (programCs.Contains("0.0.0.0") || programCs.Contains("$PORT") || programCs.Contains("PORT"));
                
                // Check for RUNTIME_ERROR_ENDPOINT_URL in middleware file (not just Program.cs)
                var middlewareFile = await _githubService.GetFileContentAsync(owner, repo, "backend/Middleware/GlobalExceptionHandlerMiddleware.cs", accessToken, branch) ??
                                    await _githubService.GetFileContentAsync(owner, repo, "Middleware/GlobalExceptionHandlerMiddleware.cs", accessToken, branch);
                var hasRuntimeErrorEndpoint = programCs.Contains("RUNTIME_ERROR_ENDPOINT_URL") || 
                                            (programCs.Contains("Environment.GetEnvironmentVariable") && programCs.Contains("RUNTIME_ERROR_ENDPOINT_URL")) ||
                                            (!string.IsNullOrEmpty(middlewareFile) && middlewareFile.Contains("RUNTIME_ERROR_ENDPOINT_URL"));
                
                details["MiddlewareRegistered"] = hasMiddleware;
                details["CorsConfigured"] = hasCors;
                details["PortBindingConfigured"] = hasPortBinding;
                details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                
                if (!hasMiddleware)
                {
                    issues.Add("GlobalExceptionHandlerMiddleware is not registered in Program.cs");
                }
                if (!hasCors)
                {
                    issues.Add("CORS is not configured in Program.cs");
                }
                if (!hasPortBinding)
                {
                    issues.Add("Port binding to 0.0.0.0 is not configured in Program.cs (required for Railway)");
                }
                if (!hasRuntimeErrorEndpoint)
                {
                    issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in middleware code");
                }

                // Check package dependencies
                var csproj = await _githubService.GetFileContentAsync(owner, repo, "backend/Backend.csproj", accessToken, branch) ??
                            await _githubService.GetFileContentAsync(owner, repo, "Backend.csproj", accessToken, branch);
                
                if (!string.IsNullOrEmpty(csproj))
                {
                    var hasNpgsql = csproj.Contains("Npgsql");
                    details["HasNpgsqlPackage"] = hasNpgsql;
                    if (!hasNpgsql)
                    {
                        issues.Add("Npgsql package not found in .csproj file (required for PostgreSQL)");
                    }
                }
            }
            else
            {
                // Check for Python main.py
                var mainPy = await _githubService.GetFileContentAsync(owner, repo, "backend/main.py", accessToken, branch) ??
                            await _githubService.GetFileContentAsync(owner, repo, "main.py", accessToken, branch);
                
                if (!string.IsNullOrEmpty(mainPy))
                {
                        var hasExceptionHandler = mainPy.Contains("setup_exception_handlers") || mainPy.Contains("ExceptionHandler") ||
                                                 (mainPy.Contains("from ExceptionHandler") || mainPy.Contains("import ExceptionHandler"));
                    var hasCors = mainPy.Contains("CORSMiddleware") || mainPy.Contains("allow_origins");
                    var hasPortBinding = mainPy.Contains("0.0.0.0") || mainPy.Contains("$PORT") || mainPy.Contains("PORT");
                    var hasRuntimeErrorEndpoint = mainPy.Contains("RUNTIME_ERROR_ENDPOINT_URL") || (mainPy.Contains("os.getenv") && mainPy.Contains("RUNTIME_ERROR_ENDPOINT_URL"));
                    
                    details["ExceptionHandlerRegistered"] = hasExceptionHandler;
                    details["CorsConfigured"] = hasCors;
                    details["PortBindingConfigured"] = hasPortBinding;
                    details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                    
                    if (!hasExceptionHandler)
                    {
                        issues.Add("Exception handler is not registered in main.py");
                    }
                    if (!hasCors)
                    {
                        issues.Add("CORS is not configured in main.py");
                    }
                    if (!hasPortBinding)
                    {
                        issues.Add("Port binding to 0.0.0.0 is not configured in main.py (required for Railway)");
                    }
                    if (!hasRuntimeErrorEndpoint)
                    {
                        issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in exception handler code");
                    }

                    // Check for SET search_path in database queries

                    // Check package dependencies
                    var requirementsTxt = await _githubService.GetFileContentAsync(owner, repo, "backend/requirements.txt", accessToken, branch) ??
                                         await _githubService.GetFileContentAsync(owner, repo, "requirements.txt", accessToken, branch);
                    
                    if (!string.IsNullOrEmpty(requirementsTxt))
                    {
                        var hasFastApi = requirementsTxt.Contains("fastapi");
                        var hasUvicorn = requirementsTxt.Contains("uvicorn");
                        // Python uses psycopg (psycopg3), not asyncpg
                        var hasPsycopg = requirementsTxt.Contains("psycopg");
                        
                        details["HasFastApi"] = hasFastApi;
                        details["HasUvicorn"] = hasUvicorn;
                        details["HasPsycopg"] = hasPsycopg;
                        
                        if (!hasFastApi)
                        {
                            issues.Add("fastapi package not found in requirements.txt");
                        }
                        if (!hasUvicorn)
                        {
                            issues.Add("uvicorn package not found in requirements.txt");
                        }
                        if (!hasPsycopg)
                        {
                            issues.Add("psycopg package not found in requirements.txt (required for PostgreSQL)");
                        }
                    }
                }
                else
                {
                    // Check for Node.js app.js
                    var appJs = await _githubService.GetFileContentAsync(owner, repo, "backend/app.js", accessToken, branch) ??
                               await _githubService.GetFileContentAsync(owner, repo, "app.js", accessToken, branch);
                    
                    if (!string.IsNullOrEmpty(appJs))
                    {
                        // Check for error middleware - actual pattern is app.use((err, req, res, next) => {
                        var hasErrorMiddleware = appJs.Contains("app.use((err,") || appJs.Contains("app.use((error,") || 
                                                 appJs.Contains("ERROR HANDLER") || appJs.Contains("error-handler") ||
                                                 appJs.Contains("errorMiddleware");
                        var hasCors = appJs.Contains("cors()") || appJs.Contains("CORS");
                        var hasPortBinding = appJs.Contains("listen") && (appJs.Contains("0.0.0.0") || appJs.Contains("process.env.PORT"));
                        var hasRuntimeErrorEndpoint = appJs.Contains("RUNTIME_ERROR_ENDPOINT_URL") || (appJs.Contains("process.env") && appJs.Contains("RUNTIME_ERROR_ENDPOINT_URL"));
                        
                        details["ErrorMiddlewareRegistered"] = hasErrorMiddleware;
                        details["CorsConfigured"] = hasCors;
                        details["PortBindingConfigured"] = hasPortBinding;
                        details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                        
                        if (!hasErrorMiddleware)
                        {
                            issues.Add("Error middleware is not registered in app.js");
                        }
                        if (!hasCors)
                        {
                            issues.Add("CORS is not configured in app.js");
                        }
                        if (!hasPortBinding)
                        {
                            issues.Add("Port binding to 0.0.0.0 is not configured in app.js (required for Railway)");
                        }
                        if (!hasRuntimeErrorEndpoint)
                        {
                            issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in error middleware code");
                        }


                        // Check package dependencies
                        var packageJson = await _githubService.GetFileContentAsync(owner, repo, "backend/package.json", accessToken, branch) ??
                                         await _githubService.GetFileContentAsync(owner, repo, "package.json", accessToken, branch);
                        
                        if (!string.IsNullOrEmpty(packageJson))
                        {
                            var hasExpress = packageJson.Contains("\"express\"") || packageJson.Contains("'express'");
                            var hasPg = packageJson.Contains("\"pg\"") || packageJson.Contains("'pg'");
                            var hasCorsPackage = packageJson.Contains("\"cors\"") || packageJson.Contains("'cors'");
                            
                            details["HasExpress"] = hasExpress;
                            details["HasPg"] = hasPg;
                            details["HasCorsPackage"] = hasCorsPackage;
                            
                            if (!hasExpress)
                            {
                                issues.Add("express package not found in package.json");
                            }
                            if (!hasPg)
                            {
                                issues.Add("pg package not found in package.json (required for PostgreSQL)");
                            }
                            if (!hasCorsPackage)
                            {
                                issues.Add("cors package not found in package.json");
                            }
                        }
                    }
                    else
                    {
                        // Check for Java Application.java
                        var applicationJava = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/Application.java", accessToken, branch) ??
                                            await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/Application.java", accessToken, branch);
                        
                        if (!string.IsNullOrEmpty(applicationJava))
                        {
                        // Java/Spring Boot backend - check application.properties or application.yml for port and database config
                        var applicationProperties = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/resources/application.properties", accessToken, branch) ??
                                                   await _githubService.GetFileContentAsync(owner, repo, "src/main/resources/application.properties", accessToken, branch);
                        var applicationYml = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/resources/application.yml", accessToken, branch) ??
                                            await _githubService.GetFileContentAsync(owner, repo, "src/main/resources/application.yml", accessToken, branch);
                        
                        var configFile = applicationProperties ?? applicationYml ?? "";
                        
                        // Check for exception handler (Spring Boot @ControllerAdvice or @ExceptionHandler) - check multiple locations including Exception subpackage
                        var exceptionHandler = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/Exception/GlobalExceptionHandler.java", accessToken, branch) ??
                                              await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/Exception/GlobalExceptionHandler.java", accessToken, branch) ??
                                              await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/GlobalExceptionHandler.java", accessToken, branch) ??
                                              await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/GlobalExceptionHandler.java", accessToken, branch) ??
                                              await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/GlobalExceptionHandler.java", accessToken, branch) ??
                                              await _githubService.GetFileContentAsync(owner, repo, "src/main/java/GlobalExceptionHandler.java", accessToken, branch);
                        
                        // Also check for exception handler in any Java file in the backend package
                        var hasExceptionHandler = !string.IsNullOrEmpty(exceptionHandler) && 
                                                 (exceptionHandler.Contains("@ControllerAdvice") || exceptionHandler.Contains("@ExceptionHandler"));
                        
                        // If not found in expected location, check if Application.java has exception handling
                        if (!hasExceptionHandler)
                        {
                            hasExceptionHandler = applicationJava.Contains("@ExceptionHandler") || 
                                                 applicationJava.Contains("@ControllerAdvice") ||
                                                 applicationJava.Contains("ExceptionHandler");
                        }
                        
                        var hasPortBinding = configFile.Contains("server.port") || configFile.Contains("PORT") || 
                                           applicationJava.Contains("System.getenv(\"PORT\")") ||
                                           configFile.Contains("${PORT}") ||
                                           configFile.Contains("SERVER_PORT");
                        var hasRuntimeErrorEndpoint = applicationJava.Contains("RUNTIME_ERROR_ENDPOINT_URL") || 
                                                     configFile.Contains("RUNTIME_ERROR_ENDPOINT_URL") ||
                                                     (!string.IsNullOrEmpty(exceptionHandler) && exceptionHandler.Contains("RUNTIME_ERROR_ENDPOINT_URL"));
                        
                        details["PortBindingConfigured"] = hasPortBinding;
                        details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                        details["ExceptionHandlerRegistered"] = hasExceptionHandler;
                        
                        if (!hasPortBinding)
                        {
                            issues.Add("Port binding configuration not found in Application.java or application.properties/yml (required for Railway)");
                        }
                        if (!hasRuntimeErrorEndpoint)
                        {
                            issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in GlobalExceptionHandler.java");
                        }
                        if (!hasExceptionHandler)
                        {
                            issues.Add("Global exception handler is not registered (GlobalExceptionHandler.java with @ControllerAdvice)");
                        }

                        // Check for CORS configuration - check multiple locations and patterns
                        // CORS can be configured in CorsConfig.java (actual generated file), WebConfig, Application.java, or application.properties/yml
                        var corsConfig = await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/Config/CorsConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/Config/CorsConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/WebConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/WebConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/com/backend/Config/WebConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "src/main/java/com/backend/Config/WebConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "backend/src/main/java/WebConfig.java", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "src/main/java/WebConfig.java", accessToken, branch);
                        
                        var hasCors = (!string.IsNullOrEmpty(corsConfig) && (corsConfig.Contains("CorsConfiguration") || corsConfig.Contains("addCorsMappings") || corsConfig.Contains("CorsRegistry") || corsConfig.Contains("CorsConfig"))) ||
                                     applicationJava.Contains("CorsConfiguration") || 
                                     applicationJava.Contains("addCorsMappings") ||
                                     applicationJava.Contains("CorsRegistry") ||
                                     applicationJava.Contains("@CrossOrigin") ||
                                     configFile.Contains("cors") ||
                                     configFile.Contains("CORS") ||
                                     configFile.Contains("spring.web.cors");
                        details["CorsConfigured"] = hasCors;
                        if (!hasCors)
                        {
                            issues.Add("CORS is not configured in Java backend");
                        }


                        // Check package dependencies (pom.xml)
                        var pomXml = await _githubService.GetFileContentAsync(owner, repo, "backend/pom.xml", accessToken, branch) ??
                                    await _githubService.GetFileContentAsync(owner, repo, "pom.xml", accessToken, branch);
                        
                        if (!string.IsNullOrEmpty(pomXml))
                        {
                            var hasPostgresql = pomXml.Contains("postgresql");
                            var hasSpringBoot = pomXml.Contains("spring-boot-starter-web");
                            
                            details["HasPostgresql"] = hasPostgresql;
                            details["HasSpringBoot"] = hasSpringBoot;
                            
                            if (!hasPostgresql)
                            {
                                issues.Add("PostgreSQL driver not found in pom.xml (required for PostgreSQL)");
                            }
                            if (!hasSpringBoot)
                            {
                                issues.Add("Spring Boot starter not found in pom.xml");
                            }
                        }
                    }
                    else
                    {
                        // Check for Ruby app.rb
                        var appRb = await _githubService.GetFileContentAsync(owner, repo, "backend/app.rb", accessToken, branch) ??
                                   await _githubService.GetFileContentAsync(owner, repo, "app.rb", accessToken, branch);
                        
                        if (!string.IsNullOrEmpty(appRb))
                        {
                            // Ruby/Sinatra backend
                            // Check puma.rb for port binding (Ruby uses Puma server)
                            var pumaRb = await _githubService.GetFileContentAsync(owner, repo, "backend/puma.rb", accessToken, branch) ??
                                        await _githubService.GetFileContentAsync(owner, repo, "puma.rb", accessToken, branch);
                            
                            var hasPortBinding = (!string.IsNullOrEmpty(pumaRb) && (pumaRb.Contains("0.0.0.0") || pumaRb.Contains("bind"))) ||
                                                appRb.Contains("0.0.0.0") || appRb.Contains("ENV['PORT']") || appRb.Contains("ENV.fetch('PORT'") ||
                                                appRb.Contains("set :bind, '0.0.0.0'");
                            var hasRuntimeErrorEndpoint = appRb.Contains("RUNTIME_ERROR_ENDPOINT_URL") || appRb.Contains("ENV['RUNTIME_ERROR_ENDPOINT_URL']");
                            
                            details["PortBindingConfigured"] = hasPortBinding;
                            details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                            
                            if (!hasPortBinding)
                            {
                                issues.Add("Port binding to 0.0.0.0 is not configured in app.rb or puma.rb (required for Railway)");
                            }
                            if (!hasRuntimeErrorEndpoint)
                            {
                                issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in exception handler code");
                            }

                            // Check for exception middleware
                            var hasExceptionMiddleware = appRb.Contains("error do") || (appRb.Contains("extract_board_id") && appRb.Contains("send_error_to_endpoint")) ||
                                                       appRb.Contains("exception") || appRb.Contains("rescue");
                            details["ExceptionHandlerRegistered"] = hasExceptionMiddleware;
                            if (!hasExceptionMiddleware)
                            {
                                issues.Add("Exception handling is not configured in app.rb");
                            }

                            // Check for CORS headers
                            var hasCors = appRb.Contains("Access-Control-Allow-Origin") || appRb.Contains("CORS") || appRb.Contains("before do");
                            details["CorsConfigured"] = hasCors;
                            if (!hasCors)
                            {
                                issues.Add("CORS headers are not configured in app.rb");
                            }


                            // Check package dependencies (Gemfile)
                            var gemfile = await _githubService.GetFileContentAsync(owner, repo, "backend/Gemfile", accessToken, branch) ??
                                         await _githubService.GetFileContentAsync(owner, repo, "Gemfile", accessToken, branch);
                            
                            if (!string.IsNullOrEmpty(gemfile))
                            {
                                var hasSinatra = gemfile.Contains("sinatra");
                                var hasPg = gemfile.Contains("pg");
                                
                                details["HasSinatra"] = hasSinatra;
                                details["HasPg"] = hasPg;
                                
                                if (!hasSinatra)
                                {
                                    issues.Add("sinatra gem not found in Gemfile");
                                }
                                if (!hasPg)
                                {
                                    issues.Add("pg gem not found in Gemfile (required for PostgreSQL)");
                                }
                            }
                        }
                        else
                        {
                            // Check for Go main.go
                            var mainGo = await _githubService.GetFileContentAsync(owner, repo, "backend/main.go", accessToken, branch) ??
                                       await _githubService.GetFileContentAsync(owner, repo, "main.go", accessToken, branch);
                            
                            if (!string.IsNullOrEmpty(mainGo))
                            {
                                // Go backend
                                var hasPortBinding = mainGo.Contains("0.0.0.0") || mainGo.Contains("PORT") || mainGo.Contains("Listen");
                                var hasRuntimeErrorEndpoint = mainGo.Contains("RUNTIME_ERROR_ENDPOINT_URL") || mainGo.Contains("os.Getenv(\"RUNTIME_ERROR_ENDPOINT_URL\")");
                                
                                details["PortBindingConfigured"] = hasPortBinding;
                                details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                                
                                if (!hasPortBinding)
                                {
                                    issues.Add("Port binding to 0.0.0.0 is not configured in main.go (required for Railway)");
                                }
                                if (!hasRuntimeErrorEndpoint)
                                {
                                    issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in error handler code");
                                }

                                // Check for error middleware/handler
                                var hasErrorHandler = mainGo.Contains("panicRecoveryMiddleware") || 
                                                   (mainGo.Contains("recover") && mainGo.Contains("PANIC RECOVERY")) ||
                                                   mainGo.Contains("Error") || mainGo.Contains("error");
                                details["ErrorHandlerRegistered"] = hasErrorHandler;
                                if (!hasErrorHandler)
                                {
                                    issues.Add("Error handler/middleware is not configured in main.go");
                                }

                                // Check for CORS middleware
                                var hasCors = mainGo.Contains("CORS") || mainGo.Contains("cors") || mainGo.Contains("Access-Control");
                                details["CorsConfigured"] = hasCors;
                                if (!hasCors)
                                {
                                    issues.Add("CORS middleware is not configured in main.go");
                                }


                                // Check package dependencies (go.mod)
                                var goMod = await _githubService.GetFileContentAsync(owner, repo, "backend/go.mod", accessToken, branch) ??
                                          await _githubService.GetFileContentAsync(owner, repo, "go.mod", accessToken, branch);
                                
                                if (!string.IsNullOrEmpty(goMod))
                                {
                                    var hasLibPq = goMod.Contains("lib/pq");
                                    
                                    details["HasLibPq"] = hasLibPq;
                                    
                                    if (!hasLibPq)
                                    {
                                        issues.Add("github.com/lib/pq package not found in go.mod (required for PostgreSQL)");
                                    }
                                }
                            }
                            else
                            {
                                // Check for PHP index.php
                                var indexPhp = await _githubService.GetFileContentAsync(owner, repo, "backend/index.php", accessToken, branch) ??
                                            await _githubService.GetFileContentAsync(owner, repo, "index.php", accessToken, branch);
                                
                                if (!string.IsNullOrEmpty(indexPhp))
                                {
                                    // PHP backend
                                    var hasExceptionHandler = indexPhp.Contains("set_exception_handler") || indexPhp.Contains("set_error_handler");
                                    // PHP with FrankenPHP/Caddy doesn't need PORT in index.php - the server handles it
                                    // So we skip this check for PHP
                                    var hasPortBinding = true; // Always true for PHP as FrankenPHP handles PORT binding
                                    var hasRuntimeErrorEndpoint = indexPhp.Contains("RUNTIME_ERROR_ENDPOINT_URL") || indexPhp.Contains("$_ENV['RUNTIME_ERROR_ENDPOINT_URL']") || indexPhp.Contains("getenv('RUNTIME_ERROR_ENDPOINT_URL')");
                                    
                                    details["ExceptionHandlerRegistered"] = hasExceptionHandler;
                                    details["PortBindingConfigured"] = hasPortBinding;
                                    details["RuntimeErrorEndpointReferenced"] = hasRuntimeErrorEndpoint;
                                    
                                    if (!hasExceptionHandler)
                                    {
                                        issues.Add("Exception handler (set_exception_handler/set_error_handler) is not configured in index.php");
                                    }
                                    
                                    // Skip PORT binding check for PHP - FrankenPHP handles this
                                    // if (!hasPortBinding)
                                    // {
                                    //     issues.Add("Port binding to 0.0.0.0 is not configured in index.php (required for Railway)");
                                    // }
                                    
                                    if (!hasRuntimeErrorEndpoint)
                                    {
                                        issues.Add("RUNTIME_ERROR_ENDPOINT_URL environment variable is not referenced in error handler code");
                                    }

                                    // Check for CORS headers
                                    var hasCors = indexPhp.Contains("Access-Control-Allow-Origin") || indexPhp.Contains("header('Access-Control") || indexPhp.Contains("CORS");
                                    details["CorsConfigured"] = hasCors;
                                    if (!hasCors)
                                    {
                                        issues.Add("CORS headers are not configured in index.php");
                                    }

                                    // Check package dependencies (composer.json)
                                    var composerJson = await _githubService.GetFileContentAsync(owner, repo, "backend/composer.json", accessToken, branch) ??
                                                     await _githubService.GetFileContentAsync(owner, repo, "composer.json", accessToken, branch);
                                    
                                    if (!string.IsNullOrEmpty(composerJson))
                                    {
                                        var hasPdoPg = composerJson.Contains("pdo_pgsql") || composerJson.Contains("pgsql");
                                        
                                        details["HasPdoPg"] = hasPdoPg;
                                        
                                        if (!hasPdoPg)
                                        {
                                            issues.Add("PostgreSQL PDO driver not found in composer.json (required for PostgreSQL)");
                                        }
                                    }
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
            _logger.LogError(ex, "Error validating code structure for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating code structure: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateGitHubFrontendAsync(ProjectBoard board, string boardId, string branch = "main")
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.GithubFrontendUrl))
        {
            issues.Add("GithubFrontendUrl is not set in ProjectBoards table");
            return (false, issues, details);
        }

        try
        {
            // Parse GitHub URL
            var uri = new Uri(board.GithubFrontendUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                issues.Add($"Invalid GitHub frontend URL format: {board.GithubFrontendUrl}");
                return (false, issues, details);
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");

            details["Owner"] = owner;
            details["Repository"] = repo;
            details["Url"] = board.GithubFrontendUrl;

            // Check if repository exists via GitHub API
            var accessToken = _configuration["GitHub:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                issues.Add("GitHub access token not configured");
                return (false, issues, details);
            }

            // Use the named HttpClient "DeploymentController" which has SSL certificate validation configured for GitHub
            using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var repoResponse = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}");
            if (!repoResponse.IsSuccessStatusCode)
            {
                issues.Add($"GitHub repository not found or not accessible: {board.GithubFrontendUrl} (Status: {repoResponse.StatusCode})");
                details["RepositoryExists"] = false;
                return (false, issues, details);
            }

            details["RepositoryExists"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating GitHub frontend for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating GitHub repository: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateFrontendConfigAsync(ProjectBoard board, string boardId, string branch = "main")
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.GithubFrontendUrl))
        {
            issues.Add("GithubFrontendUrl is not set - cannot validate config.js");
            return (false, issues, details);
        }

        try
        {
            // Parse GitHub URL
            var uri = new Uri(board.GithubFrontendUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                issues.Add("Invalid GitHub frontend URL format");
                return (false, issues, details);
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");
            var accessToken = _configuration["GitHub:AccessToken"];

            if (string.IsNullOrEmpty(accessToken))
            {
                issues.Add("GitHub access token not configured");
                return (false, issues, details);
            }

            // Check for config.js
            var configJs = await _githubService.GetFileContentAsync(owner, repo, "config.js", accessToken, branch) ??
                          await _githubService.GetFileContentAsync(owner, repo, "frontend/config.js", accessToken, branch);

            details["ConfigJsExists"] = !string.IsNullOrEmpty(configJs);
            if (string.IsNullOrEmpty(configJs))
            {
                issues.Add("config.js file not found in frontend repository");
                return (false, issues, details);
            }

            // Check if config.js is included in index.html
            var indexHtml = await _githubService.GetFileContentAsync(owner, repo, "index.html", accessToken, branch) ??
                           await _githubService.GetFileContentAsync(owner, repo, "frontend/index.html", accessToken, branch);
            
            if (!string.IsNullOrEmpty(indexHtml))
            {
                // Check for static script tag with config.js (e.g., <script src="config.js">)
                var staticScriptTag = System.Text.RegularExpressions.Regex.IsMatch(indexHtml, 
                    @"<script[^>]*src\s*=\s*[""'][^""']*config\.js[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                // Check for dynamic script loading (e.g., configScript.src = 'config.js' or .src = "config.js")
                // This pattern matches JavaScript code that sets the src property to config.js
                var dynamicScriptLoading = System.Text.RegularExpressions.Regex.IsMatch(indexHtml, 
                    @"\.src\s*=\s*[""']config\.js[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                var configJsIncluded = staticScriptTag || dynamicScriptLoading;
                
                details["ConfigJsIncludedInIndexHtml"] = configJsIncluded;
                details["StaticScriptTag"] = staticScriptTag;
                details["DynamicScriptLoading"] = dynamicScriptLoading;
                
                if (!configJsIncluded)
                {
                    issues.Add("config.js is not included/referenced in index.html (required for CONFIG object to be available)");
                }
            }
            else
            {
                details["ConfigJsIncludedInIndexHtml"] = false;
                issues.Add("index.html file not found - cannot verify if config.js is included");
            }

            // Validate config.js content
            var hasApiUrl = configJs.Contains("API_URL") || configJs.Contains("apiUrl");
            details["HasApiUrl"] = hasApiUrl;

            if (!hasApiUrl)
            {
                issues.Add("config.js does not contain API_URL configuration");
            }
            else
            {
                // Extract API_URL value
                var apiUrlMatch = System.Text.RegularExpressions.Regex.Match(
                    configJs,
                    @"API_URL\s*:\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (apiUrlMatch.Success)
                {
                    var apiUrl = apiUrlMatch.Groups[1].Value;
                    details["ApiUrl"] = apiUrl;

                    // Validate API URL
                    var isHttps = apiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                    var isNotProjectUrl = !apiUrl.Contains("railway.app/project/");
                    var matchesWebApiUrl = !string.IsNullOrEmpty(board.WebApiUrl) &&
                                          (apiUrl.Equals(board.WebApiUrl, StringComparison.OrdinalIgnoreCase) ||
                                           apiUrl.Contains(board.WebApiUrl, StringComparison.OrdinalIgnoreCase) ||
                                           board.WebApiUrl.Contains(apiUrl, StringComparison.OrdinalIgnoreCase));

                    details["ApiUrlIsHttps"] = isHttps;
                    details["ApiUrlNotProjectUrl"] = isNotProjectUrl;
                    details["ApiUrlMatchesWebApiUrl"] = matchesWebApiUrl;
                    details["ProjectBoardsWebApiUrl"] = board.WebApiUrl;

                    if (!isHttps)
                    {
                        issues.Add($"API_URL in config.js is not HTTPS: {apiUrl}");
                    }
                    if (!isNotProjectUrl)
                    {
                        issues.Add($"API_URL in config.js is a Railway project URL (not a service URL): {apiUrl}");
                    }
                    if (!matchesWebApiUrl && !string.IsNullOrEmpty(board.WebApiUrl))
                    {
                        issues.Add($"API_URL in config.js ({apiUrl}) does not match WebApiUrl in ProjectBoards ({board.WebApiUrl})");
                    }
                }
                else
                {
                    issues.Add("Could not extract API_URL value from config.js");
                }
            }

            // Check if frontend code uses CONFIG.API_URL (not hardcoded URLs)
            // Note: indexHtml was already loaded above, reuse it
            if (string.IsNullOrEmpty(indexHtml))
            {
                indexHtml = await _githubService.GetFileContentAsync(owner, repo, "index.html", accessToken, branch) ??
                           await _githubService.GetFileContentAsync(owner, repo, "frontend/index.html", accessToken, branch);
            }
            var scriptJs = await _githubService.GetFileContentAsync(owner, repo, "script.js", accessToken, branch) ??
                          await _githubService.GetFileContentAsync(owner, repo, "frontend/script.js", accessToken, branch) ??
                          await _githubService.GetFileContentAsync(owner, repo, "js/script.js", accessToken, branch);
            
            // Check if script.js is referenced in index.html but doesn't exist
            if (!string.IsNullOrEmpty(indexHtml))
            {
                // Check for script tag referencing script.js (various patterns)
                var scriptJsReferenced = System.Text.RegularExpressions.Regex.IsMatch(indexHtml, 
                    @"<script[^>]*src\s*=\s*[""'][^""']*script\.js[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                    System.Text.RegularExpressions.Regex.IsMatch(indexHtml, 
                    @"\.src\s*=\s*[""']script\.js[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                details["ScriptJsReferencedInIndexHtml"] = scriptJsReferenced;
                details["ScriptJsExists"] = !string.IsNullOrEmpty(scriptJs);
                
                _logger.LogInformation("🔍 [VALIDATION] script.js check - Referenced: {Referenced}, Exists: {Exists}", 
                    scriptJsReferenced, !string.IsNullOrEmpty(scriptJs));
                
                if (scriptJsReferenced && string.IsNullOrEmpty(scriptJs))
                {
                    var issue = "script.js is referenced in index.html but the file does not exist in the repository";
                    issues.Add(issue);
                    _logger.LogWarning("❌ [VALIDATION] {Issue}", issue);
                }
            }
            else
            {
                _logger.LogInformation("🔍 [VALIDATION] script.js check skipped - index.html is empty or not found");
            }
            
            // Check if frontend code (excluding config.js) uses CONFIG.API_URL
            // config.js is expected to have the hardcoded URL, so we only check index.html and script.js
            var frontendCode = (indexHtml ?? "") + (scriptJs ?? "");
            if (!string.IsNullOrEmpty(frontendCode))
            {
                var usesConfigApiUrl = frontendCode.Contains("CONFIG.API_URL") || frontendCode.Contains("CONFIG['API_URL']") || 
                                      frontendCode.Contains("config.API_URL") || frontendCode.Contains("config['API_URL']");
                
                // Check for hardcoded URLs ONLY in actual API calls (fetch, XMLHttpRequest, $.ajax)
                // This excludes URLs in script tags, links, or config.js references
                var hasHardcodedApiCalls = System.Text.RegularExpressions.Regex.IsMatch(frontendCode, 
                    @"(fetch|XMLHttpRequest|\.ajax|axios)\s*\(\s*[""'](https?://[^\s""']+\.railway\.app|https?://[^\s""']+\.github\.io)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                details["UsesConfigApiUrl"] = usesConfigApiUrl;
                details["HasHardcodedApiCalls"] = hasHardcodedApiCalls;
                
                // Only report issues if there are hardcoded API URLs in actual API calls
                // AND the code doesn't use CONFIG.API_URL
                if (hasHardcodedApiCalls && !usesConfigApiUrl)
                {
                    issues.Add("Frontend code contains hardcoded API URLs in fetch/AJAX calls instead of using CONFIG.API_URL");
                }
                else if (!usesConfigApiUrl && !string.IsNullOrEmpty(scriptJs))
                {
                    // Only report if script.js exists but doesn't use CONFIG.API_URL
                    // (if script.js doesn't exist, there might not be any API calls)
                    issues.Add("Frontend code does not use CONFIG.API_URL for API calls");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating frontend config for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating config.js: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateFrontendDeploymentAsync(ProjectBoard board, string boardId)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(board.GithubFrontendUrl))
        {
            issues.Add("GithubFrontendUrl is not set - cannot validate deployment");
            return (false, issues, details);
        }

        try
        {
            // Parse GitHub URL
            var uri = new Uri(board.GithubFrontendUrl);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathParts.Length < 2)
            {
                issues.Add("Invalid GitHub frontend URL format");
                return (false, issues, details);
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");
            var accessToken = _configuration["GitHub:AccessToken"];

            if (string.IsNullOrEmpty(accessToken))
            {
                issues.Add("GitHub access token not configured");
                return (false, issues, details);
            }

            // Check GitHub Pages status
            // Use the named HttpClient "DeploymentController" which has SSL certificate validation configured for GitHub
            using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "StrAppersBackend/1.0");
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var pagesResponse = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}/pages");
            if (pagesResponse.IsSuccessStatusCode)
            {
                var pagesContent = await pagesResponse.Content.ReadAsStringAsync();
                var pagesDoc = System.Text.Json.JsonDocument.Parse(pagesContent);
                
                var status = pagesDoc.RootElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                var htmlUrl = pagesDoc.RootElement.TryGetProperty("html_url", out var htmlUrlProp) ? htmlUrlProp.GetString() : null;

                details["GitHubPagesEnabled"] = status != null;
                details["GitHubPagesStatus"] = status;
                details["GitHubPagesUrl"] = htmlUrl;

                if (status == null)
                {
                    issues.Add("GitHub Pages is not enabled for the frontend repository");
                }
            }
            else
            {
                details["GitHubPagesEnabled"] = false;
                issues.Add($"GitHub Pages is not enabled or not accessible (Status: {pagesResponse.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating frontend deployment for BoardId: {BoardId}", boardId);
            issues.Add($"Error validating deployment: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateBuildStatusBackendAsync(ProjectBoard board, string boardId)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        try
        {
            // Query BoardStates table for Railway build status
            var boardState = await _context.BoardStates
                .Where(bs => bs.BoardId == boardId && bs.Source == "Railway")
                .OrderByDescending(bs => bs.UpdatedAt)
                .FirstOrDefaultAsync();

            details["BoardStateExists"] = boardState != null;

            if (boardState == null)
            {
                issues.Add("No BoardState record found for Railway source (backend may not have been deployed yet)");
                details["LastBuildStatus"] = null;
                return (false, issues, details);
            }

            details["LastBuildStatus"] = boardState.LastBuildStatus;
            details["LastUpdated"] = boardState.UpdatedAt;
            details["ServiceName"] = boardState.ServiceName;

            if (string.IsNullOrEmpty(boardState.LastBuildStatus))
            {
                issues.Add("LastBuildStatus is not set in BoardStates table for Railway source");
                return (false, issues, details);
            }

            if (!boardState.LastBuildStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Last build status is '{boardState.LastBuildStatus}', expected 'SUCCESS' for Railway backend deployment");
                if (!string.IsNullOrEmpty(boardState.ErrorMessage))
                {
                    details["ErrorMessage"] = boardState.ErrorMessage;
                }
                if (!string.IsNullOrEmpty(boardState.LatestErrorSummary))
                {
                    details["LatestErrorSummary"] = boardState.LatestErrorSummary;
                }
                return (false, issues, details);
            }

            details["BuildStatusValid"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating build status for backend BoardId: {BoardId}", boardId);
            issues.Add($"Error validating build status: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    private async Task<(bool Valid, List<string> Issues, object Details)> ValidateBuildStatusFrontendAsync(ProjectBoard board, string boardId)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        try
        {
            // Query BoardStates table for GitHub Pages build status
            // Note: This is for future implementation when GitHub Pages deployment tracking is added
            var boardState = await _context.BoardStates
                .Where(bs => bs.BoardId == boardId && bs.Source == "GithubPages")
                .OrderByDescending(bs => bs.UpdatedAt)
                .FirstOrDefaultAsync();

            details["BoardStateExists"] = boardState != null;
            details["ImplementationNote"] = "GitHub Pages build status tracking will be implemented in the future";

            if (boardState == null)
            {
                // For now, we don't fail validation if the record doesn't exist
                // since GitHub Pages tracking is not yet implemented
                details["LastBuildStatus"] = null;
                details["Status"] = "Not yet implemented - GitHub Pages build status tracking will be added later";
                return (true, issues, details); // Return valid for now since it's not implemented
            }

            details["LastBuildStatus"] = boardState.LastBuildStatus;
            details["LastUpdated"] = boardState.UpdatedAt;

            if (string.IsNullOrEmpty(boardState.LastBuildStatus))
            {
                // Don't fail if status is not set yet (implementation pending)
                details["Status"] = "Build status tracking not yet fully implemented";
                return (true, issues, details);
            }

            if (!boardState.LastBuildStatus.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Last build status is '{boardState.LastBuildStatus}', expected 'SUCCESS' for GitHub Pages frontend deployment");
                if (!string.IsNullOrEmpty(boardState.ErrorMessage))
                {
                    details["ErrorMessage"] = boardState.ErrorMessage;
                }
                if (!string.IsNullOrEmpty(boardState.LatestErrorSummary))
                {
                    details["LatestErrorSummary"] = boardState.LatestErrorSummary;
                }
                return (false, issues, details);
            }

            details["BuildStatusValid"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating build status for frontend BoardId: {BoardId}", boardId);
            issues.Add($"Error validating build status: {ex.Message}");
        }

        return (issues.Count == 0, issues, details);
    }

    #endregion

        /// <summary>
        /// Filters out database connection information from messages to prevent using outdated credentials
        /// </summary>
        private string FilterDatabaseConnectionInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;
            
            // Remove common database connection patterns
            var filtered = message;
            
            // Remove connection strings (postgresql:// or postgres://)
            filtered = System.Text.RegularExpressions.Regex.Replace(
                filtered, 
                @"postgresql://[^\s\n]+|postgres://[^\s\n]+", 
                "[DATABASE_CONNECTION_STRING_REMOVED_FROM_HISTORY]", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove database credentials patterns
            filtered = System.Text.RegularExpressions.Regex.Replace(
                filtered,
                @"(?:Host|Username|Password|Database|Port|Connection String)[:\s]+[^\n]+",
                "[DATABASE_CREDENTIAL_REMOVED_FROM_HISTORY]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Remove neondb_owner references (old default role)
            filtered = System.Text.RegularExpressions.Regex.Replace(
                filtered,
                @"neondb_owner",
                "[OLD_DATABASE_ROLE_REMOVED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove AppDB_ patterns that might be from old boards
            filtered = System.Text.RegularExpressions.Regex.Replace(
                filtered,
                @"AppDB_[a-f0-9]{24,}",
                "[OLD_DATABASE_NAME_REMOVED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            return filtered;
    }

    /// <summary>
    /// Chat history item for API calls
    /// </summary>
    public class ChatHistoryItem
    {
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

        /// <summary>
        /// Request model for creating a GitHub branch
        /// </summary>
        public class CreateGitHubBranchRequest
        {
            public int SprintNumber { get; set; }
            public bool IsBackend { get; set; }
            public string BoardId { get; set; } = string.Empty;
        }

        /// <summary>
        /// Response model for creating a GitHub branch
        /// </summary>
        public class CreateGitHubBranchResponse
        {
            public bool Success { get; set; }
            public string? BranchUrl { get; set; }
            public string? BranchName { get; set; }
            public string? GitHubResponse { get; set; }
            public string? ErrorMessage { get; set; }
            public int StatusCode { get; set; }
        }

        /// <summary>
        /// Request model for code review endpoint
        /// </summary>
        public class CodeReviewRequest
        {
            public string BoardId { get; set; } = string.Empty;
            public string GithubBranch { get; set; } = string.Empty;
            public string? AIServiceName { get; set; } // Model Name from GET /api/Mentor/use/get-models
        }

        /// <summary>
        /// Extracts the base source name from a sequenced source (e.g., "Junior-1" -> "Junior", "GitHub-Success-PR-2" -> "GitHub-Success-PR")
        /// </summary>
        private string ExtractBaseSource(string source)
        {
            // Check if source ends with a sequence pattern like "-1", "-2", etc.
            var lastDashIndex = source.LastIndexOf('-');
            if (lastDashIndex > 0 && lastDashIndex < source.Length - 1)
            {
                var sequencePart = source.Substring(lastDashIndex + 1);
                if (int.TryParse(sequencePart, out _))
                {
                    // It's a sequence number, return the base source
                    return source.Substring(0, lastDashIndex);
                }
            }
            // No sequence found, return as-is
            return source;
        }

        /// <summary>
        /// Gets the next sequence number for a given (BoardId, baseSource, GithubBranch) combination
        /// Returns 1 if no previous records exist, otherwise returns last sequence + 1
        /// </summary>
        private async Task<int> GetNextSequenceNumberAsync(string boardId, string baseSource, string githubBranch, string? serviceName = null)
        {
            // Get all records with the same BoardId, baseSource (with or without sequence), and GithubBranch
            // If serviceName is provided, filter by it; otherwise check all records
            var query = _context.BoardStates
                .Where(bs => bs.BoardId == boardId && 
                            bs.GithubBranch == githubBranch);
            
            if (!string.IsNullOrEmpty(serviceName))
            {
                query = query.Where(bs => bs.ServiceName == serviceName);
            }
            
            var existingRecords = await query.ToListAsync();

            int maxSequence = 0;
            foreach (var record in existingRecords)
            {
                var recordBaseSource = ExtractBaseSource(record.Source);
                if (recordBaseSource == baseSource)
                {
                    // Extract sequence from this record's source
                    var lastDashIndex = record.Source.LastIndexOf('-');
                    if (lastDashIndex > 0 && lastDashIndex < record.Source.Length - 1)
                    {
                        var sequencePart = record.Source.Substring(lastDashIndex + 1);
                        if (int.TryParse(sequencePart, out var sequence))
                        {
                            maxSequence = Math.Max(maxSequence, sequence);
                        }
                    }
                    else
                    {
                        // Record has base source without sequence, treat as sequence 0
                        maxSequence = Math.Max(maxSequence, 0);
                    }
                }
            }

            return maxSequence + 1;
        }

        /// <summary>
        /// Generates a sequenced source name for records
        /// Format: "{baseSource}-{sequenceNumber}" (e.g., "Junior-1", "GitHub-Success-PR-2", "GitHub-Failed-PR-1")
        /// </summary>
        private async Task<string> GetSequencedSourceAsync(string boardId, string baseSource, string githubBranch, string? serviceName = null)
        {
            var nextSequence = await GetNextSequenceNumberAsync(boardId, baseSource, githubBranch, serviceName);
            return $"{baseSource}-{nextSequence}";
        }

        /// <summary>
        /// Determines DevRole based on Source and GithubBranch
        /// Rules:
        /// - Source="Railway" -> "Backend"
        /// - Source="GithubPages" -> "Frontend"
        /// - For other sources: if GithubBranch contains 'B' -> "Backend", otherwise "Frontend"
        /// </summary>
        private string? DetermineDevRole(string source, string? githubBranch)
        {
            // Railway is always Backend
            if (source == "Railway")
            {
                return "Backend";
            }
            
            // GithubPages is always Frontend
            if (source == "GithubPages")
            {
                return "Frontend";
            }
            
            // For other sources, check GithubBranch
            if (!string.IsNullOrEmpty(githubBranch))
            {
                // If branch name contains 'B' (case-insensitive), it's Backend
                if (githubBranch.Contains('B', StringComparison.OrdinalIgnoreCase))
                {
                    return "Backend";
                }
                // Otherwise, it's Frontend
                return "Frontend";
            }
            
            // If no GithubBranch, return null (will be set later or remain null)
            return null;
        }

        /// <summary>
        /// Performs code review for a branch and logs it to BoardStates
        /// Matches branch name to Trello card, reviews code, and provides feedback
        /// </summary>
        [HttpPost("use/code-review")]
        public async Task<ActionResult<object>> CodeReview([FromBody] CodeReviewRequest request)
        {
            return await CodeReviewInternal(request, "Junior");
        }

        /// <summary>
        /// Internal method for code review with explicit source
        /// </summary>
        private async Task<ActionResult<object>> CodeReviewInternal(CodeReviewRequest request, string source, bool isWebhook = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest(new { Success = false, Message = "BoardId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.GithubBranch))
                {
                    return BadRequest(new { Success = false, Message = "GithubBranch is required" });
                }

                _logger.LogInformation("📝 [CODE REVIEW] Starting code review for BoardId: {BoardId}, Branch: {Branch}, Source: {Source}", 
                    request.BoardId, request.GithubBranch, source);

                // Deduplication: Check if a recent "GitHub-Success-PR" record was just created (within last 30 seconds)
                // This prevents duplicate code reviews when both POST /api/Mentor/use/github-pr and webhook process the same PR
                // We use a 30-second window to catch records created recently, accounting for processing time
                if (source == "GitHub-Success-PR")
                {
                    var recentCutoff = DateTime.UtcNow.AddSeconds(-30);
                    var recentRecord = await _context.BoardStates
                        .Where(bs => bs.BoardId == request.BoardId && 
                                    bs.GithubBranch == request.GithubBranch &&
                                    bs.ServiceName == "CodeReview" &&
                                    (bs.Source.StartsWith("GitHub-Success-PR-") || bs.Source == "GitHub-Success-PR"))
                        .OrderByDescending(bs => bs.CreatedAt)
                        .FirstOrDefaultAsync();
                    
                    if (recentRecord != null && recentRecord.CreatedAt >= recentCutoff)
                    {
                        var baseSource = ExtractBaseSource(recentRecord.Source);
                        if (baseSource == "GitHub-Success-PR")
                        {
                            _logger.LogInformation("⏭️ [CODE REVIEW] Skipping duplicate code review - recent record found: Source={Source}, CreatedAt={CreatedAt}, Webhook={Webhook}, Feedback length={FeedbackLength}", 
                                recentRecord.Source, recentRecord.CreatedAt, recentRecord.Webhook, recentRecord.MentorFeedback?.Length ?? 0);
                            
                            // Return the existing feedback
                            return Ok(new 
                            { 
                                Success = true, 
                                Message = "Code review already completed recently",
                                Feedback = recentRecord.MentorFeedback ?? "Code review completed successfully.",
                                Source = recentRecord.Source,
                                PRStatus = recentRecord.PRStatus
                            });
                        }
                    }
                }

                // Get board from database
                var board = await _context.ProjectBoards
                    .Include(pb => pb.Project)
                    .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);

                if (board == null)
                {
                    return NotFound(new { Success = false, Message = $"Board with ID {request.BoardId} not found" });
                }

                // Parse branch name to get sprint number and role
                // Format: "SprintNumber-RoleLetter" (e.g., "2-F" for Sprint 2, Frontend)
                var branchParts = request.GithubBranch.Split('-');
                if (branchParts.Length != 2 || !int.TryParse(branchParts[0], out var sprintNumber))
                {
                    return BadRequest(new { Success = false, Message = $"Invalid branch name format. Expected format: 'SprintNumber-RoleLetter' (e.g., '2-F')" });
                }

                var roleLetter = branchParts[1].ToUpper();
                var cardId = request.GithubBranch; // CardId matches branch name

                // Get Trello card by CardId
                var trelloCard = await _trelloService.GetCardByCardIdAsync(request.BoardId, cardId);
                if (trelloCard == null)
                {
                    return NotFound(new { Success = false, Message = $"Trello card with CardId '{cardId}' not found" });
                }

                // Determine if backend or frontend based on role letter
                var isBackend = roleLetter == "B";
                var isFrontend = roleLetter == "F";
                var githubUrl = isBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;

                if (string.IsNullOrEmpty(githubUrl))
                {
                    return BadRequest(new { Success = false, Message = $"GitHub {(isBackend ? "backend" : "frontend")} URL not found for board" });
                }

                // Extract owner and repo from GitHub URL
                var uri = new Uri(githubUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = $"Invalid GitHub URL format: {githubUrl}" });
                }

                var owner = pathParts[0];
                var repo = pathParts[1];
                var accessToken = _configuration["GitHub:AccessToken"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return StatusCode(500, new { Success = false, Message = "GitHub access token is not configured" });
                }

                // Diff-Only Review: Use GitHub Compare API to get only the code changes (patch/diff)
                // Compare main...{branch} to get only the changes, not the whole repo
                _logger.LogInformation("📊 [CODE REVIEW] Getting diff using Compare API: main...{Branch}", request.GithubBranch);
                var commitDiff = await _githubService.GetCompareDiffAsync(owner, repo, "main", request.GithubBranch, accessToken);
                
                if (commitDiff == null || commitDiff.FileChanges == null || !commitDiff.FileChanges.Any())
                {
                    _logger.LogWarning("⚠️ [CODE REVIEW] No code changes found in diff for branch {Branch}", request.GithubBranch);
                    return BadRequest(new { Success = false, Message = "No code changes found in branch. Please make sure you have committed and pushed changes." });
                }

                // Check if there are actual code changes (additions or deletions)
                // Require at least 3 lines of changes to filter out trivial changes (whitespace, single newlines, etc.)
                var hasActualCodeChanges = (commitDiff.TotalAdditions + commitDiff.TotalDeletions) >= 3;
                
                // Also check if any file has meaningful patch content (not just whitespace)
                var hasMeaningfulChanges = false;
                foreach (var fileChange in commitDiff.FileChanges)
                {
                    // Check if patch contains actual code (not just whitespace, newlines, or comments)
                    if (!string.IsNullOrEmpty(fileChange.Patch))
                    {
                        // Count non-whitespace lines in the patch (lines starting with + or - that have actual content)
                        var patchLines = fileChange.Patch.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        var meaningfulLines = patchLines.Count(line => 
                        {
                            var trimmed = line.Trim();
                            // Skip lines that are just diff markers, whitespace, or pure whitespace changes
                            if (trimmed.Length < 3) return false;
                            if (trimmed.StartsWith("+++") || trimmed.StartsWith("---") || trimmed.StartsWith("@@")) return false;
                            // Count lines that start with + or - and have actual content (not just whitespace)
                            if ((trimmed.StartsWith("+") || trimmed.StartsWith("-")) && trimmed.Length > 2)
                            {
                                var content = trimmed.Substring(1).Trim();
                                // Must have at least 2 non-whitespace characters to be meaningful
                                return content.Length >= 2 && !string.IsNullOrWhiteSpace(content);
                            }
                            return false;
                        });
                        
                        if (meaningfulLines >= 2) // At least 2 meaningful lines of code changes
                        {
                            hasMeaningfulChanges = true;
                            break;
                        }
                    }
                    // If no patch but has additions/deletions, check if it's significant
                    else if ((fileChange.Additions + fileChange.Deletions) >= 3)
                    {
                        hasMeaningfulChanges = true;
                        break;
                    }
                }
                
                if (!hasActualCodeChanges && !hasMeaningfulChanges)
                {
                    _logger.LogWarning("⚠️ [CODE REVIEW] Diff has no meaningful code changes (only trivial changes like whitespace) for branch {Branch}. Total: {Additions} additions, {Deletions} deletions", 
                        request.GithubBranch, commitDiff.TotalAdditions, commitDiff.TotalDeletions);
                    return BadRequest(new { Success = false, Message = "No meaningful code changes found in diff. The branch may only contain trivial changes (whitespace, formatting) or metadata changes. Please make actual code changes before requesting review." });
                }

                _logger.LogInformation("✅ [CODE REVIEW] Found {FileCount} changed files in diff with {Additions} additions, {Deletions} deletions", 
                    commitDiff.FileChanges.Count, commitDiff.TotalAdditions, commitDiff.TotalDeletions);
                
                // Build diff content from file changes (patch/diff format)
                var diffContent = new StringBuilder();
                foreach (var fileChange in commitDiff.FileChanges)
                {
                    diffContent.AppendLine($"=== {fileChange.FilePath} ({fileChange.Status}) ===");
                    if (!string.IsNullOrEmpty(fileChange.Patch))
                    {
                        diffContent.AppendLine(fileChange.Patch);
                    }
                    else
                    {
                        diffContent.AppendLine($"[File {fileChange.Status}: +{fileChange.Additions} -{fileChange.Deletions} lines]");
                    }
                    diffContent.AppendLine();
                }

                // Extract card information
                var cardName = trelloCard.Value.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                var cardDesc = trelloCard.Value.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "";
                
                // Get checklist items
                var checklistItems = new List<string>();
                if (trelloCard.Value.TryGetProperty("idChecklists", out var checklistIdsProp))
                {
                    // Note: Would need to fetch checklist details separately if needed
                }

                // Prepare AI prompt for code review
                // Align the diff against the actual Trello card requirements
                var codeReviewPrompt = $@"Please review the following code changes (diff) for a Trello card task.

Trello Card Information:
- Card ID: {cardId}
- Card Name: {cardName}
- Description: {cardDesc}
- Sprint: {sprintNumber}
- Role: {(isBackend ? "Backend Developer" : "Frontend Developer")}

Code Changes (Diff):
{diffContent}

Please provide:
1. General code review (code quality, best practices, potential bugs)
2. Alignment check: Does the code changes align with the Trello card requirements? What's missing or incorrect?
3. Detailed feedback and suggestions for improvements

Format your response as a clear, actionable feedback that can be provided to the developer.
Focus on the actual changes made (the diff), not the entire codebase.";

                // Look up AI model from database if AIServiceName is provided
                string? modelName = null;
                if (!string.IsNullOrWhiteSpace(request.AIServiceName))
                {
                    var aiModel = await _context.AIModels
                        .FirstOrDefaultAsync(m => m.Name == request.AIServiceName && m.IsActive);
                    
                    if (aiModel == null)
                    {
                        _logger.LogWarning("⚠️ [CODE REVIEW] AI model '{ModelName}' not found or not active, using default", request.AIServiceName);
                    }
                    else
                    {
                        modelName = aiModel.Name;
                        _logger.LogInformation("✅ [CODE REVIEW] Using AI model: {ModelName} (Provider: {Provider})", aiModel.Name, aiModel.Provider);
                    }
                }

                // Call AI service for code review
                var feedback = await _aiService.GenerateTextResponseAsync(codeReviewPrompt, modelName);

                if (string.IsNullOrEmpty(feedback))
                {
                    return StatusCode(500, new { Success = false, Message = "Failed to generate code review feedback" });
                }

                // Record in BoardStates (APPEND-ONLY - preserve history)
                // For CodeReview records, we use sequenced sources (e.g., "Junior-1", "GitHub-Success-PR-2")
                // to allow multiple records while maintaining uniqueness
                // For GitHub-Success-PR: Replace any existing GitHub-Failed-PR records for the same (BoardId, GithubBranch)
                try
                {
                    // Get the base source (without sequence)
                    var baseSource = ExtractBaseSource(source);
                    
                    // If this is a successful PR validation, delete any existing GitHub-Failed-PR records
                    // (regardless of ServiceName - could be "CodeReview", "Github", or "Validator")
                    if (baseSource == "GitHub-Success-PR")
                    {
                        var failedRecords = await _context.BoardStates
                            .Where(bs => bs.BoardId == request.BoardId && 
                                        bs.GithubBranch == request.GithubBranch &&
                                        (bs.Source.StartsWith("GitHub-Failed-PR-") || bs.Source == "GitHub-Failed-PR"))
                            .ToListAsync();
                        
                        if (failedRecords.Any())
                        {
                            foreach (var failedRecord in failedRecords)
                            {
                                var failedBaseSource = ExtractBaseSource(failedRecord.Source);
                                if (failedBaseSource == "GitHub-Failed-PR")
                                {
                                    _context.BoardStates.Remove(failedRecord);
                                    _logger.LogInformation("ℹ️ [CODE REVIEW] Removing failed PR record (Source: {Source}, ServiceName: {ServiceName}) to be replaced by success record", 
                                        failedRecord.Source, failedRecord.ServiceName);
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                    
                    // Generate sequenced source (e.g., "Junior-1", "GitHub-Success-PR-2")
                    // Retry logic to handle race conditions when multiple requests come in concurrently
                    const int maxRetries = 5;
                    bool recordCreated = false;
                    string? sequencedSource = null;
                    int lastAttemptedSequence = 0;
                    
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            // On retry, query fresh from database to see what actually exists
                            if (attempt > 1)
                            {
                                // Clear change tracker and wait a bit for other transactions to commit
                                _context.ChangeTracker.Clear();
                                await Task.Delay(200 * attempt); // Longer delay on later attempts
                                
                                // Query database fresh to see what records actually exist now
                                var existingRecords = await _context.BoardStates
                                    .Where(bs => bs.BoardId == request.BoardId && 
                                               bs.GithubBranch == request.GithubBranch &&
                                               bs.ServiceName == "CodeReview")
                                    .ToListAsync();
                                
                                // Find max sequence from existing records
                                int maxSequence = 0;
                                foreach (var record in existingRecords)
                                {
                                    var recordBaseSource = ExtractBaseSource(record.Source);
                                    if (recordBaseSource == baseSource)
                                    {
                                        var lastDashIndex = record.Source.LastIndexOf('-');
                                        if (lastDashIndex > 0 && lastDashIndex < record.Source.Length - 1)
                                        {
                                            var sequencePart = record.Source.Substring(lastDashIndex + 1);
                                            if (int.TryParse(sequencePart, out var sequence))
                                            {
                                                maxSequence = Math.Max(maxSequence, sequence);
                                            }
                                        }
                                        else
                                        {
                                            maxSequence = Math.Max(maxSequence, 0);
                                        }
                                    }
                                }
                                
                                // Use max + 1, but ensure it's higher than what we tried before
                                // Add attempt number to be more aggressive and avoid conflicts
                                lastAttemptedSequence = Math.Max(maxSequence + 1, lastAttemptedSequence + attempt);
                                sequencedSource = $"{baseSource}-{lastAttemptedSequence}";
                                
                                _logger.LogInformation("🔄 [CODE REVIEW] Retry attempt {Attempt}/{MaxRetries}: Found max sequence {MaxSequence}, using {Source}", 
                                    attempt, maxRetries, maxSequence, sequencedSource);
                            }
                            else
                            {
                                // First attempt - use normal sequenced source generation
                                sequencedSource = await GetSequencedSourceAsync(request.BoardId, baseSource, request.GithubBranch, "CodeReview");
                                var lastDashIndex = sequencedSource.LastIndexOf('-');
                                if (lastDashIndex > 0 && int.TryParse(sequencedSource.Substring(lastDashIndex + 1), out var seq))
                                {
                                    lastAttemptedSequence = seq;
                                }
                            }
                            
                            // Double-check that a record with this exact source and webhook flag doesn't already exist
                            var existingRecord = await _context.BoardStates
                                .FirstOrDefaultAsync(bs => bs.BoardId == request.BoardId && 
                                                           bs.Source == sequencedSource && 
                                                           bs.Webhook == isWebhook);
                            
                            if (existingRecord != null)
                            {
                                _logger.LogWarning("⚠️ [CODE REVIEW] Record with Source={Source} already exists, skipping creation on attempt {Attempt}", 
                                    sequencedSource, attempt);
                                
                                // If it's a retry, continue to next attempt; if first attempt, this shouldn't happen
                                if (attempt < maxRetries)
                                {
                                    continue; // Will retry with new sequence
                                }
                                else
                                {
                                    _logger.LogError("❌ [CODE REVIEW] Record already exists and max retries reached for BoardId: {BoardId}, Source: {Source}", 
                                        request.BoardId, sequencedSource);
                                    break;
                                }
                            }
                            
                            var boardState = new BoardState
                            {
                                BoardId = request.BoardId,
                                Source = sequencedSource,
                                Webhook = isWebhook, // Set based on whether this is from webhook or platform-triggered
                                ServiceName = "CodeReview",
                                GithubBranch = request.GithubBranch,
                                MentorFeedback = feedback,
                                PRStatus = baseSource == "GitHub-Success-PR" ? "Approved" : null,
                                BranchStatus = null, // BranchStatus should remain null for GitHub-Success-PR records
                                LastBuildStatus = baseSource == "GitHub-Success-PR" ? "SUCCESS" : null,
                                DevRole = DetermineDevRole(baseSource, request.GithubBranch),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            _context.BoardStates.Add(boardState);
                            await _context.SaveChangesAsync();
                            
                            recordCreated = true;
                            _logger.LogInformation("✅ [CODE REVIEW] Successfully recorded code review for BoardId: {BoardId}, Branch: {Branch}, Source: {Source}, PRStatus: {PRStatus}, DevRole: {DevRole}, Webhook: {Webhook}", 
                                request.BoardId, request.GithubBranch, sequencedSource, boardState.PRStatus, boardState.DevRole, boardState.Webhook);
                            break;
                        }
                        catch (DbUpdateException dbEx) when (dbEx.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                        {
                            // Duplicate key error - another request created a record with the same source
                            // Retry with a new sequence number
                            if (attempt < maxRetries)
                            {
                                _logger.LogWarning("⚠️ [CODE REVIEW] Duplicate key detected on attempt {Attempt}/{MaxRetries} for BoardId: {BoardId}, Source: {Source}, Webhook: {Webhook}. Retrying with new sequence...", 
                                    attempt, maxRetries, request.BoardId, sequencedSource, isWebhook);
                                
                                // Will retry with fresh query in next iteration
                            }
                            else
                            {
                                _logger.LogError(dbEx, "❌ [CODE REVIEW] Failed to create record after {MaxRetries} attempts due to duplicate key for BoardId: {BoardId}, Source: {Source}, Webhook: {Webhook}. Constraint: {Constraint}", 
                                    maxRetries, request.BoardId, sequencedSource, isWebhook, pgEx.ConstraintName ?? "unknown");
                                // Don't throw - continue execution even if record creation fails
                            }
                        }
                        catch (Exception ex)
                        {
                            // Catch any other exceptions during record creation
                            _logger.LogError(ex, "❌ [CODE REVIEW] Unexpected error creating record on attempt {Attempt}/{MaxRetries} for BoardId: {BoardId}, Source: {Source}, Webhook: {Webhook}: {Error}", 
                                attempt, maxRetries, request.BoardId, sequencedSource, isWebhook, ex.Message);
                            
                            if (attempt < maxRetries)
                            {
                                // Retry on next attempt
                                continue;
                            }
                            else
                            {
                                // Max retries reached
                                break;
                            }
                        }
                    }
                    
                    if (!recordCreated)
                    {
                        _logger.LogError("❌ [CODE REVIEW] Failed to create record after {MaxRetries} retries for BoardId: {BoardId}. Code review completed but record was not saved.", maxRetries, request.BoardId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [CODE REVIEW] Failed to record code review in BoardStates for BoardId: {BoardId}", request.BoardId);
                    // Continue even if recording fails
                }

                return Ok(new
                {
                    Success = true,
                    BoardId = request.BoardId,
                    Branch = request.GithubBranch,
                    CardId = cardId,
                    CardName = cardName,
                    Feedback = feedback,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [CODE REVIEW] Error performing code review for BoardId: {BoardId}", request.BoardId);
                return StatusCode(500, new { Success = false, Message = $"Error performing code review: {ex.Message}" });
            }
        }

        /// <summary>
        /// GitHub webhook endpoint - receives and processes GitHub events
        /// </summary>
        [HttpPost("github-webhook")]
        public async Task<IActionResult> GitHubWebhook()
        {
            try
            {
                // Read raw request body for HMAC verification
                Request.EnableBuffering();
                var bodyStream = new MemoryStream();
                await Request.Body.CopyToAsync(bodyStream);
                bodyStream.Position = 0;
                var rawBody = await new StreamReader(bodyStream).ReadToEndAsync();
                Request.Body.Position = 0;

                // Verify HMAC signature
                var webhookSecret = _configuration["GitHub:WebhookSecret"];
                if (string.IsNullOrEmpty(webhookSecret))
                {
                    _logger.LogError("❌ [WEBHOOK] WebhookSecret is not configured");
                    return Unauthorized(new { Success = false, Message = "Webhook secret not configured" });
                }

                var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (string.IsNullOrEmpty(signatureHeader))
                {
                    _logger.LogWarning("⚠️ [WEBHOOK] Missing X-Hub-Signature-256 header");
                    return Unauthorized(new { Success = false, Message = "Missing signature header" });
                }

                // Compute HMAC SHA256
                using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret)))
                {
                    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
                    var computedHash = "sha256=" + BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                    if (signatureHeader != computedHash)
                    {
                        _logger.LogWarning("⚠️ [WEBHOOK] HMAC signature verification failed");
                        return Unauthorized(new { Success = false, Message = "Invalid signature" });
                    }
                }

                _logger.LogInformation("✅ [WEBHOOK] HMAC signature verified");

                // Parse webhook payload
                var payload = JsonSerializer.Deserialize<JsonElement>(rawBody);
                var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault() ?? "unknown";
                var deliveryId = Request.Headers["X-GitHub-Delivery"].FirstOrDefault() ?? "unknown";

                _logger.LogInformation("📥 [WEBHOOK] Received GitHub event: {EventType}, Delivery: {DeliveryId}", eventType, deliveryId);

                // Extract repository information
                string? owner = null;
                string? repo = null;
                string? boardId = null;

                if (payload.TryGetProperty("repository", out var repoProp))
                {
                    if (repoProp.TryGetProperty("owner", out var ownerProp))
                    {
                        owner = ownerProp.TryGetProperty("login", out var loginProp) ? loginProp.GetString() : null;
                    }
                    repo = repoProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                }

                // Try to find board by GitHub URL
                if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
                {
                    var githubUrl = $"https://github.com/{owner}/{repo}";
                    var board = await _context.ProjectBoards
                        .FirstOrDefaultAsync(pb => pb.GithubBackendUrl == githubUrl || pb.GithubFrontendUrl == githubUrl);
                    
                    if (board != null)
                    {
                        boardId = board.Id;
                        _logger.LogInformation("Found ProjectBoard: BoardId={BoardId}, CreatedAt={CreatedAt}, TimeSinceCreation={TimeSinceCreation} seconds", 
                            board.Id, board.CreatedAt, (DateTime.UtcNow - board.CreatedAt).TotalSeconds);
                    }
                    else
                    {
                        // Check for potential timing issues
                        var recentBoards = await _context.ProjectBoards
                            .Where(pb => pb.CreatedAt >= DateTime.UtcNow.AddMinutes(-5))
                            .OrderByDescending(pb => pb.CreatedAt)
                            .Select(pb => new { pb.Id, pb.GithubBackendUrl, pb.GithubFrontendUrl, pb.CreatedAt })
                            .Take(10)
                            .ToListAsync();
                        
                        _logger.LogWarning("ProjectBoard not found for GitHub URL: {GithubUrl}. " +
                            "Recent boards created in last 5 minutes: {RecentBoardCount}. " +
                            "Recent board URLs: {RecentBoardUrls}", 
                            githubUrl,
                            recentBoards.Count,
                            string.Join(", ", recentBoards.Select(b => $"Backend: {b.GithubBackendUrl}, Frontend: {b.GithubFrontendUrl}")));
                    }
                }

                // Verify ProjectBoard exists before creating BoardState
                if (string.IsNullOrEmpty(boardId))
                {
                    _logger.LogWarning("BoardId is null or empty, cannot create BoardState. Skipping webhook recording.");
                    return Ok(new { Success = true, Message = "Webhook received but BoardId not found, skipping BoardState update" });
                }

                // Verify ProjectBoard exists in database to prevent FK constraint violation
                var projectBoardExists = await _context.ProjectBoards
                    .AnyAsync(pb => pb.Id == boardId);
                
                if (!projectBoardExists)
                {
                    var recentBoards = await _context.ProjectBoards
                        .Where(pb => pb.CreatedAt >= DateTime.UtcNow.AddMinutes(-5))
                        .OrderByDescending(pb => pb.CreatedAt)
                        .Select(pb => new { pb.Id, pb.CreatedAt })
                        .Take(10)
                        .ToListAsync();
                    
                    _logger.LogError("ProjectBoard with BoardId {BoardId} does not exist. " +
                        "This may be a timing issue (webhook arrived before board creation completed) or the board creation failed. " +
                        "Recent boards created in last 5 minutes: {RecentBoardCount}. " +
                        "Recent board IDs: {RecentBoardIds}. " +
                        "Skipping BoardState update to avoid foreign key constraint violation.", 
                        boardId,
                        recentBoards.Count,
                        string.Join(", ", recentBoards.Select(b => $"{b.Id} (created: {b.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC)")));
                    
                    return Ok(new { Success = true, Message = $"ProjectBoard {boardId} not found, skipping BoardState update" });
                }

                // Record webhook event in BoardStates (non-replaceable, always append)
                try
                {
                        var boardState = new BoardState
                        {
                            BoardId = boardId,
                            Source = "GitHub",
                            Webhook = true,
                            ServiceName = eventType,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                    // Extract additional information based on event type
                    if (eventType == "pull_request")
                    {
                        if (payload.TryGetProperty("action", out var actionProp))
                        {
                            var action = actionProp.GetString();
                            boardState.LatestEvent = $"pull_request_{action}";

                            // Extract PR information for ALL actions (opened, synchronize, reopened, closed, etc.)
                            if (payload.TryGetProperty("pull_request", out var prProp))
                            {
                                string? headRef = null;
                                string? headSha = null;
                                int? issueNumber = null;
                                
                                // Extract branch and SHA for all actions
                                if (prProp.TryGetProperty("head", out var headProp))
                                {
                                    if (headProp.TryGetProperty("ref", out var refProp))
                                    {
                                        headRef = refProp.GetString();
                                        boardState.GithubBranch = headRef;
                                        boardState.DevRole = DetermineDevRole("GitHub", headRef);
                                    }
                                    if (headProp.TryGetProperty("sha", out var shaProp))
                                    {
                                        headSha = shaProp.GetString();
                                        boardState.LatestCommitId = headSha;
                                    }
                                }
                                
                                if (prProp.TryGetProperty("number", out var numberProp))
                                {
                                    issueNumber = numberProp.GetInt32();
                                    boardState.LatestEvent = $"pull_request_{action}_#{issueNumber}";
                                    _logger.LogInformation("📥 [WEBHOOK] PR {Action}: Issue #{IssueNumber}, Branch: {Branch}, SHA: {Sha}", 
                                        action, issueNumber, headRef, headSha);
                                }

                                // Handle "reopened" action - set PRStatus and update GitHub-Merge record
                                if (action == "reopened")
                                {
                                    boardState.PRStatus = "Re-opened";
                                    
                                    // Update existing "GitHub-Merge" record if it exists
                                    if (!string.IsNullOrEmpty(headRef) && !string.IsNullOrEmpty(boardId))
                                    {
                                        try
                                        {
                                            var existingMergeRecord = await _context.BoardStates
                                                .FirstOrDefaultAsync(bs => bs.BoardId == boardId && 
                                                                           bs.Source == "GitHub-Merge" && 
                                                                           bs.GithubBranch == headRef);
                                            
                                            if (existingMergeRecord != null)
                                            {
                                                existingMergeRecord.PRStatus = "Re-opened";
                                                existingMergeRecord.BranchStatus = "Un-Merged";
                                                existingMergeRecord.UpdatedAt = DateTime.UtcNow;
                                                await _context.SaveChangesAsync();
                                                _logger.LogInformation("✅ [WEBHOOK] Updated GitHub-Merge record to Re-opened for PR #{IssueNumber}, Branch: {Branch}", issueNumber, headRef);
                                            }
                                        }
                                        catch (Exception mergeUpdateEx)
                                        {
                                            _logger.LogWarning(mergeUpdateEx, "⚠️ [WEBHOOK] Failed to update GitHub-Merge record for PR #{IssueNumber} (non-critical)", issueNumber);
                                        }
                                    }
                                }

                                if (action == "opened" || action == "synchronize" || action == "reopened")
                                {
                                    // When PR is opened, update existing "GitHub-Merge" record if it exists
                                    if (action == "opened" && !string.IsNullOrEmpty(headRef) && !string.IsNullOrEmpty(boardId))
                                    {
                                        try
                                        {
                                            var existingMergeRecord = await _context.BoardStates
                                                .FirstOrDefaultAsync(bs => bs.BoardId == boardId && 
                                                                           bs.Source == "GitHub-Merge" && 
                                                                           bs.GithubBranch == headRef);
                                            
                                            if (existingMergeRecord != null)
                                            {
                                                existingMergeRecord.PRStatus = "Re-opened";
                                                existingMergeRecord.BranchStatus = "Un-Merged";
                                                existingMergeRecord.UpdatedAt = DateTime.UtcNow;
                                                await _context.SaveChangesAsync();
                                                _logger.LogInformation("✅ [WEBHOOK] Updated GitHub-Merge record to Re-opened for PR #{IssueNumber}, Branch: {Branch}", issueNumber, headRef);
                                            }
                                        }
                                        catch (Exception mergeUpdateEx)
                                        {
                                            _logger.LogWarning(mergeUpdateEx, "⚠️ [WEBHOOK] Failed to update GitHub-Merge record for PR #{IssueNumber} (non-critical)", issueNumber);
                                        }
                                    }
                                    
                                    // Identify if repo is Backend or Frontend based on name
                                    var isBackend = repo?.StartsWith("backend_") ?? false;
                                    
                                    // Deduplication: Check if validation is already in progress or recently completed
                                    // This prevents duplicate processing when POST /api/Mentor/use/github-pr creates a PR and triggers this webhook
                                    // Strategy: Check both database records AND GitHub commit status to catch in-progress validations
                                    bool shouldSkipValidation = false;
                                    if ((action == "opened" || action == "reopened") && !string.IsNullOrEmpty(boardId) && !string.IsNullOrEmpty(headRef) && !string.IsNullOrEmpty(headSha) && !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
                                    {
                                        // Check 1: Look for recent "GitHub-Success-PR" records (within last 30 seconds)
                                        // This catches cases where POST endpoint already completed
                                        var recentCutoff = DateTime.UtcNow.AddSeconds(-30);
                                        var recentRecord = await _context.BoardStates
                                            .Where(bs => bs.BoardId == boardId && 
                                                        bs.GithubBranch == headRef &&
                                                        bs.ServiceName == "CodeReview" &&
                                                        (bs.Source.StartsWith("GitHub-Success-PR-") || bs.Source == "GitHub-Success-PR"))
                                            .OrderByDescending(bs => bs.CreatedAt)
                                            .FirstOrDefaultAsync();
                                        
                                        if (recentRecord != null && recentRecord.CreatedAt >= recentCutoff)
                                        {
                                            var baseSource = ExtractBaseSource(recentRecord.Source);
                                            if (baseSource == "GitHub-Success-PR")
                                            {
                                                shouldSkipValidation = true;
                                                _logger.LogInformation("⏭️ [WEBHOOK] Skipping validation for PR #{IssueNumber} - recent GitHub-Success-PR record found (likely created by POST endpoint): Source={Source}, CreatedAt={CreatedAt}, Webhook={Webhook}", 
                                                    issueNumber, recentRecord.Source, recentRecord.CreatedAt, recentRecord.Webhook);
                                            }
                                        }
                                        
                                        // Check 2: If no record found, check GitHub commit status
                                        // Skip when: "pending" (POST is processing), "success" (already passed), or "failure" (already failed)
                                        // This prevents duplicate validation when POST creates PR then fails (e.g. Trello card not found) and webhook arrives
                                        if (!shouldSkipValidation)
                                        {
                                            try
                                            {
                                                var validationContext = _configuration["GitHub:ValidationContext"] ?? "Mentor-Validation";
                                                var accessToken = _configuration["GitHub:AccessToken"];
                                                var commitStatus = await _githubService.GetCommitStatusAsync(owner, repo, headSha, validationContext, accessToken);
                                                
                                                if (commitStatus != null)
                                                {
                                                    var state = commitStatus.State ?? "";
                                                    if (state == "pending")
                                                    {
                                                        shouldSkipValidation = true;
                                                        _logger.LogInformation("⏭️ [WEBHOOK] Skipping validation for PR #{IssueNumber} - GitHub commit status is 'pending' (validation in progress from POST endpoint)", issueNumber);
                                                    }
                                                    else if (state == "success" || state == "failure")
                                                    {
                                                        shouldSkipValidation = true;
                                                        _logger.LogInformation("⏭️ [WEBHOOK] Skipping validation for PR #{IssueNumber} - GitHub commit status is '{State}' (validation already completed by POST endpoint)", issueNumber, state);
                                                    }
                                                }
                                            }
                                            catch (Exception statusEx)
                                            {
                                                _logger.LogWarning(statusEx, "⚠️ [WEBHOOK] Failed to check commit status for deduplication (non-critical), proceeding with validation");
                                                // Continue - if status check fails, proceed with validation to be safe
                                            }
                                        }
                                    }
                                    
                                    // Call shared validation service (only if not skipped)
                                    if (shouldSkipValidation)
                                    {
                                        // Validation was skipped due to deduplication - this is expected and correct
                                        _logger.LogInformation("ℹ️ [WEBHOOK] Validation skipped for PR #{IssueNumber} (deduplication)", issueNumber);
                                    }
                                    else if (!string.IsNullOrEmpty(headSha) && !string.IsNullOrEmpty(repo) && !string.IsNullOrEmpty(boardId) && !string.IsNullOrEmpty(headRef))
                                    {
                                        _logger.LogInformation("📥 [WEBHOOK] Triggering shared validation service for PR #{IssueNumber}: Repo={Repo}, SHA={Sha}, Branch={Branch}, IsBackend={IsBackend}", 
                                            issueNumber, repo, headSha, headRef, isBackend);
                                        
                                        // Create a background task with its own service scope to avoid DbContext disposal issues
                                        // Capture variables before the background task
                                        var capturedBoardId = boardId;
                                        var capturedHeadRef = headRef;
                                        var capturedHeadSha = headSha;
                                        var capturedRepo = repo;
                                        var capturedIsBackend = isBackend;
                                        var capturedIssueNumber = issueNumber;
                                        
                                        _ = Task.Run(async () =>
                                        {
                                            // Create a new scope for the background task
                                            using (var scope = _serviceScopeFactory.CreateScope())
                                            {
                                                ILogger<MentorController>? scopedLogger = null;
                                                try
                                                {
                                                    await Task.Delay(2000); // Small delay to ensure BoardState is saved
                                                    
                                                    // Get all required services from the new scope
                                                    scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<MentorController>>();
                                                    var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                                                    
                                                    // Verify context is not disposed by attempting a simple query
                                                    try
                                                    {
                                                        await scopedContext.Database.CanConnectAsync();
                                                    }
                                                    catch (Exception ctxEx)
                                                    {
                                                        scopedLogger.LogError(ctxEx, "❌ [WEBHOOK] Context validation failed for PR #{IssueNumber}: {Error}", capturedIssueNumber, ctxEx.Message);
                                                        return;
                                                    }
                                                    
                                                    var scopedTrelloService = scope.ServiceProvider.GetRequiredService<ITrelloService>();
                                                    var scopedGitHubService = scope.ServiceProvider.GetRequiredService<IGitHubService>();
                                                    var scopedAIService = scope.ServiceProvider.GetRequiredService<IAIService>();
                                                    var scopedCodeReviewAgent = scope.ServiceProvider.GetRequiredService<ICodeReviewAgent>();
                                                    var scopedConfiguration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                                                    var scopedHttpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                                                    var scopedIntentService = scope.ServiceProvider.GetRequiredService<IMentorIntentService>();
                                                    var scopedPromptConfig = scope.ServiceProvider.GetRequiredService<IOptions<PromptConfig>>();
                                                    var scopedTrelloConfig = scope.ServiceProvider.GetRequiredService<IOptions<TrelloConfig>>();
                                                    var scopedDeploymentsConfig = scope.ServiceProvider.GetRequiredService<IOptions<DeploymentsConfig>>();
                                                    var scopedTestingConfig = scope.ServiceProvider.GetRequiredService<IOptions<TestingConfig>>();
                                                    
                                                    // Create a temporary controller instance with scoped services to call ProcessValidationAsync
                                                    var tempController = new MentorController(
                                                        scopedContext,
                                                        scopedLogger,
                                                        scopedTrelloService,
                                                        scopedGitHubService,
                                                        scopedIntentService,
                                                        scopedCodeReviewAgent,
                                                        scopedPromptConfig,
                                                        scopedTrelloConfig,
                                                        scopedConfiguration,
                                                        scopedHttpClientFactory,
                                                        scopedAIService,
                                                        scopedDeploymentsConfig,
                                                        scopedTestingConfig,
                                                        scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());
                                                    
                                                    // Call ProcessValidationAsync with scoped services (isWebhook=true for webhook-initiated)
                                                    var (success, feedback, errorMessage) = await tempController.ProcessValidationAsync(
                                                        capturedRepo, 
                                                        capturedHeadRef, 
                                                        capturedHeadSha, 
                                                        capturedBoardId, 
                                                        capturedIsBackend, 
                                                        capturedIssueNumber,
                                                        isWebhook: true);
                                                    
                                                    scopedLogger.LogInformation("✅ [WEBHOOK] Validation completed for PR #{IssueNumber}: Success={Success}", capturedIssueNumber, success);
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (scopedLogger == null)
                                                    {
                                                        scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<MentorController>>();
                                                    }
                                                    scopedLogger.LogError(ex, "❌ [WEBHOOK] Failed to process validation for PR #{IssueNumber}: {Error}", capturedIssueNumber, ex.Message);
                                                }
                                            }
                                        });
                                    }
                                    else
                                    {
                                        // Data is actually missing
                                        _logger.LogWarning("⚠️ [WEBHOOK] Missing required PR data: HeadRef={HeadRef}, HeadSha={HeadSha}, BoardId={BoardId}, Repo={Repo}", 
                                            headRef, headSha, boardId, repo);
                                    }
                                }
                                
                                // Handle "closed" action - PSA Tracking: Handle closed PRs with merged status
                                if (action == "closed")
                                {
                                    // Reuse headRef and issueNumber already extracted above
                                    string? branchName = headRef;
                                    
                                    // Update LatestEvent for closed action
                                    if (issueNumber.HasValue)
                                    {
                                        boardState.LatestEvent = $"pull_request_closed_#{issueNumber}";
                                    }
                                    
                                    if (prProp.TryGetProperty("merged", out var mergedProp))
                                    {
                                        var merged = mergedProp.GetBoolean();
                                        
                                        // Handle PR merge: Update or create "GitHub-Merge" record (key: BoardId, Source="GitHub-Merge", GithubBranch)
                                        if (merged && !string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(boardId))
                                        {
                                            try
                                            {
                                                // Find existing "GitHub-Merge" record
                                                var existingMergeRecord = await _context.BoardStates
                                                    .FirstOrDefaultAsync(bs => bs.BoardId == boardId && 
                                                                               bs.Source == "GitHub-Merge" && 
                                                                               bs.GithubBranch == branchName);
                                                
                                                DateTime mergedAt = DateTime.UtcNow;
                                                if (prProp.TryGetProperty("merged_at", out var mergedAtProp))
                                                {
                                                    var mergedAtStr = mergedAtProp.GetString();
                                                    if (!string.IsNullOrEmpty(mergedAtStr) && DateTime.TryParse(mergedAtStr, out var parsedMergedAt))
                                                    {
                                                        // Ensure UTC - PostgreSQL requires UTC for timestamp with time zone
                                                        mergedAt = parsedMergedAt.Kind == DateTimeKind.Utc 
                                                            ? parsedMergedAt 
                                                            : parsedMergedAt.ToUniversalTime();
                                                    }
                                                }
                                                
                                                if (existingMergeRecord != null)
                                                {
                                                    // UPDATE existing "GitHub-Merge" record
                                                    existingMergeRecord.BranchStatus = "Completed-Merged";
                                                    existingMergeRecord.PRStatus = "Merged";
                                                    existingMergeRecord.LastBuildStatus = "Completed-Merged";
                                                    existingMergeRecord.LastMergeDate = mergedAt;
                                                    existingMergeRecord.Webhook = true; // Mark as webhook-initiated
                                                    existingMergeRecord.UpdatedAt = DateTime.UtcNow;
                                                    
                                                    await _context.SaveChangesAsync();
                                                    _logger.LogInformation("✅ [WEBHOOK] Updated existing GitHub-Merge record for PR #{IssueNumber}, Branch: {Branch}", issueNumber, branchName);
                                                }
                                                else
                                                {
                                                    // CREATE new "GitHub-Merge" record
                                                    var mergeBoardState = new BoardState
                                                    {
                                                        BoardId = boardId,
                                                        Source = "GitHub-Merge",
                                                        Webhook = true, // Webhook-initiated
                                                        ServiceName = "GitHubMerge",
                                                        BranchStatus = "Completed-Merged",
                                                        PRStatus = "Merged",
                                                        LastBuildStatus = "Completed-Merged",
                                                        GithubBranch = branchName,
                                                        LastMergeDate = mergedAt,
                                                        DevRole = DetermineDevRole("GitHub-Merge", branchName),
                                                        CreatedAt = DateTime.UtcNow,
                                                        UpdatedAt = DateTime.UtcNow
                                                    };
                                                    
                                                    _context.BoardStates.Add(mergeBoardState);
                                                    await _context.SaveChangesAsync();
                                                    _logger.LogInformation("✅ [WEBHOOK] Created new GitHub-Merge record for PR #{IssueNumber}, Branch: {Branch}", issueNumber, branchName);
                                                }
                                            }
                                            catch (Exception mergeEx)
                                            {
                                                _logger.LogWarning(mergeEx, "⚠️ [WEBHOOK] Failed to update/create GitHub-Merge record for PR #{IssueNumber} (non-critical)", issueNumber);
                                            }
                                            
                                            // Don't set merge status on the webhook record itself (it's just for webhook tracking)
                                            boardState.BranchStatus = null;
                                            boardState.PRStatus = null;
                                            boardState.LastBuildStatus = null;
                                        }
                                        else if (!merged)
                                        {
                                            // PR closed without merge
                                            boardState.BranchStatus = "Abandoned-Discarded";
                                            boardState.PRStatus = "Closed";
                                            boardState.LastBuildStatus = "Abandoned-Discarded";
                                            _logger.LogInformation("⚠️ [WEBHOOK] PR #{IssueNumber} closed without merge - Status: Abandoned-Discarded", issueNumber);
                                        }
                                        
                                        // Terminate: Stop further processing; do not trigger validation router
                                        _logger.LogInformation("📥 [WEBHOOK] PR closed - skipping validation router");
                                    }
                                }
                            }
                        }
                    }

                    // Handle deployment_status events for GitHub Pages
                    if (eventType == "deployment_status" && !string.IsNullOrEmpty(boardId) && !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
                    {
                        try
                        {
                            // Check if this is a GitHub Pages deployment
                            if (payload.TryGetProperty("deployment_status", out var deploymentStatusProp))
                            {
                                var state = deploymentStatusProp.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;
                                var environment = deploymentStatusProp.TryGetProperty("environment", out var envProp) ? envProp.GetString() : null;
                                
                                // GitHub Pages deployments typically have environment "github-pages"
                                if (environment == "github-pages" || (environment == null && state != null))
                                {
                                    _logger.LogInformation("📥 [WEBHOOK] GitHub Pages deployment status: {State}, Environment: {Environment}", state, environment ?? "N/A");
                                    
                                    // Update BoardState with deployment status
                                    var accessToken = _configuration["GitHub:AccessToken"];
                                    if (!string.IsNullOrEmpty(accessToken))
                                    {
                                        await UpdateGithubPagesBoardStateAsync(boardId, owner, repo, accessToken);
                                    }
                                }
                            }
                        }
                        catch (Exception deployEx)
                        {
                            _logger.LogWarning(deployEx, "⚠️ [WEBHOOK] Failed to process deployment_status event (non-critical)");
                        }
                    }

                    // Append webhook record (constraint now includes GithubBranch, allowing multiple PRs for different branches)
                    // The unique constraint on (BoardId, Source, Webhook, GithubBranch) allows:
                    // - Multiple PRs for different branches (different GithubBranch values)
                    // - Multiple records with NULL GithubBranch (NULLs are distinct in PostgreSQL)
                    _context.BoardStates.Add(boardState);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ [WEBHOOK] Recorded webhook event {EventType} for BoardId: {BoardId}, Branch: {Branch}", 
                        eventType, boardId, boardState.GithubBranch ?? "N/A");
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    // Duplicate key error - record already exists, this is expected for webhook events
                    _logger.LogInformation("ℹ️ [WEBHOOK] Webhook record already exists for BoardId: {BoardId}, EventType: {EventType} (duplicate key)", boardId, eventType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [WEBHOOK] Failed to record webhook event in BoardStates");
                }

                return Ok(new { Success = true, Message = "Webhook processed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [WEBHOOK] Error processing GitHub webhook");
                return StatusCode(500, new { Success = false, Message = $"Error processing webhook: {ex.Message}" });
            }
        }

        /// <summary>
        /// Validation router endpoint - validates PR and updates GitHub status
        /// </summary>
        [HttpGet("use/validate-pr")]
        public async Task<ActionResult<object>> ValidatePR([FromQuery] string repoName, [FromQuery] string sha, [FromQuery] string type, [FromQuery] int? issueNumber = null, [FromQuery] string? headRef = null)
        {
            var accessToken = _configuration["GitHub:AccessToken"];
            var owner = _configuration["GitHub:Organization"] ?? "";
            var validationContext = _configuration["GitHub:ValidationContext"] ?? "Mentor-Validation";
            var statusSet = false; // Track if we've set a status to ensure we always set a final status
            
            try
            {
                if (string.IsNullOrWhiteSpace(repoName) || string.IsNullOrWhiteSpace(sha) || string.IsNullOrWhiteSpace(type))
                {
                    _logger.LogWarning("🔍 [VALIDATE-PR] Missing required parameters: Repo={Repo}, SHA={Sha}, Type={Type}", repoName, sha, type);
                    return BadRequest(new { Success = false, Message = "repoName, sha, and type are required" });
                }

                if (type != "be" && type != "fe")
                {
                    _logger.LogWarning("🔍 [VALIDATE-PR] Invalid type parameter: {Type} (must be 'be' or 'fe')", type);
                    return BadRequest(new { Success = false, Message = "type must be 'be' or 'fe'" });
                }

                _logger.LogInformation("🔍 [VALIDATE-PR] ===== Starting PR validation =====");
                _logger.LogInformation("🔍 [VALIDATE-PR] Repository: {Repo}, SHA: {Sha}, Type: {Type}", repoName, sha, type);

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(owner))
                {
                    _logger.LogError("🔍 [VALIDATE-PR] GitHub configuration missing: AccessToken={HasToken}, Owner={Owner}", 
                        !string.IsNullOrEmpty(accessToken), owner);
                    return StatusCode(500, new { Success = false, Message = "GitHub configuration is missing" });
                }

                // Step 1: Set status to pending
                _logger.LogInformation("🔍 [VALIDATE-PR] Step 1: Setting status to 'pending'");
                try
                {
                    await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "pending", validationContext, 
                        "Mentor is analyzing your code...", accessToken);
                    statusSet = true;
                    _logger.LogInformation("✅ [VALIDATE-PR] Status set to 'pending' successfully");
                }
                catch (Exception statusEx)
                {
                    _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set status to 'pending': {Error}", statusEx.Message);
                    // Continue anyway - we'll try to set final status later
                }

                // Step 2: Branch naming validation
                _logger.LogInformation("🔍 [VALIDATE-PR] Step 2: Validating branch naming");
                
                // Use head_ref from query parameter (passed from webhook) or fallback to BoardStates
                string? branchName = headRef;
                
                if (string.IsNullOrEmpty(branchName))
                {
                    _logger.LogInformation("🔍 [VALIDATE-PR] headRef not provided, attempting to get from BoardStates");
                    var latestWebhook = await _context.BoardStates
                        .Where(bs => bs.Source == "GitHub" && bs.ServiceName == "pull_request" && bs.GithubBranch != null && bs.LatestCommitId == sha)
                        .OrderByDescending(bs => bs.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latestWebhook != null && !string.IsNullOrEmpty(latestWebhook.GithubBranch))
                    {
                        branchName = latestWebhook.GithubBranch;
                        _logger.LogInformation("🔍 [VALIDATE-PR] Retrieved branch name from BoardStates: {Branch}", branchName);
                    }
                }
                
                if (string.IsNullOrEmpty(branchName))
                {
                    _logger.LogError("❌ [VALIDATE-PR] Could not determine branch name (head_ref). HeadRef={HeadRef}, SHA={Sha}", headRef, sha);
                    
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            "Could not determine branch name (head_ref)", accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set failure status: {Error}", statusEx.Message);
                    }
                    
                    return BadRequest(new { Success = false, Message = "Could not determine branch name (head_ref) from PR" });
                }
                _logger.LogInformation("🔍 [VALIDATE-PR] Extracted branch name: {Branch}", branchName);
                
                var patternKey = type == "be" ? "GitHub:BranchNamingPatterns:Backend" : "GitHub:BranchNamingPatterns:Frontend";
                var pattern = _configuration[patternKey] ?? (type == "be" ? "^([1-9]|1[0-9]|20)-B$" : "^([1-9]|1[0-9]|20)-F$");
                _logger.LogInformation("🔍 [VALIDATE-PR] Branch naming pattern: {Pattern} (from config key: {PatternKey})", pattern, patternKey);

                var regex = new System.Text.RegularExpressions.Regex(pattern);
                if (!regex.IsMatch(branchName))
                {
                    _logger.LogWarning("❌ [VALIDATE-PR] Branch naming validation FAILED: Branch '{Branch}' does not match pattern '{Pattern}'", branchName, pattern);
                    
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            $"Invalid branch name: '{branchName}' does not match pattern '{pattern}'", accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set failure status: {Error}", statusEx.Message);
                    }

                    // Record in BoardStates - find board by repo URL
                    string? boardId = null;
                    var githubUrl = $"https://github.com/{owner}/{repoName}";
                    var boardForNaming = await _context.ProjectBoards
                        .FirstOrDefaultAsync(pb => (type == "be" ? pb.GithubBackendUrl : pb.GithubFrontendUrl) == githubUrl);
                    
                    if (boardForNaming != null)
                    {
                        boardId = boardForNaming.Id;
                    }
                    else
                    {
                        // Fallback: try to get from latest webhook if available
                        var latestWebhook = await _context.BoardStates
                            .Where(bs => bs.Source == "GitHub" && bs.ServiceName == "pull_request" && bs.GithubBranch == branchName && bs.LatestCommitId == sha)
                            .OrderByDescending(bs => bs.CreatedAt)
                            .FirstOrDefaultAsync();
                        
                        if (latestWebhook != null)
                        {
                            boardId = latestWebhook.BoardId;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(boardId))
                    {
                        var boardState = new BoardState
                        {
                            BoardId = boardId,
                            Source = type == "be" ? "PR-BackendValidation" : "PR-FrontendValidation",
                            Webhook = false,
                            ServiceName = "BranchNamingValidation",
                            ErrorMessage = $"Invalid branch name naming convention. Branch: '{branchName}', Expected pattern: '{pattern}'",
                            GithubBranch = branchName,
                            DevRole = DetermineDevRole(type == "be" ? "PR-BackendValidation" : "PR-FrontendValidation", branchName),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.BoardStates.Add(boardState);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [VALIDATE-PR] Could not find board for repo {Repo}, skipping BoardState record", repoName);
                    }

                    _logger.LogInformation("🔍 [VALIDATE-PR] ===== Validation FAILED: Branch naming =====");
                    return Ok(new { Success = false, Message = "Branch naming validation failed", BranchName = branchName, Pattern = pattern });
                }

                _logger.LogInformation("✅ [VALIDATE-PR] Branch naming validation PASSED for '{Branch}'", branchName);

                // Step 3: Execute validation
                _logger.LogInformation("🔍 [VALIDATE-PR] Step 3: Finding board for repository");
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.GithubBackendUrl.Contains(repoName) || pb.GithubFrontendUrl.Contains(repoName));

                if (board == null)
                {
                    _logger.LogError("❌ [VALIDATE-PR] Board not found for repository: {Repo}", repoName);
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            "Board not found for repository", accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set failure status: {Error}", statusEx.Message);
                    }
                    return NotFound(new { Success = false, Message = "Board not found for repository" });
                }

                _logger.LogInformation("✅ [VALIDATE-PR] Found board: BoardId={BoardId}", board.Id);
                _logger.LogInformation("🔍 [VALIDATE-PR] Step 4: Running {Type} validation for BoardId={BoardId}, Branch={Branch}", 
                    type == "be" ? "Backend" : "Frontend", board.Id, branchName);

                var validationStartTime = DateTime.UtcNow;
                ActionResult<object> validationResult;
                
                try
                {
                    validationResult = type == "be" 
                        ? await ValidateBackend(board.Id, branchName)
                        : await ValidateFrontend(board.Id, branchName);
                    
                    var validationDuration = (DateTime.UtcNow - validationStartTime).TotalSeconds;
                    _logger.LogInformation("✅ [VALIDATE-PR] Validation completed in {Duration} seconds", validationDuration);
                }
                catch (Exception validationEx)
                {
                    _logger.LogError(validationEx, "❌ [VALIDATE-PR] Exception during {Type} validation: {Error}", 
                        type == "be" ? "Backend" : "Frontend", validationEx.Message);
                    throw; // Re-throw to be caught by outer catch block
                }

                // Step 4: Report result
                _logger.LogInformation("🔍 [VALIDATE-PR] Step 5: Parsing validation result");
                bool isValid = false;
                List<string> validationIssues = new List<string>();
                
                // Handle ActionResult<object> - extract value using Value property
                try
                {
                    // ActionResult<object> has a Value property when it's an OkObjectResult
                    // We need to check the actual result type
                    var resultValue = validationResult.Value;
                    
                    if (resultValue != null)
                    {
                        var jsonString = JsonSerializer.Serialize(resultValue);
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                        
                        if (jsonElement.TryGetProperty("Overall", out var overallProp))
                        {
                            if (overallProp.TryGetProperty("Valid", out var validProp))
                            {
                                isValid = validProp.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Overall validation result: {IsValid}", isValid);
                            }
                            
                            if (overallProp.TryGetProperty("Issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var issue in issuesProp.EnumerateArray())
                                {
                                    if (issue.ValueKind == JsonValueKind.String)
                                    {
                                        validationIssues.Add(issue.GetString() ?? "");
                                    }
                                }
                            }
                        }
                        
                        // Log individual validation results
                        if (type == "be")
                        {
                            if (jsonElement.TryGetProperty("GitHub", out var githubProp))
                            {
                                var githubValid = githubProp.TryGetProperty("Valid", out var gv) && gv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] GitHub validation: {Valid}", githubValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("Railway", out var railwayProp))
                            {
                                var railwayValid = railwayProp.TryGetProperty("Valid", out var rv) && rv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Railway validation: {Valid}", railwayValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("Database", out var dbProp))
                            {
                                var dbValid = dbProp.TryGetProperty("Valid", out var dv) && dv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Database validation: {Valid}", dbValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("CodeStructure", out var codeProp))
                            {
                                var codeValid = codeProp.TryGetProperty("Valid", out var cv) && cv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Code structure validation: {Valid}", codeValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("BuildStatus", out var buildProp))
                            {
                                var buildValid = buildProp.TryGetProperty("Valid", out var bv) && bv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Build status validation: {Valid}", buildValid ? "PASSED" : "FAILED");
                            }
                        }
                        else // Frontend
                        {
                            if (jsonElement.TryGetProperty("GitHub", out var githubProp))
                            {
                                var githubValid = githubProp.TryGetProperty("Valid", out var gv) && gv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] GitHub validation: {Valid}", githubValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("Config", out var configProp))
                            {
                                var configValid = configProp.TryGetProperty("Valid", out var cv) && cv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Config validation: {Valid}", configValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("Deployment", out var deployProp))
                            {
                                var deployValid = deployProp.TryGetProperty("Valid", out var dv) && dv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Deployment validation: {Valid}", deployValid ? "PASSED" : "FAILED");
                            }
                            if (jsonElement.TryGetProperty("BuildStatus", out var buildProp))
                            {
                                var buildValid = buildProp.TryGetProperty("Valid", out var bv) && bv.GetBoolean();
                                _logger.LogInformation("🔍 [VALIDATE-PR] Build status validation: {Valid}", buildValid ? "PASSED" : "FAILED");
                            }
                        }
                    }
                    else
                    {
                        // If Value is null, check if it's an error result
                        _logger.LogWarning("❌ [VALIDATE-PR] Validation result Value is null, assuming invalid");
                        isValid = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [VALIDATE-PR] Failed to parse validation result, assuming invalid: {Error}", ex.Message);
                    isValid = false;
                }
                
                if (validationIssues.Any())
                {
                    _logger.LogWarning("❌ [VALIDATE-PR] Validation found {IssueCount} issues:", validationIssues.Count);
                    foreach (var issue in validationIssues)
                    {
                        _logger.LogWarning("  - {Issue}", issue);
                    }
                }

                if (isValid)
                {
                    _logger.LogInformation("✅ [VALIDATE-PR] All validations PASSED");
                    _logger.LogInformation("🔍 [VALIDATE-PR] Step 6: Running code review");
                    
                    // Success: Call code-review endpoint (internal call with source="GitHub-Success-PR")
                    var codeReviewRequest = new CodeReviewRequest
                    {
                        BoardId = board.Id,
                        GithubBranch = branchName,
                        AIServiceName = null
                    };

                    string feedback = "Code review completed successfully.";
                    try
                    {
                        var codeReviewActionResult = await CodeReviewInternal(codeReviewRequest, "GitHub-Success-PR");
                        
                        // Get feedback from BoardStates (handle sequenced sources like "GitHub-Success-PR-1", "GitHub-Success-PR-2")
                        // Fetch all matching records and filter by base source in memory
                        var baseSource = "GitHub-Success-PR";
                        var allCodeReviewStates = await _context.BoardStates
                            .Where(bs => bs.BoardId == board.Id && 
                                        bs.ServiceName == "CodeReview" && 
                                        bs.GithubBranch == branchName &&
                                        (bs.Source.StartsWith(baseSource + "-") || bs.Source == baseSource))
                            .ToListAsync();
                        
                        var codeReviewState = allCodeReviewStates
                            .Where(bs => ExtractBaseSource(bs.Source) == baseSource)
                            .OrderByDescending(bs => bs.CreatedAt)
                            .FirstOrDefault();

                        feedback = codeReviewState?.MentorFeedback ?? feedback;
                        _logger.LogInformation("✅ [VALIDATE-PR] Code review completed");
                    }
                    catch (Exception codeReviewEx)
                    {
                        _logger.LogWarning(codeReviewEx, "⚠️ [VALIDATE-PR] Code review failed (non-critical): {Error}", codeReviewEx.Message);
                        // Continue - code review failure doesn't block validation success
                    }

                    // Step 6: Post AI Feedback to GitHub PR comment BEFORE setting success status
                    // Use issue_number from query parameter or extract from BoardStates
                    int? prNumber = issueNumber;
                    
                    if (!prNumber.HasValue)
                    {
                        _logger.LogInformation("🔍 [VALIDATE-PR] Issue number not provided, attempting to get from BoardStates");
                        var prWebhook = await _context.BoardStates
                            .Where(bs => bs.Source == "GitHub" && bs.ServiceName == "pull_request" && bs.GithubBranch == branchName && bs.LatestCommitId == sha)
                            .OrderByDescending(bs => bs.CreatedAt)
                            .FirstOrDefaultAsync();

                        // Extract PR number from LatestEvent
                        if (prWebhook != null && !string.IsNullOrEmpty(prWebhook.LatestEvent))
                        {
                            // LatestEvent format: "pull_request_opened_#123" or "pull_request_synchronize_#123"
                            var parts = prWebhook.LatestEvent.Split('#');
                            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedPrNumber))
                            {
                                prNumber = parsedPrNumber;
                                _logger.LogInformation("🔍 [VALIDATE-PR] Found PR number from BoardStates: {PRNumber}", prNumber);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("🔍 [VALIDATE-PR] Using PR number from query parameter: {PRNumber}", prNumber);
                    }

                    // Post comment to GitHub PR BEFORE setting success status
                    if (prNumber.HasValue)
                    {
                        _logger.LogInformation("🔍 [VALIDATE-PR] Step 6: Posting feedback comment to PR #{PRNumber}", prNumber);
                        try
                        {
                            await _githubService.AddPullRequestCommentAsync(owner, repoName, prNumber.Value, feedback, accessToken);
                            _logger.LogInformation("✅ [VALIDATE-PR] Posted comment to PR #{PRNumber}", prNumber);
                        }
                        catch (Exception commentEx)
                        {
                            _logger.LogError(commentEx, "❌ [VALIDATE-PR] Failed to add PR comment: {Error}", commentEx.Message);
                            // Continue - comment failure doesn't block success status, but log it
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [VALIDATE-PR] PR number not available, skipping comment post");
                    }

                    // Step 7: Update GitHub Status to "success" ONLY after comment is posted
                    _logger.LogInformation("🔍 [VALIDATE-PR] Step 7: Setting status to 'success' (after comment posted)");
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "success", validationContext,
                            "Validation passed!", accessToken);
                        statusSet = true;
                        _logger.LogInformation("✅ [VALIDATE-PR] Status set to 'success' - Status check will CLEAR");
                        _logger.LogInformation("🔍 [VALIDATE-PR] ===== Validation COMPLETED SUCCESSFULLY =====");
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set success status: {Error}", statusEx.Message);
                        throw; // Re-throw to be caught by outer catch
                    }

                    return Ok(new { Success = true, Message = "Validation passed", Feedback = feedback });
                }
                else
                {
                    var failureMessage = validationIssues.Any() 
                        ? $"Validation failed: {string.Join("; ", validationIssues.Take(3))}" 
                        : "Check the Board for feedback.";
                    
                    _logger.LogWarning("❌ [VALIDATE-PR] Validation FAILED - Status check will NOT clear");
                    _logger.LogWarning("❌ [VALIDATE-PR] Failure reason: {Message}", failureMessage);
                    if (validationIssues.Any())
                    {
                        _logger.LogWarning("❌ [VALIDATE-PR] Failed validations: {Issues}", string.Join(" | ", validationIssues));
                    }
                    
                    _logger.LogInformation("🔍 [VALIDATE-PR] Step 7: Setting status to 'failure'");
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            failureMessage, accessToken);
                        statusSet = true;
                        _logger.LogInformation("✅ [VALIDATE-PR] Status set to 'failure'");
                        _logger.LogInformation("🔍 [VALIDATE-PR] ===== Validation COMPLETED WITH FAILURES =====");
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Failed to set failure status: {Error}", statusEx.Message);
                        throw; // Re-throw to be caught by outer catch
                    }
                    
                    return Ok(new { Success = false, Message = "Validation failed", Issues = validationIssues });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [VALIDATE-PR] ===== EXCEPTION during validation =====");
                _logger.LogError(ex, "❌ [VALIDATE-PR] Repository: {Repo}, SHA: {Sha}, Type: {Type}", repoName, sha, type);
                _logger.LogError(ex, "❌ [VALIDATE-PR] Exception type: {ExceptionType}, Message: {Message}", ex.GetType().Name, ex.Message);
                _logger.LogError(ex, "❌ [VALIDATE-PR] Stack trace: {StackTrace}", ex.StackTrace);
                
                // Always try to set a failure status if we haven't set a final status yet
                if (!statusSet || statusSet) // Always try to set final status on exception
                {
                    try
                    {
                        var errorDescription = $"Validation error: {ex.GetType().Name} - {ex.Message}";
                        if (errorDescription.Length > 140) // GitHub status description limit
                        {
                            errorDescription = errorDescription.Substring(0, 137) + "...";
                        }
                        
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            errorDescription, accessToken);
                        _logger.LogInformation("✅ [VALIDATE-PR] Set failure status due to exception");
                        _logger.LogInformation("🔍 [VALIDATE-PR] ===== Validation FAILED due to exception =====");
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] CRITICAL: Failed to set failure status after exception: {Error}", statusEx.Message);
                        _logger.LogError(statusEx, "❌ [VALIDATE-PR] Status check may remain 'Waiting for status to be reported'");
                    }
                }
                
                return StatusCode(500, new { Success = false, Message = $"Error validating PR: {ex.Message}", ExceptionType = ex.GetType().Name });
            }
        }

        /// <summary>
        /// Shared validation service - processes PR validation, code review, and status updates
        /// This method is used by both the webhook and the platform-triggered endpoint
        /// </summary>
        private async Task<(bool Success, string? Feedback, string? ErrorMessage)> ProcessValidationAsync(
            string repoName, 
            string branchName, 
            string sha, 
            string boardId, 
            bool isBackend, 
            int? issueNumber = null,
            bool isWebhook = false)
        {
            var accessToken = _configuration["GitHub:AccessToken"];
            var owner = _configuration["GitHub:Organization"] ?? "";
            var validationContext = _configuration["GitHub:ValidationContext"] ?? "Mentor-Validation";
            var type = isBackend ? "be" : "fe";
            var statusSet = false;

            try
            {
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(owner))
                {
                    _logger.LogError("🔍 [SHARED VALIDATION] GitHub configuration missing");
                    return (false, null, "GitHub configuration is missing");
                }

                // Step 1: Set status to pending
                _logger.LogInformation("🔍 [SHARED VALIDATION] Step 1: Setting status to 'pending'");
                try
                {
                    await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "pending", validationContext,
                        "Mentor is analyzing your code...", accessToken);
                    statusSet = true;
                }
                catch (Exception statusEx)
                {
                    _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set status to 'pending': {Error}", statusEx.Message);
                }

                // Step 2: Branch naming validation
                _logger.LogInformation("🔍 [SHARED VALIDATION] Step 2: Validating branch naming");
                var patternKey = type == "be" ? "GitHub:BranchNamingPatterns:Backend" : "GitHub:BranchNamingPatterns:Frontend";
                var pattern = _configuration[patternKey] ?? (type == "be" ? "^([1-9]|1[0-9]|20)-B$" : "^([1-9]|1[0-9]|20)-F$");
                var regex = new System.Text.RegularExpressions.Regex(pattern);
                
                if (!regex.IsMatch(branchName))
                {
                    _logger.LogWarning("❌ [SHARED VALIDATION] Branch naming validation FAILED: Branch '{Branch}' does not match pattern '{Pattern}'", branchName, pattern);
                    
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            $"Invalid branch name: '{branchName}' does not match pattern '{pattern}'", accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set failure status: {Error}", statusEx.Message);
                    }

                    // Record in BoardStates
                    try
                    {
                        var boardState = new BoardState
                        {
                            BoardId = boardId,
                            Source = type == "be" ? "PR-BackendValidation" : "PR-FrontendValidation",
                            Webhook = false,
                            ServiceName = "BranchNamingValidation",
                            ErrorMessage = $"Invalid branch name naming convention. Branch: '{branchName}', Expected pattern: '{pattern}'",
                            GithubBranch = branchName,
                            DevRole = DetermineDevRole(type == "be" ? "PR-BackendValidation" : "PR-FrontendValidation", branchName),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.BoardStates.Add(boardState);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [SHARED VALIDATION] Failed to record branch naming failure in BoardStates");
                    }

                    return (false, null, $"Branch naming validation failed: '{branchName}' does not match pattern '{pattern}'");
                }

                _logger.LogInformation("✅ [SHARED VALIDATION] Branch naming validation PASSED");

                // Step 3: Execute validation
                _logger.LogInformation("🔍 [SHARED VALIDATION] Step 3: Running {Type} validation", type == "be" ? "Backend" : "Frontend");
                var board = await _context.ProjectBoards.FirstOrDefaultAsync(pb => pb.Id == boardId);
                if (board == null)
                {
                    _logger.LogError("❌ [SHARED VALIDATION] Board not found: {BoardId}", boardId);
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            "Board not found", accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set failure status: {Error}", statusEx.Message);
                    }
                    return (false, null, "Board not found");
                }

                var validationStartTime = DateTime.UtcNow;
                ActionResult<object> validationResult;
                
                try
                {
                    validationResult = type == "be" 
                        ? await ValidateBackend(board.Id, branchName)
                        : await ValidateFrontend(board.Id, branchName);
                    
                    var validationDuration = (DateTime.UtcNow - validationStartTime).TotalSeconds;
                    _logger.LogInformation("✅ [SHARED VALIDATION] Validation completed in {Duration} seconds", validationDuration);
                }
                catch (Exception validationEx)
                {
                    _logger.LogError(validationEx, "❌ [SHARED VALIDATION] Exception during validation: {Error}", validationEx.Message);
                    throw;
                }

                // Step 4: Parse validation result
                _logger.LogInformation("🔍 [SHARED VALIDATION] Step 4: Parsing validation result");
                bool isValid = false;
                List<string> validationIssues = new List<string>();
                
                try
                {
                    // Extract value from ActionResult - when called directly, Value may be null, so check Result property
                    object? resultValue = validationResult.Value;
                    
                    if (resultValue == null && validationResult.Result != null)
                    {
                        _logger.LogInformation("🔍 [SHARED VALIDATION] Value is null, extracting from Result property. Result type: {ResultType}", 
                            validationResult.Result.GetType().Name);
                        
                        // If Value is null, try to extract from Result (OkObjectResult, etc.)
                        if (validationResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult okResult)
                        {
                            resultValue = okResult.Value;
                            _logger.LogInformation("🔍 [SHARED VALIDATION] Extracted value from OkObjectResult");
                        }
                        else if (validationResult.Result is Microsoft.AspNetCore.Mvc.ObjectResult objectResult)
                        {
                            resultValue = objectResult.Value;
                            _logger.LogInformation("🔍 [SHARED VALIDATION] Extracted value from ObjectResult");
                        }
                    }
                    
                    if (resultValue == null)
                    {
                        _logger.LogWarning("❌ [SHARED VALIDATION] Validation result value is NULL. Result type: {ResultType}, Value type: {ValueType}", 
                            validationResult.Result?.GetType().Name ?? "null", 
                            validationResult.Value?.GetType().Name ?? "null");
                        isValid = false;
                    }
                    else
                    {
                        var jsonString = JsonSerializer.Serialize(resultValue);
                        _logger.LogInformation("🔍 [SHARED VALIDATION] Validation result JSON: {JsonResult}", jsonString);
                        
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                        
                        // Log all top-level properties to understand the structure
                        _logger.LogInformation("🔍 [SHARED VALIDATION] Top-level properties: {Properties}", 
                            string.Join(", ", jsonElement.EnumerateObject().Select(p => p.Name)));
                        
                        if (jsonElement.TryGetProperty("Overall", out var overallProp))
                        {
                            _logger.LogInformation("🔍 [SHARED VALIDATION] Found 'Overall' property, type: {ValueKind}", overallProp.ValueKind);
                            
                            // Log all properties within Overall
                            if (overallProp.ValueKind == JsonValueKind.Object)
                            {
                                var overallProps = overallProp.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}").ToList();
                                _logger.LogInformation("🔍 [SHARED VALIDATION] 'Overall' properties: {Properties}", string.Join(", ", overallProps));
                            }
                            
                            if (overallProp.TryGetProperty("Valid", out var validProp))
                            {
                                isValid = validProp.GetBoolean();
                                _logger.LogInformation("✅ [SHARED VALIDATION] Found 'Overall.Valid' = {IsValid}", isValid);
                            }
                            else
                            {
                                _logger.LogWarning("❌ [SHARED VALIDATION] 'Overall.Valid' property NOT FOUND in validation result");
                            }
                            
                            if (overallProp.TryGetProperty("Issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var issue in issuesProp.EnumerateArray())
                                {
                                    if (issue.ValueKind == JsonValueKind.String)
                                    {
                                        validationIssues.Add(issue.GetString() ?? "");
                                    }
                                }
                                _logger.LogInformation("🔍 [SHARED VALIDATION] Found {Count} validation issues", validationIssues.Count);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("❌ [SHARED VALIDATION] 'Overall' property NOT FOUND in validation result. Available properties: {Properties}", 
                                string.Join(", ", jsonElement.EnumerateObject().Select(p => p.Name)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [SHARED VALIDATION] Failed to parse validation result: {Error}", ex.Message);
                    isValid = false;
                }

                _logger.LogInformation("🔍 [SHARED VALIDATION] Final validation result: isValid={IsValid}, IssuesCount={IssuesCount}", 
                    isValid, validationIssues.Count);
                
                if (isValid)
                {
                    _logger.LogInformation("✅ [SHARED VALIDATION] All validations PASSED");
                    _logger.LogInformation("🔍 [SHARED VALIDATION] Step 5: Running code review");
                    
                    // Deduplication: Check if a recent "GitHub-Success-PR" record was just created (within last 30 seconds)
                    // This prevents duplicate code reviews when both POST /api/Mentor/use/github-pr and webhook process the same PR
                    // We use a 30-second window to catch records created recently, accounting for processing time
                    var recentCutoff = DateTime.UtcNow.AddSeconds(-30);
                    var recentRecord = await _context.BoardStates
                        .Where(bs => bs.BoardId == board.Id && 
                                    bs.GithubBranch == branchName &&
                                    bs.ServiceName == "CodeReview" &&
                                    (bs.Source.StartsWith("GitHub-Success-PR-") || bs.Source == "GitHub-Success-PR"))
                        .OrderByDescending(bs => bs.CreatedAt)
                        .FirstOrDefaultAsync();
                    
                    string feedback = "Code review completed successfully.";
                    bool codeReviewSuccess = false;
                    string? codeReviewError = null;
                    
                    if (recentRecord != null && recentRecord.CreatedAt >= recentCutoff)
                    {
                        var baseSource = ExtractBaseSource(recentRecord.Source);
                        if (baseSource == "GitHub-Success-PR")
                        {
                            _logger.LogInformation("⏭️ [SHARED VALIDATION] Skipping duplicate code review - recent record found: Source={Source}, CreatedAt={CreatedAt}, Webhook={Webhook}", 
                                recentRecord.Source, recentRecord.CreatedAt, recentRecord.Webhook);
                            feedback = recentRecord.MentorFeedback ?? "Code review completed successfully.";
                            codeReviewSuccess = true;
                        }
                    }
                    
                    if (!codeReviewSuccess)
                    {
                        // Success: Call code-review endpoint
                        var codeReviewRequest = new CodeReviewRequest
                        {
                            BoardId = board.Id,
                            GithubBranch = branchName,
                            AIServiceName = null
                        };
                        
                        try
                        {
                            var codeReviewActionResult = await CodeReviewInternal(codeReviewRequest, "GitHub-Success-PR", isWebhook);
                        
                            // Extract result from ActionResult
                            object? codeReviewResult = null;
                            if (codeReviewActionResult.Value != null)
                            {
                                codeReviewResult = codeReviewActionResult.Value;
                            }
                            else if (codeReviewActionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult okResult)
                            {
                                codeReviewResult = okResult.Value;
                            }
                            else if (codeReviewActionResult.Result is Microsoft.AspNetCore.Mvc.ObjectResult objectResult)
                            {
                                codeReviewResult = objectResult.Value;
                            }
                            
                            if (codeReviewResult != null)
                            {
                                var jsonString = JsonSerializer.Serialize(codeReviewResult);
                                var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                                
                                // Check if code review was successful
                                if (jsonElement.TryGetProperty("Success", out var successProp))
                                {
                                    codeReviewSuccess = successProp.GetBoolean();
                                    
                                    if (codeReviewSuccess)
                                    {
                                        // Extract feedback if available
                                        if (jsonElement.TryGetProperty("Feedback", out var feedbackProp))
                                        {
                                            feedback = feedbackProp.GetString() ?? feedback;
                                        }
                                        
                                        // Also check BoardStates as fallback (handle sequenced sources)
                                        var baseSource = "GitHub-Success-PR";
                                        var allCodeReviewStates = await _context.BoardStates
                                            .Where(bs => bs.BoardId == board.Id && 
                                                        bs.ServiceName == "CodeReview" && 
                                                        bs.GithubBranch == branchName &&
                                                        (bs.Source.StartsWith(baseSource + "-") || bs.Source == baseSource))
                                            .ToListAsync();
                                        
                                        var codeReviewState = allCodeReviewStates
                                            .Where(bs => ExtractBaseSource(bs.Source) == baseSource)
                                            .OrderByDescending(bs => bs.CreatedAt)
                                            .FirstOrDefault();
                                        
                                        if (codeReviewState?.MentorFeedback != null)
                                        {
                                            feedback = codeReviewState.MentorFeedback;
                                        }
                                        
                                        _logger.LogInformation("✅ [SHARED VALIDATION] Code review completed successfully");
                                        
                                        // Verify the record was actually created
                                        var verifyRecord = await _context.BoardStates
                                            .Where(bs => bs.BoardId == board.Id && 
                                                        bs.ServiceName == "CodeReview" && 
                                                        bs.GithubBranch == branchName &&
                                                        (bs.Source.StartsWith("GitHub-Success-PR-") || bs.Source == "GitHub-Success-PR") &&
                                                        bs.Webhook == isWebhook)
                                            .OrderByDescending(bs => bs.CreatedAt)
                                            .FirstOrDefaultAsync();
                                        
                                        if (verifyRecord != null)
                                        {
                                            _logger.LogInformation("✅ [SHARED VALIDATION] Verified GitHub-Success-PR record created: Source={Source}, DevRole={DevRole}, Webhook={Webhook}", 
                                                verifyRecord.Source, verifyRecord.DevRole, verifyRecord.Webhook);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("⚠️ [SHARED VALIDATION] Code review reported success but GitHub-Success-PR record not found in database!");
                                        }
                                    }
                                    else
                                    {
                                        // Extract error message
                                        if (jsonElement.TryGetProperty("Message", out var messageProp))
                                        {
                                            codeReviewError = messageProp.GetString() ?? "Code review failed";
                                        }
                                        else
                                        {
                                            codeReviewError = "Code review failed";
                                        }
                                        
                                        _logger.LogWarning("❌ [SHARED VALIDATION] Code review failed: {Error}", codeReviewError);
                                    }
                                }
                                else
                                {
                                    // If Success property not found, assume failure
                                    codeReviewError = "Code review response missing Success property";
                                    _logger.LogWarning("❌ [SHARED VALIDATION] Code review response missing Success property");
                                }
                            }
                            else
                            {
                                codeReviewError = "Code review returned null result";
                                _logger.LogWarning("❌ [SHARED VALIDATION] Code review returned null result");
                            }
                        }
                        catch (Exception codeReviewEx)
                        {
                            codeReviewError = $"Code review exception: {codeReviewEx.Message}";
                            _logger.LogError(codeReviewEx, "❌ [SHARED VALIDATION] Code review failed with exception: {Error}", codeReviewEx.Message);
                        }
                    }
                    
                    // If code review failed, mark validation as failed
                    if (!codeReviewSuccess)
                    {
                        var failureMessage = codeReviewError ?? "Code review failed";
                        _logger.LogWarning("❌ [SHARED VALIDATION] Validation FAILED due to code review failure: {Error}", failureMessage);
                        
                        try
                        {
                            // GitHub status description has a 140 character limit - truncate if needed
                            var statusDescription = failureMessage.Length > 140 
                                ? failureMessage.Substring(0, 137) + "..." 
                                : failureMessage;
                            
                            await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                                statusDescription, accessToken);
                            statusSet = true;
                        }
                        catch (Exception statusEx)
                        {
                            _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set failure status: {Error}", statusEx.Message);
                            throw;
                        }
                        
                        // No record created for failed PR validation
                        // Records are only created for successful reviews (GitHub-Success-PR or Junior)
                        _logger.LogInformation("ℹ️ [SHARED VALIDATION] Code review failed - no record created (only success records are logged)");
                        
                        // Return failure with code review error as feedback
                        return (false, failureMessage, failureMessage);
                    }

                    // Post comment to GitHub PR BEFORE setting success status
                    if (issueNumber.HasValue)
                    {
                        _logger.LogInformation("🔍 [SHARED VALIDATION] Step 6: Posting feedback comment to PR #{PRNumber}", issueNumber);
                        try
                        {
                            await _githubService.AddPullRequestCommentAsync(owner, repoName, issueNumber.Value, feedback, accessToken);
                            _logger.LogInformation("✅ [SHARED VALIDATION] Posted comment to PR #{PRNumber}", issueNumber);
                        }
                        catch (Exception commentEx)
                        {
                            _logger.LogError(commentEx, "❌ [SHARED VALIDATION] Failed to add PR comment: {Error}", commentEx.Message);
                        }
                    }

                    // Step 7: Update GitHub Status to "success" ONLY after comment is posted
                    _logger.LogInformation("🔍 [SHARED VALIDATION] Step 7: Setting status to 'success'");
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "success", validationContext,
                            "Validation passed!", accessToken);
                        statusSet = true;
                        _logger.LogInformation("✅ [SHARED VALIDATION] Status set to 'success'");
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set success status: {Error}", statusEx.Message);
                        throw;
                    }

                    // Note: The successful PR record is created by the CodeReview method with Source="GitHub-Success-PR", ServiceName="CodeReview", PRStatus="Approved"
                    return (true, feedback, null);
                }
                else
                {
                    var failureMessage = validationIssues.Any() 
                        ? $"Validation failed: {string.Join("; ", validationIssues.Take(3))}" 
                        : "Check the Board for feedback.";
                    
                    _logger.LogWarning("❌ [SHARED VALIDATION] Validation FAILED - isValid={IsValid}, IssuesCount={IssuesCount}, FailureMessage={FailureMessage}", 
                        isValid, validationIssues.Count, failureMessage);
                    try
                    {
                        await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                            failureMessage, accessToken);
                        statusSet = true;
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogError(statusEx, "❌ [SHARED VALIDATION] Failed to set failure status: {Error}", statusEx.Message);
                        throw;
                    }
                    
                    // No record created for failed PR validation
                    // Records are only created for successful reviews (GitHub-Success-PR or Junior)
                    _logger.LogInformation("ℹ️ [SHARED VALIDATION] PR validation failed - no record created (only success records are logged)");
                    
                    return (false, null, failureMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SHARED VALIDATION] Exception during validation");
                
                // Always try to set a failure status
                try
                {
                    var errorDescription = $"Validation error: {ex.GetType().Name} - {ex.Message}";
                    if (errorDescription.Length > 140)
                    {
                        errorDescription = errorDescription.Substring(0, 137) + "...";
                    }
                    
                    await _githubService.UpdateCommitStatusAsync(owner, repoName, sha, "failure", validationContext,
                        errorDescription, accessToken);
                }
                catch (Exception statusEx)
                {
                    _logger.LogError(statusEx, "❌ [SHARED VALIDATION] CRITICAL: Failed to set failure status after exception");
                }
                
                return (false, null, $"Error validating PR: {ex.Message}");
            }
        }

        /// <summary>
        /// Platform-triggered PR validation endpoint
        /// Route: POST /api/Mentor/use/github-pr
        /// Parameters: boardId, branchName, isBackend
        /// </summary>
        [HttpPost("use/github-pr")]
        public async Task<ActionResult<object>> GitHubPRValidation([FromBody] GitHubPRValidationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest(new { Success = false, Message = "boardId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.BranchName))
                {
                    return BadRequest(new { Success = false, Message = "branchName is required" });
                }

                _logger.LogInformation("📥 [GITHUB-PR] Platform-triggered validation: BoardId={BoardId}, Branch={Branch}, IsBackend={IsBackend}",
                    request.BoardId, request.BranchName, request.IsBackend);

                // Get board to find GitHub repo info
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);

                if (board == null)
                {
                    return NotFound(new { Success = false, Message = $"Board with ID {request.BoardId} not found" });
                }

                // Determine GitHub URL based on isBackend
                var githubUrl = request.IsBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;
                if (string.IsNullOrEmpty(githubUrl))
                {
                    return BadRequest(new { Success = false, Message = $"GitHub {(request.IsBackend ? "backend" : "frontend")} URL not found for board" });
                }

                // Extract owner and repo from GitHub URL
                var uri = new Uri(githubUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = $"Invalid GitHub URL format: {githubUrl}" });
                }

                var owner = pathParts[0];
                var repo = pathParts[1];
                var accessToken = _configuration["GitHub:AccessToken"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return StatusCode(500, new { Success = false, Message = "GitHub access token is not configured" });
                }

                // SHA Lookup: Get the latest commit SHA for the branch
                _logger.LogInformation("📥 [GITHUB-PR] Looking up SHA for branch {Branch} in {Owner}/{Repo}", request.BranchName, owner, repo);
                var sha = await _githubService.GetBranchShaAsync(owner, repo, request.BranchName, accessToken);

                if (string.IsNullOrEmpty(sha))
                {
                    _logger.LogError("❌ [GITHUB-PR] Failed to get SHA for branch {Branch}", request.BranchName);
                    return BadRequest(new { Success = false, Message = $"Failed to get SHA for branch '{request.BranchName}'. Branch may not exist." });
                }

                _logger.LogInformation("✅ [GITHUB-PR] Found SHA {Sha} for branch {Branch}", sha, request.BranchName);

                // Check if PR exists (but DON'T create it yet - only create AFTER validation passes)
                _logger.LogInformation("📥 [GITHUB-PR] Checking if PR exists for branch {Branch}", request.BranchName);
                var existingPR = await _githubService.GetPullRequestByBranchAsync(owner, repo, request.BranchName, accessToken);
                int? prNumber = null;

                if (existingPR != null && existingPR.Number > 0)
                {
                    prNumber = existingPR.Number;
                    _logger.LogInformation("✅ [GITHUB-PR] Found existing PR #{PRNumber} for branch {Branch}", prNumber, request.BranchName);
                }

                // Call shared validation service FIRST (before creating PR)
                // Pass prNumber if available for PR comment posting
                var (success, feedback, errorMessage) = await ProcessValidationAsync(
                    repo, 
                    request.BranchName, 
                    sha, 
                    request.BoardId, 
                    request.IsBackend, 
                    issueNumber: prNumber);

                // Only create PR if validation PASSED
                if (success && (existingPR == null || existingPR.Number == 0))
                {
                    _logger.LogInformation("📥 [GITHUB-PR] Validation passed, creating PR for branch {Branch}", request.BranchName);
                    var prTitle = $"Sprint Task {request.BranchName}";
                    var prBody = $"Pull request for sprint task {request.BranchName}.\n\nThis PR was automatically created by the validation system.";
                    var newPR = await _githubService.CreatePullRequestAsync(owner, repo, request.BranchName, "main", prTitle, prBody, accessToken);
                    
                    if (newPR != null && newPR.Number > 0)
                    {
                        prNumber = newPR.Number;
                        _logger.LogInformation("✅ [GITHUB-PR] Created PR #{PRNumber} for branch {Branch} after successful validation", prNumber, request.BranchName);
                        
                        // When PR is created, update existing "GitHub-Merge" record if it exists
                        try
                        {
                            var existingMergeRecord = await _context.BoardStates
                                .FirstOrDefaultAsync(bs => bs.BoardId == request.BoardId && 
                                                           bs.Source == "GitHub-Merge" && 
                                                           bs.GithubBranch == request.BranchName);
                            
                            if (existingMergeRecord != null)
                            {
                                existingMergeRecord.PRStatus = "Re-opened";
                                existingMergeRecord.BranchStatus = "Un-Merged";
                                existingMergeRecord.Webhook = false; // Platform-triggered, not webhook
                                existingMergeRecord.UpdatedAt = DateTime.UtcNow;
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("✅ [GITHUB-PR] Updated GitHub-Merge record to Re-opened for PR #{PRNumber}, Branch: {Branch}", prNumber, request.BranchName);
                            }
                        }
                        catch (Exception mergeUpdateEx)
                        {
                            _logger.LogWarning(mergeUpdateEx, "⚠️ [GITHUB-PR] Failed to update GitHub-Merge record for PR #{PRNumber} (non-critical)", prNumber);
                        }
                    }
                    else
                    {
                        // PR creation failed AFTER validation passed - this is unexpected but log it
                        _logger.LogError("❌ [GITHUB-PR] Validation passed but failed to create PR for branch {Branch}", request.BranchName);
                        // Don't fail the request - validation already passed, PR creation is secondary
                    }
                }
                else if (!success && (existingPR == null || existingPR.Number == 0))
                {
                    // Validation failed and no PR exists - this is expected, don't create PR
                    _logger.LogInformation("ℹ️ [GITHUB-PR] Validation failed, skipping PR creation for branch {Branch}", request.BranchName);
                }

                if (success)
                {
                    return Ok(new 
                    { 
                        Success = true, 
                        Message = "Validation passed", 
                        Feedback = feedback,
                        CodeReviewFeedback = feedback, // AI response for code review
                        CodeReviewStatus = "success",
                        Sha = sha 
                    });
                }
                else
                {
                    // Check if failure was due to code review (feedback will contain code review error)
                    var isCodeReviewFailure = !string.IsNullOrEmpty(feedback) && 
                                             (errorMessage?.Contains("Code review") == true || 
                                              errorMessage?.Contains("Trello card") == true ||
                                              feedback.Contains("Code review") == true ||
                                              feedback.Contains("Trello card") == true);
                    
                    return Ok(new 
                    { 
                        Success = false, 
                        Message = errorMessage ?? "Validation failed",
                        Feedback = feedback, // Code review error message if code review failed
                        CodeReviewFeedback = isCodeReviewFailure ? (feedback ?? errorMessage) : null,
                        CodeReviewStatus = isCodeReviewFailure ? "failed" : "not_attempted",
                        Sha = sha 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GITHUB-PR] Error processing platform-triggered validation");
                return StatusCode(500, new { Success = false, Message = $"Error validating PR: {ex.Message}" });
            }
        }

        /// <summary>
        /// Request model for platform-triggered PR validation
        /// </summary>
        public class GitHubPRValidationRequest
        {
            public string BoardId { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public bool IsBackend { get; set; }
        }

        /// <summary>
        /// Platform-triggered PR merge endpoint
        /// Route: POST /api/Mentor/use/github-merge
        /// Parameters: boardId, branchName, isBackend
        /// </summary>
        [HttpPost("use/github-merge")]
        public async Task<ActionResult<object>> GitHubPRMerge([FromBody] GitHubPRValidationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.BoardId))
                {
                    return BadRequest(new { Success = false, Message = "boardId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.BranchName))
                {
                    return BadRequest(new { Success = false, Message = "branchName is required" });
                }

                _logger.LogInformation("📥 [GITHUB-MERGE] Platform-triggered merge: BoardId={BoardId}, Branch={Branch}, IsBackend={IsBackend}",
                    request.BoardId, request.BranchName, request.IsBackend);

                // Get board to find GitHub repo info
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == request.BoardId);

                if (board == null)
                {
                    return NotFound(new { Success = false, Message = $"Board with ID {request.BoardId} not found" });
                }

                // Determine GitHub URL based on isBackend
                var githubUrl = request.IsBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;
                if (string.IsNullOrEmpty(githubUrl))
                {
                    return BadRequest(new { Success = false, Message = $"GitHub {(request.IsBackend ? "backend" : "frontend")} URL not found for board" });
                }

                // Extract owner and repo from GitHub URL
                var uri = new Uri(githubUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = $"Invalid GitHub URL format: {githubUrl}" });
                }

                var owner = pathParts[0];
                var repo = pathParts[1];
                var accessToken = _configuration["GitHub:AccessToken"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return StatusCode(500, new { Success = false, Message = "GitHub access token is not configured" });
                }

                // Phase 1: Identify the Pull Request
                _logger.LogInformation("📥 [GITHUB-MERGE] Phase 1: Finding open PR for branch {Branch}", request.BranchName);
                var pr = await _githubService.GetPullRequestByBranchAsync(owner, repo, request.BranchName, accessToken);

                if (pr == null || pr.Number == 0)
                {
                    _logger.LogWarning("❌ [GITHUB-MERGE] No open PR found for branch {Branch}", request.BranchName);
                    return NotFound(new { Success = false, Message = $"No open pull request found for branch '{request.BranchName}'" });
                }

                _logger.LogInformation("✅ [GITHUB-MERGE] Found PR #{PRNumber} for branch {Branch}", pr.Number, request.BranchName);

                // Phase 2: Security Check (Pre-Merge)
                _logger.LogInformation("📥 [GITHUB-MERGE] Phase 2: Performing security checks");

                // Check mergeability - if null, GitHub hasn't computed it yet, so we'll poll for it
                if (pr.Mergeable == null)
                {
                    _logger.LogInformation("⏳ [GITHUB-MERGE] PR #{PRNumber} mergeable status is null, waiting for GitHub to compute it...", pr.Number);
                    
                    // Poll up to 5 times with 2 second delays (max 10 seconds wait)
                    const int maxRetries = 5;
                    const int delaySeconds = 2;
                    bool mergeableComputed = false;
                    
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        _logger.LogInformation("🔄 [GITHUB-MERGE] Refetching PR #{PRNumber} to check mergeable status (attempt {Attempt}/{MaxRetries})", 
                            pr.Number, attempt, maxRetries);
                        
                        var refreshedPR = await _githubService.GetPullRequestByBranchAsync(owner, repo, request.BranchName, accessToken);
                        if (refreshedPR != null && refreshedPR.Number == pr.Number)
                        {
                            pr = refreshedPR; // Update with refreshed data
                            if (pr.Mergeable.HasValue)
                            {
                                mergeableComputed = true;
                                _logger.LogInformation("✅ [GITHUB-MERGE] PR #{PRNumber} mergeable status computed: {Mergeable}", pr.Number, pr.Mergeable);
                                break;
                            }
                        }
                    }
                    
                    if (!mergeableComputed)
                    {
                        _logger.LogWarning("⚠️ [GITHUB-MERGE] PR #{PRNumber} mergeable status still null after {MaxRetries} attempts. Proceeding with merge attempt (GitHub will reject if not mergeable).", 
                            pr.Number, maxRetries);
                        // Continue - GitHub API will reject the merge if it's not actually mergeable
                    }
                }

                // Only fail if mergeable is explicitly false
                if (pr.Mergeable == false)
                {
                    _logger.LogWarning("❌ [GITHUB-MERGE] PR #{PRNumber} is not mergeable (mergeable=false). This usually means there are merge conflicts.", pr.Number);
                    return StatusCode(403, new { Success = false, Message = "Pull request is not mergeable. Please resolve any conflicts first." });
                }
                
                // If mergeable is still null after polling, log a warning but proceed
                // GitHub API will reject the merge if it's not actually mergeable
                if (pr.Mergeable == null)
                {
                    _logger.LogWarning("⚠️ [GITHUB-MERGE] PR #{PRNumber} mergeable status is still null. Proceeding with merge - GitHub will reject if conflicts exist.", pr.Number);
                }

                // Check validation status
                var validationContext = _configuration["GitHub:ValidationContext"] ?? "Mentor-Validation";
                var headSha = pr.HeadSha;
                
                if (string.IsNullOrEmpty(headSha))
                {
                    // Fallback: get SHA from branch
                    headSha = await _githubService.GetBranchShaAsync(owner, repo, request.BranchName, accessToken);
                }

                if (string.IsNullOrEmpty(headSha))
                {
                    _logger.LogWarning("❌ [GITHUB-MERGE] Could not determine SHA for branch {Branch}", request.BranchName);
                    return StatusCode(500, new { Success = false, Message = "Could not determine commit SHA for branch" });
                }

                _logger.LogInformation("📥 [GITHUB-MERGE] Checking validation status for SHA {Sha} with context {Context}", headSha, validationContext);
                var commitStatus = await _githubService.GetCommitStatusAsync(owner, repo, headSha, validationContext, accessToken);

                if (commitStatus == null)
                {
                    _logger.LogWarning("❌ [GITHUB-MERGE] No validation status found for SHA {Sha} with context {Context}", headSha, validationContext);
                    return StatusCode(403, new { Success = false, Message = "Code must pass Mentor Validation before merging." });
                }

                if (commitStatus.State != "success")
                {
                    _logger.LogWarning("❌ [GITHUB-MERGE] Validation status is '{State}' (expected 'success') for SHA {Sha}", commitStatus.State, headSha);
                    return StatusCode(403, new { Success = false, Message = "Code must pass Mentor Validation before merging." });
                }

                _logger.LogInformation("✅ [GITHUB-MERGE] Security checks passed: Mergeable={Mergeable}, Validation={ValidationState}", 
                    pr.Mergeable, commitStatus.State);

                // Phase 3: Execute Merge & Update PSA
                _logger.LogInformation("📥 [GITHUB-MERGE] Phase 3: Executing merge");
                var commitTitle = $"Completed: Sprint Task {request.BranchName}";
                var mergeSuccess = await _githubService.MergePullRequestAsync(owner, repo, pr.Number, commitTitle, accessToken);

                if (!mergeSuccess)
                {
                    _logger.LogError("❌ [GITHUB-MERGE] Failed to merge PR #{PRNumber}", pr.Number);
                    return StatusCode(500, new { Success = false, Message = "Failed to merge pull request" });
                }

                _logger.LogInformation("✅ [GITHUB-MERGE] Successfully merged PR #{PRNumber}", pr.Number);

                // Update or create BoardState record for merge (key: BoardId, Source="GitHub-Merge", GithubBranch)
                try
                {
                    var existingMergeRecord = await _context.BoardStates
                        .FirstOrDefaultAsync(bs => bs.BoardId == request.BoardId && 
                                                   bs.Source == "GitHub-Merge" && 
                                                   bs.GithubBranch == request.BranchName);
                    
                    DateTime mergedAt = DateTime.UtcNow;
                    
                    if (existingMergeRecord != null)
                    {
                        // UPDATE existing "GitHub-Merge" record
                        existingMergeRecord.BranchStatus = "Completed-Merged";
                        existingMergeRecord.PRStatus = "Merged";
                        existingMergeRecord.LastBuildStatus = "Completed-Merged";
                        existingMergeRecord.LastMergeDate = mergedAt;
                        existingMergeRecord.Webhook = false; // Platform-triggered, not webhook
                        existingMergeRecord.UpdatedAt = DateTime.UtcNow;
                        
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ [GITHUB-MERGE] Updated existing GitHub-Merge record for PR #{PRNumber}, Branch: {Branch}", pr.Number, request.BranchName);
                    }
                    else
                    {
                        // CREATE new "GitHub-Merge" record
                        var mergeBoardState = new BoardState
                        {
                            BoardId = request.BoardId,
                            Source = "GitHub-Merge",
                            Webhook = false, // Platform-triggered, not webhook
                            ServiceName = "GitHubMerge",
                            BranchStatus = "Completed-Merged",
                            PRStatus = "Merged",
                            LastBuildStatus = "Completed-Merged",
                            GithubBranch = request.BranchName,
                            LastMergeDate = mergedAt,
                            DevRole = DetermineDevRole("GitHub-Merge", request.BranchName),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.BoardStates.Add(mergeBoardState);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ [GITHUB-MERGE] Created new GitHub-Merge record for PR #{PRNumber}, Branch: {Branch}", pr.Number, request.BranchName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [GITHUB-MERGE] Failed to update/create BoardState record (non-critical): {Error}", ex.Message);
                    // Continue - merge was successful, BoardState record creation is non-critical
                }

                return Ok(new { Success = true, Message = "Pull request merged successfully", PRNumber = pr.Number, BranchName = request.BranchName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GITHUB-MERGE] Error processing platform-triggered merge");
                return StatusCode(500, new { Success = false, Message = $"Error merging PR: {ex.Message}" });
            }
        }

        /// <summary>
        /// Checks if a branch exists and is unmerged (has commits not in main)
        /// </summary>
        [HttpGet("use/is-open-branch")]
        public async Task<ActionResult<object>> IsOpenBranch([FromQuery] string boardId, [FromQuery] string branchName, [FromQuery] bool isBackend)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(boardId))
                {
                    return BadRequest(new { Success = false, Message = "boardId is required" });
                }

                if (string.IsNullOrWhiteSpace(branchName))
                {
                    return BadRequest(new { Success = false, Message = "branchName is required" });
                }

                _logger.LogInformation("🔍 [IS-OPEN-BRANCH] Checking if branch '{BranchName}' is unmerged for BoardId: {BoardId}, IsBackend: {IsBackend}", 
                    branchName, boardId, isBackend);

                // Get board to find GitHub repo info
                var board = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.Id == boardId);

                if (board == null)
                {
                    return NotFound(new { Success = false, Message = $"Board with ID {boardId} not found" });
                }

                // Determine GitHub URL based on isBackend
                var githubUrl = isBackend ? board.GithubBackendUrl : board.GithubFrontendUrl;
                if (string.IsNullOrEmpty(githubUrl))
                {
                    return BadRequest(new { Success = false, Message = $"GitHub {(isBackend ? "backend" : "frontend")} URL not found for board" });
                }

                // Extract owner and repo from GitHub URL
                var uri = new Uri(githubUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = $"Invalid GitHub URL format: {githubUrl}" });
                }

                var owner = pathParts[0];
                var repo = pathParts[1];
                var accessToken = _configuration["GitHub:AccessToken"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return StatusCode(500, new { Success = false, Message = "GitHub access token is not configured" });
                }

                // Check if branch is main/master - these are considered merged (they are the base branch)
                if (branchName.Equals("main", StringComparison.OrdinalIgnoreCase) || 
                    branchName.Equals("master", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("ℹ️ [IS-OPEN-BRANCH] Branch '{BranchName}' is the default branch, considered merged", branchName);
                    return Ok(new 
                    { 
                        Success = true, 
                        BranchExists = true,
                        BranchMerged = true,
                        Message = "Branch is the default branch (main/master)" 
                    });
                }

                // Check if branch exists by getting its SHA
                var branchSha = await _githubService.GetBranchShaAsync(owner, repo, branchName, accessToken);
                
                if (string.IsNullOrEmpty(branchSha))
                {
                    _logger.LogInformation("ℹ️ [IS-OPEN-BRANCH] Branch '{BranchName}' does not exist", branchName);
                    return Ok(new 
                    { 
                        Success = true, 
                        BranchExists = false,
                        BranchMerged = false,
                        Message = "Branch does not exist" 
                    });
                }

                _logger.LogInformation("✅ [IS-OPEN-BRANCH] Branch '{BranchName}' exists with SHA: {Sha}", branchName, branchSha);

                // Check if branch is merged into main by comparing main...branch
                // Use GitHub Compare API to get ahead_by and behind_by
                var compareUrl = $"https://api.github.com/repos/{owner}/{repo}/compare/main...{branchName}";
                using var httpClient = _httpClientFactory.CreateClient("DeploymentController");
                var compareRequest = new HttpRequestMessage(HttpMethod.Get, compareUrl);
                compareRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                compareRequest.Headers.Add("User-Agent", "StrAppersBackend/1.0");
                compareRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                
                var compareResponse = await httpClient.SendAsync(compareRequest);
                int aheadBy = 0;
                int behindBy = 0;
                bool branchMerged = false;
                
                if (compareResponse.IsSuccessStatusCode)
                {
                    var compareContent = await compareResponse.Content.ReadAsStringAsync();
                    var compareData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(compareContent, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    // Extract ahead_by and behind_by from compare response
                    aheadBy = compareData.TryGetProperty("ahead_by", out var aheadProp) ? aheadProp.GetInt32() : 0;
                    behindBy = compareData.TryGetProperty("behind_by", out var behindProp) ? behindProp.GetInt32() : 0;
                    
                    // IMPORTANT: If a branch exists (and is not main/master), it is considered "unmerged" 
                    // even if its commits are in main, because the branch itself still exists and needs cleanup.
                    // This matches the logic in POST /api/Mentor/use/github-branch which blocks creation
                    // if ANY branch exists (except main/master).
                    // A branch is only "merged" if it doesn't exist OR if it's the main/master branch.
                    // Since we already checked that the branch exists and is not main/master, it's unmerged.
                    branchMerged = false;
                    
                    _logger.LogInformation("📊 [IS-OPEN-BRANCH] Branch '{BranchName}' comparison: AheadBy={AheadBy}, BehindBy={BehindBy}, BranchMerged={BranchMerged} (branch exists, so considered unmerged)", 
                        branchName, aheadBy, behindBy, branchMerged);
                }
                else
                {
                    // If compare fails, assume unmerged since branch exists and is not main
                    _logger.LogWarning("⚠️ [IS-OPEN-BRANCH] Could not compare main...{BranchName} (Status: {StatusCode}), assuming unmerged since branch exists", 
                        branchName, compareResponse.StatusCode);
                    branchMerged = false;
                }

                return Ok(new 
                { 
                    Success = true, 
                    BranchExists = true,
                    BranchMerged = branchMerged,
                    AheadBy = aheadBy,
                    BehindBy = behindBy,
                    Message = branchMerged 
                        ? $"Branch '{branchName}' is fully merged into main" 
                        : $"Branch '{branchName}' exists and is unmerged (has {aheadBy} commit(s) ahead of main)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [IS-OPEN-BRANCH] Error checking if branch is unmerged: {Message}", ex.Message);
                return StatusCode(500, new { Success = false, Message = $"Error checking branch: {ex.Message}" });
            }
        }

        /// <summary>
        /// Pushes a system message to MentorChatHistory table
        /// </summary>
        [HttpPost("use/push-mentor-system-message")]
        public async Task<ActionResult<object>> PushMentorSystemMessage([FromBody] PushMentorSystemMessageRequest request)
        {
            try
            {
                if (request.StudentId <= 0)
                {
                    return BadRequest(new { Success = false, Message = "StudentId is required and must be greater than 0" });
                }

                if (request.SprintId < 0)
                {
                    return BadRequest(new { Success = false, Message = "SprintId is required and must be greater than or equal to 0" });
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { Success = false, Message = "Message is required" });
                }

                _logger.LogInformation("💬 [PUSH-MENTOR-SYSTEM-MESSAGE] Pushing system message for StudentId: {StudentId}, SprintId: {SprintId}", 
                    request.StudentId, request.SprintId);

                // Verify student exists
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return NotFound(new { Success = false, Message = $"Student with ID {request.StudentId} not found" });
                }

                // Create and save the system message
                var systemMessage = new MentorChatHistory
                {
                    StudentId = request.StudentId,
                    SprintId = request.SprintId,
                    Role = "assistant", // System messages are stored as assistant role
                    Message = request.Message.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.MentorChatHistory.Add(systemMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ [PUSH-MENTOR-SYSTEM-MESSAGE] Successfully saved system message with Id: {MessageId} for StudentId: {StudentId}, SprintId: {SprintId}", 
                    systemMessage.Id, request.StudentId, request.SprintId);

                return Ok(new
                {
                    Success = true,
                    MessageId = systemMessage.Id,
                    StudentId = systemMessage.StudentId,
                    SprintId = systemMessage.SprintId,
                    Message = "System message successfully saved to chat history",
                    CreatedAt = systemMessage.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PUSH-MENTOR-SYSTEM-MESSAGE] Error pushing system message: {Message}", ex.Message);
                return StatusCode(500, new { Success = false, Message = $"Error pushing system message: {ex.Message}" });
            }
        }

        /// <summary>
        /// Request model for pushing a mentor system message
        /// </summary>
        public class PushMentorSystemMessageRequest
        {
            public int StudentId { get; set; }
            public int SprintId { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}

