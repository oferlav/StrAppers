using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>
/// Hard Skills assessment. Where the generic engine scores a configured Metric (a soft skill),
/// this scores the student against their role's own Professional Skills definition — Role.Competencies,
/// authored in Roles &amp; Professional Skills definition — and narrows the evidence to the role's Main
/// Tool (Role.Skill). Stored in CacheMetrics as MetricId=-1.
/// </summary>
public partial class MetricsController
{
    /// <summary>Metrics.Id sentinel for hard-skills rows in CacheMetrics (seeded row named "HardSkills").</summary>
    internal const int HardSkillsMetricId = -1;

    /// <summary>Name of the in-memory stand-in, and the slug the batch loop dispatches on.</summary>
    internal const string HardSkillsMetricName = "HardSkills";
    internal const string HardSkillsMetricSlug = "hardskills";

    /// <summary>Label shown in the staff dashboard's metric combo.</summary>
    internal const string HardSkillsDisplayName = "Professional Skills";

    /// <summary>
    /// In-memory stand-in for the hard-skills sentinel, appended to the batch loop's metric list and
    /// to the assessment-config response. Never persisted: the seeded Metrics row exists only to
    /// satisfy the CacheMetrics FK, and none of its columns are read — dispatch keys off this Name,
    /// so renaming the database row cannot break it.
    /// </summary>
    internal static Metric HardSkillsBatchMetric() => new()
    {
        Id = HardSkillsMetricId,
        Name = HardSkillsMetricName,
        Required = true,
    };

    public record HardSkillsAssessmentRequest(
        string BoardId,
        int StudentId,
        int SprintNumber,
        bool Test = false,
        string? TrelloRoleLabel = null);

    /// <summary>
    /// Builds the in-memory Metric the shared context builder runs off. Nothing is persisted —
    /// it exists only to carry the rubric and the sensor flags.
    ///
    /// The Main Tool (Role.Skill.Name) selects which sensor the evidence comes from. The catalog
    /// is small and fixed (GitHub, Trello, Figma, CRM); "Other" — or any unrecognised value —
    /// leaves every sensor on, because there is no single source to narrow to.
    /// </summary>
    internal static Metric BuildHardSkillsMetric(string competencies, string? mainTool)
    {
        var metric = new Metric
        {
            Id = HardSkillsMetricId,
            Name = "Professional Skills",
            Skill = competencies,
            Required = false,
        };

        void OnlyThese(Action<Metric> enable)
        {
            metric.UseCustomerChat = false;
            metric.UseMentorChat = false;
            metric.UseCodebaseQuality = false;
            metric.UseResources = false;
            metric.UseStakeholders = false;
            metric.UseProjectModule = false;
            metric.UseMeetingTranscripts = false;
            metric.UseGroupChat = false;
            metric.UsePrivateChat = false;
            metric.UseTrelloTasks = false;
            metric.UseTrelloUserStory = false;
            metric.UseFigmaDesign = false;
            enable(metric);
        }

        switch ((mainTool ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "github":
                // Codebase evidence also carries the board state the code was produced against.
                OnlyThese(m => { m.UseCodebaseQuality = true; m.UseResources = true; });
                break;
            case "trello":
                OnlyThese(m => { m.UseTrelloTasks = true; m.UseTrelloUserStory = true; });
                break;
            case "figma":
                OnlyThese(m => { m.UseFigmaDesign = true; m.UseResources = true; });
                break;
            case "crm":
                OnlyThese(m => { m.UseStakeholders = true; m.UseCustomerChat = true; });
                break;
            default:
                break; // "Other"/unset — every sensor stays enabled.
        }

        return metric;
    }

    /// <summary>
    /// Finds the role row that actually carries the Hard Skills text.
    ///
    /// A student's StudentRole often points at the global role (InstituteId=null), while
    /// "Roles &amp; Professional Skills definition" saves Competencies onto a *separate* institute-scoped
    /// row with the same name — see the comment in RolesController's save: "dto.Id may be a global
    /// role Id (InstituteId=null) ... so a new institute-specific row is created instead". Reading
    /// Competencies straight off the assigned row therefore finds nothing for most students.
    ///
    /// So when the assigned row has no Competencies, fall back to the institute's row of the same
    /// name. Main Tool is read from whichever row wins, since that is the row the staff configured.
    /// </summary>
    internal async Task<Role> ResolveHardSkillsRoleAsync(Role assigned, int? instituteId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(assigned.Competencies))
            return assigned;
        if (instituteId is null or <= 0)
            return assigned;

        var name = (assigned.Name ?? string.Empty).Trim().ToLower();
        if (name.Length == 0)
            return assigned;

        var candidate = await _context.Roles
            .AsNoTracking()
            .Include(r => r.Skill)
            .Where(r => r.InstituteId == instituteId
                        && r.IsActive
                        && r.Name.ToLower() == name
                        && r.Competencies != null
                        && r.Competencies != "")
            // Institute-level row first, then squad-scoped ones; oldest wins within each group.
            .OrderBy(r => r.SquadId == null ? 0 : 1)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(ct);

        return candidate ?? assigned;
    }

    /// <summary>
    /// Scores one student's hard skills for a sprint and caches it as MetricId=-1.
    /// Mirrors <see cref="RunAssessmentEngine"/>, but the rubric is the role's Professional Skills text
    /// rather than a Metric row.
    /// </summary>
    [HttpPost("use/assess-hard-skills")]
    public async Task<IActionResult> RunHardSkillsAssessment(
        [FromBody] HardSkillsAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { message = "SprintNumber must be >= 0." });

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
                    .ThenInclude(r => r!.Skill)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { message = $"Student {request.StudentId} not found." });

        var assignedRole = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.Role
                           ?? student.StudentRoles?.FirstOrDefault()?.Role;
        if (assignedRole == null)
            return UnprocessableEntity(new { message = $"Student {request.StudentId} has no role, so there are no hard skills to assess." });

        var role = await ResolveHardSkillsRoleAsync(assignedRole, student.InstituteId, cancellationToken);

        var competencies = role.Competencies?.Trim() ?? string.Empty;
        if (competencies.Length == 0)
            return UnprocessableEntity(new
            {
                message = $"Role \"{assignedRole.Name}\" has no Professional Skills defined for institute {student.InstituteId?.ToString() ?? "(none)"}. "
                          + "Add them in Roles & Professional Skills definition, then re-run.",
                assignedRoleId = assignedRole.Id,
                assignedRoleInstituteId = assignedRole.InstituteId,
            });

        var mainTool = role.Skill?.Name?.Trim();

        // Board labels carry the student's assigned role name, not whichever scoped row the
        // Hard Skills text was found on (the names match, but the assigned row is the authority here).
        var trelloRoleLabel = request.TrelloRoleLabel;
        if (string.IsNullOrWhiteSpace(trelloRoleLabel) && !string.IsNullOrWhiteSpace(assignedRole.Name))
            trelloRoleLabel = board.IsSingleRole && student.RoleIndex > 0
                ? $"{assignedRole.Name.Trim()} {student.RoleIndex}"
                : assignedRole.Name.Trim();

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

        var hardSkillsMetric = BuildHardSkillsMetric(competencies, mainTool);

        var contextMd = await BuildAssessmentContextAsync(
            hardSkillsMetric, boardId, board, student, request.SprintNumber,
            trelloRoleLabel, haveWindow, windowStart, windowEnd,
            cancellationToken);

        var parsedCategories = ParseRubricCategories(competencies);
        var categoryScoringInstruction = BuildCategoryScoringInstruction(parsedCategories);

        // The Code & GitHub sensor emits states the model cannot interpret unaided — "could not
        // observe" (a platform failure, not a student outcome) and the empty branch compare that a
        // squash merge normally produces. This matters most here: when the role's Main Tool is
        // GitHub, BuildHardSkillsMetric narrows the evidence to this sensor, so the whole score rests
        // on reading those states correctly.
        var gitHubScoringRules = hardSkillsMetric.UseCodebaseQuality ? BuildGitHubScoringRules() : string.Empty;

        var toolLine = string.IsNullOrWhiteSpace(mainTool)
            ? "The role has no single Main Tool, so all available sprint evidence is provided."
            : $"The role's Main Tool is {mainTool}. The evidence below is drawn from it — judge the professional skills through that work.";

        var systemPrompt = $$"""
            You are a professional-skills assessment expert for the role "{{role.Name}}".

            Your task: score how well the student demonstrated the PROFESSIONAL SKILLS below during this sprint.
            The professional skills definition is your highest-priority instruction — follow it exactly, even when
            the evidence is sparse or seems to point elsewhere.

            === PROFESSIONAL SKILLS DEFINITION ===
            {{competencies}}
            === END PROFESSIONAL SKILLS DEFINITION ===

            {{toolLine}}

            Scoring rules:
            {{categoryScoringInstruction}}
            - Scores are integers on a 0–100 scale. Calibrate to these bands: 0–19 = no meaningful evidence,
              20–39 = minimal, 40–59 = partial, 60–79 = good, 80–100 = excellent. Use the full range —
              a weak-but-present performance is ~20–40, not a single-digit score.
            - Assess technical capability only. Communication, teamwork and other soft skills are scored
              separately — ignore them here except as evidence of technical work.
            - Use the Sprint Context in the user message as your evidence — ground every score in verbatim evidence from it.
            - Sections marked _(squad-level)_ cover the whole team; only attribute activity to this student if they are explicitly named or identifiable.
            - Do not invent activity. Sections marked _(none for this sprint)_ have no data; do not speculate about them.
            - Output valid JSON only, no markdown fences:
              {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            - narrative: brief markdown summary of technical strengths, gaps, and 1–3 concrete follow-up suggestions.{{gitHubScoringRules}}
            """;

        var userPrompt = new StringBuilder()
            .AppendLine($"## Sprint Context — Sprint {request.SprintNumber} | Student: {student.FirstName} {student.LastName} | Board: {boardId}")
            .AppendLine($"Role: {role.Name} | Main Tool: {mainTool ?? "(none)"}")
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
                role = role.Name,
                roleId = role.Id,
                roleInstituteId = role.InstituteId,
                assignedRoleId = assignedRole.Id,
                mainTool,
                systemPrompt,
                userPrompt,
            });
        }

        try
        {
            var aiModel = await ResolveAssessmentEngineModelAsync(student.InstituteId, cancellationToken);

            var (llmText, inputTokens, outputTokens) = await _chatCompletionService.GetChatCompletionAsync(
                aiModel, systemPrompt, userPrompt, null);

            if (!TryParseGapAnalysisJson(llmText, out var dto) || dto == null)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "Hard skills assessment did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            dto.Categories = ApplyAssessmentCategoryPolicy(dto.Categories, parsedCategories, "Professional Skills");

            var rows = dto.Categories
                .Select(c => (c.Name, Math.Clamp(c.Score, 0, 100)))
                .ToList();

            var graphBase64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(rows, "Professional Skills"));

            var reviewContent = FormatAssessmentReviewContent("Professional Skills", dto);

            await UpsertCacheMetricsAsync(
                boardId, student.Id, request.SprintNumber, HardSkillsMetricId,
                reviewContent, graphBase64, cancellationToken);

            return Ok(new
            {
                success  = true,
                metricId = HardSkillsMetricId,
                role     = role.Name,
                mainTool,
                reviewContent,
                graphBase64,
                model    = aiModel.Name,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Hard skills assessment failed for student {StudentId} board {BoardId} sprint {Sprint}",
                request.StudentId, boardId, request.SprintNumber);
            return StatusCode(500, new { message = "AI hard skills assessment call failed. Please try again." });
        }
    }
}
