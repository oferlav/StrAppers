using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

public partial class MentorController
{
    private const int TaskBuilderTrelloJsonMaxChars = 120_000;

    /// <summary>
    /// Task Builder assistant: module + project extended description + full Trello template JSON + sprint/role + user message.
    /// Returns optional <c>description</c> / <c>checklistItems</c> for the selected sprint+role card (same shape as <c>sprintPlan.cards[].description</c> and <c>checklistItems</c>).
    /// </summary>
    [HttpPost("use/task-builder")]
    public async Task<ActionResult<object>> TaskBuilderAssistant(
        [FromBody] TaskBuilderMentorRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (request.ProjectId <= 0)
            return BadRequest(new { success = false, message = "ProjectId is required." });
        if (string.IsNullOrWhiteSpace(request.UserMessage))
            return BadRequest(new { success = false, message = "UserMessage is required." });

        var project = await _context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project == null)
            return NotFound(new { success = false, message = $"Project {request.ProjectId} not found." });

        ProjectModule? module = null;
        if (request.ModuleId > 0)
        {
            module = await _context.ProjectModules.AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.Id == request.ModuleId && m.ProjectId == request.ProjectId,
                    cancellationToken);
        }

        var trelloRaw = request.TrelloBoardJson?.Trim() ?? "{}";
        if (trelloRaw.Length > TaskBuilderTrelloJsonMaxChars)
        {
            trelloRaw = trelloRaw[..TaskBuilderTrelloJsonMaxChars] +
                        "\n\n… [truncated for prompt size; staff UI holds full JSON.]";
        }

        var systemPrompt = LoadMentorPromptFile("TaskBuilderAssistant")
            ?? "You are a helpful mentor for task templates. Output JSON only with keys aiReply, description, checklistItems.";

        var userSb = new StringBuilder();
        userSb.AppendLine("## Selected sprint / role");
        userSb.AppendLine($"- Sprint list name: {(string.IsNullOrWhiteSpace(request.SprintName) ? "(none)" : request.SprintName.Trim())}");
        userSb.AppendLine($"- Role: {(string.IsNullOrWhiteSpace(request.RoleName) ? "(none)" : request.RoleName.Trim())}");
        userSb.AppendLine("- Task Builder may only return updates for this card’s task description + checklist (Sprint Overview + Checklist in the UI). Module and project text below is context only — not editable via this endpoint.");
        userSb.AppendLine();
        userSb.AppendLine("## Project");
        userSb.AppendLine($"- Id: {project.Id}");
        userSb.AppendLine($"- Title: {project.Title ?? "(none)"}");
        userSb.AppendLine("- ExtendedDescription:");
        userSb.AppendLine(string.IsNullOrWhiteSpace(project.ExtendedDescription)
            ? "(none)"
            : project.ExtendedDescription!.Trim());
        userSb.AppendLine();
        userSb.AppendLine("## Selected module");
        if (request.ModuleId <= 0 || module == null)
        {
            userSb.AppendLine("(No module selected or module not found for this project.)");
        }
        else
        {
            userSb.AppendLine($"- ModuleId: {module.Id}");
            userSb.AppendLine($"- Title: {module.Title ?? "(none)"}");
            userSb.AppendLine("- Description:");
            userSb.AppendLine(string.IsNullOrWhiteSpace(module.Description) ? "(none)" : module.Description!.Trim());
        }

        userSb.AppendLine();
        userSb.AppendLine("## Current card fields in Task Builder (may be empty)");
        userSb.AppendLine("- Description:");
        userSb.AppendLine(string.IsNullOrWhiteSpace(request.CurrentDescription)
            ? "(none)"
            : request.CurrentDescription!.Trim());
        userSb.AppendLine("- Checklist items:");
        if (request.CurrentChecklistItems == null || request.CurrentChecklistItems.Count == 0)
            userSb.AppendLine("(none)");
        else
        {
            foreach (var line in request.CurrentChecklistItems)
                userSb.AppendLine($"  - {line}");
        }

        userSb.AppendLine();
        userSb.AppendLine("## Full TrelloBoardJson (template)");
        userSb.AppendLine(trelloRaw);
        userSb.AppendLine();
        userSb.AppendLine("## User message");
        userSb.AppendLine(request.UserMessage.Trim());

        var userPrompt = userSb.ToString();

        if (request.Test)
        {
            return Ok(new
            {
                success = true,
                test = true,
                systemPrompt = StripDebugMarkers(systemPrompt.Trim()),
                userPrompt,
            });
        }

        try
        {
            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 8192,
                DefaultTemperature = 0.25,
            };

            var (llmText, inputTokens, outputTokens) =
                await _chatCompletionService.GetChatCompletionAsync(
                    aiModel,
                    StripDebugMarkers(systemPrompt.Trim()),
                    userPrompt,
                    null);

            if (!TryParseTaskBuilderMentorJson(llmText, out var parsed) || parsed == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "Assistant did not return valid JSON. Try a shorter question or rephrase.",
                    preview = TruncateForTaskBuilder(llmText.Trim(), 4000),
                    inputTokens,
                    outputTokens,
                });
            }

            var aiReply = string.IsNullOrWhiteSpace(parsed.AiReply)
                ? "(No aiReply in model output.)"
                : parsed.AiReply.Trim();

            List<string>? checklist = null;
            if (parsed.ChecklistItems is { Count: > 0 })
            {
                checklist = parsed.ChecklistItems
                    .Select(s => (s ?? "").Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
                if (checklist.Count == 0)
                    checklist = null;
            }

            string? description = string.IsNullOrWhiteSpace(parsed.Description)
                ? null
                : parsed.Description.Trim();

            return Ok(new
            {
                success = true,
                aiReply,
                description,
                checklistItems = checklist,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task builder assistant failed for ProjectId={ProjectId}", request.ProjectId);
            return StatusCode(500, new { success = false, message = "Task builder assistant failed.", detail = ex.Message });
        }
    }

    private static string TruncateForTaskBuilder(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    private static bool TryParseTaskBuilderMentorJson(string llmText, out TaskBuilderMentorLlmResult? dto)
    {
        dto = null;
        var json = ExtractJsonObjectForTaskBuilder(llmText);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            dto = JsonSerializer.Deserialize<TaskBuilderMentorLlmResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            return dto != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractJsonObjectForTaskBuilder(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            t = t["```json".Length..].Trim();
        if (t.StartsWith("```"))
            t = t[3..].Trim();
        if (t.EndsWith("```"))
            t = t[..^3].Trim();

        var i = t.IndexOf('{');
        var j = t.LastIndexOf('}');
        if (i < 0 || j <= i)
            return null;
        return t[i..(j + 1)];
    }
}
