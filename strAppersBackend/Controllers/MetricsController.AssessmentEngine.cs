using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>Generic Data Assessment Engine — evaluates any metric against all available sprint-level context sensors.</summary>
public partial class MetricsController
{
    public record AssessmentEngineRequest(
        int MetricId,
        string BoardId,
        int StudentId,
        int SprintNumber,
        bool Test = false,
        string? TrelloRoleLabel = null);

    [HttpPost("use/assess")]
    public async Task<IActionResult> RunAssessmentEngine(
        [FromBody] AssessmentEngineRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { message = "BoardId is required." });
        if (request.MetricId <= 0)
            return BadRequest(new { message = "MetricId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { message = "SprintNumber must be >= 0." });

        var metric = await _context.Metrics
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MetricId, cancellationToken);
        if (metric == null)
            return NotFound(new { message = $"Metric {request.MetricId} not found." });
        if (string.IsNullOrWhiteSpace(metric.Skill))
            return UnprocessableEntity(new { message = $"Metric '{metric.Name}' has no Skill definition. Cannot run assessment." });

        var boardId = request.BoardId.Trim();
        var board = await _context.ProjectBoards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return NotFound(new { message = $"Board '{boardId}' not found." });

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { message = $"Student {request.StudentId} not found." });

        var sprintLengthWeeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var sprintMerge = await _context.ProjectBoardSprintMerges
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == request.SprintNumber, cancellationToken);
        var haveWindow =
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                sprintMerge, request.SprintNumber, sprintLengthWeeks, out var windowStart, out var windowEnd)
            || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                board.SprintPlan, board.StartDate, request.SprintNumber, out windowStart, out windowEnd);

        var contextMd = await BuildAssessmentContextAsync(
            boardId, board, student, request.SprintNumber,
            request.TrelloRoleLabel, haveWindow, windowStart, windowEnd,
            cancellationToken);

        var expertise = string.IsNullOrWhiteSpace(metric.AIExpertise)
            ? "professional academic skills assessment expert"
            : metric.AIExpertise.Trim();

        var systemPrompt = $$"""
            You are a {{expertise}}. Assess the student's sprint work using ONLY the CONTEXT provided below.

            Rules:
            - Score 0–100 for each dimension defined in the rubric.
            - Ground every score in verbatim evidence from the CONTEXT. Do not invent activity.
            - Sections marked _(none for this sprint)_ have no data — do not speculate about them.
            - Output valid JSON only, no markdown fences:
              {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            - narrative: brief markdown summary of strengths, gaps, and 1–3 concrete follow-up suggestions.
            """;

        var userPrompt = new StringBuilder()
            .AppendLine($"## Assessment Rubric — {metric.Name}")
            .AppendLine(metric.Skill.Trim())
            .AppendLine()
            .AppendLine($"## Sprint Context — Sprint {request.SprintNumber} | Student: {student.FirstName} {student.LastName} | Board: {boardId}")
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
                userPrompt,
            });
        }

        try
        {
            var modelName = _configuration["OpenAI:CheapModel"] ?? "gpt-4o-mini";
            var aiModel = new AIModel
            {
                Name               = modelName,
                Provider           = "OpenAI",
                BaseUrl            = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1",
                MaxTokens          = 16384,
                DefaultTemperature = 0.2f,
            };

            var (llmText, inputTokens, outputTokens) = await _chatCompletionService.GetChatCompletionAsync(
                aiModel, systemPrompt, userPrompt, null);

            if (!TryParseGapAnalysisJson(llmText, out var dto) || dto == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = $"Metric '{metric.Name}' assessment did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            var rows = dto.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => (c.Name.Trim(), Math.Clamp(c.Score, 0, 100)))
                .ToList();
            if (rows.Count == 0)
                rows.Add(("Overall", 0));

            var graphBase64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(rows, metric.Name));

            var reviewContent = FormatAssessmentReviewContent(metric.Name, dto);

            await UpsertCacheMetricsAsync(
                boardId, student.Id, request.SprintNumber, request.MetricId,
                reviewContent, graphBase64, cancellationToken);

            return Ok(new
            {
                success    = true,
                metricId   = request.MetricId,
                reviewContent,
                graphBase64,
                model      = modelName,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Assessment engine failed for metric {MetricId} board {BoardId} sprint {Sprint}",
                request.MetricId, boardId, request.SprintNumber);
            return StatusCode(500, new { message = "AI assessment call failed. Please try again." });
        }
    }

    private async Task<string> BuildAssessmentContextAsync(
        string boardId,
        ProjectBoard board,
        Student student,
        int sprintNumber,
        string? trelloRoleLabel,
        bool haveWindow,
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        var sb = new StringBuilder();

        await AppendAssessmentCustomerChatAsync(sb, student.Id, sprintNumber, ct);
        await AppendAssessmentMentorChatAsync(sb, student.Id, sprintNumber, ct);
        await AppendAssessmentBoardStateAsync(sb, boardId, sprintNumber, ct);
        await AppendAssessmentResourcesAsync(sb, boardId, student.Id, sprintNumber, ct);
        await AppendAssessmentStakeholdersAsync(sb, boardId, ct);
        await AppendAssessmentProjectModuleAsync(sb, boardId, board, sprintNumber, trelloRoleLabel, ct);

        sb.AppendLine("### Meeting transcripts");
        if (!string.IsNullOrWhiteSpace(student.Email) && haveWindow)
            await AppendAssessmentMeetingTranscriptsAsync(sb, boardId, student.Email, windowStart, windowEnd, ct);
        else
            sb.AppendLine("_(none for this sprint)_");
        sb.AppendLine();

        AppendChatBlobSection(sb, "### Group chat (squad)",
            haveWindow ? FilterChatBlobByWindow(board.GroupChat, windowStart, windowEnd) : null,
            haveWindow);

        if (!string.IsNullOrWhiteSpace(student.Email) && haveWindow)
            await AppendAssessmentPrivateChatAsync(sb, boardId, student.Email, windowStart, windowEnd, ct);
        else
        {
            sb.AppendLine("### Private chats (1-on-1)");
            sb.AppendLine("_(none for this sprint)_");
            sb.AppendLine();
        }

        return sb.Length == 0 ? "(No context blocks available for this sprint.)" : sb.ToString();
    }

    private async Task AppendAssessmentCustomerChatAsync(StringBuilder sb, int studentId, int sprintNumber, CancellationToken ct)
    {
        var rows = await _context.CustomerChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

        sb.AppendLine("### AI Customer chat");
        if (rows.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var r in rows)
        {
            var role = r.Role?.Trim().ToLowerInvariant() == "assistant" ? "Assistant" : "User";
            sb.AppendLine($"- **[{role}]** ({r.CreatedAt:u}): {Truncate(r.Message?.Trim() ?? "", 3000)}");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentMentorChatAsync(StringBuilder sb, int studentId, int sprintNumber, CancellationToken ct)
    {
        var rows = await _context.MentorChatHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId && h.SprintId == sprintNumber)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

        sb.AppendLine("### AI Mentor chat");
        if (rows.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var r in rows)
        {
            var role = r.Role?.Trim().ToLowerInvariant() == "assistant" ? "Assistant" : "User";
            sb.AppendLine($"- **[{role}]** ({r.CreatedAt:u}): {Truncate(r.Message?.Trim() ?? "", 3000)}");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentBoardStateAsync(StringBuilder sb, string boardId, int sprintNumber, CancellationToken ct)
    {
        var states = await _context.BoardStates.AsNoTracking()
            .Where(s => s.BoardId == boardId && s.SprintNumber == sprintNumber)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        sb.AppendLine("### Board state (GitHub / CI)");
        if (states.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var s in states)
        {
            sb.AppendLine($"- Source: {s.Source} | DevRole: {s.DevRole ?? "—"} | Branch: {s.BranchName ?? "—"} | PR: {s.PRStatus ?? "—"} | BranchStatus: {s.BranchStatus ?? "—"}");
            if (!string.IsNullOrWhiteSpace(s.LatestCommitDescription))
                sb.AppendLine($"  Latest commit: {Truncate(s.LatestCommitDescription.Trim(), 500)}");
            if (!string.IsNullOrWhiteSpace(s.LastBuildStatus))
                sb.AppendLine($"  Build: {s.LastBuildStatus}");
            if (!string.IsNullOrWhiteSpace(s.LastTestStatus))
                sb.AppendLine($"  Tests: {s.LastTestStatus}");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentResourcesAsync(StringBuilder sb, string boardId, int studentId, int sprintNumber, CancellationToken ct)
    {
        var resources = await _context.Resources.AsNoTracking()
            .Where(r => r.BoardId == boardId && r.StudentId == studentId
                && (r.SprintNumber == null || r.SprintNumber == sprintNumber))
            .OrderBy(r => r.IsFigma ? 0 : 1).ThenBy(r => r.Name)
            .ToListAsync(ct);

        sb.AppendLine("### Resources & Figma");
        if (resources.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var r in resources)
            sb.AppendLine($"- {(r.IsFigma ? "[Figma]" : "[Resource]")} **{r.Name}**: {r.Url}");
        sb.AppendLine();
    }

    private async Task AppendAssessmentStakeholdersAsync(StringBuilder sb, string boardId, CancellationToken ct)
    {
        var stakeholders = await _context.Stakeholders.AsNoTracking()
            .Include(s => s.Category)
            .Include(s => s.Status)
            .Where(s => s.BoardId == boardId)
            .ToListAsync(ct);

        sb.AppendLine("### CRM / Stakeholders");
        if (stakeholders.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var s in stakeholders)
        {
            sb.AppendLine($"- **{s.Name}** | Category: {s.Category?.Name ?? "—"} | Status: {s.Status?.Name ?? "—"} | Alignment: {s.V1AlignmentScore}");
            if (!string.IsNullOrWhiteSpace(s.Delta))
                sb.AppendLine($"  Notes: {Truncate(s.Delta.Trim(), 500)}");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentProjectModuleAsync(
        StringBuilder sb,
        string boardId,
        ProjectBoard board,
        int sprintNumber,
        string? trelloRoleLabel,
        CancellationToken ct)
    {
        string? moduleIdStr = null;
        if (!string.IsNullOrWhiteSpace(trelloRoleLabel))
            moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, trelloRoleLabel);

        if (string.IsNullOrWhiteSpace(moduleIdStr))
        {
            foreach (var lbl in GapAnalysisPmSprintCardLabels)
            {
                moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, lbl);
                if (!string.IsNullOrWhiteSpace(moduleIdStr)) break;
            }
        }

        if (!string.IsNullOrWhiteSpace(moduleIdStr) && int.TryParse(moduleIdStr.Trim(), out var moduleId))
        {
            await AppendGapAnalysisProjectModuleSectionFromModuleIdAsync(
                sb, board, moduleId, "### Project module (from Trello sprint card ModuleId)", ct);
            return;
        }

        sb.AppendLine("### Project module");
        sb.AppendLine("_(none for this sprint)_");
        sb.AppendLine();
    }

    private async Task AppendAssessmentMeetingTranscriptsAsync(
        StringBuilder sb,
        string boardId,
        string studentEmail,
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        var emailLower = studentEmail.ToLowerInvariant();
        var meetings = await _context.BoardMeetings.AsNoTracking()
            .Where(m => m.BoardId == boardId
                && m.StudentEmail != null && m.StudentEmail.ToLower() == emailLower
                && m.MeetingTime >= windowStart && m.MeetingTime <= windowEnd)
            .OrderBy(m => m.MeetingTime)
            .ToListAsync(ct);

        if (meetings.Count == 0) { sb.AppendLine("_(none for this sprint)_"); return; }
        foreach (var m in meetings)
        {
            sb.AppendLine($"- Meeting: {m.MeetingTime:u} | Attended: {(m.Attended ? "yes" : "no")} | Speaker: {m.SpeakerName ?? "—"}");
            if (!string.IsNullOrWhiteSpace(m.TranscriptVtt))
            {
                sb.AppendLine("  Transcript excerpt:");
                sb.AppendLine(Truncate(m.TranscriptVtt.Trim(), 6000));
            }
        }
    }

    private static string FormatAssessmentReviewContent(string metricName, GapAnalysisLlmResult dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {metricName} Assessment");
        if (!string.IsNullOrWhiteSpace(dto.Narrative))
        {
            sb.AppendLine();
            sb.AppendLine(dto.Narrative.Trim());
        }
        if (dto.Categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Scores");
            foreach (var c in dto.Categories)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                sb.Append("- **").Append(c.Name.Trim())
                  .Append("** (").Append(Math.Clamp(c.Score, 0, 100)).Append("): ")
                  .AppendLine(string.IsNullOrWhiteSpace(c.Rationale) ? "(no rationale)" : c.Rationale.Trim());
            }
        }
        return sb.ToString().Trim();
    }

    // Parses a chat blob (line-per-message, "[yyyy-MM-dd HH:mm:ss] email: text\n" format)
    // and returns only lines whose timestamp falls within [windowStart, windowEnd] (UTC inclusive).
    // Lines with no parseable timestamp are skipped. Null blob returns empty list.
    internal static List<string> FilterChatBlobByWindow(string? blob, DateTime windowStart, DateTime windowEnd)
    {
        if (string.IsNullOrEmpty(blob)) return [];
        var result = new List<string>();
        foreach (var line in blob.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.Length < 21 || trimmed[0] != '[') continue; // no timestamp prefix
            var closeBracket = trimmed.IndexOf(']', 1);
            if (closeBracket < 0) continue;
            var tsStr = trimmed.Substring(1, closeBracket - 1);
            if (!DateTime.TryParseExact(tsStr, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
                continue;
            var tsUtc = ts.ToUniversalTime();
            if (tsUtc >= windowStart && tsUtc <= windowEnd)
                result.Add(trimmed);
        }
        return result;
    }

    private static void AppendChatBlobSection(StringBuilder sb, string header, List<string>? lines, bool haveWindow)
    {
        sb.AppendLine(header);
        if (!haveWindow || lines == null || lines.Count == 0)
        {
            sb.AppendLine("_(none for this sprint)_");
        }
        else
        {
            foreach (var line in lines)
                sb.AppendLine($"- {line}");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentPrivateChatAsync(
        StringBuilder sb,
        string boardId,
        string studentEmail,
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        var emailLower = studentEmail.Trim().ToLowerInvariant();
        var chats = await _context.PrivateChats.AsNoTracking()
            .Where(c => c.BoardId == boardId
                && (c.Email1 == emailLower || c.Email2 == emailLower))
            .ToListAsync(ct);

        sb.AppendLine("### Private chats (1-on-1)");
        if (chats.Count == 0)
        {
            sb.AppendLine("_(none for this sprint)_");
            sb.AppendLine();
            return;
        }

        var anyLines = false;
        foreach (var chat in chats)
        {
            var lines = FilterChatBlobByWindow(chat.ChatHistory, windowStart, windowEnd);
            if (lines.Count == 0) continue;
            anyLines = true;
            var peer = string.Equals(chat.Email1, emailLower, StringComparison.Ordinal) ? chat.Email2 : chat.Email1;
            sb.AppendLine($"#### With {peer}");
            foreach (var line in lines)
                sb.AppendLine($"- {line}");
        }
        if (!anyLines)
            sb.AppendLine("_(none for this sprint)_");
        sb.AppendLine();
    }
}
