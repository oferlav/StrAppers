using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

public partial class MentorController
{
    /// <summary>
    /// PM-oriented review: user story card vs customer chat; internal scope/backstory for reasoning only.
    /// ModuleId is read from the Product Manager sprint card; requires a matching user story card in Trello.
    /// Includes the same <see cref="GetMentorContextInternal"/> workspace text as other mentor reviews (this sprint’s Trello tasks for the student and teammates) so the model can compare planned sprint work to the story.
    /// </summary>
    [HttpPost("use/story-review")]
    public async Task<ActionResult<object>> StoryReview([FromBody] ResourceReviewRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0 (0 = Bugs)." });

        var boardId = request.BoardId.Trim();
        // Returned here only — the LLM is never called when ModuleId is missing on the PM sprint card.
        const string noModuleMessage =
            "Story review did not run. The Product Manager sprint card in Trello has no Module ID, so there is no linked user story for this check.";
        const string noUserStoryMessage =
            "We see a **Module ID** on your PM sprint card, but there’s no matching **user story** in Trello’s User Stories list for that id yet " +
            "(or the story’s Module ID doesn’t match). Link or create one when you want **Story review** for this module; otherwise you can ignore this.";

        try
        {
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .Include(s => s.ProjectBoard)
                .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
            if (student == null)
                return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
            if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

            var board = await _context.ProjectBoards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
            if (board == null)
                return NotFound(new { success = false, message = $"Board {boardId} not found." });
            var effectiveBoardId = board.UserStoryBoardId ?? boardId;

            var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
            var originalRoleName = activeRole?.Role?.Name ?? "Team Member";
            var roleId = activeRole?.RoleId;

            string? moduleIdStr = null;
            foreach (var pmLabel in StoryReviewPmSprintCardLabels)
            {
                moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, request.SprintNumber, pmLabel);
                if (!string.IsNullOrWhiteSpace(moduleIdStr))
                    break;
            }

            if (string.IsNullOrWhiteSpace(moduleIdStr))
            {
                return Ok(new
                {
                    success = false,
                    skippedLlm = true,
                    reason = "no_module_id_on_pm_card",
                    message = noModuleMessage,
                    boardId,
                    sprintNumber = request.SprintNumber,
                    roleName = originalRoleName,
                    roleId,
                });
            }

            var contextMd = new StringBuilder();
            contextMd.AppendLine("## [INTERNAL — do not name or discuss this block in the student-facing review]");
            contextMd.AppendLine($"- BoardId: `{boardId}`, ProjectId: {board.ProjectId}, SprintNumber: {request.SprintNumber}, StudentId: {request.StudentId}");
            contextMd.AppendLine($"- Role: {originalRoleName} (RoleId: {roleId?.ToString() ?? "(none)"}), ModuleId (PM sprint card): {moduleIdStr.Trim()}");
            contextMd.AppendLine();

            if (int.TryParse(moduleIdStr.Trim(), out var moduleInt))
            {
                var pmRow = await strAppersBackend.Utilities.ProjectModuleLookup.FindByBoardScopeAsync(
                    _context, moduleInt, board.ProjectId, board.InstituteProjectId, cancellationToken);
                contextMd.AppendLine("### [INTERNAL] Product scope text (reasoning only; never cite as \"module\" or \"database\" to the student)");
                if (pmRow == null)
                {
                    contextMd.AppendLine($"No matching scope row for module id {moduleInt} in this project.");
                }
                else
                {
                    contextMd.AppendLine($"- Id: {pmRow.Id}, Title: {pmRow.Title ?? "(none)"}");
                    contextMd.AppendLine("- Scope / description:");
                    contextMd.AppendLine(string.IsNullOrWhiteSpace(pmRow.Description) ? "(none)" : pmRow.Description.Trim());
                }

                contextMd.AppendLine();
            }
            else
            {
                contextMd.AppendLine("### [INTERNAL] Product scope text");
                contextMd.AppendLine($"Module id from Trello is not numeric (`{moduleIdStr.Trim()}`); scope row not loaded.");
                contextMd.AppendLine();
            }

            var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(effectiveBoardId, moduleIdStr.Trim());
            var usCard = ExtractUserStoryCardFromResourceReviewResult(usResult);
            if (usCard == null)
            {
                return Ok(new
                {
                    success = false,
                    skippedLlm = true,
                    reason = "no_user_story_card",
                    message = noUserStoryMessage,
                    boardId,
                    sprintNumber = request.SprintNumber,
                    moduleIdFromTrello = moduleIdStr.Trim(),
                    roleName = originalRoleName,
                    roleId,
                });
            }

            contextMd.AppendLine("### User story card (PRIMARY — this is what you review in depth for the student)");
            contextMd.AppendLine(FormatResourceReviewUserStoryCard(usCard));
            contextMd.AppendLine();

            var (customerChatSection, customerChatMessageCount) = await BuildResourceReviewCustomerChatSectionAsync(
                request.StudentId,
                request.SprintNumber,
                cancellationToken);

            var mentorCtx = await GetMentorContextInternal(request.StudentId, request.SprintNumber, null);
            if (mentorCtx == null)
                return BadRequest(new { success = false, message = "Could not build mentor workspace context (board, sprint list, or project missing?)." });

            var mentorCtxJson = JsonSerializer.Serialize(mentorCtx);
            var mentorCtxEl = JsonSerializer.Deserialize<JsonElement>(mentorCtxJson);
            var workspaceUserPrompt = "";
            if (mentorCtxEl.TryGetProperty("UserPrompt", out var up1))
                workspaceUserPrompt = up1.GetString() ?? "";
            else if (mentorCtxEl.TryGetProperty("userPrompt", out var up2))
                workspaceUserPrompt = up2.GetString() ?? "";

            var (_, effectiveCustomerPastStory, _) = await strAppersBackend.Utilities.ProjectContextHelper.GetEffectiveProjectDataAsync(
                _context, board.ProjectId, board.InstituteProjectId, cancellationToken);
            var customerPastStory = string.IsNullOrWhiteSpace(effectiveCustomerPastStory)
                ? "(Not set on this project.)"
                : effectiveCustomerPastStory.Trim();

            var reviewInstructions = LoadMentorPromptFile("StoryReviewSystem")?.Trim()
                ?? "Review the user story card and customer chat; use MENTOR WORKSPACE (sprint tasks) as context for planned vs story coverage. Do not name internal scope/backstory.";

            var baseSystem = StripDebugMarkers(DbgConfig("SystemPrompt") + (_promptConfig.Mentor.SystemPrompt ?? ""));
            var fullStackBlock = BuildFullStackDeveloperMentorInstructions(originalRoleName);
            if (!string.IsNullOrEmpty(fullStackBlock))
                baseSystem += fullStackBlock;

            var systemPrompt = $"{GetPlatformInterfaceAndRolePermissions()}\n\n{baseSystem}\n\n{reviewInstructions}".Trim();

            var userMessage = new StringBuilder();
            userMessage.AppendLine("The student only sees your answer. Blocks marked [INTERNAL] are platform-only: use for reasoning, never name them, never use them as section titles, never quote their narrative.");
            userMessage.AppendLine();
            userMessage.AppendLine("=== STRUCTURED CONTEXT (internal scope + user story card) ===");
            userMessage.AppendLine(contextMd.ToString().Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== MENTOR WORKSPACE (this sprint — Trello tasks for the student and teammates; planned work vs user story) ===");
            userMessage.AppendLine(string.IsNullOrWhiteSpace(workspaceUserPrompt) ? "(Empty — no task summary was built.)" : workspaceUserPrompt.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== CUSTOMER CONVERSATION THIS SPRINT (simulated customer chat — OK to reference as conversation with the customer) ===");
            userMessage.AppendLine(customerChatSection.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== [INTERNAL] Additional customer/product backstory (reasoning only — do not mention, title, or summarize for the student) ===");
            userMessage.AppendLine(customerPastStory);
            userMessage.AppendLine();
            userMessage.AppendLine("Write the review now: primary focus is the **user story card** and the **customer conversation**, informed by **MENTOR WORKSPACE** (sprint tasks — what the team committed to this sprint). Do not output sections about \"module description,\" \"database,\" or \"customer past story\" as labeled topics.");

            var userPromptFinal = userMessage.ToString();

            if (request.Test)
            {
                return Ok(new
                {
                    success = true,
                    test = true,
                    skippedLlm = false,
                    moduleIdFromTrello = moduleIdStr.Trim(),
                    roleName = originalRoleName,
                    roleId,
                    inputTokens = 0,
                    outputTokens = 0,
                    totalTokensConsumed = 0,
                    userPromptCharLength = userPromptFinal.Length,
                    customerChatMessageCount,
                    generatedSystemPrompt = systemPrompt,
                    message = "Test mode: LLM not called. Inspect generatedSystemPrompt and userPromptCharLength."
                });
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

            var (llmText, inputTokens, outputTokens) =
                await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPromptFinal);

            if (!request.Test)
                await TryPersistCacheReviewAsync(boardId, request.StudentId, request.SprintNumber, CacheReviewType.Skill, llmText, cancellationToken);

            return Ok(new
            {
                success = true,
                test = false,
                skippedLlm = false,
                model = cheapName,
                moduleIdFromTrello = moduleIdStr.Trim(),
                roleName = originalRoleName,
                roleId,
                inputTokens,
                outputTokens,
                totalTokensConsumed = inputTokens + outputTokens,
                llmResponse = llmText,
                customerChatMessageCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "story-review failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>Trello label names tried on the sprint list card when resolving ModuleId for story review (PM card).</summary>
    private static readonly string[] StoryReviewPmSprintCardLabels = { "Product Manager", "PM" };
}
