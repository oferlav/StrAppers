using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface IAIService
{
    Task<SystemDesignResponse> GenerateSystemDesignAsync(SystemDesignRequest request);
    Task<SprintPlanningResponse> GenerateSprintPlanAsync(SprintPlanningRequest request);
    Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription, int maxModules, int minWordsPerModule);
    Task<CreateDataModelResponse> CreateDataModelAsync(int projectId, string modulesData);
    Task<UpdateModuleResponse> UpdateModuleAsync(int moduleId, string currentDescription, string userInput);
    Task<UpdateDataModelResponse> UpdateDataModelAsync(int projectId, string currentSqlScript, string userInput);
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
            
            // Try to deserialize with more flexible options
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            
            var sprintPlan = JsonSerializer.Deserialize<SprintPlan>(sprintPlanJson, options);
            
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
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        
        return $@"You are a senior software architect. Generate a comprehensive system design document based on the following project requirements:

PROJECT DESCRIPTION:
{request.ExtendedDescription}

TEAM COMPOSITION:
{teamRolesText}

Please generate a structured system design document in JSON format with the following sections:

1. **Architecture Overview**
   - System type (web app, mobile app, desktop, etc.)
   - High-level architecture pattern (MVC, microservices, monolithic, etc.)
   - Technology stack recommendations

2. **Core Components**
   - List of main system components
   - Component responsibilities
   - Component interactions

3. **Database Design**
   - Database type recommendation (PostgreSQL, MongoDB, etc.)
   - Key entities and relationships
   - Data flow patterns

4. **API Design**
   - RESTful API structure
   - Key endpoints
   - Authentication/authorization approach

5. **Infrastructure**
   - Deployment strategy
   - Hosting recommendations
   - Scalability considerations

6. **Security**
   - Security measures
   - Data protection strategies

7. **Development Guidelines**
   - Coding standards
   - Testing strategy
   - CI/CD recommendations

Return the response as a well-structured JSON object that can be easily parsed and stored in a database.";
    }

    private string BuildFormattedSystemDesignPrompt(SystemDesignRequest request)
    {
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        
        return $@"You are a senior software architect. Generate a concise, human-readable system design summary for the following project:

PROJECT DESCRIPTION:
{request.ExtendedDescription}

TEAM COMPOSITION:
{teamRolesText}

Please generate a brief, professional summary (maximum 2000 characters) that covers:

1. System Type & Architecture Pattern
2. Key Technologies Recommended
3. Main Components & Their Purpose
4. Database & Data Strategy
5. Deployment & Infrastructure

Format the response as clear, readable text with bullet points and short paragraphs. This will be displayed to stakeholders and team members who need a quick overview of the system design.

Keep it concise, professional, and easy to understand. DO NOT use JSON format - use plain text with formatting.";
    }

    private string BuildSprintPlanningPrompt(SprintPlanningRequest request)
    {
        var teamRolesText = string.Join(", ", request.TeamRoles.Select(r => $"{r.RoleName} ({r.StudentCount} students)"));
        var totalSprints = _systemDesignConfig.DefaultSprintCount; // Use configured sprint count instead of calculated
        
        // Filter out Data Model section to save tokens and focus on system modules
        var filteredSystemDesign = FilterOutDataModelSection(request.SystemDesign);
        
        // Build role-specific task instructions
        var roleInstructions = BuildRoleSpecificInstructions(request.TeamRoles);
        
        return $@"Generate a detailed sprint plan for a {request.ProjectLengthWeeks}-week project with {request.SprintLengthWeeks}-week sprints.

TEAM: {teamRolesText}
START DATE: {request.StartDate:yyyy-MM-dd}
TOTAL SPRINTS: {totalSprints} (CONFIGURED - must fill ALL sprints even if fewer modules)

SYSTEM DESIGN:
{filteredSystemDesign ?? "No system design available"}

CRITICAL REQUIREMENTS:
- You MUST create exactly {totalSprints} sprints (configured number, not based on modules)
- Each sprint must have tasks for ALL roles: {string.Join(", ", request.TeamRoles.Select(r => r.RoleName))}
- Map system modules to sprints (distribute modules across sprints as needed)
- Create MULTIPLE specific tasks per role per sprint based on module inputs/outputs
- Tasks must be detailed and specific to the module's functionality
- ONE sprint must include database layer tasks for Backend/Full Stack developers
- Tasks must have: id, title, description, roleId, roleName, estimatedHours, priority, dependencies

ROLE-SPECIFIC TASK GENERATION:
{roleInstructions}

TASK GENERATION RULES:
- For each module, analyze the Inputs and Outputs described
- Create specific tasks that implement those inputs/outputs
- Each task should be specific and actionable (not generic)
- Multiple tasks per role per sprint are expected and encouraged
- If you run out of modules, create additional tasks like: testing, documentation, integration, optimization, deployment

SPRINT DISTRIBUTION STRATEGY:
- Distribute the {GetModuleCount(filteredSystemDesign)} system modules across {totalSprints} sprints
- One sprint should focus on database layer implementation
- Remaining sprints should cover all modules plus additional tasks
- Ensure every sprint has meaningful work for all roles
- If you have fewer modules than sprints, create additional tasks like: testing, documentation, integration, optimization, deployment

Return ONLY valid JSON with exactly {totalSprints} sprints (NO EPICS):
{{
  ""sprints"": [{{""sprintNumber"": 1, ""name"": ""Sprint 1"", ""startDate"": ""{request.StartDate:yyyy-MM-dd}"", ""endDate"": ""{request.StartDate.AddDays(request.SprintLengthWeeks * 7 - 1):yyyy-MM-dd}"", ""tasks"": [{{""id"": ""task1"", ""title"": ""Specific Task Title"", ""description"": ""Detailed task description based on module inputs/outputs"", ""roleId"": 1, ""roleName"": ""Role"", ""estimatedHours"": 8, ""priority"": 1, ""dependencies"": []}}], ""totalStoryPoints"": 10, ""roleWorkload"": {{""1"": 8}}}}],
  ""totalSprints"": {totalSprints},
  ""totalTasks"": 0,
  ""estimatedWeeks"": {request.ProjectLengthWeeks}
}}";
    }

    /// <summary>
    /// Builds role-specific task generation instructions
    /// </summary>
    private string BuildRoleSpecificInstructions(List<RoleInfo> teamRoles)
    {
        var instructions = new List<string>();
        
        foreach (var role in teamRoles)
        {
            switch (role.RoleName.ToLower())
            {
                case "frontend developer":
                    instructions.Add($"- Frontend Developer: Focus on React/Vue/Angular components, user interfaces, client-side logic, responsive design, user experience, form validation, state management, API integration from frontend");
                    break;
                case "backend developer":
                    instructions.Add($"- Backend Developer: Focus on API development, server-side logic, database design, authentication, authorization, data processing, business logic, microservices, database optimization");
                    break;
                case "full stack developer":
                    instructions.Add($"- Full Stack Developer: Focus on both frontend and backend tasks, API integration, database design, full application features, end-to-end functionality, system integration");
                    break;
                case "ui/ux designer":
                    instructions.Add($"- UI/UX Designer: Focus on user interface design, user experience research, wireframes, prototypes, visual design, accessibility, user testing, design systems, mockups");
                    break;
                case "quality assurance":
                    instructions.Add($"- Quality Assurance: Focus on testing strategies, test case creation, automated testing, bug tracking, quality metrics, user acceptance testing, performance testing, security testing");
                    break;
                case "project manager":
                    instructions.Add($"- Project Manager: Focus on project coordination, task management, stakeholder communication, progress tracking, risk management, resource planning, documentation, team coordination");
                    break;
                case "marketing":
                    instructions.Add($"- Marketing: Focus on user acquisition strategies, content creation, social media integration, analytics, user engagement features, promotional materials, market research, and ALWAYS include a task for creating a video demo of the application");
                    break;
                case "documentation specialist":
                    instructions.Add($"- Documentation Specialist: Focus on technical documentation, user guides, API documentation, code documentation, training materials, knowledge base, help systems");
                    break;
                default:
                    instructions.Add($"- {role.RoleName}: Create appropriate tasks based on the role's typical responsibilities and the module requirements");
                    break;
            }
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
        var expectedSprints = _systemDesignConfig.DefaultSprintCount; // Use configured sprint count
        
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

    public async Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription, int maxModules, int minWordsPerModule)
    {
        try
        {
            _logger.LogInformation("Initiating modules for Project {ProjectId}", projectId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:InitiateModules");
            var baseSystemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert software architect.";
            
            // Enhance system prompt to emphasize word count requirements
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
                             $"Be verbose, thorough, and comprehensive - aim for {minWordsPerModule}+ words per description.\n" +
                             $"🌍 LANGUAGE CONSISTENCY: Always respond in the SAME language as the project description provided by the user.";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Project Description: {0}";
            
            // Enhance the user prompt with MaxModules and MinWordsPerModule constraints
            var enhancedUserPrompt = $"{string.Format(userPromptTemplate, extendedDescription)}\n\n" +
                                   $"🚨 CRITICAL REQUIREMENTS - NON-NEGOTIABLE 🚨\n" +
                                   $"- Generate modules based on the given content, but do not exceed {maxModules} modules\n" +
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
                                   $"- 🌍 LANGUAGE CONSISTENCY: Respond in the SAME language as the project description provided above";
            
            var userPrompt = enhancedUserPrompt;
            
            // Log the complete prompt being sent to AI
            _logger.LogInformation("=== AI PROMPT DEBUG ===");
            _logger.LogInformation("System Prompt: {SystemPrompt}", systemPrompt);
            _logger.LogInformation("User Prompt: {UserPrompt}", userPrompt);
            _logger.LogInformation("MaxModules: {MaxModules}, MinWordsPerModule: {MinWordsPerModule}", maxModules, minWordsPerModule);
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

            // Parse the JSON response
            var modulesResponse = JsonSerializer.Deserialize<ModulesResponse>(cleanedContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
            
            // Log details about each module for debugging
            for (int i = 0; i < modulesResponse.Modules.Count; i++)
            {
                var module = modulesResponse.Modules[i];
                var wordCount = module.Description?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
                _logger.LogInformation("Module {Index}: '{Title}' - {WordCount} words", i + 1, module.Title, wordCount);
            }

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
            
            // Remove the "Is this what you meant?" section
            var questionIndex = updatedDescription.IndexOf("Is this what you meant?", StringComparison.OrdinalIgnoreCase);
            if (questionIndex > 0)
            {
                updatedDescription = updatedDescription.Substring(0, questionIndex).Trim();
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
