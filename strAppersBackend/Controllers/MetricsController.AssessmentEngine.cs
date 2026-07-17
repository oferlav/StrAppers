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

        var boardId = request.BoardId.Trim();
        var board = await _context.ProjectBoards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null)
            return NotFound(new { message = $"Board '{boardId}' not found." });

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentRoles)
            .ThenInclude(sr => sr.Role)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { message = $"Student {request.StudentId} not found." });

        // Resolve the Trello role label (same convention as Adherence) when the caller didn't pass one —
        // the sprint role card is matched by role label, and it carries the sprint's task requests.
        var trelloRoleLabel = request.TrelloRoleLabel;
        if (string.IsNullOrWhiteSpace(trelloRoleLabel))
        {
            var activeRoleName = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.Role?.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(activeRoleName))
                trelloRoleLabel = board.IsSingleRole && student.RoleIndex > 0
                    ? $"{activeRoleName} {student.RoleIndex}"
                    : activeRoleName;
        }

        var sprintLengthWeeks = _configuration.GetValue("BusinessLogicConfig:SprintLengthInWeeks", 1);
        var sprintLengthDays = await SprintLengthResolver.ResolveForBoardAsync(_context, boardId, sprintLengthWeeks, cancellationToken);
        var sprintMerge = await _context.ProjectBoardSprintMerges
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectBoardId == boardId && m.SprintNumber == request.SprintNumber, cancellationToken);
        var haveWindow =
            SprintPlanDateResolver.TryGetInclusiveUtcRangeFromSprintMerge(
                sprintMerge, request.SprintNumber, sprintLengthDays, out var windowStart, out var windowEnd)
            || SprintPlanDateResolver.TryGetSprintInclusiveUtcRange(
                board.SprintPlan, board.StartDate, request.SprintNumber, out windowStart, out windowEnd, sprintLengthDays);

        var contextMd = await BuildAssessmentContextAsync(
            metric, boardId, board, student, request.SprintNumber,
            trelloRoleLabel, haveWindow, windowStart, windowEnd,
            cancellationToken);

        // Layer 2 (deterministic categories): the skill definition is the authority. If it defines
        // "Category:" lines those become the score dimensions; otherwise the metric name is the single
        // dimension. Blank skill falls back to a generic rubric built from the metric name.
        var skillRubric = metric.Skill?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(skillRubric))
            skillRubric = $"Assess the student's overall \"{metric.Name}\" for this sprint based on the available evidence.";

        var expectedCategories = ParseRubricCategories(skillRubric);
        if (expectedCategories.Count == 0)
            expectedCategories.Add(metric.Name.Trim());

        var expertise = string.IsNullOrWhiteSpace(metric.AIExpertise)
            ? "professional academic skills assessment expert"
            : metric.AIExpertise.Trim();

        var explicitRulesBlock = BuildExplicitRulesBlock(metric.ExplicitRules);

        // Layer 1 (rubric authority): the skill rubric lives in the system prompt, above the evidence.
        var systemPrompt = $$"""
            You are a {{expertise}}.

            Your task: score the student's sprint performance according to the ASSESSMENT RUBRIC below.
            The rubric is your highest-priority instruction — follow its rules exactly, even when the
            evidence is sparse or seems to point elsewhere.

            === ASSESSMENT RUBRIC ===
            {{skillRubric}}
            === END RUBRIC ==={{explicitRulesBlock}}

            Scoring rules:
            - Return scores for exactly these categories and no others: {{string.Join(" | ", expectedCategories)}}
            - Use the category names verbatim as given above.
            - Scores are integers on a 0–100 scale. Calibrate to these bands: 0–19 = no meaningful evidence,
              20–39 = minimal, 40–59 = partial, 60–79 = good, 80–100 = excellent. Use the full range —
              a weak-but-present performance is ~20–40, not a single-digit score.
            - Use the Sprint Context in the user message as your evidence — ground every score in verbatim evidence from it, unless the rubric instructs otherwise.
            - Sections marked _(squad-level)_ cover the whole team; only attribute activity to this student if they are explicitly named or identifiable.
            - Do not invent activity. Sections marked _(none for this sprint)_ have no data; do not speculate about them.
            - When a resource under Resources & Figma shows "document content extracted on server", that text is the actual content of the uploaded document (e.g. a PRD, spec, or schema) — assess its substance directly rather than treating it as an unverified link. When extraction failed, that is a server-side limitation, not evidence of a missing or low-quality deliverable — do not lower the score solely because content could not be extracted.
            - Output valid JSON only, no markdown fences:
              {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            - narrative: brief markdown summary of strengths, gaps, and 1–3 concrete follow-up suggestions.
            """;

        var userPrompt = new StringBuilder()
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

        // Debug:AiContext=true → email the exact prompts (same pipeline as the legacy metrics).
        if (DebugAiContext)
        {
            try
            {
                var dbg = $"=== Assessment Engine Debug ===\n" +
                          $"Metric:    {metric.Id} ({metric.Name})\n" +
                          $"Student:   {request.StudentId} ({student.FirstName} {student.LastName})\n" +
                          $"Board:     {boardId} | Sprint: {request.SprintNumber} | RoleLabel: {trelloRoleLabel ?? "(none)"}\n" +
                          $"Window:    {(haveWindow ? $"{windowStart:u} .. {windowEnd:u}" : "(not resolved)")}\n\n" +
                          $"--- SYSTEM PROMPT ---\n{systemPrompt}\n\n--- USER PROMPT ---\n{userPrompt}";
                await _smtpEmailService.SendPlainEmailAsync(
                    "ofer@skill-in.com",
                    $"[AssessmentEngine Debug] {metric.Name} | Student {request.StudentId} | Sprint {request.SprintNumber}",
                    dbg);
            }
            catch { /* never interrupt the AI flow */ }
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

            // Layer 3 (code enforcement): the report only ever shows the expected category names,
            // regardless of what the model returned.
            dto.Categories = NormalizeAssessmentCategories(dto.Categories, expectedCategories);

            var rows = dto.Categories
                .Select(c => (c.Name, Math.Clamp(c.Score, 0, 100)))
                .ToList();

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
        Metric metric,
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
        var email = student.Email;
        var hasEmail = !string.IsNullOrWhiteSpace(email);

        if (metric.UseCustomerChat)
            await AppendAssessmentCustomerChatAsync(sb, student.Id, sprintNumber, ct);
        if (metric.UseMentorChat)
            await AppendAssessmentMentorChatAsync(sb, student.Id, sprintNumber, ct);
        if (metric.UseCodebaseQuality)
            await AppendAssessmentBoardStateAsync(sb, boardId, sprintNumber, trelloRoleLabel, ct);
        if (metric.UseResources)
            await AppendAssessmentResourcesAsync(sb, boardId, student.Id, sprintNumber, ct);
        if (metric.UseStakeholders)
            await AppendAssessmentStakeholdersAsync(sb, boardId, ct);
        if (metric.UseProjectModule)
            await AppendAssessmentProjectModuleAsync(sb, boardId, board, sprintNumber, trelloRoleLabel, ct);

        if (metric.UseMeetingTranscripts)
        {
            sb.AppendLine("### Meeting transcripts");
            if (hasEmail && haveWindow)
                await AppendAssessmentMeetingTranscriptsAsync(sb, boardId, email!, windowStart, windowEnd, ct);
            else
                sb.AppendLine("_(none for this sprint)_");
            sb.AppendLine();
        }

        if (metric.UseGroupChat)
            AppendChatBlobSection(sb, "### Group chat (squad) _(squad-level — all team members)_",
                haveWindow ? FilterChatBlobByWindow(board.GroupChat, windowStart, windowEnd) : null,
                haveWindow);

        if (metric.UsePrivateChat)
        {
            if (hasEmail && haveWindow)
                await AppendAssessmentPrivateChatAsync(sb, boardId, email!, windowStart, windowEnd, ct);
            else
            {
                sb.AppendLine("### Private chats (1-on-1)");
                sb.AppendLine("_(none for this sprint)_");
                sb.AppendLine();
            }
        }

        if (metric.UseTrelloTasks)
            await AppendAssessmentTrelloTasksAsync(sb, boardId, email, trelloRoleLabel, sprintNumber, ct);

        if (metric.UseTrelloUserStory)
            await AppendAssessmentTrelloUserStoryAsync(sb, boardId, board.UserStoryBoardId, trelloRoleLabel, sprintNumber, haveWindow, windowStart, windowEnd, ct);

        if (metric.UseFigmaDesign && haveWindow)
            await AppendAssessmentFigmaDesignAsync(sb, boardId, windowStart, windowEnd, ct);

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

    private async Task AppendAssessmentBoardStateAsync(StringBuilder sb, string boardId, int sprintNumber, string? trelloRoleLabel, CancellationToken ct)
    {
        var all = await _context.BoardStates.AsNoTracking()
            .Where(s => s.BoardId == boardId && s.SprintNumber == sprintNumber)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        // Filter to this student's dev role when possible; fall back to all if no match.
        var states = all;
        if (!string.IsNullOrWhiteSpace(trelloRoleLabel))
        {
            var filtered = all.Where(s =>
                !string.IsNullOrWhiteSpace(s.DevRole) &&
                (s.DevRole.Contains(trelloRoleLabel, StringComparison.OrdinalIgnoreCase) ||
                 trelloRoleLabel.Contains(s.DevRole, StringComparison.OrdinalIgnoreCase))).ToList();
            if (filtered.Count > 0) states = filtered;
        }

        sb.AppendLine("### Codebase Quality (GitHub / CI)");
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

    internal async Task AppendAssessmentResourcesAsync(StringBuilder sb, string boardId, int studentId, int sprintNumber, CancellationToken ct)
    {
        var resources = await _context.Resources.AsNoTracking()
            .Where(r => r.BoardId == boardId && r.StudentId == studentId
                && (r.SprintNumber == null || r.SprintNumber == sprintNumber))
            .OrderBy(r => r.IsFigma ? 0 : 1).ThenBy(r => r.Name)
            .ToListAsync(ct);

        sb.AppendLine("### Resources & Figma");
        if (resources.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }
        foreach (var r in resources)
        {
            if (r.IsFigma || LooksLikeImageUrl(r.Url))
            {
                sb.AppendLine($"- {(r.IsFigma ? "[Figma]" : "[Resource]")} **{r.Name}**: {r.Url}");
                continue;
            }

            // Same extraction as GapAnalysis (TryExtractResourceDocumentTextAsync) — the model was
            // previously shown only the URL for non-image resources, so it could never verify a
            // deliverable's actual content (e.g. an uploaded PRD) even when UseResources was enabled.
            var extracted = await TryExtractResourceDocumentTextAsync(r.Url, r.Name, ct);
            if (extracted != null)
            {
                sb.AppendLine($"- [Resource] **{r.Name}**: {r.Url} (document content extracted on server — see below; score its actual content, not just its presence)");
                sb.AppendLine($"  Extracted content of \"{r.Name}\":");
                sb.AppendLine("  ```");
                sb.AppendLine(extracted);
                sb.AppendLine("  ```");
            }
            else
            {
                sb.AppendLine($"- [Resource] **{r.Name}**: {r.Url} (non-image — URL confirms storage; content could not be extracted for review, e.g. unsupported file type or empty/scanned document — do not penalise the student for a server-side extraction limitation)");
            }
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentStakeholdersAsync(StringBuilder sb, string boardId, CancellationToken ct)
    {
        var stakeholders = await _context.Stakeholders.AsNoTracking()
            .Include(s => s.Category)
            .Include(s => s.Status)
            .Where(s => s.BoardId == boardId)
            .ToListAsync(ct);

        sb.AppendLine("### CRM / Stakeholders _(squad-level — all team members)_");
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

    /// <summary>
    /// Formats <see cref="Metric.ExplicitRules"/> as a labeled block appended after the rubric in the
    /// system prompt (see <see cref="RunAssessmentEngine"/>). Kept separate from <c>skillRubric</c> so
    /// <see cref="ParseRubricCategories"/> never sees rules text as candidate scoring dimensions.
    /// Returns an empty string when there are no explicit rules, so the prompt is unchanged for metrics
    /// that don't use this field.
    /// </summary>
    internal static string BuildExplicitRulesBlock(string? explicitRules)
    {
        var trimmed = explicitRules?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        return "\n\n" + $$"""
            === EXPLICIT RULES ===
            {{trimmed}}
            === END EXPLICIT RULES ===
            """;
    }

    /// <summary>
    /// Extracts named score dimensions from a skill rubric. A dimension is a line starting with
    /// "Category:" (optionally bulleted), e.g. "Category: Initiative — visible activity in chats and tasks".
    /// The name is the text before the first separator (—, –, -, or :). Returns an empty list for free-form prose.
    /// </summary>
    internal static List<string> ParseRubricCategories(string skillRubric)
    {
        var names = new List<string>();
        foreach (var rawLine in skillRubric.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('-', '*', '•').TrimStart();
            if (!line.StartsWith("Category:", StringComparison.OrdinalIgnoreCase))
                continue;
            var rest = line["Category:".Length..].Trim();
            var sepIdx = rest.IndexOfAny(new[] { '—', '–' });
            var dashIdx = rest.IndexOf(" - ", StringComparison.Ordinal);
            var colonIdx = rest.IndexOf(':');
            var cut = new[] { sepIdx, dashIdx, colonIdx }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
            var name = (cut >= 0 ? rest[..cut] : rest).Trim();
            if (name.Length > 0 && !names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                names.Add(name);
        }
        return names;
    }

    /// <summary>
    /// Forces the LLM's category list to exactly match the expected names (order preserved):
    /// matched categories keep their score/rationale under the canonical name, invented ones are
    /// dropped, and missing ones are filled with a no-score note. When a single category is expected
    /// and the model renamed it, the first returned category is adopted under the expected name.
    /// </summary>
    internal static List<GapAnalysisCategoryScore> NormalizeAssessmentCategories(
        List<GapAnalysisCategoryScore> returned, List<string> expected)
    {
        var usable = returned.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        var result = new List<GapAnalysisCategoryScore>();
        foreach (var name in expected)
        {
            var match = usable.FirstOrDefault(c =>
                string.Equals(c.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (match == null && expected.Count == 1)
                match = usable.FirstOrDefault();

            result.Add(new GapAnalysisCategoryScore
            {
                Name = name,
                Score = match != null ? Math.Clamp(match.Score, 0, 100) : 0,
                Rationale = match?.Rationale is { Length: > 0 } r
                    ? r
                    : "(no score returned for this category)",
            });
        }
        return result;
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

    private async Task AppendAssessmentTrelloTasksAsync(
        StringBuilder sb, string boardId, string? studentEmail, string? roleLabel, int sprintNumber, CancellationToken ct)
    {
        // Sprint role card first: it carries the sprint's task requests and is matched by role
        // label, not member assignment — so it is invisible to the member-cards lookup below.
        if (!string.IsNullOrWhiteSpace(roleLabel))
        {
            sb.AppendLine($"### Sprint {sprintNumber} task card (role: {roleLabel}) — the tasks requested from this student this sprint");
            try
            {
                var labels = await _trelloService.ResolveSprintLabelsAsync(boardId, sprintNumber, roleLabel);
                var found = false;
                foreach (var lbl in labels)
                {
                    var snap = await _trelloService.GetSprintRoleCardSnapshotAsync(boardId, sprintNumber, lbl);
                    if (snap == null) continue;
                    found = true;
                    sb.AppendLine($"#### {snap.CardName}");
                    if (!string.IsNullOrWhiteSpace(snap.Description))
                        sb.AppendLine(Truncate(snap.Description.Trim(), 2000));
                    if (!string.IsNullOrWhiteSpace(snap.ChecklistsText))
                        sb.AppendLine(snap.ChecklistsText);
                }
                if (!found) sb.AppendLine("_(no sprint task card found for this role)_");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sprint role card lookup failed for board {BoardId}, role {Role}", boardId, roleLabel);
                sb.AppendLine("_(sensor error — Trello unavailable)_");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Trello tasks (assigned to student)");
        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            sb.AppendLine("_(student has no email — cannot look up assigned cards)_");
            sb.AppendLine();
            return;
        }
        try
        {
            var cards = await _trelloService.GetMemberBoardCardsAsync(boardId, studentEmail);
            if (cards.Count == 0) { sb.AppendLine("_(none)_"); sb.AppendLine(); return; }
            foreach (var card in cards)
            {
                sb.AppendLine($"#### {card.CardName}");
                if (!string.IsNullOrWhiteSpace(card.Description))
                    sb.AppendLine(Truncate(card.Description.Trim(), 1000));
                if (!string.IsNullOrWhiteSpace(card.ChecklistsText))
                    sb.AppendLine(card.ChecklistsText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TrelloTasks sensor failed for board {BoardId}", boardId);
            sb.AppendLine("_(sensor error — Trello unavailable)_");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentTrelloUserStoryAsync(
        StringBuilder sb, string boardId, string? userStoryBoardId, string? roleLabel, int sprintNumber,
        bool haveWindow, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        sb.AppendLine("### Trello user story (this sprint's deliverable for this student)");
        try
        {
            // Role-based attribution: user stories are the PM's deliverable. Other roles don't
            // write them, so their assessments should not see them as personal activity.
            if (!Services.TrelloService.IsPMRole(roleLabel))
            {
                sb.AppendLine("_(user stories are the Product Manager's deliverable — not this student's role)_");
                sb.AppendLine();
                return;
            }

            // Primary linkage: sprint role card (main board) → ModuleId custom field → story card
            // with the same ModuleId. The sprint card IS the sprint scoping — no date heuristics.
            // The story card lives on the dedicated user-story board when one exists, otherwise on
            // the main board's "User Stories" list (legacy).
            var storyBoard = string.IsNullOrWhiteSpace(userStoryBoardId) ? boardId : userStoryBoardId;
            string? moduleIdStr = null;
            var labels = await _trelloService.ResolveSprintLabelsAsync(boardId, sprintNumber, roleLabel!);
            foreach (var lbl in labels)
            {
                moduleIdStr = await _trelloService.GetModuleIdFromSprintCardAsync(boardId, sprintNumber, lbl);
                if (!string.IsNullOrWhiteSpace(moduleIdStr)) break;
            }

            if (!string.IsNullOrWhiteSpace(moduleIdStr))
            {
                var usResult = await _trelloService.GetUserStoryCardByModuleIdAsync(storyBoard, moduleIdStr.Trim());
                var card = GetUserStoryCardFromResult(usResult);
                var storyText = ConcatenateUserStoryText(card);
                if (!string.IsNullOrWhiteSpace(storyText))
                {
                    sb.AppendLine($"_(linked from the sprint {sprintNumber} role card via ModuleId {moduleIdStr.Trim()})_");
                    sb.AppendLine(storyText.Trim());
                    sb.AppendLine();
                    return;
                }
                _logger.LogInformation("[USER-STORY-SENSOR] ModuleId {ModuleId} set on sprint card but no matching story card on board {Board}; falling back to full list.",
                    moduleIdStr.Trim(), storyBoard);
            }

            // Fallback (no ModuleId on the sprint card, or no matching card): full list, window-filtered.
            var cards = await _trelloService.GetUserStoryCardsAsync(
                boardId, userStoryBoardId,
                haveWindow ? windowStart : null, haveWindow ? windowEnd : null);

            // dateLastActivity only reflects the LAST touch, so on retroactive assessments a story
            // worked on this sprint but touched later falls outside the window. Fall back to the
            // unfiltered list, honestly labeled, rather than reporting no story exists at all.
            if (cards.Count == 0 && haveWindow)
            {
                cards = await _trelloService.GetUserStoryCardsAsync(boardId, userStoryBoardId);
                if (cards.Count > 0)
                    sb.AppendLine("_(no story activity detected inside this sprint window — the stories below are shown as persistent artifacts; assess their quality but do NOT credit their creation to this sprint)_");
            }
            if (cards.Count == 0) { sb.AppendLine("_(none for this sprint)_"); sb.AppendLine(); return; }

            foreach (var card in cards)
            {
                sb.AppendLine($"#### {card.CardName}");
                if (!string.IsNullOrWhiteSpace(card.Description))
                    sb.AppendLine(Truncate(card.Description.Trim(), 500));
                if (!string.IsNullOrWhiteSpace(card.ChecklistsText))
                    sb.AppendLine(card.ChecklistsText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TrelloUserStory sensor failed for board {BoardId}", boardId);
            sb.AppendLine("_(sensor error — Trello unavailable)_");
        }
        sb.AppendLine();
    }

    private async Task AppendAssessmentFigmaDesignAsync(
        StringBuilder sb, string boardId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        sb.AppendLine("### Figma design (version history) _(squad-level — versions attributed by user handle)_");
        try
        {
            var figmaRow = await _context.Figma.AsNoTracking()
                .FirstOrDefaultAsync(f => f.BoardId == boardId, ct);
            if (figmaRow == null || string.IsNullOrEmpty(figmaRow.FigmaFileKey))
            {
                sb.AppendLine("_(no Figma integration configured for this board)_");
                sb.AppendLine();
                return;
            }

            // Refresh token if expired.
            var token = figmaRow.FigmaAccessToken;
            if (string.IsNullOrEmpty(token))
            {
                sb.AppendLine("_(Figma OAuth token missing — reconnect Figma for this board)_");
                sb.AppendLine();
                return;
            }

            if (figmaRow.FigmaTokenExpiry.HasValue && figmaRow.FigmaTokenExpiry.Value <= DateTime.UtcNow)
            {
                token = await TryRefreshFigmaTokenAsync(figmaRow, ct);
                if (string.IsNullOrEmpty(token))
                {
                    sb.AppendLine("_(Figma token expired and could not be refreshed)_");
                    sb.AppendLine();
                    return;
                }
            }

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var versionsUrl = $"https://api.figma.com/v1/files/{figmaRow.FigmaFileKey}/versions";
            var res = await http.GetAsync(versionsUrl, ct);
            if (!res.IsSuccessStatusCode)
            {
                sb.AppendLine($"_(Figma versions API returned {(int)res.StatusCode})_");
                sb.AppendLine();
                return;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("versions", out var versions))
            {
                sb.AppendLine("_(no versions returned)_");
                sb.AppendLine();
                return;
            }

            var count = 0;
            foreach (var v in versions.EnumerateArray())
            {
                var createdAtStr = v.TryGetProperty("created_at", out var ca) ? ca.GetString() : null;
                if (createdAtStr == null || !DateTime.TryParse(createdAtStr,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var ts))
                    continue;
                var tsUtc = ts.ToUniversalTime();
                if (tsUtc < windowStart || tsUtc > windowEnd) continue;

                var label = v.TryGetProperty("label", out var lp) ? lp.GetString() : null;
                var descr = v.TryGetProperty("description", out var dp) ? dp.GetString() : null;
                var user  = v.TryGetProperty("user", out var up)
                    ? (up.TryGetProperty("handle", out var hp) ? hp.GetString() : null) : null;
                sb.Append($"- [{tsUtc:yyyy-MM-dd HH:mm}]");
                if (!string.IsNullOrWhiteSpace(user))   sb.Append($" by {user}");
                if (!string.IsNullOrWhiteSpace(label))  sb.Append($" — {label}");
                if (!string.IsNullOrWhiteSpace(descr))  sb.Append($": {Truncate(descr.Trim(), 200)}");
                sb.AppendLine();
                count++;
            }
            if (count == 0) sb.AppendLine("_(no Figma versions in this sprint window)_");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FigmaDesign sensor failed for board {BoardId}", boardId);
            sb.AppendLine("_(sensor error — Figma unavailable)_");
        }
        sb.AppendLine();
    }

    private async Task<string?> TryRefreshFigmaTokenAsync(Models.Figma figmaRow, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(figmaRow.FigmaRefreshToken)) return null;
        try
        {
            using var http = _httpClientFactory.CreateClient();
            var form = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id",     _configuration["Figma:ClientId"] ?? ""),
                new KeyValuePair<string, string>("client_secret", _configuration["Figma:ClientSecret"] ?? ""),
                new KeyValuePair<string, string>("refresh_token", figmaRow.FigmaRefreshToken),
                new KeyValuePair<string, string>("grant_type",    "refresh_token"),
            ]);
            var res = await http.PostAsync("https://api.figma.com/v1/oauth/token", form, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("access_token", out var ap) ? ap.GetString() : null;
        }
        catch { return null; }
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

    internal static void AppendChatBlobSection(StringBuilder sb, string header, List<string>? lines, bool haveWindow)
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
