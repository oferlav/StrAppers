using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

public partial class MetricsController
{
    private const int CustomerEngagementMetricId = 7;

    /// <summary>
    /// Shown in API and <see cref="CacheMetrics.ReviewContent"/> when there is no AI Customer chat for this student/sprint.
    /// </summary>
    private const string NoCustomerChatReviewMessage =
        "No conversation with the AI Customer was recorded for this sprint, so a communication review was not produced.";

    public class CustomerEngagementRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        /// <summary>When true, returns generated prompts only (no LLM, no CacheMetrics write).</summary>
        public bool Test { get; set; }
        /// <summary>Institute metric Id to store CacheMetrics under (set by the batch runner). Null = legacy base metric Id.</summary>
        public int? MetricIdOverride { get; set; }
    }

    /// <summary>
    /// Communication-skills review from <see cref="CustomerChatHistory"/> (and context: customer narrative, optional module).
    /// If there is no chat for this student+sprint, the LLM is not called. Non-Developer roles only. Persists to <see cref="CacheMetrics"/> (MetricId 7).
    /// </summary>
    [HttpPost("use/CustomerEngagement")]
    public async Task<ActionResult<object>> CustomerEngagement(
        [FromBody] CustomerEngagementRequest? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0." });

        var boardId = request.BoardId.Trim();
        var metricId = request.MetricIdOverride ?? CustomerEngagementMetricId;

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentRoles)
            .ThenInclude(sr => sr.Role)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var activeRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive);
        var roleName = activeRole?.Role?.Name?.Trim() ?? "Team Member";
        var hasCustomerEngagement = ResolveCustomerEngagement(activeRole?.Role);
        var isDeveloper = ContainsDeveloper(roleName);
        _logger.LogInformation(
            "CustomerEngagement gate: studentId={StudentId} roleName={RoleName} instituteId={InstId} hasCustomerEngagement={CE} isDeveloper={Dev} → willSkip={Skip}",
            request.StudentId, roleName, student.InstituteId, hasCustomerEngagement, isDeveloper, !hasCustomerEngagement && isDeveloper);
        if (DebugAiContext)
        {
            var dbg = $"StudentId={request.StudentId} BoardId={boardId} RoleName={roleName} InstituteId={student.InstituteId} " +
                      $"Role.CE={activeRole?.Role?.CustomerEngagement} ResolvedCE={hasCustomerEngagement} IsDeveloper={isDeveloper} WillSkip={!hasCustomerEngagement && isDeveloper}";
            try { await _smtpEmailService.SendPlainEmailAsync("ofer@skill-in.com", $"[Metrics Debug] CustomerEngagement gate student={request.StudentId}", dbg); } catch { /* ignore */ }
        }
        if (!hasCustomerEngagement && isDeveloper)
            return Ok(new { success = true, metricId = metricId, skippedLlm = true, reviewContent = (string?)null, message = "Developer role without CustomerEngagement enabled; skipping." });

        var board = await _context.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return NotFound(new { success = false, message = $"Board {boardId} not found." });

        var hasAiCustomerChat = await HasCustomerGapAnalysisChatRowsAsync(
            request.StudentId, request.SprintNumber, cancellationToken);

        if (!hasAiCustomerChat)
        {
            if (request.Test)
            {
                return Ok(new
                {
                    success = true,
                    test = true,
                    skippedLlm = true,
                    message = "Test mode: No AI Customer chat for this sprint — LLM not called; CacheMetrics not updated.",
                    reviewContent = NoCustomerChatReviewMessage,
                });
            }

            await UpsertCacheMetricsAsync(
                boardId, request.StudentId, request.SprintNumber, metricId,
                NoCustomerChatReviewMessage, graphBase64: null, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = metricId,
                skippedLlm = true,
                reviewContent = NoCustomerChatReviewMessage,
                graphBase64 = (string?)null,
                model = (object?)null,
            });
        }

        var contextMd = await BuildCustomerEngagementContextMarkdownAsync(
            boardId, board, request.StudentId, request.SprintNumber, activeRole?.Role, student.RoleIndex, cancellationToken);

        var systemPrompt = LoadCustomerEngagementSystemPrompt();
        var userPromptText = new StringBuilder()
            .AppendLine("## CONTEXT (customer narrative, AI Customer chat, and optional module for this sprint)")
            .AppendLine(contextMd)
            .AppendLine()
            .AppendLine("Respond with JSON only as specified in your instructions.")
            .ToString();

        if (request.Test)
        {
            return Ok(new
            {
                success = true,
                test = true,
                message = "Test mode: LLM not called; CacheMetrics not updated.",
                systemPrompt,
                userPrompt = userPromptText,
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
                MaxTokens = 16384,
                DefaultTemperature = 0.2
            };

            var (llmText, _, _) = await _chatCompletionService.GetChatCompletionAsync(aiModel, systemPrompt, userPromptText, null);
            var parsed = TryParseGapAnalysisJson(llmText, out var dto);
            if (!parsed || dto == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "Customer engagement did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            var rows = dto.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => (c.Name.Trim(), Math.Clamp(c.Score, 0, 100)))
                .ToList();
            if (rows.Count == 0)
                rows.Add(("Overall communication", 0));

            var graphB64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(rows, "Customer engagement"));

            var reviewContent = FormatCustomerEngagementReviewContent(dto);
            await UpsertCacheMetricsAsync(
                boardId, request.StudentId, request.SprintNumber, metricId, reviewContent, graphB64, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = metricId,
                skippedLlm = false,
                reviewContent,
                graphBase64 = graphB64,
                model = dto,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomerEngagement failed for board {BoardId}", boardId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    internal static string FormatCustomerEngagementReviewContent(GapAnalysisLlmResult dto)
    {
        var sb = new StringBuilder();
        // Same average-of-categories Final Score the generic engine's reviews lead with; the metric
        // name itself is rendered by the report UI as the card heading, so the score line comes first.
        var finalScore = ComputeFinalScore(dto.Categories);
        if (finalScore.HasValue)
        {
            sb.AppendLine($"**Final Score: {finalScore.Value}**");
            sb.AppendLine();
        }
        sb.AppendLine(dto.Narrative.Trim());
        if (dto.Categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Categories");
            foreach (var c in dto.Categories)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                sb.Append("- **").Append(c.Name.Trim()).Append("** (").Append(Math.Clamp(c.Score, 0, 100)).Append("): ");
                sb.AppendLine(string.IsNullOrWhiteSpace(c.Rationale) ? "(no rationale)" : c.Rationale.Trim());
            }
        }

        return sb.ToString().Trim();
    }

    private async Task<string> BuildCustomerEngagementContextMarkdownAsync(
        string boardId,
        ProjectBoard board,
        int studentId,
        int sprintNumber,
        Role? role,
        int roleIndex,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        var (_, effectiveCustomerPastStory, _) = await strAppersBackend.Utilities.ProjectContextHelper.GetEffectiveProjectDataAsync(
            _context, board.ProjectId, board.InstituteProjectId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(effectiveCustomerPastStory))
        {
            sb.AppendLine("### Customer background (project)");
            sb.AppendLine(effectiveCustomerPastStory.Trim());
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Customer background (project)");
            sb.AppendLine("(Not set on this project.)");
            sb.AppendLine();
        }

        await AppendGapAnalysisCustomerChatHistoryAsync(sb, studentId, sprintNumber, cancellationToken);

        var trelloLabel = board.IsSingleRole && roleIndex > 0
            ? $"{role?.Name?.Trim() ?? string.Empty} {roleIndex}"
            : ResolveTrelloSprintCardLabel(role, fullStackTrackLabel: null);
        var moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, trelloLabel);
        if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleId))
        {
            await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                sb,
                board,
                moduleId,
                "### Project module (from this role’s Trello sprint card ModuleId — use for requirement-gathering communication review)",
                cancellationToken);
        }

        return sb.Length == 0 ? "(No context blocks.)" : sb.ToString();
    }

    private static string LoadCustomerEngagementSystemPrompt()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "Metrics", "CustomerEngagementSystem.txt");
        if (System.IO.File.Exists(path))
        {
            var t = System.IO.File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(t))
                return t;
        }

        return "You review student↔AI Customer communication. Output JSON with categories (name, score 0-100, rationale) and narrative (markdown).";
    }
}
