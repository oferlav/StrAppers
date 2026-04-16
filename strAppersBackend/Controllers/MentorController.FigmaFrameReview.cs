using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MentorController
{
    /// <summary>
    /// UI/UX Figma frame review: builds system prompt from <c>Prompts/Mentor/FigmaFrameReviewSystem.txt</c> + sprint context (module, optional user story, UI/UX Trello card, customer chat),
    /// then runs the same semantic Figma → cheap LLM pipeline as <c>Test/figma/metadata-llm</c>.
    /// When <see cref="FigmaFrameReviewRequest.Test"/> is true, returns <c>generatedSystemPrompt</c> and skips Figma + LLM (for Swagger inspection).
    /// </summary>
    [HttpPost("use/figma-frame-review")]
    public async Task<ActionResult<object>> FigmaFrameReview([FromBody] FigmaFrameReviewRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 1)
            return BadRequest(new { success = false, message = "SprintNumber must be at least 1." });
        if (string.IsNullOrWhiteSpace(request.FigmaFileUrl))
            return BadRequest(new { success = false, message = "FigmaFileUrl is required (with ?node-id=…)." });

        var boardId = request.BoardId.Trim();
        try
        {
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
            if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

            var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId);
            if (board == null)
                return NotFound(new { success = false, message = $"Board {boardId} not found." });

            var template = LoadMentorPromptFile("FigmaFrameReviewSystem");
            if (string.IsNullOrWhiteSpace(template))
                return StatusCode(500, new { success = false, message = "Prompt file Prompts/Mentor/FigmaFrameReviewSystem.txt is missing or empty." });

            var sprintContext = await BuildFigmaFrameSprintContextMarkdownAsync(
                boardId,
                board.ProjectId,
                request.StudentId,
                request.SprintNumber);

            var systemPrompt = template.Replace("{{SPRINT_CONTEXT}}", sprintContext.Markdown.Trim(), StringComparison.Ordinal);

            if (request.Test)
            {
                return Ok(new
                {
                    success = true,
                    test = true,
                    generatedSystemPrompt = systemPrompt,
                    moduleIdFromTrello = sprintContext.ModuleIdFromTrello,
                    hadUserStorySection = sprintContext.HadUserStory,
                    hadUiUxCard = sprintContext.HadUiUxCard,
                    customerChatTurns = sprintContext.CustomerChatTurns,
                    message = "Test mode: Figma download and LLM were not called. Use test=false for a full review."
                });
            }

            var baseUrl = (_configuration["ApiBaseUrl"] ?? $"{Request.Scheme}://{Request.Host.Value}").TrimEnd('/');
            var figmaPayload = new DownloadMetadataRequest
            {
                BoardId = boardId,
                FigmaFileUrl = request.FigmaFileUrl.Trim(),
                Depth = request.FigmaDepth
            };
            var figmaJson = JsonSerializer.Serialize(figmaPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(10);
            using var figmaResponse = await http.PostAsync(
                $"{baseUrl}/api/Figma/use/download-metadata",
                new StringContent(figmaJson, Encoding.UTF8, "application/json"));

            var metadataBody = await figmaResponse.Content.ReadAsStringAsync();
            if (!figmaResponse.IsSuccessStatusCode)
            {
                // Forward download-metadata JSON as-is so Swagger shows the real Figma status (e.g. 429) and full figmaResponse text.
                return new ContentResult
                {
                    StatusCode = (int)figmaResponse.StatusCode,
                    Content = metadataBody,
                    ContentType = "application/json; charset=utf-8"
                };
            }

            var rawCharLength = metadataBody.Length;
            var pruned = false;
            if (request.PruneHeavyFigmaKeys)
            {
                var prunedBody = FigmaMetadataPruner.PruneHeavyKeys(metadataBody);
                pruned = prunedBody.Length < rawCharLength;
                metadataBody = prunedBody;
            }

            var preSemanticLength = metadataBody.Length;
            var semanticApplied = false;
            if (request.UseSemanticFigmaTransform)
            {
                var semantic = FigmaSemanticJsonTransformer.TryTransform(metadataBody);
                if (!string.IsNullOrEmpty(semantic))
                {
                    metadataBody = semantic;
                    semanticApplied = true;
                }
            }

            var maxMetadataChars = _configuration.GetValue("FigmaMetadataLlm:MaxMetadataChars", 2_000_000);
            if (metadataBody.Length > maxMetadataChars)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Figma JSON too large after processing. Narrow node-id, lower figmaDepth, or raise FigmaMetadataLlm:MaxMetadataChars.",
                    metadataCharLength = metadataBody.Length,
                    maxMetadataChars
                });
            }

            try
            {
                using var _ = JsonDocument.Parse(metadataBody);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { success = false, message = "Invalid JSON after Figma processing.", detail = ex.Message });
            }

            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 16384,
                DefaultTemperature = 0.2
            };

            var payloadKind = semanticApplied
                ? "logic-first semantic Figma JSON (hierarchy, auto-layout intent, text content, inferred roles — not a raw Figma API dump)."
                : "Figma file metadata JSON from GET /v1/files (download-metadata).";
            var userPrompt =
                $"The following is {payloadKind} Follow your system instructions when analyzing it.\n\n--- BEGIN FIGMA PAYLOAD ---\n" +
                metadataBody +
                "\n--- END FIGMA PAYLOAD ---";

            var (llmText, inputTokens, outputTokens) = await _chatCompletionService.GetChatCompletionAsync(
                aiModel,
                StripDebugMarkers(systemPrompt.Trim()),
                userPrompt);

            var totalTokensConsumed = inputTokens + outputTokens;

            if (!request.Test)
                await TryPersistCacheReviewAsync(boardId, request.StudentId, request.SprintNumber, CacheReviewType.Skill, llmText, cancellationToken);

            return Ok(new
            {
                success = true,
                test = false,
                model = cheapName,
                figmaJsonCharLength = metadataBody.Length,
                rawFigmaJsonCharLength = rawCharLength,
                figmaJsonPruned = pruned,
                semanticFigmaTransformApplied = semanticApplied,
                charLengthBeforeSemantic = preSemanticLength,
                figmaDepth = request.FigmaDepth,
                inputTokens,
                outputTokens,
                totalTokensConsumed,
                moduleIdFromTrello = sprintContext.ModuleIdFromTrello,
                hadUserStorySection = sprintContext.HadUserStory,
                hadUiUxCard = sprintContext.HadUiUxCard,
                customerChatTurns = sprintContext.CustomerChatTurns,
                llmResponse = llmText
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "figma-frame-review failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    private async Task<FigmaFrameSprintContextBuild> BuildFigmaFrameSprintContextMarkdownAsync(
        string boardId,
        int projectId,
        int studentId,
        int sprintNumber)
    {
        var sb = new StringBuilder();
        string? moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, "UI/UX Designer")
            ?? await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, "UI/UX");

        sb.AppendLine("## Sprint meta");
        sb.AppendLine($"- **BoardId:** `{boardId}`");
        sb.AppendLine($"- **ProjectId:** {projectId}");
        sb.AppendLine($"- **Sprint number:** {sprintNumber}");
        sb.AppendLine($"- **StudentId:** {studentId}");
        sb.AppendLine($"- **ModuleId (from UI/UX Designer Trello card in this sprint):** {(moduleIdStr ?? "(not set on card)")}");
        sb.AppendLine();

        var hadUserStory = false;
        var hadUiUx = false;

        if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleInt))
        {
            var pm = await _context.ProjectModules.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == moduleInt && m.ProjectId == projectId);
            sb.AppendLine("## Project module (database)");
            if (pm == null)
            {
                sb.AppendLine($"No `ProjectModules` row found for ModuleId **{moduleInt}** and ProjectId **{projectId}**.");
            }
            else
            {
                sb.AppendLine($"- **ModuleId:** {pm.Id}");
                sb.AppendLine($"- **Title:** {pm.Title ?? "(none)"}");
                sb.AppendLine("- **Description:**");
                sb.AppendLine(string.IsNullOrWhiteSpace(pm.Description) ? "(none)" : pm.Description.Trim());
            }
            sb.AppendLine();

            var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleIdStr.Trim());
            var usCard = ExtractUserStoryCardFromResult(usResult);
            if (usCard != null)
            {
                hadUserStory = true;
                sb.AppendLine("## User story (Trello — User Stories list, same ModuleId)");
                sb.AppendLine(FormatTrelloUserStoryCardForPrompt(usCard));
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("## Project module (database)");
            sb.AppendLine("Skipped: ModuleId could not be read from the UI/UX Designer card (set ModuleId custom field on that card).");
            sb.AppendLine();
        }

        var sprintSnap = await _trelloService.GetSprintFromBoardAsync(boardId, $"Sprint {sprintNumber}")
            ?? await _trelloService.GetSprintFromBoardAsync(boardId, $"Sprint{sprintNumber}");
        if (sprintSnap?.Cards == null || sprintSnap.Cards.Count == 0)
        {
            sb.AppendLine("## UI/UX Designer — sprint Trello card");
            sb.AppendLine($"No cards found for sprint list **Sprint {sprintNumber}** (or list missing).");
        }
        else
        {
            var uxCards = sprintSnap.Cards
                .Where(c => !string.IsNullOrEmpty(c.RoleName) &&
                            c.RoleName.Contains("UI/UX", StringComparison.OrdinalIgnoreCase))
                .ToList();
            sb.AppendLine("## UI/UX Designer — sprint Trello card(s)");
            if (uxCards.Count == 0)
            {
                sb.AppendLine("No card in this sprint list has a role label containing **UI/UX**.");
            }
            else
            {
                hadUiUx = true;
                for (var i = 0; i < uxCards.Count; i++)
                {
                    var c = uxCards[i];
                    if (uxCards.Count > 1)
                        sb.AppendLine($"### Card {i + 1} of {uxCards.Count}");
                    sb.AppendLine($"- **Card title:** {c.Name}");
                    sb.AppendLine($"- **Role label:** {c.RoleName}");
                    if (!string.IsNullOrWhiteSpace(c.Description))
                    {
                        sb.AppendLine("- **Description:**");
                        sb.AppendLine(c.Description.Trim());
                    }
                    if (c.ChecklistItems is { Count: > 0 })
                    {
                        sb.AppendLine("- **Tasks (all checklist items, flattened):**");
                        foreach (var item in c.ChecklistItems)
                            sb.AppendLine($"  - {item}");
                    }
                    else
                        sb.AppendLine("- **Tasks:** (no checklist items on this card)");
                    sb.AppendLine();
                }
            }
        }
        sb.AppendLine();

        var chatRows = await _context.CustomerChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();

        sb.AppendLine("## Customer chat history (this student + sprint)");
        if (chatRows.Count == 0)
        {
            sb.AppendLine("(No messages in `CustomerChatHistory` for this StudentId and SprintId.)");
        }
        else
        {
            foreach (var row in chatRows)
            {
                var role = row.Role?.Trim().ToLowerInvariant() == "assistant" ? "Assistant" : "User";
                var msg = row.Message?.Trim() ?? "";
                if (msg.Length > 4000)
                    msg = msg[..4000] + "…";
                sb.AppendLine($"- **[{role}]** ({row.CreatedAt:u}): {msg}");
            }
        }

        return new FigmaFrameSprintContextBuild(
            sb.ToString(),
            moduleIdStr,
            hadUserStory,
            hadUiUx,
            chatRows.Count);
    }

    private static object? ExtractUserStoryCardFromResult(object? getUserStoryResult)
    {
        if (getUserStoryResult == null) return null;
        var t = getUserStoryResult.GetType();
        var success = t.GetProperty("Success")?.GetValue(getUserStoryResult) is bool b && b;
        if (!success) return null;
        return t.GetProperty("Card")?.GetValue(getUserStoryResult);
    }

    private static string FormatTrelloUserStoryCardForPrompt(object card)
    {
        var t = card.GetType();
        var name = t.GetProperty("Name")?.GetValue(card)?.ToString() ?? "";
        var desc = t.GetProperty("Description")?.GetValue(card)?.ToString() ?? "";
        var sb = new StringBuilder();
        sb.AppendLine($"- **Title:** {name}");
        if (!string.IsNullOrWhiteSpace(desc))
        {
            sb.AppendLine("- **Description:**");
            sb.AppendLine(desc.Trim());
        }
        var checklists = t.GetProperty("Checklists")?.GetValue(card) as System.Collections.IEnumerable;
        if (checklists != null)
        {
            sb.AppendLine("- **Checklists:**");
            foreach (var cl in checklists)
            {
                if (cl == null) continue;
                var clt = cl.GetType();
                var clName = clt.GetProperty("Name")?.GetValue(cl)?.ToString() ?? "Checklist";
                sb.AppendLine($"  - **{clName}**");
                var items = clt.GetProperty("CheckItems")?.GetValue(cl) as System.Collections.IEnumerable;
                if (items == null) continue;
                foreach (var ci in items)
                {
                    if (ci == null) continue;
                    var cit = ci.GetType();
                    var itemName = cit.GetProperty("Name")?.GetValue(ci)?.ToString() ?? "";
                    var state = cit.GetProperty("State")?.GetValue(ci)?.ToString() ?? "";
                    var done = string.Equals(state, "complete", StringComparison.OrdinalIgnoreCase) ? "[x]" : "[ ]";
                    sb.AppendLine($"    - {done} {itemName}");
                }
            }
        }
        return sb.ToString().TrimEnd();
    }

    private sealed record FigmaFrameSprintContextBuild(
        string Markdown,
        string? ModuleIdFromTrello,
        bool HadUserStory,
        bool HadUiUxCard,
        int CustomerChatTurns);
}

/// <summary>Request for <c>POST /api/Mentor/use/figma-frame-review</c>.</summary>
public class FigmaFrameReviewRequest
{
    public string BoardId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int SprintNumber { get; set; }
    public string FigmaFileUrl { get; set; } = string.Empty;
    public int? FigmaDepth { get; set; }
    public bool PruneHeavyFigmaKeys { get; set; } = true;
    public bool UseSemanticFigmaTransform { get; set; } = true;
    /// <summary>When true, response includes <c>generatedSystemPrompt</c> and Figma/LLM are skipped. Default is false (full pipeline).</summary>
    [DefaultValue(false)]
    public bool Test { get; set; } = false;
}
