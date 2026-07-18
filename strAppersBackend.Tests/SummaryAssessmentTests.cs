using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests for the Sprint Summary / Course Summary feature (MetricsController.Summary.cs):
/// prompt builders (summaries must be based ONLY on collected CacheMetrics.ReviewContent), the
/// MetricId=0 / SprintNumber=-1 sentinels, and how the assessment report maps summary rows
/// ("Sprint Summary" display name, course summaries split out of the sprint sections).
/// </summary>
public class SummaryAssessmentTests
{
    // ── Sentinels ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sentinels_MatchTheAgreedStorageScheme()
    {
        // Per design: sprint summaries at MetricId=0; course summary additionally at SprintNumber=-1
        // (NOT 0 — sprint 0 is the real "Bugs" sprint and may have its own sprint summary).
        Assert.Equal(0, MetricsController.SummaryMetricId);
        Assert.Equal(-1, MetricsController.CourseSummarySprintNumber);
    }

    // ── Sprint Summary prompts ───────────────────────────────────────────────────

    [Fact]
    public void SprintSummarySystemPrompt_RestrictsToProvidedDataOnly()
    {
        var prompt = MetricsController.BuildSprintSummarySystemPrompt();

        Assert.Contains("EXCLUSIVELY", prompt);
        Assert.Contains("only source of truth", prompt);
        Assert.Contains("do not invent", prompt);
    }

    [Fact]
    public void SprintSummarySystemPrompt_AsksForOneCategoryPerSourceMetric()
    {
        var prompt = MetricsController.BuildSprintSummarySystemPrompt();

        Assert.Contains("PER SOURCE METRIC", prompt);
        Assert.Contains("\"categories\"", prompt);
        Assert.Contains("\"narrative\"", prompt);
    }

    [Fact]
    public void SprintSummaryUserPrompt_IncludesEachMetricNameAndReviewContent()
    {
        var sources = new List<(string, string)>
        {
            ("CustomerEngagement", "## CustomerEngagement review\n- **Clarity** (80): good."),
            ("CustomerRequirementsFidelity", "## CRF Assessment — **Final Score: 50**\nDetails here."),
        };

        var prompt = MetricsController.BuildSprintSummaryUserPrompt(2, sources);

        Assert.Contains("Sprint 2", prompt);
        Assert.Contains("### Metric: CustomerEngagement", prompt);
        Assert.Contains("- **Clarity** (80): good.", prompt);
        Assert.Contains("### Metric: CustomerRequirementsFidelity", prompt);
        Assert.Contains("**Final Score: 50**", prompt);
        Assert.Contains("ONLY data", prompt);
    }

    [Fact]
    public void SprintSummaryUserPrompt_MarksEmptyReviews()
    {
        var prompt = MetricsController.BuildSprintSummaryUserPrompt(1, new List<(string, string)> { ("Attendance", "  ") });

        Assert.Contains("(empty review)", prompt);
    }

    // ── Course Summary prompts ───────────────────────────────────────────────────

    [Fact]
    public void CourseSummarySystemPrompt_RestrictsToProvidedDataOnly_AndAsksPerSprintCategories()
    {
        var prompt = MetricsController.BuildCourseSummarySystemPrompt();

        Assert.Contains("EXCLUSIVELY", prompt);
        Assert.Contains("PER SPRINT", prompt);
        Assert.Contains("\"Sprint N\"", prompt);
        Assert.Contains("progression", prompt);
    }

    [Fact]
    public void CourseSummaryUserPrompt_IncludesEachSprintSection_InOrder()
    {
        var sources = new List<(int, string)>
        {
            (1, "Sprint 1 was decent. Final Score: 60"),
            (2, "Sprint 2 improved. Final Score: 75"),
        };

        var prompt = MetricsController.BuildCourseSummaryUserPrompt(sources);

        var s1 = prompt.IndexOf("### Sprint 1", StringComparison.Ordinal);
        var s2 = prompt.IndexOf("### Sprint 2", StringComparison.Ordinal);
        Assert.True(s1 >= 0 && s2 > s1);
        Assert.Contains("Final Score: 60", prompt);
        Assert.Contains("Final Score: 75", prompt);
        Assert.Contains("ONLY data", prompt);
    }

    // ── Report mapping of summary rows ───────────────────────────────────────────

    private static CacheMetrics Row(int studentId, int sprint, int metricId, string content = "content", Metric? metric = null) =>
        new()
        {
            BoardId = "board-1",
            StudentId = studentId,
            SprintNumber = sprint,
            MetricId = metricId,
            ReviewContent = content,
            Metric = metric,
            Student = new Student { Id = studentId, FirstName = "Ran", LastName = "Brook" },
        };

    [Fact]
    public void MapMetricDto_NamesSummaryRow_SprintSummary()
    {
        // Even though the sentinel Metrics row is named "SprintSummary" (no space), the report shows
        // the friendly display name.
        var dto = MetricsController.MapMetricDto(Row(65, 1, 0, metric: new Metric { Id = 0, Name = "SprintSummary" }));

        Assert.Equal("Sprint Summary", dto.MetricName);
        Assert.Equal(0, dto.MetricId);
    }

    [Fact]
    public void BuildAssessmentSprints_ExcludesCourseSummaryRows()
    {
        var rows = new List<CacheMetrics>
        {
            Row(65, 1, 7, "engagement review"),
            Row(65, -1, 0, "course summary"),
        };

        var sprints = MetricsController.BuildAssessmentSprints(rows);

        Assert.Single(sprints);
        Assert.Equal(1, sprints[0].SprintNumber);
        Assert.DoesNotContain(sprints, s => s.SprintNumber == -1);
    }

    [Fact]
    public void BuildAssessmentSprints_SummaryRowSortsFirst_WithinStudent()
    {
        var rows = new List<CacheMetrics>
        {
            Row(65, 1, 113, "crf review"),
            Row(65, 1, 0, "sprint summary"),
            Row(65, 1, 7, "engagement review"),
        };

        var sprints = MetricsController.BuildAssessmentSprints(rows);
        var metrics = sprints[0].Students[0].Metrics;

        Assert.Equal(3, metrics.Count);
        Assert.Equal(0, metrics[0].MetricId); // summary first (MetricId ascending)
    }

    [Fact]
    public void BuildCourseSummaries_ReturnsOnlyCourseRows_WithStudentName()
    {
        var rows = new List<CacheMetrics>
        {
            Row(65, 1, 7, "engagement review"),
            Row(65, 1, 0, "sprint summary"),
            Row(65, -1, 0, "the course summary text"),
        };

        var summaries = MetricsController.BuildCourseSummaries(rows);

        var s = Assert.Single(summaries);
        Assert.Equal(65, s.StudentId);
        Assert.Equal("Ran Brook", s.StudentName);
        Assert.Equal("the course summary text", s.ReviewContent);
    }

    [Fact]
    public void BuildCourseSummaries_EmptyWhenNoCourseRows()
    {
        var rows = new List<CacheMetrics> { Row(65, 1, 7), Row(65, 1, 0) };

        Assert.Empty(MetricsController.BuildCourseSummaries(rows));
    }
}
