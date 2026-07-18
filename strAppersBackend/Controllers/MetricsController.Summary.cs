using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Utilities;

namespace strAppersBackend.Controllers;

/// <summary>
/// AI summary reports built exclusively from already-collected <see cref="CacheMetrics"/> data —
/// no sensors, no Trello/GitHub/chat access, one LLM call each (fits comfortably under the Azure
/// front-end request timeout that kills long multi-metric batch runs).
/// - Sprint Summary: condenses one student's sprint metric reviews → stored as MetricId=0 for that sprint.
/// - Course Summary: condenses that student's Sprint Summaries across sprints → stored as MetricId=0,
///   SprintNumber=-1 (sprint 0 is the real "Bugs" sprint, so -1 is the "no sprint" sentinel).
/// The MetricId=0 sentinel row in Metrics is seeded by the SeedSummaryMetricRow migration.
/// </summary>
public partial class MetricsController
{
    /// <summary>Metrics.Id sentinel for summary rows in CacheMetrics (seeded row named "SprintSummary").</summary>
    internal const int SummaryMetricId = 0;

    /// <summary>CacheMetrics.SprintNumber sentinel for the course-level summary (sprint 0 is the real Bugs sprint).</summary>
    internal const int CourseSummarySprintNumber = -1;

    public class SprintSummaryRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public int SprintNumber { get; set; }
        /// <summary>When true, returns generated prompts only (no LLM, no CacheMetrics write).</summary>
        public bool Test { get; set; }
    }

    public class CourseSummaryRequest
    {
        public string BoardId { get; set; } = string.Empty;
        public int StudentId { get; set; }
        /// <summary>When true, returns generated prompts only (no LLM, no CacheMetrics write).</summary>
        public bool Test { get; set; }
    }

    /// <summary>
    /// Sprint Summary: one report per student+sprint, built only from that sprint's collected
    /// CacheMetrics.ReviewContent. Chart = one bar per source metric. Stored as MetricId=0.
    /// </summary>
    [HttpPost("use/sprint-summary")]
    public async Task<ActionResult<object>> SprintSummary([FromBody] SprintSummaryRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });
        if (request.SprintNumber < 0)
            return BadRequest(new { success = false, message = "SprintNumber must be >= 0." });

        var boardId = request.BoardId.Trim();
        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var metricRows = await _context.CacheMetrics.AsNoTracking()
            .Include(c => c.Metric)
            .Where(c => c.BoardId == boardId && c.StudentId == request.StudentId &&
                        c.SprintNumber == request.SprintNumber && c.MetricId != SummaryMetricId)
            .OrderBy(c => c.MetricId)
            .ToListAsync(cancellationToken);

        if (metricRows.Count == 0)
        {
            return Ok(new
            {
                success = false,
                skippedLlm = true,
                message = "No collected metric data exists for this student and sprint yet. Run the assessment metrics first, then generate the Sprint Summary.",
            });
        }

        var sources = metricRows
            .Select(r => (MetricName: r.Metric?.Name?.Trim() is { Length: > 0 } n ? n : $"Metric {r.MetricId}",
                          ReviewContent: r.ReviewContent ?? ""))
            .ToList();

        var systemPrompt = BuildSprintSummarySystemPrompt();
        var userPrompt = BuildSprintSummaryUserPrompt(request.SprintNumber, sources);

        if (request.Test)
            return Ok(new { success = true, test = true, message = "Test mode: LLM not called; CacheMetrics not updated.", systemPrompt, userPrompt });

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
                    message = "Sprint Summary did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            dto.Categories = ApplyAssessmentCategoryPolicy(dto.Categories, new List<string>(), "Sprint Summary");
            var chartRows = dto.Categories.Select(c => (c.Name, Math.Clamp(c.Score, 0, 100))).ToList();
            var graphBase64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(chartRows, "Sprint Summary"));
            var reviewContent = FormatAssessmentReviewContent("Sprint Summary", dto);

            await UpsertCacheMetricsAsync(
                boardId, request.StudentId, request.SprintNumber, SummaryMetricId,
                reviewContent, graphBase64, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = SummaryMetricId,
                sprintNumber = request.SprintNumber,
                reviewContent,
                graphBase64,
                model = aiModel.Name,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sprint Summary failed for board {BoardId}, student {StudentId}, sprint {Sprint}",
                boardId, request.StudentId, request.SprintNumber);
            return StatusCode(500, new { success = false, message = "Sprint Summary generation failed. Please try again." });
        }
    }

    /// <summary>
    /// Course Summary: one report per student across all their Sprint Summaries. Chart = one bar per
    /// sprint (progression view). Stored as MetricId=0, SprintNumber=-1.
    /// </summary>
    [HttpPost("use/course-summary")]
    public async Task<ActionResult<object>> CourseSummary([FromBody] CourseSummaryRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { success = false, message = "BoardId is required." });
        if (request.StudentId <= 0)
            return BadRequest(new { success = false, message = "StudentId is required." });

        var boardId = request.BoardId.Trim();
        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);
        if (student == null)
            return NotFound(new { success = false, message = $"Student {request.StudentId} not found." });
        if (!string.Equals(student.BoardId?.Trim(), boardId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Student is not assigned to this BoardId." });

        var summaryRows = await _context.CacheMetrics.AsNoTracking()
            .Where(c => c.BoardId == boardId && c.StudentId == request.StudentId &&
                        c.MetricId == SummaryMetricId && c.SprintNumber >= 0)
            .OrderBy(c => c.SprintNumber)
            .ToListAsync(cancellationToken);

        if (summaryRows.Count == 0)
        {
            return Ok(new
            {
                success = false,
                skippedLlm = true,
                message = "No Sprint Summaries exist for this student yet. Generate a Sprint Summary for at least one sprint first, then run the Course Summary.",
            });
        }

        var sources = summaryRows
            .Select(r => (r.SprintNumber, ReviewContent: r.ReviewContent ?? ""))
            .ToList();

        var systemPrompt = BuildCourseSummarySystemPrompt();
        var userPrompt = BuildCourseSummaryUserPrompt(sources);

        if (request.Test)
            return Ok(new { success = true, test = true, message = "Test mode: LLM not called; CacheMetrics not updated.", systemPrompt, userPrompt });

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
                    message = "Course Summary did not return valid JSON. Nothing was saved to CacheMetrics.",
                    preview = Truncate(llmText.Trim(), 4000),
                });
            }

            dto.Categories = ApplyAssessmentCategoryPolicy(dto.Categories, new List<string>(), "Course Summary");
            var chartRows = dto.Categories.Select(c => (c.Name, Math.Clamp(c.Score, 0, 100))).ToList();
            var graphBase64 = GapAnalysisBarChartRenderer.ToBase64Png(
                GapAnalysisBarChartRenderer.RenderSingleChart(chartRows, "Course Summary"));
            var reviewContent = FormatAssessmentReviewContent("Course Summary", dto);

            await UpsertCacheMetricsAsync(
                boardId, request.StudentId, CourseSummarySprintNumber, SummaryMetricId,
                reviewContent, graphBase64, cancellationToken);

            return Ok(new
            {
                success = true,
                metricId = SummaryMetricId,
                sprintNumber = CourseSummarySprintNumber,
                sprintsSummarized = summaryRows.Select(r => r.SprintNumber).ToList(),
                reviewContent,
                graphBase64,
                model = aiModel.Name,
                inputTokens,
                outputTokens,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Course Summary failed for board {BoardId}, student {StudentId}", boardId, request.StudentId);
            return StatusCode(500, new { success = false, message = "Course Summary generation failed. Please try again." });
        }
    }

    internal static string BuildSprintSummarySystemPrompt()
    {
        return """
            You are an academic performance report writer for a project-based software course.

            Your task: write one consolidated SPRINT SUMMARY of a single student's sprint, based EXCLUSIVELY on the collected metric reviews provided in the user message. Those reviews are your only source of truth — do not use outside knowledge, do not invent activity, evidence, or scores that the reviews do not support, and do not speculate about anything the reviews don't cover.

            Rules:
            - Return one categories[] entry PER SOURCE METRIC, named exactly after that metric (as given in the section headers), with a 0-100 score reflecting the student's performance in that metric according to its review. When a review states a Final Score, use it; otherwise derive a faithful score from the review's own findings.
            - narrative: a concise markdown summary of the sprint as a whole — overall performance, main strengths, main gaps, and 1-3 concrete recommendations for the next sprint. Synthesize across metrics; do not simply restate each review.
            - Output valid JSON only, no markdown fences:
              {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            """;
    }

    internal static string BuildSprintSummaryUserPrompt(int sprintNumber, IReadOnlyList<(string MetricName, string ReviewContent)> sources)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Collected metric reviews — Sprint {sprintNumber}");
        sb.AppendLine("These reviews are the ONLY data you may base the summary on.");
        sb.AppendLine();
        foreach (var (metricName, reviewContent) in sources)
        {
            sb.AppendLine($"### Metric: {metricName}");
            sb.AppendLine(string.IsNullOrWhiteSpace(reviewContent) ? "(empty review)" : reviewContent.Trim());
            sb.AppendLine();
        }
        sb.AppendLine("Respond with JSON only as specified in your instructions.");
        return sb.ToString();
    }

    internal static string BuildCourseSummarySystemPrompt()
    {
        return """
            You are an academic performance report writer for a project-based software course.

            Your task: write one consolidated COURSE SUMMARY of a single student's performance across the whole course, based EXCLUSIVELY on the per-sprint summaries provided in the user message. Those summaries are your only source of truth — do not use outside knowledge, do not invent activity, evidence, or scores they do not support.

            Rules:
            - Return one categories[] entry PER METRIC that appears in the sprint summaries' Scores sections, named exactly after that metric, with a 0-100 course-level score for the student's performance in that metric across ALL sprints. Derive it from that metric's per-sprint scores — weight a clear improving or declining trend rather than blindly averaging. Each rationale must note the metric's trajectory across the sprints.
            - narrative: a concise markdown report of the whole course — the student's trajectory (improving, declining, steady), enduring strengths, recurring gaps, and an overall closing evaluation suitable for sharing with the student. Include the per-sprint overall scores in the narrative (e.g. "Sprint 1: 60 → Sprint 2: 75") so the progression stays visible.
            - Output valid JSON only, no markdown fences:
              {"categories":[{"name":"string","score":0,"rationale":"string"}],"narrative":"markdown"}
            """;
    }

    internal static string BuildCourseSummaryUserPrompt(IReadOnlyList<(int SprintNumber, string ReviewContent)> sources)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Per-sprint summaries for this student");
        sb.AppendLine("These summaries are the ONLY data you may base the course report on.");
        sb.AppendLine();
        foreach (var (sprintNumber, reviewContent) in sources)
        {
            sb.AppendLine($"### Sprint {sprintNumber}");
            sb.AppendLine(string.IsNullOrWhiteSpace(reviewContent) ? "(empty summary)" : reviewContent.Trim());
            sb.AppendLine();
        }
        sb.AppendLine("Respond with JSON only as specified in your instructions.");
        return sb.ToString();
    }
}
