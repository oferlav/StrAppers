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
    /// Reviews stakeholder (CRM) rows created or last updated in the sprint’s date window, with the same mentor context as resource-review
    /// (module, user story, workspace tasks, customer chat). Sprint window matches the board UI when
    /// <see cref="ProjectBoardSprintMerge"/> has a row (same as GET sprint-schedule); otherwise uses SprintPlan JSON or board StartDate fallback.
    /// </summary>
    [HttpPost("use/crm-review")]
    public async Task<ActionResult<object>> CrmReview([FromBody] ResourceReviewRequest? request, CancellationToken cancellationToken)
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
        const string bugsMessage = "CRM review applies to numbered sprints only, not Bugs.";
        const string noWindowMessage =
            "Could not resolve this sprint’s dates from the board plan. Ensure Sprint lists in the board plan have Start/End dates, or set the board start date.";
        const string noStakeholdersMessage =
            "There are no stakeholder entries on your board for this sprint yet. Add them in CRM during this sprint, then try CRM review again.";

        if (request.SprintNumber == 0)
        {
            return Ok(new
            {
                success = false,
                skippedLlm = true,
                reason = "bugs_sprint",
                message = bugsMessage,
                boardId,
                sprintNumber = request.SprintNumber,
            });
        }

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

            var sprintLengthWeeks = _configuration.GetValue<int>("BusinessLogicConfig:SprintLengthInWeeks", 1);
            var sprintMerge = await _context.ProjectBoardSprintMerges.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == request.SprintNumber, cancellationToken);

            DateTime windowStartUtc;
            DateTime windowEndInclusiveUtc;
            var haveSprintWindow =
                SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                    sprintMerge, request.SprintNumber, sprintLengthWeeks, out windowStartUtc, out windowEndInclusiveUtc)
                || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                    board.SprintPlan,
                    board.StartDate,
                    request.SprintNumber,
                    out windowStartUtc,
                    out windowEndInclusiveUtc);

            if (!haveSprintWindow)
            {
                return Ok(new
                {
                    success = false,
                    skippedLlm = true,
                    reason = "no_sprint_date_window",
                    message = noWindowMessage,
                    boardId,
                    sprintNumber = request.SprintNumber,
                });
            }

            var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
            var originalRoleName = activeRole?.Role?.Name ?? "Team Member";
            var roleId = activeRole?.RoleId;

            string? moduleIdStr = null;
            foreach (var label in GetTrelloLabelNamesForRole(originalRoleName))
            {
                moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, request.SprintNumber, label);
                if (!string.IsNullOrWhiteSpace(moduleIdStr))
                    break;
            }

            var hasProjectModule = false;
            var hasUserStoryCard = false;

            var contextMd = new StringBuilder();
            contextMd.AppendLine("## Sprint / module / user story");
            contextMd.AppendLine($"- **BoardId:** `{boardId}`");
            contextMd.AppendLine($"- **ProjectId:** {board.ProjectId}");
            contextMd.AppendLine($"- **SprintNumber:** {request.SprintNumber}");
            contextMd.AppendLine($"- **Sprint window (UTC, inclusive):** {windowStartUtc:O} .. {windowEndInclusiveUtc:O}");
            contextMd.AppendLine($"- **StudentId:** {request.StudentId}");
            contextMd.AppendLine($"- **Role:** {originalRoleName} (RoleId: {roleId?.ToString() ?? "(none)"})");
            contextMd.AppendLine($"- **ModuleId (Trello sprint card custom field):** {(moduleIdStr ?? "(not set)")}");
            contextMd.AppendLine();

            if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleInt))
            {
                var pm = await strAppersBackend.Utilities.ProjectModuleLookup.FindByBoardScopeAsync(
                    _context,
                    moduleInt,
                    board.ProjectId,
                    cancellationToken);
                contextMd.AppendLine("### Project module (database)");
                if (pm == null)
                {
                    contextMd.AppendLine(
                        $"No module row for ModuleId **{moduleInt}** and project scope **{board.ProjectId}** (catalog or institute design).");
                }
                else
                {
                    hasProjectModule = true;
                    contextMd.AppendLine($"- **ModuleId:** {pm.Id}");
                    contextMd.AppendLine($"- **Title:** {pm.Title ?? "(none)"}");
                    contextMd.AppendLine("- **Description:**");
                    contextMd.AppendLine(string.IsNullOrWhiteSpace(pm.Description) ? "(none)" : pm.Description!.Trim());
                }

                contextMd.AppendLine();
                var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(boardId, moduleIdStr.Trim());
                var usCard = ExtractUserStoryCardFromResourceReviewResult(usResult);
                if (usCard != null)
                {
                    hasUserStoryCard = true;
                    contextMd.AppendLine("### User story (Trello — User Stories list)");
                    contextMd.AppendLine(FormatResourceReviewUserStoryCard(usCard));
                    contextMd.AppendLine();
                }
            }
            else
            {
                contextMd.AppendLine("### Project module (database)");
                contextMd.AppendLine("Skipped: ModuleId could not be read from the student’s sprint card (custom field on role-labeled card).");
                contextMd.AppendLine();
            }

            contextMd.AppendLine("### Review constraints (for the mentor model)");
            if (!hasProjectModule && !hasUserStoryCard)
            {
                contextMd.AppendLine("- **No project module** and **no user story card** are present in this sprint’s context above.");
                contextMd.AppendLine("- **Do not** relate stakeholder entries to a user story, feature, or sprint scope that is not explicitly shown. Do not use the internal customer/product backstory alone to invent such a link.");
                contextMd.AppendLine("- **Do** infer what this sprint is about from **MENTOR WORKSPACE** (task titles, checklists). If work is testing, QA, or bug-fixing, say so and judge whether CRM entries fit that phase.");
            }
            else if (hasProjectModule && !hasUserStoryCard)
            {
                contextMd.AppendLine("- A **project module** is present but **no user story card** was found. Do not invent user-story wording; reference only the module title/description above.");
            }
            else if (!hasProjectModule && hasUserStoryCard)
            {
                contextMd.AppendLine("- A **user story** is present but **no matching project module row** was found. Ground scope in the user story section only.");
            }

            contextMd.AppendLine();

            var mentorCtx = await GetMentorContextInternal(request.StudentId, request.SprintNumber, null);
            if (mentorCtx == null)
                return BadRequest(new { success = false, message = "Could not build mentor context (board, sprint list, or project missing?)." });

            var ctxJson = JsonSerializer.Serialize(mentorCtx);
            var ctxEl = JsonSerializer.Deserialize<JsonElement>(ctxJson);
            var workspaceUserPrompt = "";
            if (ctxEl.TryGetProperty("UserPrompt", out var up1))
                workspaceUserPrompt = up1.GetString() ?? "";
            else if (ctxEl.TryGetProperty("userPrompt", out var up2))
                workspaceUserPrompt = up2.GetString() ?? "";

            var (customerChatSection, customerChatMessageCount) = await BuildResourceReviewCustomerChatSectionAsync(
                request.StudentId,
                request.SprintNumber,
                cancellationToken);

            var project = await _context.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == board.ProjectId, cancellationToken);
            var customerPastStory = string.IsNullOrWhiteSpace(project?.CustomerPastStory)
                ? "(Not set on this project.)"
                : project!.CustomerPastStory!.Trim();

            var stakeholderRows = await _context.Stakeholders
                .AsNoTracking()
                .Include(s => s.Category)
                .Include(s => s.Status)
                .Where(s => s.BoardId != null && s.BoardId == boardId
                            && (
                                (s.CreatedAt != null
                                 && s.CreatedAt >= windowStartUtc
                                 && s.CreatedAt <= windowEndInclusiveUtc)
                                || (s.UpdatedAt != null
                                    && s.UpdatedAt >= windowStartUtc
                                    && s.UpdatedAt <= windowEndInclusiveUtc)))
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    CategoryName = s.Category != null ? s.Category.Name : "",
                    StatusName = s.Status != null ? s.Status.Name : "",
                    s.V1AlignmentScore,
                    s.Delta,
                    s.CreatedAt,
                    s.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            if (stakeholderRows.Count == 0)
            {
                return Ok(new
                {
                    success = false,
                    skippedLlm = true,
                    reason = "no_stakeholders_in_sprint_window",
                    message = noStakeholdersMessage,
                    boardId,
                    sprintNumber = request.SprintNumber,
                    windowStartUtc,
                    windowEndInclusiveUtc,
                    stakeholderCount = 0,
                });
            }

            var crmSb = new StringBuilder();
            foreach (var row in stakeholderRows)
            {
                var added = row.CreatedAt.HasValue ? row.CreatedAt.Value.ToString("O") : "(unknown)";
                var lastEdit = row.UpdatedAt.HasValue ? row.UpdatedAt.Value.ToString("O") : "(unknown)";
                crmSb.AppendLine(
                    $"- **{row.Name}** | Category: {row.CategoryName} | Status: {row.StatusName} | V1Alignment: {row.V1AlignmentScore} | Added (UTC): {added} | Last updated (UTC): {lastEdit}");
                if (!string.IsNullOrWhiteSpace(row.Delta))
                {
                    crmSb.AppendLine("  Notes/Delta:");
                    crmSb.AppendLine("  " + row.Delta.Trim().Replace("\n", "\n  "));
                }

                crmSb.AppendLine();
            }

            var reviewInstructions = LoadMentorPromptFile("CrmReviewSystem")?.Trim()
                ?? "Review CRM stakeholder entries for this sprint. If module/user story are missing from context, do not invent them; use MENTOR WORKSPACE tasks. Be specific.";

            var baseSystem = StripDebugMarkers(DbgConfig("SystemPrompt") + (_promptConfig.Mentor.SystemPrompt ?? ""));
            var fullStackBlock = BuildFullStackDeveloperMentorInstructions(originalRoleName);
            if (!string.IsNullOrEmpty(fullStackBlock))
                baseSystem += fullStackBlock;

            var systemPrompt = $"{GetPlatformInterfaceAndRolePermissions()}\n\n{baseSystem}\n\n{reviewInstructions}".Trim();

            var userMessage = new StringBuilder();
            userMessage.AppendLine("=== STRUCTURED CONTEXT (sprint, module, user story — may be incomplete) ===");
            userMessage.AppendLine(contextMd.ToString().Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== MENTOR WORKSPACE (Trello tasks, team, sprint summary) ===");
            userMessage.AppendLine(workspaceUserPrompt.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== CUSTOMER CHAT (AI Customer — same StudentId and SprintId / SprintNumber) ===");
            userMessage.AppendLine(customerChatSection.Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("=== [INTERNAL] Customer/product backstory (reasoning only) ===");
            userMessage.AppendLine(customerPastStory);
            userMessage.AppendLine();
            userMessage.AppendLine("=== STAKEHOLDERS FOR THIS SPRINT (created or last updated within sprint window; category & status names) ===");
            userMessage.AppendLine(crmSb.ToString().Trim());
            userMessage.AppendLine();
            userMessage.AppendLine("Provide your CRM / stakeholders review now.");

            var userPromptFinal = userMessage.ToString();

            if (request.Test)
            {
                return Ok(new
                {
                    success = true,
                    test = true,
                    stakeholderCount = stakeholderRows.Count,
                    windowStartUtc,
                    windowEndInclusiveUtc,
                    roleName = originalRoleName,
                    roleId,
                    inputTokens = 0,
                    outputTokens = 0,
                    totalTokensConsumed = 0,
                    userPromptCharLength = userPromptFinal.Length,
                    customerChatMessageCount,
                    generatedSystemPrompt = systemPrompt,
                    message = "Test mode: LLM not called. Inspect generatedSystemPrompt and userPromptCharLength.",
                });
            }

            var cheapName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name = cheapName,
                Provider = "OpenAI",
                BaseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens = 16384,
                DefaultTemperature = 0.2,
            };

            var (llmText, inputTokens, outputTokens) =
                await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPromptFinal);

            if (!request.Test)
                await TryPersistCacheReviewAsync(boardId, request.StudentId, request.SprintNumber, CacheReviewType.Skill, llmText, cancellationToken);

            return Ok(new
            {
                success = true,
                test = false,
                model = cheapName,
                stakeholderCount = stakeholderRows.Count,
                windowStartUtc,
                windowEndInclusiveUtc,
                roleName = originalRoleName,
                roleId,
                inputTokens,
                outputTokens,
                totalTokensConsumed = inputTokens + outputTokens,
                llmResponse = llmText,
                customerChatMessageCount,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "crm-review failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
