using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text;
using System.Text.Json;
using System.IO;

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

        public MentorController(
            ApplicationDbContext context,
            ILogger<MentorController> logger,
            ITrelloService trelloService,
            IGitHubService githubService,
            IMentorIntentService intentService,
            ICodeReviewAgent codeReviewAgent,
            IOptions<PromptConfig> promptConfig,
            IOptions<TrelloConfig> trelloConfig,
            IConfiguration configuration)
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

                // E. Fetch GitHub Repository Files
                var githubFiles = new List<string>();
                object? githubCommitSummary = null;
                // Get GitHub repo from ProjectBoard's GithubUrl or Project's GitHubRepo field
                var githubRepo = student.ProjectBoard?.GithubUrl ?? "";
                if (string.IsNullOrEmpty(githubRepo))
                {
                    // Try to extract from project description or other fields if needed
                    // For now, we'll skip if no GitHub URL is available
                }
                else
                {
                    try
                    {
                        // Extract repo name from GitHub URL (format: https://github.com/owner/repo)
                        var repoName = githubRepo.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/');
                        githubFiles = await GetGitHubRepositoryFilesAsync(repoName);
                        
                        // Fetch GitHub commit summary for developer roles only
                        var isDeveloperRole = !string.IsNullOrEmpty(roleName) && 
                            (roleName.Contains("Developer", StringComparison.OrdinalIgnoreCase) || 
                             roleName.Contains("Programmer", StringComparison.OrdinalIgnoreCase) ||
                             roleName.Contains("Engineer", StringComparison.OrdinalIgnoreCase));
                        
                        if (isDeveloperRole)
                        {
                            githubCommitSummary = await GetGitHubCommitSummaryAsync(githubRepo, student.GithubUser);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch GitHub files for repo {Repo}", githubRepo);
                    }
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

                var formattedPrompt = string.Format(
                    _promptConfig.Mentor.UserPromptTemplate,
                    student.FirstName,                    // {0} - User's first name
                    roleName,                              // {1} - Role
                    sprintId,                              // {2} - Sprint number
                    sprintListName ?? $"Sprint {sprintId}", // {3} - Module name (using sprint name as module context)
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
                        GitHubCommitSummary = githubCommitSummary
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
        /// Get GitHub commit summary (recent commits with file changes summary) for mentor context
        /// Optimized to save tokens by summarizing instead of including full diffs
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
        /// Build enhanced system prompt with context information
        /// </summary>
        private string BuildEnhancedSystemPrompt(string baseSystemPrompt, JsonElement contextData)
        {
            if (contextData.ValueKind == JsonValueKind.Undefined || contextData.ValueKind == JsonValueKind.Null)
            {
                return baseSystemPrompt;
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
            
            // Build GitHub context info from commit summary (if available and developer role)
            string githubContextSection = "";
            if (isDeveloperRole && !string.IsNullOrEmpty(githubContextInfo))
            {
                var githubTemplate = _promptConfig.Mentor.EnhancedPrompt.GitHubContextTemplate ?? 
                    "GITHUB REPOSITORY STATUS:\n{0}\n\nUse this information to provide accurate, context-aware responses about the student's code and repository activity.";
                githubContextSection = $"\n\n{string.Format(githubTemplate, githubContextInfo)}";
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
                return $"{baseSystemPrompt}\n\nCURRENT CONTEXT:\n{contextInfo}{githubContextSection}{capabilitiesSection}\n\n{_promptConfig.Mentor.EnhancedPrompt.ContextReminder}";
            }

            // Even without context, add capability information if available
            if (!string.IsNullOrEmpty(capabilitiesInfo))
            {
                return $"{baseSystemPrompt}\n\n{capabilitiesInfo}";
            }
            
            return baseSystemPrompt;
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

                // Detect intent from user question
                var intent = await _intentService.DetectIntentAsync(request.UserQuestion ?? "");
                _logger.LogInformation("Detected intent: {IntentType} with confidence {Confidence}", intent.Type, intent.Confidence);

                var userQuestion = request.UserQuestion ?? "";
                
                // Check if user is responding to a file selection question (from chat history)
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
                        _logger.LogInformation("Detected file selection response: {UserQuestion}", userQuestion);
                        
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
                    _logger.LogWarning(ex, "Error checking chat history for file selection");
                }

                // Additional check: if user explicitly asks to review code, force code_review intent
                // OR if user is selecting a file from a previous file list, force code_review intent
                // OR if user mentions a file name (likely selecting a file), force code_review intent
                var lowerQuestion = userQuestion.ToLowerInvariant();
                var isMentioningFileName = lowerQuestion.Contains(".cs") || lowerQuestion.Contains(".js") || 
                                          lowerQuestion.Contains(".ts") || lowerQuestion.Contains(".py") ||
                                          lowerQuestion.Contains(".java") || lowerQuestion.Contains(".cpp") ||
                                          lowerQuestion.Contains(".c") || lowerQuestion.Contains(".html") ||
                                          lowerQuestion.Contains(".css") || lowerQuestion.Contains(".tsx") ||
                                          lowerQuestion.Contains(".jsx");
                
                if (isFileSelectionResponse || isMentioningFileName)
                {
                    _logger.LogInformation("Forcing code_review intent based on file selection response or file name mention: {UserQuestion}", userQuestion);
                    intent.Type = "code_review";
                    intent.Confidence = 0.9;
                }
                else if (string.IsNullOrEmpty(intent.Type) || intent.Type == "general")
                {
                    if (lowerQuestion.Contains("review") && (lowerQuestion.Contains("code") || lowerQuestion.Contains("my code") || lowerQuestion.Contains("repo")))
                    {
                        _logger.LogInformation("Forcing code_review intent based on explicit 'review code' request");
                        intent.Type = "code_review";
                        intent.Confidence = 0.9;
                    }
                }

                // Route based on intent
                switch (intent.Type)
                {
                    case "code_review":
                        return await HandleCodeReviewIntent(request, intent, aiModel);
                    case "github_help":
                        return await HandleGitHubHelpIntent(request, intent);
                    default:
                        return await HandleGeneralMentorResponse(request, intent, aiModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor response for StudentId: {StudentId}, SprintId: {SprintId}, Model: {Model}", 
                    request.StudentId, request.SprintId, aiModelName);
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
            }
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

                var githubRepoUrl = student.ProjectBoard?.GithubUrl;
                if (string.IsNullOrEmpty(githubRepoUrl))
                {
                    return BadRequest(new { Success = false, Message = "GitHub repository URL not found for this project" });
                }

                // Parse GitHub repo URL (format: https://github.com/owner/repo)
                var repoParts = githubRepoUrl.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/').Split('/');
                if (repoParts.Length < 2)
                {
                    return BadRequest(new { Success = false, Message = "Invalid GitHub repository URL format" });
                }

                var owner = repoParts[0];
                var repo = repoParts[1];

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

                var githubRepoUrl = student.ProjectBoard?.GithubUrl ?? "";
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
                var chatHistory = await _context.MentorChatHistory
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
                        if (!string.IsNullOrEmpty(student.GithubUser) && !string.IsNullOrEmpty(student.ProjectBoard?.GithubUrl))
                        {
                            var githubRepoUrl = student.ProjectBoard.GithubUrl;
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
                        if (!string.IsNullOrEmpty(student.GithubUser) && !string.IsNullOrEmpty(student.ProjectBoard?.GithubUrl))
                        {
                            var githubRepoUrl = student.ProjectBoard.GithubUrl;
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
                    var githubRepoUrl = student.ProjectBoard?.GithubUrl ?? "";
                    
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
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n⚠️ IMPORTANT: This is NOT a new conversation - there is existing chat history. Do NOT greet the user with phrases like \"Hi [name]! How can I help you today?\" or similar greetings. Go straight to answering their question based on the conversation context.";
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
                if (!string.IsNullOrEmpty(readmeContent) && isDeveloperRole)
                {
                    enhancedSystemPrompt = $"{enhancedSystemPrompt}\n\n=== PROJECT INFORMATION ===\n" +
                        $"{readmeContent}\n\n" +
                        $"=== END OF PROJECT INFORMATION ===\n\n" +
                        $"⚠️ IMPORTANT: Use the project information provided above to answer the user's question naturally. " +
                        $"If the user asked about connection strings or other project details, extract and provide the relevant information from the content above. " +
                        $"Do NOT mention that this information comes from README or any specific file - simply provide the answer as if you naturally have access to this project information. " +
                        $"For example, if a connection string is present in the project information above, provide it directly without mentioning its source.";
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

                // Get sprint list
                var listsResult = await GetBoardListsAsync(student.BoardId);
                string? sprintListId = null;
                string? sprintListName = null;

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

                // Get GitHub files
                var githubFiles = new List<string>();
                var githubRepo = student.ProjectBoard?.GithubUrl ?? "";
                if (!string.IsNullOrEmpty(githubRepo))
                {
                    try
                    {
                        var repoName = githubRepo.Replace("https://github.com/", "").Replace("http://github.com/", "").TrimEnd('/');
                        githubFiles = await GetGitHubRepositoryFilesAsync(repoName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch GitHub files for repo {Repo}", githubRepo);
                    }
                }

                // Get GitHub commit summary
                var githubCommitSummary = await GetGitHubCommitSummaryAsync(githubRepo, student.GithubUser);

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

                var formattedPrompt = string.Format(
                    _promptConfig.Mentor.UserPromptTemplate,
                    student.FirstName,
                    roleName,
                    sprintId,
                    sprintListName ?? $"Sprint {sprintId}",
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
                        GitHubCommitSummary = githubCommitSummary
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

    /// <summary>
    /// Chat history item for API calls
    /// </summary>
    public class ChatHistoryItem
    {
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

