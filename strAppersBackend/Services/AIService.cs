using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface IAIService
{
    Task<SystemDesignResponse> GenerateSystemDesignAsync(SystemDesignRequest request);
    Task<SprintPlanningResponse> GenerateSprintPlanAsync(SprintPlanningRequest request);
    Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription, int exactModules, int minWordsPerModule, string? contentForLanguageDetection = null);
    Task<CreateDataModelResponse> CreateDataModelAsync(int projectId, string modulesData);
    Task<UpdateModuleResponse> UpdateModuleAsync(int moduleId, string currentDescription, string userInput);
    Task<UpdateDataModelResponse> UpdateDataModelAsync(int projectId, string currentSqlScript, string userInput);
    Task<CodebaseStructureAIResponse> GenerateCodebaseStructureAsync(string systemPrompt, string userPrompt);
    Task<TranslateModuleResponse> TranslateModuleToEnglishAsync(string title, string description);
    Task<TranslateTextResponse> TranslateTextToEnglishAsync(string text);
    Task<ProjectCriteriaClassificationResponse> ClassifyProjectCriteriaAsync(string projectTitle, string projectDescription, string? extendedDescription, List<ProjectCriteria> availableCriteria);
    Task<ParsedBuildOutput?> ParseBuildOutputAsync(string buildOutput);
}

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIService> _logger;
    private readonly string _apiKey;
    private readonly AIConfig _aiConfig;
    private readonly SystemDesignAIAgentConfig _systemDesignConfig;

    public AIService(HttpClient httpClient, IConfiguration configuration, ILogger<AIService> logger, IOptions<AIConfig> aiConfig, IOptions<SystemDesignAIAgentConfig> systemDesignConfig)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _aiConfig = aiConfig.Value;
        _systemDesignConfig = systemDesignConfig.Value;
        _apiKey = _configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        
        // Set a longer timeout for AI calls
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task<SystemDesignResponse> GenerateSystemDesignAsync(SystemDesignRequest request)
    {
        try
        {
            _logger.LogInformation("Generating system design for Project {ProjectId}", request.ProjectId);

            // Generate JSON system design
            var jsonPrompt = BuildSystemDesignPrompt(request);
            var jsonAiResponse = await CallOpenAIAsync(jsonPrompt, _aiConfig.Model);
            
            if (!jsonAiResponse.Success)
            {
                return new SystemDesignResponse
                {
                    Success = false,
                    Message = jsonAiResponse.ErrorMessage
                };
            }

            var designDocument = jsonAiResponse.Content;
            
            // Generate formatted (human-readable) system design
            _logger.LogInformation("Generating formatted system design for Project {ProjectId}", request.ProjectId);
            var formattedPrompt = BuildFormattedSystemDesignPrompt(request);
            var formattedAiResponse = await CallOpenAIAsync(formattedPrompt, _aiConfig.Model);
            
            var designDocumentFormatted = formattedAiResponse.Success ? formattedAiResponse.Content : null;
            
            if (!formattedAiResponse.Success)
            {
                _logger.LogWarning("Failed to generate formatted design document, but JSON version succeeded");
            }

            var pdfBytes = await GeneratePDFAsync(designDocument);

            return new SystemDesignResponse
            {
                Success = true,
                Message = "System design generated successfully",
                DesignDocument = designDocument,
                DesignDocumentFormatted = designDocumentFormatted,
                DesignDocumentPdf = pdfBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating system design for Project {ProjectId}", request.ProjectId);
            return new SystemDesignResponse
            {
                Success = false,
                Message = $"Error generating system design: {ex.Message}"
            };
        }
    }

    public async Task<SprintPlanningResponse> GenerateSprintPlanAsync(SprintPlanningRequest request)
    {
        (bool Success, string Content, string? ErrorMessage) aiResponse = (false, string.Empty, null);
        
        try
        {
            _logger.LogInformation("=== STARTING SPRINT PLAN GENERATION ===");
            _logger.LogInformation("Generating sprint plan for Project {ProjectId}", request.ProjectId);
            _logger.LogInformation("Request details: ProjectLength={ProjectLength}, SprintLength={SprintLength}, StartDate={StartDate}, Students={StudentCount}", 
                request.ProjectLengthWeeks, request.SprintLengthWeeks, request.StartDate, request.Students?.Count ?? 0);

            var prompt = BuildSprintPlanningPrompt(request);
            aiResponse = await CallOpenAIAsync(prompt, _aiConfig.Model);
            
            if (!aiResponse.Success)
            {
                return new SprintPlanningResponse
                {
                    Success = false,
                    Message = aiResponse.ErrorMessage
                };
            }

            _logger.LogInformation("AI response received. Content length: {ContentLength}", aiResponse.Content?.Length ?? 0);
            _logger.LogInformation("AI response preview: {ContentPreview}", aiResponse.Content?.Substring(0, Math.Min(200, aiResponse.Content?.Length ?? 0)) ?? "No content");
            
            var sprintPlanJson = ExtractJsonFromResponse(aiResponse.Content);
            _logger.LogInformation("Extracted JSON for sprint plan: {Json}", sprintPlanJson);
            
            // Fix invalid dates (e.g., Feb 29 in non-leap years) before deserialization
            sprintPlanJson = FixInvalidDatesInJson(sprintPlanJson);
            
            // Try to deserialize with more flexible options
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            
            SprintPlan? sprintPlan = null;
            try
            {
                sprintPlan = JsonSerializer.Deserialize<SprintPlan>(sprintPlanJson, options);
            }
            catch (JsonException jsonEx)
            {
                // If deserialization fails, try to fix the specific date mentioned in the error
                if (jsonEx.Path?.Contains("startDate") == true || jsonEx.Path?.Contains("endDate") == true)
                {
                    _logger.LogWarning("Date parsing error detected at {Path}, attempting to fix and retry", jsonEx.Path);
                    sprintPlanJson = FixInvalidDatesInJson(sprintPlanJson, jsonEx.Path);
                    sprintPlan = JsonSerializer.Deserialize<SprintPlan>(sprintPlanJson, options);
                }
                else
                {
                    throw; // Re-throw if it's not a date-related error
                }
            }
            
            if (sprintPlan == null)
            {
                _logger.LogError("Failed to deserialize SprintPlan from JSON: {Json}", sprintPlanJson);
                return new SprintPlanningResponse
                {
                    Success = false,
                    Message = "Failed to deserialize sprint plan from AI response"
                };
            }
            
            _logger.LogInformation("Successfully deserialized SprintPlan with {SprintCount} sprints", sprintPlan.Sprints?.Count ?? 0);

            // Validate the sprint plan
            var validationResult = ValidateSprintPlan(sprintPlan, request);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Sprint plan validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return new SprintPlanningResponse
                {
                    Success = false,
                    Message = $"Sprint plan validation failed: {string.Join(", ", validationResult.Errors)}",
                    SprintPlan = sprintPlan // Return the plan anyway for debugging
                };
            }

            return new SprintPlanningResponse
            {
                Success = true,
                Message = "Sprint plan generated successfully",
                SprintPlan = sprintPlan
            };
        }
        catch (JsonException jsonEx)
        {
            // Last resort: try to fix dates and retry if we haven't already
            if (aiResponse.Success && !string.IsNullOrEmpty(aiResponse.Content))
            {
                try
                {
                    _logger.LogWarning("JSON parsing error in outer catch, attempting final date fix and retry. Path: {Path}", jsonEx.Path);
                    var sprintPlanJson = ExtractJsonFromResponse(aiResponse.Content);
                    sprintPlanJson = FixInvalidDatesInJson(sprintPlanJson, jsonEx.Path);
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    var sprintPlan = JsonSerializer.Deserialize<SprintPlan>(sprintPlanJson, options);
                    
                    if (sprintPlan != null)
                    {
                        _logger.LogInformation("Successfully deserialized SprintPlan after final date fix attempt");
                        
                        // Validate the sprint plan
                        var validationResult = ValidateSprintPlan(sprintPlan, request);
                        if (!validationResult.IsValid)
                        {
                            _logger.LogWarning("Sprint plan validation failed after date fix: {Errors}", string.Join(", ", validationResult.Errors));
                            return new SprintPlanningResponse
                            {
                                Success = false,
                                Message = $"Sprint plan validation failed: {string.Join(", ", validationResult.Errors)}",
                                SprintPlan = sprintPlan
                            };
                        }
                        
                        return new SprintPlanningResponse
                        {
                            Success = true,
                            Message = "Sprint plan generated successfully (after fixing invalid dates)",
                            SprintPlan = sprintPlan
                        };
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Failed to fix and retry JSON parsing after final date fix attempt");
                }
            }
            
            _logger.LogError(jsonEx, "JSON parsing error for Project {ProjectId}. Path: {Path}, Line: {Line}, Position: {Position}. Raw response: {RawResponse}", 
                request.ProjectId, jsonEx.Path, jsonEx.LineNumber, jsonEx.BytePositionInLine, aiResponse.Content ?? "No response available");
            return new SprintPlanningResponse
            {
                Success = false,
                Message = $"Error parsing sprint plan JSON at {jsonEx.Path}: {jsonEx.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sprint plan for Project {ProjectId}", request.ProjectId);
            return new SprintPlanningResponse
            {
                Success = false,
                Message = $"Error generating sprint plan: {ex.Message}"
            };
        }
    }

    private string BuildSystemDesignPrompt(SystemDesignRequest request)
    {
        var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:SystemDesign");
        var systemPrompt = promptConfig["SystemPrompt"] ?? "You are a senior software architect. Generate a comprehensive system design document based on the following project requirements.";
        var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "PROJECT DESCRIPTION:\n{0}\n\nTEAM COMPOSITION:\n{1}\n\nPlease generate a structured system design document in JSON format.";
        
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        var userPrompt = string.Format(userPromptTemplate, request.ExtendedDescription, teamRolesText);
        
        return $"{systemPrompt}\n\n{userPrompt}";
    }

    private string BuildFormattedSystemDesignPrompt(SystemDesignRequest request)
    {
        var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:SystemDesignFormatted");
        var systemPrompt = promptConfig["SystemPrompt"] ?? "You are a senior software architect. Generate a concise, human-readable system design summary for the following project.";
        var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "PROJECT DESCRIPTION:\n{0}\n\nTEAM COMPOSITION:\n{1}\n\nPlease generate a brief, professional summary (maximum 2000 characters).";
        
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        var userPrompt = string.Format(userPromptTemplate, request.ExtendedDescription, teamRolesText);
        
        return $"{systemPrompt}\n\n{userPrompt}";
    }

    private string BuildSprintPlanningPrompt(SprintPlanningRequest request)
    {
        var promptConfig = _configuration.GetSection("PromptConfig:SprintPlanning");
        var systemPromptTemplate = promptConfig["SystemPrompt"] ?? "Generate a detailed sprint plan for a {0}-week project with {1}-week sprints.";
        var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "";
        
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        var totalSprints = request.ProjectLengthWeeks / request.SprintLengthWeeks; // Calculate from project length and sprint length
        
        // Filter out Data Model section to save tokens and focus on system modules
        var filteredSystemDesign = FilterOutDataModelSection(request.SystemDesign);
        
        // Build role-specific task instructions
        var roleInstructions = BuildRoleSpecificInstructions(request.TeamRoles);
        
        // Build Trello requirements section
        var trelloRequirementsSection = BuildTrelloRequirementsSection(promptConfig, totalSprints);
        
        // Build system prompt with dynamic values
        var systemPrompt = string.Format(systemPromptTemplate,
            request.ProjectLengthWeeks,
            request.SprintLengthWeeks,
            teamRolesText,
            request.StartDate.ToString("yyyy-MM-dd"),
            totalSprints,
            filteredSystemDesign ?? "No system design available"
        );
        
        // Build user prompt with all dynamic values
        // Placeholder order: {0}=roleInstructions, {1}=trelloRequirements, {2}=moduleCount, {3}=totalSprints, {4}=teamRolesList, {5}=startDate, {6}=endDate, {7}=projectLengthWeeks
        var moduleCount = GetModuleCount(filteredSystemDesign);
        var teamRolesList = string.Join(", ", request.TeamRoles.Select(r => r.RoleName));
        var startDateStr = request.StartDate.ToString("yyyy-MM-dd");
        var endDateStr = request.StartDate.AddDays(request.SprintLengthWeeks * 7 - 1).ToString("yyyy-MM-dd");
        
        // Build ProjectModules information section for AI to use actual database IDs
        var projectModulesSection = BuildProjectModulesSection(request.ProjectModules);
        
        var userPrompt = !string.IsNullOrEmpty(userPromptTemplate)
            ? string.Format(userPromptTemplate,
                roleInstructions,
                trelloRequirementsSection,
                moduleCount,
                totalSprints,
                teamRolesList,
                startDateStr,
                endDateStr,
                request.ProjectLengthWeeks
            )
            : BuildFallbackUserPrompt(request, roleInstructions, trelloRequirementsSection, filteredSystemDesign, totalSprints, moduleCount, projectModulesSection);
        
        return $"{systemPrompt}\n\n{userPrompt}";
    }

    /// <summary>
    /// Builds the Trello requirements section from configuration
    /// </summary>
    private string BuildTrelloRequirementsSection(IConfigurationSection promptConfig, int totalSprints)
    {
        var trelloConfig = promptConfig.GetSection("TrelloCardRequirements");
        var firstSprintConfig = promptConfig.GetSection("FirstSprintRequirements");
        
        var sections = new List<string>
        {
            "📋 TRELLO CARD METADATA & STRUCTURE REQUIREMENTS ⚠️",
            "The following requirements ensure proper card organization, tracking, and workflow management in Trello:\n",
            trelloConfig["CardIdFormat"] ?? "",
            trelloConfig["TaskDistribution"] ?? "",
            trelloConfig["BranchedField"] ?? "",
            trelloConfig["ModuleId"] ?? "",
            trelloConfig["Dependencies"] ?? "",
            trelloConfig["MetadataFields"] ?? "",
            trelloConfig["DeveloperCodeChecklist"] ?? "",
            trelloConfig["DeveloperDatabaseChecklist"] ?? "",
            trelloConfig["ProductManagerChecklist"] ?? "",
            trelloConfig["SystemDesignCoverage"] ?? "",
            "\n11. FIRST SPRINT SPECIAL REQUIREMENTS:",
            "The first sprint focuses on environment setup and project familiarization:\n",
            firstSprintConfig["UIUXDesigner"] ?? "",
            firstSprintConfig["ProductManager"] ?? "",
            firstSprintConfig["Marketing"] ?? "",
            firstSprintConfig["BackendDeveloper"] ?? "",
            firstSprintConfig["FrontendDeveloper"] ?? "",
            trelloConfig["FinalSprintRequirement"]?.Replace("{totalSprints}", totalSprints.ToString()) ?? ""
        };
        
        return string.Join("\n\n", sections.Where(s => !string.IsNullOrEmpty(s)));
    }

    /// <summary>
    /// Builds a section describing project modules with their database IDs
    /// </summary>
    private string BuildProjectModulesSection(List<ProjectModuleInfo> projectModules)
    {
        if (projectModules == null || !projectModules.Any())
        {
            return "\n📋 PROJECT MODULES (Database IDs):\nNo project modules found in database. Use module identifiers from System Design.";
        }
        
        var modulesList = projectModules.Select((pm, index) => 
            $"{index + 1}. Module ID: {pm.Id}, Title: {pm.Title ?? "Untitled"}, Description: {(string.IsNullOrEmpty(pm.Description) ? "No description" : pm.Description.Substring(0, Math.Min(100, pm.Description.Length)) + (pm.Description.Length > 100 ? "..." : ""))}"
        );
        
        return $"\n📋 PROJECT MODULES (Database IDs) - CRITICAL FOR MODULEID FIELD:\n" +
               $"The following modules exist in the database. You MUST use the exact Module ID (integer) when setting the moduleId field in task metadata:\n\n" +
               string.Join("\n", modulesList) +
               $"\n\n⚠️ CRITICAL: When creating tasks for a module, use the Module ID (integer) from above, NOT generic names like \"Module1\" or \"Module2\".\n" +
               $"Example: If creating a task for \"User Authentication\" module with Module ID: 5, then moduleId should be \"5\" (not \"Module1\" or \"User Authentication\").";
    }

    /// <summary>
    /// Fallback user prompt if configuration is not available (backward compatibility)
    /// </summary>
    private string BuildFallbackUserPrompt(SprintPlanningRequest request, string roleInstructions, string trelloRequirementsSection, string? filteredSystemDesign, int totalSprints, int moduleCount, string projectModulesSection)
    {
        var teamRolesList = string.Join(", ", request.TeamRoles.Select(r => r.RoleName));
        var startDateStr = request.StartDate.ToString("yyyy-MM-dd");
        var endDateStr = request.StartDate.AddDays(request.SprintLengthWeeks * 7 - 1).ToString("yyyy-MM-dd");
        
        return $@"⚠️ CRITICAL BUSINESS LOGIC REQUIREMENTS ⚠️
- You MUST create exactly {totalSprints} sprints (configured number, not based on modules)
- CRITICAL: ONLY create tasks for roles that are in the team: {teamRolesList}
- DO NOT create tasks for roles that are NOT in this list (e.g., if there is no Product Manager in the team, do NOT create Product Manager tasks)
- Each sprint MUST have AT LEAST ONE task for EACH role in the team: {teamRolesList}
- CRITICAL: If ""UI/UX Designer"" is in the team roles ({teamRolesList}), you MUST create a UI/UX Designer task in EVERY sprint with roleName=""UI/UX Designer"" and cardId=""{{SprintNumber}}-U""
- CRITICAL: If ""Product Manager"" is in the team roles ({teamRolesList}), you MUST create a Product Manager task in EVERY sprint with roleName=""Product Manager"" and cardId=""{{SprintNumber}}-P""
- CRITICAL: If ""Full Stack Developer"" is in the team roles ({teamRolesList}), you MUST create TWO separate tasks in EVERY sprint:
  * ONE Backend task with cardId=""{{SprintNumber}}-B"" and roleName=""Full Stack Developer""
  * ONE Frontend task with cardId=""{{SprintNumber}}-F"" and roleName=""Full Stack Developer""
- 🚨 SPRINT 1 SPECIAL RULE - SETUP ONLY, NO BUSINESS LOGIC 🚨:
  * Sprint 1 is ONLY for getting familiar, setup, and preparations
  * Sprint 1 tasks MUST NOT include business logic implementation
  * Sprint 1 tasks MUST follow FirstSprintRequirements (see requirement #11)
  * NO module implementation, NO business logic, NO feature development in Sprint 1
  * Business logic implementation starts from Sprint 2 onwards
- Sprints 2-{totalSprints}: EVERY task title and description MUST directly reference specific modules, inputs, outputs, or business logic from the System Design
- Sprints 2-{totalSprints}: NO GENERIC TASKS ALLOWED - Every task must be tied to specific business functionality described in the System Design
- Sprints 2-{totalSprints}: Tasks must implement the actual business logic described in the modules (inputs, outputs, functionality)
- ONE sprint (Sprint 2 or later) must include database layer tasks for Backend/Full Stack developers based on the Data Model section

MANDATORY TASK REQUIREMENTS (Sprints 2+ only - Sprint 1 is setup only):
- Task titles MUST include the module name or specific business function from System Design
- Task descriptions MUST explain how it implements specific inputs/outputs from the System Design
- Task descriptions MUST NOT mention ""Module 1"", ""Module 2"", etc. - use natural language that users will understand
- Example GOOD title: ""Implement User Registration Module - Email validation and password hashing""
- Example BAD title: ""Create registration feature"" (too generic, doesn't reference System Design)
- Example GOOD description: ""Build the User Registration module that accepts email, password, and full name and returns a confirmation token""
- Example BAD description: ""Implement user registration"" (too generic, no reference to System Design)
- Example BAD description: ""Build module that processes Module 1 inputs and returns Module 1 outputs"" (mentions Module 1 - users won't understand)

ROLE-SPECIFIC TASK GENERATION (MUST REFERENCE SYSTEM DESIGN):
{roleInstructions}

TASK GENERATION STRATEGY:
🚨 CRITICAL: Sprint 1 is SETUP ONLY - NO business logic implementation 🚨
- Sprint 1: Use ONLY FirstSprintRequirements (see requirement #11) - setup, getting familiar, preparations
- Sprint 1: NO module implementation, NO business logic, NO feature development
- Sprints 2-{totalSprints}: Read the System Design carefully and identify ALL modules with their inputs and outputs
- Sprints 2-{totalSprints}: For EACH module, create tasks that directly implement the described functionality
- Sprints 2-{totalSprints}: Task titles must reference the module name or number from System Design
- Sprints 2-{totalSprints}: Task descriptions must explain which inputs/outputs from System Design are being implemented
- Sprints 2-{totalSprints}: Distribute modules across sprints, ensuring each sprint has at least one task per role
- Sprints 2-{totalSprints}: If you have fewer modules than sprints, create additional tasks that extend the business logic (e.g., ""Add email notification to User Registration module"", ""Implement error handling for Login Module"")
- NEVER create generic tasks like ""Write tests"" or ""Update documentation"" without specifying which module they relate to

SPRINT DISTRIBUTION STRATEGY:
- Sprint 1: Setup and preparation ONLY - follow FirstSprintRequirements strictly
- Sprints 2-{totalSprints}: Distribute the {moduleCount} system modules across these sprints
- Sprints 2-{totalSprints}: Each sprint must have at least one task per role that directly implements System Design modules
- One sprint (Sprint 2 or later) should focus on database layer implementation based on the Data Model section
- Sprints 2-{totalSprints}: Ensure every sprint has meaningful, business-logic-specific work for all roles
- If you have fewer modules than sprints (excluding Sprint 1), create tasks that extend or enhance the existing modules from System Design

⚠️ CHECKLIST REQUIREMENTS ⚠️
🚨 SPRINT 1 SPECIAL RULES 🚨:
- Sprint 1 tasks MUST follow FirstSprintRequirements (see requirement #11) - setup and preparation ONLY
- Sprint 1 tasks MUST NOT include business logic implementation checklist items
- Sprint 1 is ONLY about getting familiar, setup, and preparations

Sprints 2-{totalSprints} - BUSINESS LOGIC SPECIFIC:
- Each task MUST include a ""checklistItems"" array with AT LEAST 4 sub-tasks
- EVERY checklist item MUST directly reference specific business logic, inputs, outputs, or functionality from the System Design
- Checklist items must break down the task into specific implementation steps for the business logic described
- Checklist items MUST NOT be generic (NEVER use: ""Implement feature"", ""Test code"", ""Write documentation"", ""Code review"", ""Deploy"" without specifics)
- Each checklist item must specify WHAT part of the System Design module is being implemented
- CRITICAL: For Backend Developer tasks in Sprints 2-{totalSprints}, MUST include ""Prepare mockup data"" for each module
- Example GOOD checklist items for Backend Developer ""Implement User Registration Module"" (Sprint 2+):
  * ""Create Branch (Name: 2-B)"" (MUST be first)
  * ""Prepare mockup data for User Registration module"" (MUST be second)
  * ""Create API endpoint for user registration""
  * ""Implement email format validation""
  * ""Hash password before storing in database""
  * ""Return JSON response with user data""
  * ""Commit changes""
  * ""Push changes""
  * ""Create a PR""
  * ""Merge branch (once approved by PM)""
- Example GOOD checklist items for Frontend Developer (Sprint 2+):
  * ""Create Branch (Name: 2-F)"" (MUST be first)
  * ""Collect endpoints (REST API) for the module"" (MUST be second)
  * ""Implement login form with email and password fields""
  * ""Add form validation for required fields""
  * ""Handle API response and display success/error messages""
  * ""Commit changes""
  * ""Push changes""
  * ""Create a PR""
  * ""Merge branch (once approved by PM)""
- Example BAD checklist items (TOO GENERIC - DO NOT USE):
  * ""Implement registration""
  * ""Write tests""
  * ""Code review""
  * ""Deploy to production""

{trelloRequirementsSection}

{projectModulesSection}

Return ONLY valid JSON with exactly {totalSprints} sprints. The last sprint list MUST be named ""Bugs"" (NOT ""Sprint {totalSprints}""). JSON structure:
{{
  ""sprints"": [{{""sprintNumber"": 1, ""name"": ""Sprint 1"", ""startDate"": ""{startDateStr}"", ""endDate"": ""{endDateStr}"", ""tasks"": [{{""id"": ""task1"", ""cardId"": ""1-B"", ""title"": ""Implement [Module Name from System Design] - [Specific Business Function]"", ""description"": ""Build [Module Name] that processes [specific inputs from System Design] and returns [specific outputs from System Design]. This implements the business logic described in Module X."", ""roleId"": 1, ""roleName"": ""Backend Developer"", ""estimatedHours"": 8, ""priority"": 3, ""status"": ""To Do"", ""risk"": ""Medium"", ""moduleId"": ""Module1"", ""dependencies"": [], ""branched"": false, ""checklistItems"": [""Create [specific component] that handles [specific input from Module X]"", ""Implement [specific business rule] for [specific output from Module X]"", ""Add validation for [specific input field] as described in Module X inputs"", ""Return [specific output format] matching Module X outputs specification"", ""Create Branch (Name: 1-B)"", ""Commit changes"", ""Push changes"", ""Create a PR"", ""Merge branch (once approved by PM)""]}}], ""totalStoryPoints"": 10, ""roleWorkload"": {{""1"": 8}}}}],
  ""totalSprints"": {totalSprints},
  ""totalTasks"": 0,
  ""estimatedWeeks"": {request.ProjectLengthWeeks}
}}

⚠️ CRITICAL JSON FIELD REQUIREMENTS:
- ""cardId"": Must follow format ""{{SprintNumber}}-{{RoleFirstLetter}}"" (e.g., ""1-B"", ""2-F"", ""3-U"")
- ""moduleId"": MUST use the exact Module ID (integer) from the PROJECT MODULES section above (e.g., ""5"" for Module ID 5), NOT generic names like ""Module1"" or ""Module2""
- ""priority"": Integer 1-5 (1 = Highest, 5 = Lowest)
- ""status"": Must be ""To Do"" for all new cards
- ""risk"": Must be ""Low"", ""Medium"", or ""High""
- ""dependencies"": Array of card IDs (e.g., [""1-B"", ""1-U""]) if task depends on other cards
- ""branched"": Boolean (false for new cards, only include for Developer roles)
- ""checklistItems"": Must include role-appropriate items as specified in requirements above
- Last sprint name: MUST be ""Bugs"" (not ""Sprint {totalSprints}"")

REMEMBER: 
- Sprint 1: Setup and preparation ONLY - follow FirstSprintRequirements strictly, NO business logic
- Sprints 2-{totalSprints}: Every task title, description, and checklist item MUST directly reference the System Design modules, inputs, outputs, or business logic. NO GENERIC TASKS ALLOWED. 
- Follow all Trello card metadata requirements specified above.";
    }

    /// <summary>
    /// Builds role-specific task generation instructions
    /// </summary>
    private string BuildRoleSpecificInstructions(List<RoleInfo> teamRoles)
    {
        var instructions = new List<string>();
        
        foreach (var role in teamRoles)
        {
            var roleNameLower = role.RoleName.ToLower();
            
            // Only include Developer roles, Marketing, Product Management, and UI/UX Design
            if (roleNameLower.Contains("frontend developer") || roleNameLower.Contains("front-end developer") || roleNameLower == "frontend" || roleNameLower == "front-end")
            {
                instructions.Add("- Frontend Developer: Create tasks that implement the UI components for specific modules from System Design. Each task must reference which module's inputs/outputs are being displayed. Example: \"Build User Registration Form UI for Module 1 - Display email, password, fullName inputs and show confirmationToken output\". Focus on React/Vue/Angular components that directly implement the module interfaces described in System Design.");
            }
            else if (roleNameLower.Contains("backend developer") || roleNameLower == "backend")
            {
                instructions.Add("- Backend Developer: Create tasks that implement the API endpoints and business logic for specific modules from System Design. Each task must reference which module's inputs/outputs are being processed. Example: \"Implement User Registration API for Module 1 - Process email, password, fullName inputs and return confirmationToken output\". Focus on server-side logic that directly implements the business rules described in System Design modules.");
            }
            else if (roleNameLower.Contains("full stack developer") || roleNameLower.Contains("fullstack developer") || roleNameLower == "full stack" || roleNameLower == "fullstack")
            {
                instructions.Add("- Full Stack Developer: Create tasks that implement complete features for specific modules from System Design, covering both frontend and backend. Each task must reference which module is being implemented end-to-end. Example: \"Implement complete User Registration Module 1 - Build form UI, API endpoint, and database integration for email/password/fullName inputs and confirmationToken output\". Focus on full implementation of System Design modules.");
            }
            else if (roleNameLower.Contains("ui/ux designer") || roleNameLower.Contains("ui ux designer") || roleNameLower.Contains("ux designer") || roleNameLower == "ui/ux" || roleNameLower == "ux")
            {
                instructions.Add("- UI/UX Designer: Create tasks that design the user interface for specific modules from System Design. Each task must reference which module's inputs/outputs need UI design. Example: \"Design User Registration Module 1 interface - Wireframe for email/password/fullName input fields and confirmationToken display\". Focus on designing interfaces that match the business logic and data flow described in System Design.");
            }
            else if (roleNameLower.Contains("marketing"))
            {
                instructions.Add("- Marketing: Create tasks that promote specific features from System Design modules. Each task must reference which module's functionality is being marketed. Example: \"Create marketing content for User Registration Module 1 - Highlight email validation and secure password features\". ALWAYS include a task for creating a video demo showcasing the modules from System Design.");
            }
            else if (roleNameLower.Contains("product manager") || roleNameLower.Contains("product management") || roleNameLower == "product manager")
            {
                instructions.Add("- Product Manager: Create tasks that define and coordinate product features for specific modules from System Design. Each task must reference which modules are being managed and which business requirements are being addressed. Example: \"Define product requirements for User Registration Module 1 - Specify email validation rules and password security requirements from System Design\". Focus on product strategy and requirements based on System Design modules.");
            }
            // All other roles are ignored - no instructions added
        }
        
        return string.Join("\n", instructions);
    }

    /// <summary>
    /// Counts the number of modules in the system design
    /// </summary>
    private int GetModuleCount(string? systemDesign)
    {
        if (string.IsNullOrEmpty(systemDesign))
            return 0;
            
        // Count occurrences of "### Module" pattern
        var moduleCount = System.Text.RegularExpressions.Regex.Matches(systemDesign, @"### Module \d+").Count;
        return moduleCount;
    }

    /// <summary>
    /// Filters out the Data Model section from system design content to save tokens and focus on system modules
    /// </summary>
    private string FilterOutDataModelSection(string? systemDesign)
    {
        if (string.IsNullOrEmpty(systemDesign))
            return systemDesign ?? string.Empty;

        try
        {
            // Split by "## Data Model" section
            var dataModelIndex = systemDesign.IndexOf("## Data Model", StringComparison.OrdinalIgnoreCase);
            
            if (dataModelIndex >= 0)
            {
                // Return only the content before the Data Model section
                var filteredContent = systemDesign.Substring(0, dataModelIndex).Trim();
                
                // Also remove any trailing "---" separators
                filteredContent = filteredContent.TrimEnd('-', ' ', '\n', '\r');
                
                _logger.LogInformation("Filtered out Data Model section. Original length: {OriginalLength}, Filtered length: {FilteredLength}", 
                    systemDesign.Length, filteredContent.Length);
                
                return filteredContent;
            }
            
            // If no Data Model section found, return original content
            return systemDesign;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering Data Model section: {Message}", ex.Message);
            return systemDesign; // Return original content if filtering fails
        }
    }

    /// <summary>
    /// Validates that the sprint plan meets the requirements
    /// </summary>
    private (bool IsValid, List<string> Errors) ValidateSprintPlan(SprintPlan sprintPlan, SprintPlanningRequest request)
    {
        var errors = new List<string>();
        var expectedSprints = request.ProjectLengthWeeks / request.SprintLengthWeeks; // Calculate from project length and sprint length
        
        // Check sprint count
        if (sprintPlan.Sprints?.Count != expectedSprints)
        {
            errors.Add($"Expected {expectedSprints} sprints (configured) but got {sprintPlan.Sprints?.Count ?? 0}");
        }
        
        // Check that each sprint has tasks for all roles
        var roleNames = request.TeamRoles.Select(r => r.RoleName).ToHashSet();
        if (sprintPlan.Sprints != null)
        {
            for (int i = 0; i < sprintPlan.Sprints.Count; i++)
            {
                var sprint = sprintPlan.Sprints[i];
                var sprintRoleNames = sprint.Tasks?.Select(t => t.RoleName).ToHashSet() ?? new HashSet<string>();
                
                foreach (var roleName in roleNames)
                {
                    if (!sprintRoleNames.Contains(roleName))
                    {
                        errors.Add($"Sprint {i + 1} is missing tasks for role: {roleName}");
                    }
                }
                
                // Check for multiple tasks per role (encouraged)
                var roleTaskCounts = sprint.Tasks?.GroupBy(t => t.RoleName).ToDictionary(g => g.Key, g => g.Count()) ?? new Dictionary<string, int>();
                foreach (var roleName in roleNames)
                {
                    if (roleTaskCounts.ContainsKey(roleName) && roleTaskCounts[roleName] < 2)
                    {
                        errors.Add($"Sprint {i + 1} has only {roleTaskCounts[roleName]} task(s) for {roleName} - consider adding more specific tasks");
                    }
                }
            }
        }
        
        // Check total sprints field
        if (sprintPlan.TotalSprints != expectedSprints)
        {
            errors.Add($"TotalSprints field shows {sprintPlan.TotalSprints} but expected {expectedSprints}");
        }
        
        return (errors.Count == 0, errors);
    }

    private string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Empty response from AI service");
        }

        // Remove any leading/trailing whitespace
        response = response.Trim();

        // If the response starts with {, it's likely already JSON
        if (response.StartsWith("{"))
        {
            return response;
        }

        // Try to find JSON within the response
        var startIndex = response.IndexOf("{");
        var lastIndex = response.LastIndexOf("}");

        if (startIndex >= 0 && lastIndex > startIndex)
        {
            return response.Substring(startIndex, lastIndex - startIndex + 1);
        }

        // If no JSON found, throw an exception
        throw new InvalidOperationException($"No valid JSON found in AI response. Response starts with: {response.Substring(0, Math.Min(50, response.Length))}...");
    }

    /// <summary>
    /// Fixes invalid dates in JSON (e.g., Feb 29 in non-leap years) by converting them to valid dates
    /// </summary>
    private string FixInvalidDatesInJson(string json, string? specificPath = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        // Use regex to find all date strings in format "YYYY-MM-DD"
        var datePattern = @"(\d{4})-(\d{2})-(\d{2})";
        var fixedJson = System.Text.RegularExpressions.Regex.Replace(json, datePattern, match =>
        {
            var dateStr = match.Value;
            var year = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var day = int.Parse(match.Groups[3].Value);

            // Check if the date is valid
            try
            {
                var testDate = new DateTime(year, month, day);
                // Date is valid, return as-is
                return dateStr;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid date - fix it
                _logger.LogWarning("Found invalid date {DateStr} in JSON, fixing to valid date", dateStr);
                
                // If it's Feb 29 in a non-leap year, convert to Feb 28
                if (month == 2 && day == 29)
                {
                    var fixedDate = new DateTime(year, 2, 28);
                    return fixedDate.ToString("yyyy-MM-dd");
                }
                
                // For other invalid dates, try to adjust to the last valid day of the month
                try
                {
                    // Try the last day of the month
                    var lastDayOfMonth = DateTime.DaysInMonth(year, month);
                    var fixedDate = new DateTime(year, month, Math.Min(day, lastDayOfMonth));
                    return fixedDate.ToString("yyyy-MM-dd");
                }
                catch
                {
                    // If month is invalid too, default to the last day of the previous valid month
                    var fixedDate = new DateTime(year, 12, 31);
                    return fixedDate.ToString("yyyy-MM-dd");
                }
            }
        });

        return fixedJson;
    }

    private async Task<(bool Success, string Content, string? ErrorMessage)> CallOpenAIAsync(string prompt, string model)
    {
        try
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = _aiConfig.MaxTokens,
                temperature = _aiConfig.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API with model {Model}", model);
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("OpenAI API response status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("OpenAI API response content length: {ContentLength}", responseContent?.Length ?? 0);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return (false, string.Empty, $"OpenAI API error: {response.StatusCode}");
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse == null)
            {
                _logger.LogError("Failed to deserialize OpenAI response: {Content}", responseContent);
                return (false, string.Empty, "Failed to deserialize OpenAI response");
            }

            if (openAIResponse.Choices == null || !openAIResponse.Choices.Any())
            {
                _logger.LogError("No choices in OpenAI response: {Content}", responseContent);
                return (false, string.Empty, "No choices in OpenAI response");
            }

            var messageContent = openAIResponse.Choices.First().Message?.Content;
            if (string.IsNullOrEmpty(messageContent))
            {
                _logger.LogError("Empty message content in OpenAI response: {Content}", responseContent);
                return (false, string.Empty, "Empty message content in OpenAI response");
            }

            _logger.LogInformation("OpenAI API returned content length: {ContentLength}", messageContent.Length);
            return (true, messageContent, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            return (false, string.Empty, $"Error calling OpenAI API: {ex.Message}");
        }
    }

    private async Task<byte[]> GeneratePDFAsync(string content)
    {
        // For now, return a simple text-based PDF placeholder
        // In a real implementation, you would use a PDF generation library like iTextSharp or Puppeteer
        var textBytes = Encoding.UTF8.GetBytes(content);
        return textBytes;
    }

    /// <summary>
    /// Cleans AI response to extract JSON from markdown code blocks
    /// </summary>
    private string CleanJsonFromMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Remove markdown code block markers
        content = content.Trim();
        
        // Remove ```json and ``` markers
        if (content.StartsWith("```json"))
        {
            content = content.Substring(7);
        }
        else if (content.StartsWith("```"))
        {
            content = content.Substring(3);
        }
        
        if (content.EndsWith("```"))
        {
            content = content.Substring(0, content.Length - 3);
        }
        
        // Remove any leading/trailing whitespace and newlines
        content = content.Trim();
        
        // If the content still doesn't start with {, try to find the first { character
        if (!content.StartsWith("{"))
        {
            var jsonStart = content.IndexOf('{');
            if (jsonStart >= 0)
            {
                content = content.Substring(jsonStart);
            }
        }
        
        // Find the last } character to ensure we have complete JSON
        if (content.Contains("}"))
        {
            var jsonEnd = content.LastIndexOf('}');
            if (jsonEnd >= 0)
            {
                content = content.Substring(0, jsonEnd + 1);
            }
        }
        
        return content;
    }

    /// <summary>
    /// Detects if text contains Hebrew characters
    /// </summary>
    private bool ContainsHebrewCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        
        // Hebrew Unicode range: U+0590 to U+05FF
        foreach (char c in text)
        {
            if (c >= '\u0590' && c <= '\u05FF')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fixes common JSON issues when Hebrew characters are present
    /// Repairs malformed JSON by escaping newlines, quotes, and other problematic characters
    /// </summary>
    private string FixJsonForHebrewContent(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        try
        {
            // Try to parse first - if it works, return as-is
            using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                return json;
            }
        }
        catch
        {
            // If parsing fails, try to fix common issues
            // Main issues: unescaped newlines, unescaped quotes, unescaped backslashes inside string values
            var jsonToFix = json;
            var result = new StringBuilder();
            var inString = false;
            var escapeNext = false;
            
            for (int i = 0; i < jsonToFix.Length; i++)
            {
                var c = jsonToFix[i];
                
                if (escapeNext)
                {
                    result.Append(c);
                    escapeNext = false;
                    continue;
                }
                
                if (c == '\\')
                {
                    result.Append(c);
                    escapeNext = true;
                    continue;
                }
                
                if (c == '"')
                {
                    if (!inString)
                    {
                        // Opening quote
                        inString = true;
                        result.Append(c);
                    }
                    else
                    {
                        // We're inside a string - check if this is a closing quote
                        var isClosingQuote = false;
                        // Look ahead past whitespace to see what comes next
                        for (int j = i + 1; j < jsonToFix.Length && j < i + 20; j++)
                        {
                            var nextChar = jsonToFix[j];
                            if (nextChar == ',' || nextChar == '}' || nextChar == ']')
                            {
                                isClosingQuote = true;
                                break;
                            }
                            if (nextChar == ':')
                            {
                                // This is actually the start of next property - close current string
                                isClosingQuote = true;
                                break;
                            }
                            if (!char.IsWhiteSpace(nextChar))
                            {
                                break;
                            }
                        }
                        
                        if (isClosingQuote)
                        {
                            // Closing quote
                            inString = false;
                            result.Append(c);
                        }
                        else
                        {
                            // Quote inside string value - escape it
                            result.Append("\\\"");
                        }
                    }
                    continue;
                }
                
                if (inString)
                {
                    // We're inside a string value - escape problematic characters
                    if (c == '\n')
                    {
                        result.Append("\\n");
                    }
                    else if (c == '\r')
                    {
                        // Skip \r, we'll handle \n
                        if (i + 1 >= jsonToFix.Length || jsonToFix[i + 1] != '\n')
                        {
                            result.Append("\\n");
                        }
                        // else skip it, \n will be handled
                    }
                    else if (c == '\t')
                    {
                        result.Append("\\t");
                    }
                    else if (c == '\b')
                    {
                        result.Append("\\b");
                    }
                    else if (c == '\f')
                    {
                        result.Append("\\f");
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
                else
                {
                    // Outside string - copy as-is
                    result.Append(c);
                }
            }
            
            var repairedJson = result.ToString();
            _logger.LogInformation("Attempted to repair JSON. Original length: {OriginalLength}, Repaired length: {RepairedLength}", 
                json.Length, repairedJson.Length);
            
            // Try to parse the repaired JSON
            try
            {
                using (var doc = JsonDocument.Parse(repairedJson, new JsonDocumentOptions { AllowTrailingCommas = true }))
                {
                    _logger.LogInformation("Successfully repaired JSON");
                    return repairedJson;
                }
            }
            catch (Exception repairEx)
            {
                _logger.LogWarning(repairEx, "JSON repair attempt failed, returning original");
                return json;
            }
        }
    }

    /// <summary>
    /// Cleans AI response to extract SQL from markdown code blocks
    /// </summary>
    private string CleanSqlFromMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Remove markdown code block markers
        content = content.Trim();
        
        // Remove ```sql and ``` markers
        if (content.StartsWith("```sql"))
        {
            content = content.Substring(6);
        }
        else if (content.StartsWith("```"))
        {
            content = content.Substring(3);
        }
        
        if (content.EndsWith("```"))
        {
            content = content.Substring(0, content.Length - 3);
        }
        
        // Remove any leading/trailing whitespace and newlines
        content = content.Trim();
        
        return content;
    }

    public async Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription, int exactModules, int minWordsPerModule, string? contentForLanguageDetection = null)
    {
        try
        {
            _logger.LogInformation("Initiating modules for Project {ProjectId}", projectId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:InitiateModules");
            var baseSystemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert software architect.";
            
            // Determine language from SystemDesign if available, otherwise from ExtendedDescription
            var languageSource = !string.IsNullOrEmpty(contentForLanguageDetection) ? contentForLanguageDetection : extendedDescription;
            var isHebrew = ContainsHebrewCharacters(languageSource);
            
            // Build language instruction
            var languageInstruction = isHebrew 
                ? "🌍 CRITICAL LANGUAGE REQUIREMENT - HEBREW: 🌍\n- The project description/SystemDesign is in HEBREW.\n- You MUST generate ALL modules (titles, descriptions, inputs, outputs) COMPLETELY in HEBREW.\n- Do NOT translate to English - respond entirely in Hebrew.\n- Every word, sentence, and explanation must be in Hebrew."
                : "🌍 LANGUAGE CONSISTENCY REQUIREMENT: 🌍\n- Always respond in the SAME language as the project description provided by the user.\n- Match the language exactly - if it's English, respond in English; if it's Hebrew, respond in Hebrew.";
            
            // Enhance system prompt to emphasize word count requirements and language
            var systemPrompt = $"{baseSystemPrompt}\n\n" +
                             $"🚨 CRITICAL WORD COUNT REQUIREMENT 🚨\n" +
                             $"Each module description MUST be EXACTLY {minWordsPerModule} words or MORE.\n" +
                             $"This is NON-NEGOTIABLE - descriptions under {minWordsPerModule} words will be REJECTED.\n" +
                             $"Write extremely detailed, comprehensive descriptions that include:\n" +
                             $"- Specific implementation details and technical specifications\n" +
                             $"- Detailed use cases and scenarios\n" +
                             $"- Architecture and integration points\n" +
                             $"- User interactions and data flows\n" +
                             $"- Examples and detailed explanations\n" +
                             $"Be verbose, thorough, and comprehensive - aim for {minWordsPerModule}+ words per description.\n\n" +
                             $"{languageInstruction}";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Project Description: {0}";
            
            // Build JSON format instruction (especially important for Hebrew)
            var jsonFormatInstruction = isHebrew 
                ? "\n\n⚠️ CRITICAL JSON FORMAT REQUIREMENT - MANDATORY ⚠️\n- You MUST return valid JSON that can be parsed by a standard JSON parser.\n- ALL Hebrew text MUST be inside properly quoted string values (between double quotes).\n- CRITICAL: Replace ALL actual newline characters (line breaks) in your text with the escape sequence \\n (backslash followed by n).\n- CRITICAL: Replace ALL actual tab characters with \\t.\n- Ensure ALL quotes inside Hebrew text are escaped with backslash (\\\").\n- DO NOT include actual line breaks or newlines in the JSON - use \\n instead.\n- The JSON must be on a single line or properly formatted with escaped newlines.\n- Example CORRECT format: {\"title\": \"מודול\", \"description\": \"תיאור ארוך עם\\nשורות מרובות\"}\n- Example WRONG format: {\"title\": \"מודול\", \"description\": \"תיאור ארוך עם\nשורות מרובות\"} (actual newline breaks JSON)\n- DO NOT put Hebrew characters outside of quoted strings.\n- Test your JSON: it must parse without errors."
                : "\n\n⚠️ CRITICAL JSON FORMAT REQUIREMENT - MANDATORY ⚠️\n- You MUST return valid JSON that can be parsed by a standard JSON parser.\n- ALL text MUST be inside properly quoted string values (between double quotes).\n- CRITICAL: Replace ALL actual newline characters (line breaks) in your text with the escape sequence \\n (backslash followed by n).\n- CRITICAL: Replace ALL actual tab characters with \\t.\n- Ensure ALL quotes inside text are escaped with backslash (\\\").\n- DO NOT include actual line breaks or newlines in the JSON - use \\n instead.\n- The JSON must be on a single line or properly formatted with escaped newlines.\n- Test your JSON: it must parse without errors.";
            
            // Enhance the user prompt with ExactModules and MinWordsPerModule constraints
            var enhancedUserPrompt = $"{string.Format(userPromptTemplate, extendedDescription)}\n\n" +
                                   $"🚨 CRITICAL REQUIREMENTS - NON-NEGOTIABLE 🚨\n" +
                                   $"- Generate EXACTLY {exactModules} modules (not more, not less)\n" +
                                   $"- You MUST return exactly {exactModules} modules in the JSON response\n" +
                                   $"- Each module description MUST be EXACTLY {minWordsPerModule} words or MORE\n" +
                                   $"- Count your words carefully - descriptions under {minWordsPerModule} words will be REJECTED\n" +
                                   $"- Write extremely detailed, comprehensive descriptions for each module\n" +
                                   $"- Include specific implementation details, technical specifications, and use cases\n" +
                                   $"- Explain the module's purpose, functionality, architecture, and integration points\n" +
                                   $"- Describe user interactions, data flows, and system behaviors\n" +
                                   $"- Add examples, scenarios, and detailed explanations\n" +
                                   $"- Be verbose and thorough - aim for {minWordsPerModule}+ words per description\n" +
                                   $"- Ensure each module has detailed inputs and outputs\n" +
                                   $"- DO NOT write brief or concise descriptions - be comprehensive and detailed\n" +
                                   $"- CRITICAL: The JSON must contain exactly {exactModules} modules in the modules array\n\n" +
                                   $"{languageInstruction}" +
                                   $"{jsonFormatInstruction}";
            
            var userPrompt = enhancedUserPrompt;
            
            // Log the complete prompt being sent to AI
            _logger.LogInformation("=== AI PROMPT DEBUG ===");
            _logger.LogInformation("System Prompt: {SystemPrompt}", systemPrompt);
            _logger.LogInformation("User Prompt: {UserPrompt}", userPrompt);
            _logger.LogInformation("ExactModules: {ExactModules}, MinWordsPerModule: {MinWordsPerModule}", exactModules, minWordsPerModule);
            _logger.LogInformation("=== END AI PROMPT DEBUG ===");

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = _aiConfig.Model,
                messages = messages,
                max_tokens = _aiConfig.MaxTokens,
                temperature = _aiConfig.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for module initiation");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = "Failed to get response from AI service"
                };
            }

            var aiContent = openAIResponse.Choices.First().Message.Content;
            _logger.LogInformation("AI response received for module initiation");
            _logger.LogInformation("=== AI RESPONSE DEBUG ===");
            _logger.LogInformation("AI Response Length: {Length} characters", aiContent?.Length ?? 0);
            _logger.LogInformation("AI Response Content: {Content}", aiContent);
            _logger.LogInformation("=== END AI RESPONSE DEBUG ===");

            // Clean the AI response to extract JSON from markdown code blocks
            var cleanedContent = CleanJsonFromMarkdown(aiContent);
            _logger.LogInformation("Cleaned AI content: {Content}", cleanedContent);

            // Sanitize known problematic characters before JSON parsing (but preserve Hebrew)
            var sanitizedContent = cleanedContent
                .Replace('÷', '/')
                .Replace('\u2022', '-')
                .Replace('\u2212', '-')
                .Replace('\u00A0', ' ');

            if (!ReferenceEquals(cleanedContent, sanitizedContent))
            {
                _logger.LogInformation("Sanitized AI content length: {Length}", sanitizedContent.Length);
            }

            // Try to fix common JSON issues with Hebrew characters
            sanitizedContent = FixJsonForHebrewContent(sanitizedContent);
 
            // Parse the JSON response with custom converter for flexible Inputs/Outputs handling
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new Models.FlexibleStringConverter() },
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow Hebrew characters
            };
            
            ModulesResponse? modulesResponse = null;
            try
            {
                // First try to validate JSON structure
                using (var doc = JsonDocument.Parse(sanitizedContent, new JsonDocumentOptions { AllowTrailingCommas = true }))
                {
                    _logger.LogInformation("JSON structure validated successfully");
                }
                
                modulesResponse = JsonSerializer.Deserialize<ModulesResponse>(sanitizedContent, jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization failed. Error: {Error}. Path: {Path}. Line: {Line}. Position: {Position}", 
                    ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine);
                
                // Log the problematic content around the error location
                var errorPosition = ex.BytePositionInLine > 0 ? (int)ex.BytePositionInLine : 0;
                var errorLine = ex.LineNumber > 0 ? (int)ex.LineNumber : 0;
                
                _logger.LogError("Error occurred at Line {Line}, Position {Position}", errorLine, errorPosition);
                
                // Try to extract context around the error
                if (errorPosition > 0 && sanitizedContent.Length > errorPosition)
                {
                    var startPos = Math.Max(0, errorPosition - 100);
                    var length = Math.Min(200, sanitizedContent.Length - startPos);
                    var context = sanitizedContent.Substring(startPos, length);
                    _logger.LogError("Content around error position: {Context}", context);
                }
                
                // Log full content (truncated if too long)
                if (sanitizedContent.Length > 5000)
                {
                    _logger.LogError("Full JSON content (first 2500 chars): {Content}", sanitizedContent.Substring(0, 2500));
                    _logger.LogError("Full JSON content (last 2500 chars): {Content}", sanitizedContent.Substring(sanitizedContent.Length - 2500));
                }
                else
                {
                    _logger.LogError("Full JSON content: {Content}", sanitizedContent);
                }
                
                // Try to manually extract modules if JSON structure is partially valid
                try
                {
                    // Look for the modules array pattern manually
                    var modulesStart = sanitizedContent.IndexOf("\"modules\"", StringComparison.OrdinalIgnoreCase);
                    if (modulesStart >= 0)
                    {
                        var arrayStart = sanitizedContent.IndexOf('[', modulesStart);
                        if (arrayStart >= 0)
                        {
                            _logger.LogInformation("Found modules array starting at position {Position}", arrayStart);
                            // Try to count modules by counting opening braces
                            var moduleCount = 0;
                            var braceCount = 0;
                            var inString = false;
                            var escapeNext = false;
                            
                            for (int i = arrayStart + 1; i < sanitizedContent.Length && i < arrayStart + 10000; i++)
                            {
                                var c = sanitizedContent[i];
                                
                                if (escapeNext)
                                {
                                    escapeNext = false;
                                    continue;
                                }
                                
                                if (c == '\\')
                                {
                                    escapeNext = true;
                                    continue;
                                }
                                
                                if (c == '"' && !escapeNext)
                                {
                                    inString = !inString;
                                    continue;
                                }
                                
                                if (!inString)
                                {
                                    if (c == '{')
                                    {
                                        braceCount++;
                                        if (braceCount == 1) moduleCount++;
                                    }
                                    else if (c == '}')
                                    {
                                        braceCount--;
                                    }
                                    else if (c == ']' && braceCount == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                            
                            _logger.LogInformation("Manually counted approximately {Count} modules in array", moduleCount);
                        }
                    }
                }
                catch (Exception manualEx)
                {
                    _logger.LogWarning(manualEx, "Failed to manually analyze JSON structure");
                }
                
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = $"JSON parsing error: {ex.Message}. Path: {ex.Path ?? "unknown"}. Line: {errorLine}, Position: {errorPosition}. The AI response contains invalid JSON structure - likely Hebrew characters are not properly escaped or quoted. Please try again. The prompt has been updated to ensure valid JSON format."
                };
            }

            if (modulesResponse?.Modules == null)
            {
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = "Failed to parse modules from AI response"
                };
            }

            // Validate that all modules have required fields
            var invalidModules = modulesResponse.Modules.Where(m => 
                string.IsNullOrWhiteSpace(m.Title) || 
                string.IsNullOrWhiteSpace(m.Description) || 
                string.IsNullOrWhiteSpace(m.Inputs) || 
                string.IsNullOrWhiteSpace(m.Outputs)).ToList();

            if (invalidModules.Any())
            {
                _logger.LogWarning("Found {Count} modules with missing inputs or outputs", invalidModules.Count);
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = $"AI response incomplete: {invalidModules.Count} modules are missing inputs or outputs. Please try again."
                };
            }

            _logger.LogInformation("Successfully parsed {Count} modules with complete inputs and outputs", modulesResponse.Modules.Count);
            
            // Validate exact module count
            if (modulesResponse.Modules.Count != exactModules)
            {
                _logger.LogWarning("AI returned {ActualCount} modules but {ExactCount} were requested", 
                    modulesResponse.Modules.Count, exactModules);
                return new InitiateModulesResponse
                {
                    Success = false,
                    Message = $"AI returned {modulesResponse.Modules.Count} modules but exactly {exactModules} modules were requested. Please try again."
                };
            }
            
            // Log details about each module for debugging
            for (int i = 0; i < modulesResponse.Modules.Count; i++)
            {
                var module = modulesResponse.Modules[i];
                var wordCount = module.Description?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
                _logger.LogInformation("Module {Index}: '{Title}' - {WordCount} words", i + 1, module.Title, wordCount);
            }

            _logger.LogInformation("Validation passed: Exactly {Count} modules returned as requested", exactModules);

            return new InitiateModulesResponse
            {
                Success = true,
                Modules = modulesResponse.Modules
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating modules for Project {ProjectId}", projectId);
            return new InitiateModulesResponse
            {
                Success = false,
                Message = $"Error initiating modules: {ex.Message}"
            };
        }
    }

    public async Task<CreateDataModelResponse> CreateDataModelAsync(int projectId, string modulesData)
    {
        try
        {
            _logger.LogInformation("Creating data model for Project {ProjectId}", projectId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:CreateDataModel");
            var systemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert database architect.";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Based on the following project modules, create a comprehensive database schema:\n\n{0}";
            var userPrompt = string.Format(userPromptTemplate, modulesData);

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = "gpt-4o",
                messages = messages,
                max_tokens = 6000,
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for data model creation");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new CreateDataModelResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                return new CreateDataModelResponse
                {
                    Success = false,
                    Message = "Failed to get response from AI service"
                };
            }

            var sqlScript = openAIResponse.Choices.First().Message.Content;
            _logger.LogInformation("AI response received for data model creation");

            // Clean the SQL script from markdown code blocks
            var cleanedSqlScript = CleanSqlFromMarkdown(sqlScript);
            _logger.LogInformation("Cleaned SQL script length: {Length}", cleanedSqlScript.Length);

            return new CreateDataModelResponse
            {
                Success = true,
                SqlScript = cleanedSqlScript
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data model for Project {ProjectId}", projectId);
            return new CreateDataModelResponse
            {
                Success = false,
                Message = $"Error creating data model: {ex.Message}"
            };
        }
    }

    public async Task<UpdateModuleResponse> UpdateModuleAsync(int moduleId, string currentDescription, string userInput)
    {
        try
        {
            _logger.LogInformation("Updating module {ModuleId}", moduleId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:UpdateModule");
            var baseSystemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert software architect and technical writer.";
            
            // Enhance system prompt to include language consistency
            var systemPrompt = $"{baseSystemPrompt}\n\n" +
                             $"🌍 LANGUAGE CONSISTENCY: Always respond in the SAME language as the user input provided below.";
            
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Current Module Description:\n{0}\n\nUser Feedback:\n{1}";
            var baseUserPrompt = string.Format(userPromptTemplate, currentDescription, userInput);
            
            // Enhance user prompt to emphasize language consistency
            var userPrompt = $"{baseUserPrompt}\n\n" +
                           $"🌍 IMPORTANT: Respond in the SAME language as the user feedback provided above.";

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = _aiConfig.Model,
                messages = messages,
                max_tokens = _aiConfig.MaxTokens,
                temperature = _aiConfig.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for module update");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new UpdateModuleResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                return new UpdateModuleResponse
                {
                    Success = false,
                    Message = "Failed to get response from AI service"
                };
            }

            var updatedDescription = openAIResponse.Choices.First().Message.Content;
            
            // Remove any dialog questions or conversational text at the end
            // Common patterns in multiple languages
            var dialogPatterns = new[]
            {
                "Is this what you meant?",
                "האם זה מה שהתכוונת?",
                "Please let me know if you need any adjustments",
                "אנא ידעי אותי אם יש צורך בהתאמות",
                "אנא ידע אותי אם יש צורך בהתאמות",
                "Let me know if you need",
                "ידע אותי אם",
                "Is this what you",
                "האם זה מה ש"
            };
            
            foreach (var pattern in dialogPatterns)
            {
                var index = updatedDescription.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    // Find the start of the sentence/question (look for newline or period before it)
                    var startIndex = index;
                    // Look backwards for sentence boundary
                    for (int i = index - 1; i >= 0; i--)
            {
                        if (updatedDescription[i] == '\n' || updatedDescription[i] == '.' || updatedDescription[i] == '!' || updatedDescription[i] == '?')
                        {
                            startIndex = i + 1;
                            break;
                        }
                        if (i == 0)
                        {
                            startIndex = 0;
                        }
                    }
                    updatedDescription = updatedDescription.Substring(0, startIndex).Trim();
                    break; // Remove only the first occurrence
                }
            }
            
            // Additional cleanup: Remove any trailing questions that might have question marks
            // Look for patterns like "? ..." at the end
            updatedDescription = updatedDescription.Trim();
            var lastQuestionMark = updatedDescription.LastIndexOf('?');
            if (lastQuestionMark > updatedDescription.Length * 0.8) // If question mark is in last 20% of text
            {
                // Check if there's a newline before it, suggesting it's a separate question
                var beforeQuestion = updatedDescription.Substring(0, lastQuestionMark);
                var afterQuestion = updatedDescription.Substring(lastQuestionMark + 1);
                
                // If the part after question mark is short and looks like dialog, remove it
                if (afterQuestion.Trim().Length < 100 && 
                    (afterQuestion.Contains("אנא") || afterQuestion.Contains("Please") || 
                     afterQuestion.Contains("ידע") || afterQuestion.Contains("Let me")))
                {
                    updatedDescription = beforeQuestion.Trim();
                }
            }

            _logger.LogInformation("AI response received for module update");

            return new UpdateModuleResponse
            {
                Success = true,
                UpdatedDescription = updatedDescription
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module {ModuleId}", moduleId);
            return new UpdateModuleResponse
            {
                Success = false,
                Message = $"Error updating module: {ex.Message}"
            };
        }
    }

    public async Task<UpdateDataModelResponse> UpdateDataModelAsync(int projectId, string currentSqlScript, string userInput)
    {
        try
        {
            _logger.LogInformation("Updating data model for Project {ProjectId}", projectId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:UpdateDataModel");
            var systemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert database architect. Update the SQL script based on user feedback.";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Current SQL Script:\n{0}\n\nUser Feedback:\n{1}\n\nPlease update the SQL script based on the user feedback.";

            var userPrompt = string.Format(userPromptTemplate, currentSqlScript, userInput);

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = "gpt-4o",
                messages = messages,
                max_tokens = 6000,
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for data model update");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new UpdateDataModelResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                return new UpdateDataModelResponse
                {
                    Success = false,
                    Message = "Failed to get response from AI service"
                };
            }

            var updatedSqlScript = openAIResponse.Choices.First().Message.Content;
            
            // Clean the SQL script from markdown formatting
            var cleanedSqlScript = CleanSqlFromMarkdown(updatedSqlScript);

            _logger.LogInformation("AI response received for data model update");

            return new UpdateDataModelResponse
            {
                Success = true,
                UpdatedSqlScript = cleanedSqlScript
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data model for Project {ProjectId}", projectId);
            return new UpdateDataModelResponse
            {
                Success = false,
                Message = $"Error updating data model: {ex.Message}"
            };
        }
    }

    public async Task<CodebaseStructureAIResponse> GenerateCodebaseStructureAsync(string systemPrompt, string userPrompt)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Generating codebase structure via Claude AI. System prompt length: {SysLen}, User prompt length: {UserLen}", 
                systemPrompt?.Length ?? 0, userPrompt?.Length ?? 0);
            
            _logger.LogInformation("=== SYSTEM PROMPT ===\n{SystemPrompt}", systemPrompt ?? "");
            _logger.LogInformation("=== USER PROMPT ===\n{UserPrompt}", userPrompt ?? "");

            // Get Anthropic configuration
            var anthropicApiKey = _configuration["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic API key not configured");
            var anthropicBaseUrl = _configuration["Anthropic:BaseUrl"] ?? "https://api.anthropic.com/v1";
            var anthropicVersion = _configuration["Anthropic:ApiVersion"] ?? "2023-06-01";
            var model = _configuration["Anthropic:Model"] ?? "claude-sonnet-4-5-20250929";
            
            _logger.LogDebug("Using Anthropic model: {Model}, BaseUrl: {BaseUrl}, Version: {Version}", model, anthropicBaseUrl, anthropicVersion);

            // Create a new HttpClient for this request with Anthropic-specific headers
            // Use longer timeout for large codebase generation (30 minutes)
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-api-key", anthropicApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", anthropicVersion);
            client.Timeout = TimeSpan.FromMinutes(30);

            // Claude API request format
            var requestBody = new
            {
                model = model,
                max_tokens = _aiConfig.MaxTokens,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                },
                temperature = _aiConfig.Temperature
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Calling Claude API for codebase structure generation with model {Model}", model);
            var apiCallStart = DateTime.UtcNow;
            var response = await client.PostAsync($"{anthropicBaseUrl}/messages", content);
            var apiCallDuration = (DateTime.UtcNow - apiCallStart).TotalSeconds;
            _logger.LogInformation("Claude API call completed in {Duration} seconds. Status: {StatusCode}", 
                apiCallDuration, response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Claude API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return new CodebaseStructureAIResponse
                {
                    Success = false,
                    ErrorMessage = $"Error calling Claude API: {response.StatusCode}. {errorContent}"
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response from Claude API. Length: {Length} chars", responseContent.Length);
            
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
            {
                _logger.LogError("Empty response from Claude API. Response was: {Response}", responseContent);
                return new CodebaseStructureAIResponse
                {
                    Success = false,
                    ErrorMessage = "Empty response from Claude API"
                };
            }

            // Claude returns content as an array with type="text"
            var textContent = claudeResponse.Content.FirstOrDefault(c => c.Type == "text");
            if (textContent == null || string.IsNullOrEmpty(textContent.Text))
            {
                _logger.LogError("No text content in Claude API response. Content count: {Count}", claudeResponse.Content.Count);
                return new CodebaseStructureAIResponse
                {
                    Success = false,
                    ErrorMessage = "No text content in Claude API response"
                };
            }

            var aiContent = textContent.Text;
            _logger.LogInformation("Received AI content. Length: {Length} chars. First 200 chars: {Preview}", 
                aiContent.Length, aiContent.Length > 200 ? aiContent.Substring(0, 200) : aiContent);

            // Try to parse the JSON response
            try
            {
                // Remove markdown code blocks if present
                var cleanedContent = aiContent.Trim();
                
                // Try to extract JSON from markdown code blocks
                if (cleanedContent.StartsWith("```"))
                {
                    // Find the first ``` and last ```
                    var firstIndex = cleanedContent.IndexOf("```");
                    if (firstIndex >= 0)
                    {
                        var afterFirst = cleanedContent.Substring(firstIndex + 3);
                        // Skip language identifier if present
                        if (afterFirst.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                        {
                            afterFirst = afterFirst.Substring(4);
                        }
                        // Trim whitespace after language identifier
                        afterFirst = afterFirst.TrimStart();
                        
                        // Find the closing ```
                        var lastIndex = afterFirst.LastIndexOf("```");
                        if (lastIndex >= 0)
                        {
                            cleanedContent = afterFirst.Substring(0, lastIndex).Trim();
                        }
                        else
                        {
                            // No closing ```, try to find JSON start
                            var jsonStart = afterFirst.IndexOf('{');
                            if (jsonStart >= 0)
                            {
                                cleanedContent = afterFirst.Substring(jsonStart);
                            }
                        }
                    }
                }
                
                // Fallback: try regex patterns if simple extraction didn't work
                if (cleanedContent.StartsWith("`") || !cleanedContent.TrimStart().StartsWith("{"))
                {
                    var jsonMatch = System.Text.RegularExpressions.Regex.Match(aiContent, @"```(?:json)?\s*(\{.*?\})\s*```", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                    if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                    {
                        cleanedContent = jsonMatch.Groups[1].Value;
                    }
                    else
                    {
                        // Try to find JSON object that starts after ```json
                        var jsonStartIndex = aiContent.IndexOf('{');
                        if (jsonStartIndex >= 0)
                        {
                            // Find matching closing brace (simplified - might need proper JSON parsing)
                            var jsonEndIndex = aiContent.LastIndexOf('}');
                            if (jsonEndIndex > jsonStartIndex)
                            {
                                cleanedContent = aiContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                            }
                        }
                    }
                }
                
                _logger.LogDebug("Cleaned JSON content length: {Length} chars. First 100 chars: {Preview}", 
                    cleanedContent.Length, cleanedContent.Length > 100 ? cleanedContent.Substring(0, 100) : cleanedContent);

                CodebaseStructure? codebaseStructure = null;
                try
                {
                    // Try to deserialize as new format first (devcontainer + files)
                    var newStructure = JsonSerializer.Deserialize<NewCodebaseStructure>(cleanedContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (newStructure != null)
                    {
                        // Transform new format to existing CodebaseStructure format
                        codebaseStructure = TransformNewStructureToCodebaseStructure(newStructure);
                        _logger.LogInformation("Successfully parsed new format structure and transformed it");
                    }
                    else
                    {
                        // Try old format
                        codebaseStructure = JsonSerializer.Deserialize<CodebaseStructure>(cleanedContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                catch (JsonException jsonEx)
                {
                    // Try to fix common JSON escaping issues
                    _logger.LogWarning("Initial JSON parse failed, attempting to fix common escaping issues. Error: {Error}", jsonEx.Message);
                    
                    // Fix unescaped newlines in content strings (most common issue)
                    // This regex finds "content": "..." patterns and replaces actual newlines with \n
                    var fixedContent = System.Text.RegularExpressions.Regex.Replace(
                        cleanedContent,
                        @"""content"":\s*""([^""]*)",
                        m =>
                        {
                            var content = m.Groups[1].Value;
                            // Replace actual newlines with \n
                            content = content.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
                            // Replace unescaped quotes with \"
                            content = content.Replace("\"", "\\\"");
                            // Replace unescaped backslashes (but not already escaped ones)
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"\\(?![nrt""\\])", @"\\\\");
                            return $"\"content\": \"{content}";
                        },
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    
                    // Also fix the closing quote pattern
                    fixedContent = System.Text.RegularExpressions.Regex.Replace(
                        fixedContent,
                        @"([^\\])""\s*([,\]])",
                        @"$1\""$2");
                    
                    try
                    {
                        // Try new format first
                        var newStructure = JsonSerializer.Deserialize<NewCodebaseStructure>(fixedContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (newStructure != null)
                        {
                            codebaseStructure = TransformNewStructureToCodebaseStructure(newStructure);
                            _logger.LogInformation("Successfully fixed and parsed new format JSON after escaping corrections");
                        }
                        else
                        {
                            // Try old format
                            codebaseStructure = JsonSerializer.Deserialize<CodebaseStructure>(fixedContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            _logger.LogInformation("Successfully fixed and parsed old format JSON after escaping corrections");
                        }
                    }
                    catch
                    {
                        // If fixing doesn't work, rethrow original exception
                        throw jsonEx;
                    }
                }

                if (codebaseStructure == null)
                {
                    throw new JsonException("Failed to deserialize codebase structure");
                }

                var totalDuration = (DateTime.UtcNow - startTime).TotalSeconds;
                _logger.LogInformation("Successfully generated codebase structure via AI in {Duration} seconds. Models: {ModelCount}, Services: {ServiceCount}, Controllers: {ControllerCount}", 
                    totalDuration,
                    codebaseStructure.Models?.Count ?? 0,
                    codebaseStructure.Services?.Count ?? 0,
                    codebaseStructure.Controllers?.Count ?? 0);
                return new CodebaseStructureAIResponse
                {
                    Success = true,
                    CodebaseStructure = codebaseStructure
                };
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse AI response as JSON: {Content}. Error: {Error}", 
                    aiContent.Length > 1000 ? aiContent.Substring(0, 1000) + "..." : aiContent, jsonEx.Message);
                return new CodebaseStructureAIResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse AI response: {jsonEx.Message}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating codebase structure via AI");
            return new CodebaseStructureAIResponse
            {
                Success = false,
                ErrorMessage = $"Error generating codebase structure: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Transform new structure (devcontainer + files) to existing CodebaseStructure format
    /// </summary>
    private CodebaseStructure TransformNewStructureToCodebaseStructure(NewCodebaseStructure newStructure)
    {
        var result = new CodebaseStructure();
        
        // Transform devcontainer object to CodeFile and preserve as NewCodebaseStructure
        if (newStructure.Devcontainer != null)
        {
            var devContainerJson = JsonSerializer.Serialize(newStructure.Devcontainer, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Set legacy DevContainer property (CodeFile) - will be ignored during serialization
            result.DevContainer = new CodeFile
            {
                Name = "devcontainer.json",
                Path = ".devcontainer/devcontainer.json",
                Content = devContainerJson
            };
            
            // Set new Devcontainer property (NewCodebaseStructure) - will be serialized
            result.Devcontainer = new NewCodebaseStructure
            {
                Devcontainer = newStructure.Devcontainer,
                Files = new List<CodeFile>() // Will be populated below
            };
        }
        
        // Transform files array - categorize by path patterns and extract name from path
        foreach (var file in newStructure.Files)
        {
            // Ensure file has a name (extract from path if missing)
            if (string.IsNullOrEmpty(file.Name) && !string.IsNullOrEmpty(file.Path))
            {
                file.Name = System.IO.Path.GetFileName(file.Path);
            }
            
            var path = file.Path.ToLower();
            
            if (path.Contains("/models/") || path.Contains("model."))
            {
                result.Models.Add(file);
            }
            else if (path.Contains("/services/") || path.Contains("service."))
            {
                result.Services.Add(file);
            }
            else if (path.Contains("/controllers/") || path.Contains("controller."))
            {
                result.Controllers.Add(file);
            }
            else if (path.Contains("/views/") || path.Contains("/pages/"))
            {
                result.Views.Add(file);
            }
            else if (path.Contains("applicationdbcontext") || path.Contains("dbcontext"))
            {
                result.ApplicationDbContext = file;
            }
            else if (path.Contains("appsettings") || path.Contains("app.config"))
            {
                result.AppSettings = file;
            }
            else if (path.Contains(".sql") && (path.Contains("/database/") || path.Contains("schema") || path.Contains("seed")))
            {
                result.SqlScripts.Add(file);
            }
            else if (path.Contains("devcontainer.json"))
            {
                result.DevContainer = file;
            }
            else
            {
                // Add to Files list for new structure (includes frontend, config, docs, etc.)
                result.Files.Add(file);
            }
        }
        
        return result;
    }

    public async Task<TranslateModuleResponse> TranslateModuleToEnglishAsync(string title, string description)
    {
        try
        {
            var systemPrompt = "You are a professional technical translator. Translate product and UI module content to English while preserving meaning, formatting, and any markup. Respond ONLY with valid JSON.";
            var userPrompt = $"Translate the following module title and description to English. Preserve Markdown formatting and bullet structures.\n\nTitle:\n{title}\n\nDescription:\n{description}\n\nReturn JSON in the form {{\"title\": \"...\", \"description\": \"...\"}}.";

            var requestBody = new
            {
                model = _aiConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = _aiConfig.MaxTokens,
                temperature = _aiConfig.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for module translation");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error during translation: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new TranslateModuleResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}",
                    Title = title,
                    Description = description
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var aiContent = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(aiContent))
            {
                return new TranslateModuleResponse
                {
                    Success = false,
                    Message = "Failed to get translation from AI service",
                    Title = title,
                    Description = description
                };
            }

            var cleanedContent = CleanJsonFromMarkdown(aiContent);

            ModuleTranslationResult? translationResult = null;
            try
            {
                translationResult = JsonSerializer.Deserialize<ModuleTranslationResult>(cleanedContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize translation response: {Content}", cleanedContent);
                return new TranslateModuleResponse
                {
                    Success = false,
                    Message = "Failed to parse translation response",
                    Title = title,
                    Description = description
                };
            }

            if (translationResult == null || string.IsNullOrWhiteSpace(translationResult.Title) || string.IsNullOrWhiteSpace(translationResult.Description))
            {
                return new TranslateModuleResponse
                {
                    Success = false,
                    Message = "Translation response missing required fields",
                    Title = title,
                    Description = description
                };
            }

            return new TranslateModuleResponse
            {
                Success = true,
                Title = translationResult.Title,
                Description = translationResult.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating module content to English");
            return new TranslateModuleResponse
            {
                Success = false,
                Message = $"Error translating module: {ex.Message}",
                Title = title,
                Description = description
            };
        }
    }

    public async Task<TranslateTextResponse> TranslateTextToEnglishAsync(string text)
    {
        try
        {
            var systemPrompt = "You are a professional technical translator. Translate any provided text to English while preserving formatting and meaning. Respond ONLY with valid JSON.";
            var userPrompt = $"Translate the following text to English. Preserve line breaks and Markdown formatting.\n\nText:\n{text}\n\nReturn JSON in the form {{\"text\": \"...\"}}.";

            var requestBody = new
            {
                model = _aiConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = _aiConfig.MaxTokens,
                temperature = _aiConfig.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for text translation");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error during text translation: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new TranslateTextResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}",
                    Text = text
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var aiContent = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(aiContent))
            {
                return new TranslateTextResponse
                {
                    Success = false,
                    Message = "Failed to get translation from AI service",
                    Text = text
                };
            }

            var cleanedContent = CleanJsonFromMarkdown(aiContent);

            TextTranslationResult? translationResult = null;
            try
            {
                translationResult = JsonSerializer.Deserialize<TextTranslationResult>(cleanedContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize text translation response: {Content}", cleanedContent);
                return new TranslateTextResponse
                {
                    Success = false,
                    Message = "Failed to parse translation response",
                    Text = text
                };
            }

            if (translationResult == null || string.IsNullOrWhiteSpace(translationResult.Text))
            {
                return new TranslateTextResponse
                {
                    Success = false,
                    Message = "Translation response missing required fields",
                    Text = text
                };
            }

            return new TranslateTextResponse
            {
                Success = true,
                Text = translationResult.Text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating text to English");
            return new TranslateTextResponse
            {
                Success = false,
                Message = $"Error translating text: {ex.Message}",
                Text = text
            };
        }
    }

    public async Task<ProjectCriteriaClassificationResponse> ClassifyProjectCriteriaAsync(string projectTitle, string projectDescription, string? extendedDescription, List<ProjectCriteria> availableCriteria)
    {
        try
        {
            _logger.LogInformation("Classifying project criteria for project: {Title}", projectTitle);

            // Filter out Criteria 8 (New Projects) as instructed
            var criteriaToUse = availableCriteria.Where(c => c.Id != 8).OrderBy(c => c.Id).ToList();
            
            if (!criteriaToUse.Any())
            {
                _logger.LogWarning("No criteria available for classification (excluding Criteria 8)");
                return new ProjectCriteriaClassificationResponse
                {
                    Success = false,
                    Message = "No criteria available for classification"
                };
            }

            // Build criteria list string
            var criteriaList = string.Join("\n", criteriaToUse.Select(c => $"- CriteriaId {c.Id}: {c.Name}"));

            var systemPrompt = @"You are a project classification expert. Your task is to classify projects based on available criteria.

CRITICAL INSTRUCTIONS:
1. You will be provided with a list of available project criteria (with their IDs and names)
2. You MUST ignore Criteria 8 (New Projects) - do NOT include it in your classification
3. Analyze the project title, description, and extended description
4. Classify the project by selecting ALL applicable criteria from the provided list
5. A project can have MULTIPLE criteria - select all that apply
6. Return ONLY a comma-separated string of CriteriaIds (e.g., ""1,2,5"" or ""3"" or ""2,4,6"")
7. If no criteria apply, return an empty string
8. Do NOT include any explanation, text, or formatting - ONLY the comma-separated CriteriaIds

Example responses:
- If project matches Criteria 1 and 3: ""1,3""
- If project matches only Criteria 2: ""2""
- If no criteria match: """"";

            var projectInfo = new StringBuilder();
            projectInfo.AppendLine($"PROJECT TITLE: {projectTitle}");
            projectInfo.AppendLine($"PROJECT DESCRIPTION: {projectDescription}");
            if (!string.IsNullOrWhiteSpace(extendedDescription))
            {
                projectInfo.AppendLine($"EXTENDED DESCRIPTION: {extendedDescription}");
            }
            projectInfo.AppendLine();
            projectInfo.AppendLine("AVAILABLE CRITERIA (DO NOT USE Criteria 8 - New Projects):");
            projectInfo.AppendLine(criteriaList);
            projectInfo.AppendLine();
            projectInfo.AppendLine("Return ONLY a comma-separated string of CriteriaIds that apply to this project. Example: \"1,2,5\" or \"3\" or \"\"");

            var userPrompt = projectInfo.ToString();

            var requestBody = new
            {
                model = _aiConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 100, // Short response - just CriteriaIds
                temperature = 0.2 // Lower temperature for more consistent classification
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API for project criteria classification");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error during criteria classification: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new ProjectCriteriaClassificationResponse
                {
                    Success = false,
                    Message = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (openAIResponse?.Choices == null || !openAIResponse.Choices.Any())
            {
                _logger.LogError("No choices in OpenAI response for criteria classification");
                return new ProjectCriteriaClassificationResponse
                {
                    Success = false,
                    Message = "No response from AI"
                };
            }

            var aiResponseText = openAIResponse.Choices[0].Message?.Content?.Trim() ?? "";
            
            // Clean the response - remove quotes, whitespace, and extract just the CriteriaIds
            aiResponseText = aiResponseText.Trim('"', '\'', ' ', '\n', '\r', '\t');
            
            // Validate that it's a comma-separated list of numbers (or empty)
            var criteriaIdsString = "";
            if (!string.IsNullOrWhiteSpace(aiResponseText))
            {
                // Parse and validate the CriteriaIds
                var ids = aiResponseText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(id => int.TryParse(id, out int parsedId) && parsedId != 8 && criteriaToUse.Any(c => c.Id == parsedId))
                    .Distinct()
                    .OrderBy(id => int.Parse(id))
                    .ToList();
                
                criteriaIdsString = string.Join(",", ids);
            }

            _logger.LogInformation("AI criteria classification result for project '{Title}': '{CriteriaIds}'", projectTitle, criteriaIdsString ?? "empty");

            return new ProjectCriteriaClassificationResponse
            {
                Success = true,
                Message = "Criteria classification completed successfully",
                CriteriaIds = string.IsNullOrWhiteSpace(criteriaIdsString) ? null : criteriaIdsString
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying project criteria for project: {Title}", projectTitle);
            return new ProjectCriteriaClassificationResponse
            {
                Success = false,
                Message = $"Error classifying project criteria: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses build output logs using AI to extract error information
    /// </summary>
    public async Task<ParsedBuildOutput?> ParseBuildOutputAsync(string buildOutput)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(buildOutput))
            {
                _logger.LogWarning("Build output is empty, cannot parse");
                return null;
            }

            // Get the cheap model from configuration (defaults to gpt-4o-mini)
            var cheapModel = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";

            _logger.LogInformation("Parsing build output using AI model {Model}", cheapModel);

            // Create a system prompt that instructs the AI to parse build logs and extract error information
            var systemPrompt = @"You are an expert at analyzing build logs, deployment logs, and runtime stack traces from various programming languages (C#, Java, Python, PHP, Ruby, Node.js, Go).

Your task is to analyze the provided logs/stack traces and extract the following information in JSON format:
1. File: The file path where the error occurred (if available). Extract just the filename if the full path is long (e.g., ""TestController.js"" from ""/app/Controllers/TestController.js:43:11"")
2. Line: The line number where the error occurred (if available, as an integer)
3. StackTrace: The full stack trace if available
4. LatestErrorSummary: A concise, human-readable summary of the error (2-3 sentences maximum)

IMPORTANT RULES:
- Return ONLY valid JSON, no markdown, no code blocks, no additional text
- If a field is not available, use null (not empty string)
- The JSON structure must be: {""File"": ""path/to/file"", ""Line"": 123, ""StackTrace"": ""..."", ""LatestErrorSummary"": ""...""}
- LatestErrorSummary should be clear and actionable, explaining what went wrong and why
- Look for error patterns common to the programming language (compile errors, syntax errors, import errors, runtime exceptions, etc.)
- If multiple errors exist, focus on the FIRST/PRIMARY error that caused the failure
- Extract file paths and line numbers from error messages, stack traces, or compiler output
- For stack traces, include the full trace if available, otherwise include what's available
- For Node.js stack traces, look for patterns like ""at functionName (file:line:column)"" or ""at file:line:column""
- For Python stack traces, look for patterns like ""File ""/path/to/file.py"", line X, in function""
- For Go stack traces, look for patterns like ""/path/to/file.go:line""
- For PHP stack traces, look for patterns like ""#X /path/to/file.php(line)""
- Extract the FIRST user code location (skip framework/library code when possible)

Return the JSON response now:";

            // Truncate build output if too long (keep last 8000 characters to preserve recent errors)
            var truncatedOutput = buildOutput.Length > 8000
                ? "..." + buildOutput.Substring(buildOutput.Length - 8000)
                : buildOutput;

            var userPrompt = $"Analyze the following build/deployment logs and extract error information:\n\n{truncatedOutput}";

            // Call OpenAI with the cheap model
            var aiResponse = await CallOpenAIAsync($"{systemPrompt}\n\n{userPrompt}", cheapModel);

            if (!aiResponse.Success)
            {
                _logger.LogError("Failed to parse build output: {Error}", aiResponse.ErrorMessage);
                return null;
            }

            // Parse the JSON response
            try
            {
                // Extract JSON from response (handle markdown code blocks if present)
                var jsonContent = aiResponse.Content.Trim();
                if (jsonContent.StartsWith("```json"))
                {
                    jsonContent = jsonContent.Substring(7);
                }
                if (jsonContent.StartsWith("```"))
                {
                    jsonContent = jsonContent.Substring(3);
                }
                if (jsonContent.EndsWith("```"))
                {
                    jsonContent = jsonContent.Substring(0, jsonContent.Length - 3);
                }
                jsonContent = jsonContent.Trim();

                var parsed = JsonSerializer.Deserialize<ParsedBuildOutput>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    _logger.LogInformation("Successfully parsed build output: File={File}, Line={Line}, HasSummary={HasSummary}",
                        parsed.File, parsed.Line, !string.IsNullOrEmpty(parsed.LatestErrorSummary));
                }

                return parsed;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize AI response as JSON. Response: {Response}", aiResponse.Content);
                // Return a fallback summary if JSON parsing fails
                return new ParsedBuildOutput
                {
                    LatestErrorSummary = aiResponse.Content.Length > 500
                        ? aiResponse.Content.Substring(0, 500) + "..."
                        : aiResponse.Content
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while parsing build output");
            return null;
        }
    }
}

// Helper class for parsing modules response
public class ModulesResponse
{
    public List<ModuleInfo> Modules { get; set; } = new();
}

public class OpenAIResponse
{
    public List<Choice> Choices { get; set; } = new List<Choice>();
}

public class Choice
{
    public OpenAIMessage Message { get; set; } = new OpenAIMessage();
}

public class OpenAIMessage
{
    public string Content { get; set; } = string.Empty;
}

public class CodebaseStructureAIResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CodebaseStructure? CodebaseStructure { get; set; }
}

public class CodebaseStructure
{
    public List<CodeFile> Models { get; set; } = new();
    public List<CodeFile> Services { get; set; } = new();
    public List<CodeFile> Controllers { get; set; } = new();
    public List<CodeFile> Views { get; set; } = new();
    public CodeFile? ApplicationDbContext { get; set; }
    public CodeFile? AppSettings { get; set; }
    public List<CodeFile> PublishProfiles { get; set; } = new();
    public List<CodeFile> SqlScripts { get; set; } = new();
    
    [JsonIgnore] // Ignore during serialization to avoid conflict with Devcontainer
    public CodeFile? DevContainer { get; set; } // Required for GitHub Codespaces (legacy)
    
    // New structure fields for full-stack development
    public NewCodebaseStructure? Devcontainer { get; set; }
    public List<CodeFile> Files { get; set; } = new();
}

// New structure returned by AI (devcontainer + files array)
public class NewCodebaseStructure
{
    public NewDevContainer? Devcontainer { get; set; }
    public List<CodeFile> Files { get; set; } = new();
}

public class NewDevContainer
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public Dictionary<string, object>? Features { get; set; }
    public Dictionary<string, object>? Customizations { get; set; }
    public int[]? ForwardPorts { get; set; }
    public string? PostCreateCommand { get; set; }
    public string? RemoteUser { get; set; }
}

public class CodeFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ClaudeResponse
{
    public List<ClaudeContent> Content { get; set; } = new();
    public ClaudeUsage? Usage { get; set; }
}

public class ClaudeUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public class ClaudeContent
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class TranslateModuleResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public class TranslateTextResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Text { get; set; }
}

public class ProjectCriteriaClassificationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? CriteriaIds { get; set; } // Comma-separated string of CriteriaIds
}

file sealed class ModuleTranslationResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

file sealed class TextTranslationResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }


    

}

