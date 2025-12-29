using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services;

/// <summary>
/// Result of a code review
/// </summary>
public class CodeReviewResult
{
    public bool Success { get; set; }
    public string ReviewText { get; set; } = string.Empty;
    public List<CodeReviewIssue> Issues { get; set; } = new();
    public List<CodeReviewSuggestion> Suggestions { get; set; } = new();
    public double AlignmentScore { get; set; } // 0-1, how well code matches requirements
}

/// <summary>
/// Code review issue
/// </summary>
public class CodeReviewIssue
{
    public string FilePath { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public string Severity { get; set; } = "info"; // "error", "warning", "info"
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "general"; // "requirements", "quality", "best_practices"
}

/// <summary>
/// Code review suggestion
/// </summary>
public class CodeReviewSuggestion
{
    public string FilePath { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Interface for code review agent
/// </summary>
public interface ICodeReviewAgent
{
    Task<CodeReviewResult> ReviewCodeAsync(
        GitHubCommitDiff commitDiff,
        string? moduleDescription,
        string? taskDescription,
        string programmingLanguage,
        AIModel aiModel,
        string systemPrompt,
        string userPrompt);
}

public class CodeReviewAgent : ICodeReviewAgent
{
    private readonly ILogger<CodeReviewAgent> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PromptConfig _promptConfig;

    public CodeReviewAgent(
        ILogger<CodeReviewAgent> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IOptions<PromptConfig> promptConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _promptConfig = promptConfig.Value;
    }

    public async Task<CodeReviewResult> ReviewCodeAsync(
        GitHubCommitDiff commitDiff,
        string? moduleDescription,
        string? taskDescription,
        string programmingLanguage,
        AIModel aiModel,
        string systemPrompt,
        string userPrompt)
    {
        try
        {
            // Validate that we have actual code to review
            if (commitDiff == null)
            {
                _logger.LogWarning("Attempted code review with null commitDiff");
                return new CodeReviewResult
                {
                    Success = false,
                    ReviewText = "No code changes found to review. Please make sure your code is committed and pushed to GitHub."
                };
            }

            if (commitDiff.FileChanges == null || !commitDiff.FileChanges.Any() || commitDiff.TotalFilesChanged == 0)
            {
                _logger.LogWarning("Attempted code review with no file changes for commit {CommitSha}", commitDiff.CommitSha);
                return new CodeReviewResult
                {
                    Success = false,
                    ReviewText = "No code changes found in this commit to review. Please make sure you've committed actual code files."
                };
            }

            _logger.LogInformation("Starting code review for commit {CommitSha} with {FileCount} files changed", 
                commitDiff.CommitSha, commitDiff.TotalFilesChanged);

            // Build code review prompt
            var reviewPrompt = BuildCodeReviewPrompt(commitDiff, moduleDescription, taskDescription, programmingLanguage, userPrompt);

            // Call AI API based on provider
            string aiResponse;
            if (aiModel.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                aiResponse = await CallOpenAIAsync(aiModel, systemPrompt, reviewPrompt);
            }
            else if (aiModel.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
            {
                aiResponse = await CallAnthropicAsync(aiModel, systemPrompt, reviewPrompt);
            }
            else
            {
                return new CodeReviewResult
                {
                    Success = false,
                    ReviewText = $"Unsupported AI provider: {aiModel.Provider}"
                };
            }

            // Parse AI response and create result
            var result = ParseCodeReviewResponse(aiResponse, commitDiff);
            result.Success = true;

            _logger.LogInformation("Code review completed for commit {CommitSha}. Found {IssueCount} issues, {SuggestionCount} suggestions",
                commitDiff.CommitSha, result.Issues.Count, result.Suggestions.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing code review for commit {CommitSha}", commitDiff.CommitSha);
            return new CodeReviewResult
            {
                Success = false,
                ReviewText = $"Error performing code review: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Builds the code review prompt with all context
    /// </summary>
    private string BuildCodeReviewPrompt(
        GitHubCommitDiff commitDiff,
        string? moduleDescription,
        string? taskDescription,
        string programmingLanguage,
        string originalUserPrompt)
    {
        var sb = new StringBuilder();

        // Use configured prompt header, fallback to default if not configured
        var header = !string.IsNullOrWhiteSpace(_promptConfig.Mentor.CodeReview.ReviewUserPromptHeader)
            ? _promptConfig.Mentor.CodeReview.ReviewUserPromptHeader
            : "IMPORTANT: You are reviewing ACTUAL code changes from a real GitHub commit. The code diff is provided below.\nYou MUST provide REAL feedback based on the ACTUAL code you see. Do NOT use templates or placeholders.\nStart your review immediately with specific observations about the actual code.";
        sb.AppendLine(header);
        sb.AppendLine();

        // Commit information
        sb.AppendLine("=== COMMIT INFORMATION ===");
        sb.AppendLine($"Commit SHA: {commitDiff.CommitSha}");
        sb.AppendLine($"Commit Message: {commitDiff.CommitMessage}");
        sb.AppendLine($"Date: {commitDiff.CommitDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Author: {commitDiff.Author}");
        sb.AppendLine($"Files Changed: {commitDiff.TotalFilesChanged}");
        sb.AppendLine($"Additions: +{commitDiff.TotalAdditions}, Deletions: -{commitDiff.TotalDeletions}");
        sb.AppendLine();

        // Module design context - RELEVANCE CHECK (be reasonable)
        if (!string.IsNullOrWhiteSpace(moduleDescription) || !string.IsNullOrWhiteSpace(taskDescription))
        {
            sb.AppendLine("=== RELEVANCE CHECK (BE REASONABLE) ===");
            sb.AppendLine("Check if the code is related to the module/task requirements.");
            sb.AppendLine("BE REASONABLE:");
            sb.AppendLine("- If the code is about tickets/ticket creation/ticket management, and the task mentions 'ticket', it IS relevant");
            sb.AppendLine("- If the code implements functionality related to the task description, it IS relevant");
            sb.AppendLine("- Only reject if the code is COMPLETELY unrelated (e.g., weather API code when task is about tickets)");
            sb.AppendLine("- If the code is even PARTIALLY relevant, proceed with code review");
            sb.AppendLine("- If truly not relevant, state it clearly in the first sentence, then still provide code quality feedback");
            sb.AppendLine();
        }

        // Module design context
        if (!string.IsNullOrWhiteSpace(moduleDescription))
        {
            sb.AppendLine("=== MODULE DESIGN REQUIREMENTS (COMPARE CODE TO THIS) ===");
            sb.AppendLine(moduleDescription);
            sb.AppendLine();
        }

        // Task description context
        if (!string.IsNullOrWhiteSpace(taskDescription))
        {
            sb.AppendLine("=== TASK DESCRIPTION (COMPARE CODE TO THIS) ===");
            sb.AppendLine(taskDescription);
            sb.AppendLine();
        }

        // Programming language
        sb.AppendLine($"=== PROGRAMMING LANGUAGE ===");
        sb.AppendLine(programmingLanguage);
        sb.AppendLine();

        // Code changes
        sb.AppendLine("=== CODE CHANGES ===");
        bool hasAnyPatch = false;
        int filesWithPatch = 0;
        int filesWithoutPatch = 0;
        
        foreach (var fileChange in commitDiff.FileChanges)
        {
            sb.AppendLine($"File: {fileChange.FilePath} ({fileChange.Status})");
            sb.AppendLine($"Changes: +{fileChange.Additions} -{fileChange.Deletions}");
            
            if (!string.IsNullOrWhiteSpace(fileChange.Patch))
            {
                hasAnyPatch = true;
                filesWithPatch++;
                _logger.LogInformation("File {FilePath} has patch content ({Length} chars) in commit {CommitSha}", 
                    fileChange.FilePath, fileChange.Patch.Length, commitDiff.CommitSha);
                
                // Limit patch size to avoid token limits (show first 5000 chars per file for better context)
                var patchPreview = fileChange.Patch.Length > 5000 
                    ? fileChange.Patch.Substring(0, 5000) + "\n... (truncated - showing first 5000 characters)" 
                    : fileChange.Patch;
                sb.AppendLine("Diff (ACTUAL CODE):");
                sb.AppendLine("```");
                sb.AppendLine(patchPreview);
                sb.AppendLine("```");
            }
            else
            {
                filesWithoutPatch++;
                _logger.LogWarning("File {FilePath} has NO patch content in commit {CommitSha}. Additions: {Additions}, Deletions: {Deletions}, Status: {Status}", 
                    fileChange.FilePath, commitDiff.CommitSha, fileChange.Additions, fileChange.Deletions, fileChange.Status);
                sb.AppendLine("⚠️ WARNING: No code diff/patch available for this file. The file was changed but the actual code content is not visible.");
            }
            sb.AppendLine();
        }
        
        _logger.LogInformation("Commit {CommitSha} patch summary: {FilesWithPatch} files with patch, {FilesWithoutPatch} files without patch", 
            commitDiff.CommitSha, filesWithPatch, filesWithoutPatch);
        
        if (!hasAnyPatch)
        {
            _logger.LogError("CRITICAL: No patch content found for ANY files in commit {CommitSha}. Cannot perform code review.", commitDiff.CommitSha);
            sb.AppendLine("🚨 CRITICAL: No actual code content (patch/diff) is available for this commit.");
            sb.AppendLine("This means I cannot see the actual code changes to review.");
            sb.AppendLine("Please ensure your code is committed and pushed to GitHub with the actual file content.");
        }

        // Use configured review instructions, fallback to default if not configured
        var instructions = !string.IsNullOrWhiteSpace(_promptConfig.Mentor.CodeReview.ReviewInstructions)
            ? _promptConfig.Mentor.CodeReview.ReviewInstructions
            : "=== REVIEW INSTRUCTIONS ===\nCRITICAL: Review the ACTUAL code diff provided above. Do NOT use templates or placeholders.\nProvide a comprehensive code review that:\n1. Checks if the code aligns with the module design requirements (reference specific code lines)\n2. Verifies if the code fulfills the task description (cite actual code examples)\n3. Identifies any code quality issues, bugs, or potential improvements (point to specific lines/files)\n4. Suggests best practices for the programming language (with examples from the actual code)\n5. Provides actionable feedback based on the REAL code you reviewed\n\nYour response must reference actual files, code snippets, and line numbers from the diff above.\nIf you see good code, say what's good about it. If you see issues, describe them specifically.\nNEVER use placeholders like '[Insert...]' or '[Provide...]' - provide the actual review now.";
        sb.AppendLine(instructions);

        return sb.ToString();
    }

    /// <summary>
    /// Parses AI response into structured code review result
    /// </summary>
    private CodeReviewResult ParseCodeReviewResponse(string aiResponse, GitHubCommitDiff commitDiff)
    {
        // Clean response - remove any placeholder-like text
        var cleanedResponse = RemovePlaceholders(aiResponse);
        
        // Validate response - reject template responses
        if (IsTemplateResponse(cleanedResponse))
        {
            _logger.LogWarning("AI generated a template response, rejecting it");
            return new CodeReviewResult
            {
                Success = false,
                ReviewText = "I apologize, but I need to review your actual code. Please make sure your code is committed and pushed to GitHub, then ask me to review it again."
            };
        }

        var result = new CodeReviewResult
        {
            ReviewText = cleanedResponse,
            AlignmentScore = 0.7 // Default score, could be extracted from AI response if structured
        };

        // Try to extract issues and suggestions from the response
        // This is a simple implementation - could be enhanced with structured output from AI
        ExtractIssuesAndSuggestions(cleanedResponse, result, commitDiff);

        return result;
    }

    /// <summary>
    /// Removes placeholder-like text from AI response
    /// </summary>
    private string RemovePlaceholders(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Remove lines that are just placeholders
        var lines = response.Split('\n');
        var cleanedLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip lines that are just placeholders
            if (Regex.IsMatch(trimmedLine, 
                @"^\[(?:Provide|Comment|Mention|Discuss|Highlight|Insert|Any|Suggestions?|Areas?)[^\]]*\]$", 
                RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("Removing placeholder line: {Line}", trimmedLine);
                continue;
            }
            
            // Remove placeholder patterns from within lines
            var cleanedLine = Regex.Replace(trimmedLine, 
                @"\[(?:Provide|Comment|Mention|Discuss|Highlight|Insert|Any|Suggestions?|Areas?)[^\]]*\]", 
                "", 
                RegexOptions.IgnoreCase);
            
            if (!string.IsNullOrWhiteSpace(cleanedLine))
            {
                cleanedLines.Add(cleanedLine);
            }
        }
        
        return string.Join("\n", cleanedLines).Trim();
    }

    /// <summary>
    /// Detects if the AI response is a template response that should be rejected
    /// </summary>
    private bool IsTemplateResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return true;

        var lowerResponse = response.ToLowerInvariant();

        // Check for any bracket-placeholder patterns (case-insensitive)
        if (Regex.IsMatch(response, @"\[(?:Provide|Comment|Mention|Discuss|Highlight|Insert|Any|Suggestions?|Areas?)[^\]]*\]", 
            RegexOptions.IgnoreCase))
        {
            _logger.LogWarning("Detected placeholder pattern in response");
            return true;
        }

        // Check for template phrases (case-insensitive)
        var templatePhrases = new[]
        {
            "[provide",
            "[comment",
            "[mention",
            "[discuss",
            "[highlight",
            "[any suggestions",
            "[insert",
            "code quality: [",
            "functionality: [",
            "suggestions: [",
            "let me check",
            "i'll review",
            "i'll look",
            "please hold on",
            "i'll get back to you",
            "get back to you",
            "review summary",
            "overall structure: [",
            "best practices: [",
            "areas for improvement: [",
            "feedback summary",
            "feedback on your code"
        };

        // Check if response contains template phrases
        foreach (var phrase in templatePhrases)
        {
            if (lowerResponse.Contains(phrase))
            {
                _logger.LogWarning("Detected template phrase: {Phrase}", phrase);
                return true;
            }
        }

        // Check if response has section headers followed by brackets/placeholders
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Check for any line that contains brackets (likely a placeholder)
            if (line.Contains("[") && line.Contains("]"))
            {
                // Check if it's a placeholder pattern (not just a code reference)
                if (Regex.IsMatch(line, @"\[(?:Provide|Comment|Mention|Discuss|Highlight|Insert|Any|Suggestions?|Areas?)[^\]]*\]", 
                    RegexOptions.IgnoreCase))
                {
                    _logger.LogWarning("Detected placeholder in line: {Line}", line);
                    return true;
                }
            }
            
            // Check for section headers followed by placeholder-like content
            if (i < lines.Length - 1 && (line.EndsWith(":") || line.EndsWith("?")))
            {
                var nextLine = lines[i + 1].Trim();
                if (nextLine.StartsWith("[") || 
                    Regex.IsMatch(nextLine, @"\[(?:Provide|Comment|Mention|Discuss|Highlight)", 
                        RegexOptions.IgnoreCase))
                {
                    _logger.LogWarning("Detected template structure: {Line} -> {NextLine}", line, nextLine);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts issues and suggestions from AI response text
    /// </summary>
    private void ExtractIssuesAndSuggestions(string response, CodeReviewResult result, GitHubCommitDiff commitDiff)
    {
        // Simple pattern matching to extract issues and suggestions
        // This could be improved with structured AI output or more sophisticated parsing

        var lines = response.Split('\n');
        var currentFilePath = "";
        
        foreach (var line in lines)
        {
            var lowerLine = line.ToLowerInvariant();
            
            // Detect file mentions
            foreach (var fileChange in commitDiff.FileChanges)
            {
                if (lowerLine.Contains(fileChange.FilePath.ToLowerInvariant()))
                {
                    currentFilePath = fileChange.FilePath;
                    break;
                }
            }

            // Detect issues (simple heuristics)
            if (lowerLine.Contains("error") || lowerLine.Contains("bug") || lowerLine.Contains("issue"))
            {
                result.Issues.Add(new CodeReviewIssue
                {
                    FilePath = currentFilePath,
                    Severity = lowerLine.Contains("error") || lowerLine.Contains("critical") ? "error" : "warning",
                    Message = line.Trim(),
                    Category = "quality"
                });
            }

            // Detect suggestions
            if (lowerLine.Contains("suggest") || lowerLine.Contains("recommend") || lowerLine.Contains("consider"))
            {
                result.Suggestions.Add(new CodeReviewSuggestion
                {
                    FilePath = currentFilePath,
                    Suggestion = line.Trim(),
                    Reason = "AI recommendation"
                });
            }
        }
    }

    /// <summary>
    /// Calls OpenAI API for code review
    /// </summary>
    private async Task<string> CallOpenAIAsync(AIModel aiModel, string systemPrompt, string reviewPrompt)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key not configured");
        }

        var baseUrl = aiModel.BaseUrl ?? "https://api.openai.com/v1";
        var maxTokens = aiModel.MaxTokens ?? 16384;
        // Use lower temperature for code reviews to reduce template responses
        var temperature = 0.1;

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        var requestBody = new
        {
            model = aiModel.Name,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = reviewPrompt }
            },
            max_tokens = maxTokens,
            temperature = temperature
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API error: {response.StatusCode}. {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var openAIResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (openAIResponse.TryGetProperty("choices", out var choicesProp) && 
            choicesProp.ValueKind == JsonValueKind.Array && 
            choicesProp.GetArrayLength() > 0)
        {
            var firstChoice = choicesProp[0];
            if (firstChoice.TryGetProperty("message", out var messageProp))
            {
                if (messageProp.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? "";
                }
            }
        }

        throw new Exception("Failed to parse OpenAI response");
    }

    /// <summary>
    /// Calls Anthropic API for code review
    /// </summary>
    private async Task<string> CallAnthropicAsync(AIModel aiModel, string systemPrompt, string reviewPrompt)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Anthropic API key not configured");
        }

        var baseUrl = aiModel.BaseUrl ?? "https://api.anthropic.com/v1";
        var apiVersion = aiModel.ApiVersion ?? "2023-06-01";
        var maxTokens = aiModel.MaxTokens ?? 200000;
        // Use lower temperature for code reviews to reduce template responses
        var temperature = 0.1;

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", apiVersion);
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        var requestBody = new
        {
            model = aiModel.Name,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = reviewPrompt }
            },
            temperature = temperature
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl}/messages", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Anthropic API error: {response.StatusCode}. {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var anthropicResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (anthropicResponse.TryGetProperty("content", out var contentProp) && 
            contentProp.ValueKind == JsonValueKind.Array && 
            contentProp.GetArrayLength() > 0)
        {
            var firstContent = contentProp[0];
            if (firstContent.TryGetProperty("text", out var textProp))
            {
                return textProp.GetString() ?? "";
            }
        }

        throw new Exception("Failed to parse Anthropic response");
    }
}
