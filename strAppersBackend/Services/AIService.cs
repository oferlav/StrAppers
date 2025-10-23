using System.Text;
using System.Text.Json;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

public interface IAIService
{
    Task<SystemDesignResponse> GenerateSystemDesignAsync(SystemDesignRequest request);
    Task<SprintPlanningResponse> GenerateSprintPlanAsync(SprintPlanningRequest request);
    Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription);
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

    public AIService(HttpClient httpClient, IConfiguration configuration, ILogger<AIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<SystemDesignResponse> GenerateSystemDesignAsync(SystemDesignRequest request)
    {
        try
        {
            _logger.LogInformation("Generating system design for Project {ProjectId}", request.ProjectId);

            // Generate JSON system design
            var jsonPrompt = BuildSystemDesignPrompt(request);
            var jsonAiResponse = await CallOpenAIAsync(jsonPrompt, "gpt-4o");
            
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
            var formattedAiResponse = await CallOpenAIAsync(formattedPrompt, "gpt-4o");
            
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
            aiResponse = await CallOpenAIAsync(prompt, "gpt-4o");
            
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
        var totalSprints = request.ProjectLengthWeeks / request.SprintLengthWeeks;
        
        return $@"You are an agile project manager. Generate a comprehensive sprint plan based on the system design document for this project.

PROJECT LENGTH: {request.ProjectLengthWeeks} weeks total
SPRINT LENGTH: {request.SprintLengthWeeks} weeks per sprint
TEAM COMPOSITION: {teamRolesText}

IMPORTANT: You must respond with ONLY valid JSON. Do not include any explanatory text, markdown formatting, or code blocks. Start your response directly with the opening curly brace {{ and end with the closing curly brace }}.

CRITICAL: The tasks array in each sprint must contain FULL ProjectTask objects with all required fields (id, title, description, roleId, roleName, estimatedHours, priority, dependencies). Do NOT use task IDs or references.

PLANNING CONSTRAINTS:
- The total project duration is {request.ProjectLengthWeeks} weeks
- Each sprint is {request.SprintLengthWeeks} weeks long
- You MUST create exactly {totalSprints} sprints to fill the entire project timeline
- Start date: {request.StartDate:yyyy-MM-dd}
- Plan the sprints to fit within the {request.ProjectLengthWeeks}-week project timeline
- Consider the project length when determining the scope and number of sprints
- Ensure all critical features can be delivered within the project timeline

CRITICAL SPRINT FILLING REQUIREMENTS:
- EVERY sprint must have tasks assigned to it - NO EMPTY SPRINTS ALLOWED
- EVERY role in the team must have tasks in EVERY sprint - NO ROLE LEFT WITHOUT WORK
- If you don't have enough information from the SystemDesign, INVENT realistic tasks for each role
- Distribute work evenly across all sprints so no sprint is overloaded or empty
- Each sprint should have a balanced mix of tasks for all team roles
- Include tasks like: research, planning, documentation, testing, code review, deployment, maintenance
- Make sure every role has meaningful work in every sprint

Generate a sprint plan in JSON format with the following structure:

1. **Epics** - High-level feature groups
2. **User Stories** - Detailed requirements under each epic
3. **Tasks** - Specific development tasks for each user story
4. **Sprints** - Sprint breakdown with task allocation
5. **Role Allocation** - Tasks assigned to specific roles

For each task, consider:
- Role assignment based on team composition
- Estimated hours (realistic for student developers)
- Dependencies between tasks
- Priority levels

TASK DISTRIBUTION RULES:
- Each sprint must have at least 2-3 tasks per role to keep everyone busy
- Include a mix of: development tasks, testing tasks, documentation tasks, research tasks, code review tasks
- If you run out of specific features, add generic tasks like: ""Code review and testing"", ""Documentation updates"", ""Performance optimization"", ""Bug fixes"", ""User training materials""
- Make sure every role has meaningful work in every single sprint
- Balance the workload so no role is overloaded or underutilized

Return ONLY the JSON object matching this structure:
{{
  ""epics"": [
    {{
      ""id"": ""epic1"",
      ""name"": ""Epic Name"",
      ""description"": ""Epic description"",
      ""userStories"": [
        {{
          ""id"": ""story1"",
          ""title"": ""Story Title"",
          ""description"": ""Story description"",
          ""acceptanceCriteria"": ""Acceptance criteria"",
          ""tasks"": [
            {{
              ""id"": ""task1"",
              ""title"": ""Task Title"",
              ""description"": ""Task description"",
              ""roleId"": 1,
              ""roleName"": ""Role Name"",
              ""estimatedHours"": 8,
              ""priority"": 1,
              ""dependencies"": []
            }}
          ],
          ""storyPoints"": 5,
          ""priority"": 1
        }}
      ],
      ""priority"": 1
    }}
  ],
  ""sprints"": [
    {{
      ""sprintNumber"": 1,
      ""name"": ""Sprint 1"",
      ""startDate"": ""2024-01-01"",
      ""endDate"": ""2024-01-14"",
      ""tasks"": [
        {{
          ""id"": ""task1"",
          ""title"": ""Task for Role 1"",
          ""description"": ""Task description"",
          ""roleId"": 1,
          ""roleName"": ""Role 1"",
          ""estimatedHours"": 8,
          ""priority"": 1,
          ""dependencies"": []
        }},
        {{
          ""id"": ""task2"",
          ""title"": ""Task for Role 2"",
          ""description"": ""Task description"",
          ""roleId"": 2,
          ""roleName"": ""Role 2"",
          ""estimatedHours"": 8,
          ""priority"": 1,
          ""dependencies"": []
        }}
      ],
      ""totalStoryPoints"": 5,
      ""roleWorkload"": {{""1"": 8, ""2"": 8}}
    }}
  ],
  ""totalSprints"": {totalSprints},
  ""totalTasks"": 0,
  ""estimatedWeeks"": {request.ProjectLengthWeeks}
}}";
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
                max_tokens = 8000,
                temperature = 0.7
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

    public async Task<InitiateModulesResponse> InitiateModulesAsync(int projectId, string extendedDescription)
    {
        try
        {
            _logger.LogInformation("Initiating modules for Project {ProjectId}", projectId);

            var promptConfig = _configuration.GetSection("PromptConfig:ProjectModules:InitiateModules");
            var systemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert software architect.";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Project Description: {0}";
            var userPrompt = string.Format(userPromptTemplate, extendedDescription);

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = "gpt-4o",
                messages = messages,
                max_tokens = 4000,
                temperature = 0.7
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
            var systemPrompt = promptConfig["SystemPrompt"] ?? "You are an expert software architect and technical writer.";
            var userPromptTemplate = promptConfig["UserPromptTemplate"] ?? "Current Module Description:\n{0}\n\nUser Feedback:\n{1}";
            var userPrompt = string.Format(userPromptTemplate, currentDescription, userInput);

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var requestBody = new
            {
                model = "gpt-4o",
                messages = messages,
                max_tokens = 3000,
                temperature = 0.5
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
