using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// The staff assessment report must show only metrics the institute currently has enabled.
///
/// CacheMetrics rows outlive configuration changes, so a metric assessed while it was switched on
/// kept appearing in the report afterwards. Filtering is display-only — nothing is deleted, and the
/// rows reappear the moment the metric is enabled again.
/// </summary>
public class AssessmentReportEnabledMetricsTests
{
    private static CacheMetrics Row(int metricId, int? instituteId, int studentId = 65) =>
        new()
        {
            MetricId = metricId,
            BoardId = "b1",
            StudentId = studentId,
            SprintNumber = 5,
            ReviewContent = "…",
            Student = new Student { Id = studentId, InstituteId = instituteId },
        };

    private static Dictionary<int, HashSet<int>> Enabled(params (int InstituteId, int[] MetricIds)[] entries) =>
        entries.ToDictionary(e => e.InstituteId, e => e.MetricIds.ToHashSet());

    [Fact]
    public void RowsForDisabledMetrics_AreHidden()
    {
        var rows = new List<CacheMetrics> { Row(2, 7), Row(5, 7), Row(9, 7) };

        var kept = MetricsController.FilterReportRowsToEnabledMetrics(rows, Enabled((7, new[] { 9 })));

        Assert.Single(kept);
        Assert.Equal(9, kept[0].MetricId);
    }

    /// <summary>
    /// Sprint Summary (0) and Professional Skills (-1) are sentinels, not institute-owned Metrics
    /// rows — they have no enabled flag and must never be filtered out.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SentinelRows_AreAlwaysKept(int metricId)
    {
        var kept = MetricsController.FilterReportRowsToEnabledMetrics(
            new List<CacheMetrics> { Row(metricId, 7) }, Enabled((7, Array.Empty<int>())));

        Assert.Single(kept);
    }

    [Fact]
    public void EachStudentIsJudgedAgainstTheirOwnInstitute()
    {
        // One report can span squads from different institutes with different metrics enabled.
        var rows = new List<CacheMetrics>
        {
            Row(2, 7, studentId: 1),   // enabled for institute 7
            Row(2, 8, studentId: 2),   // not enabled for institute 8
            Row(3, 8, studentId: 3),   // enabled for institute 8
        };

        var kept = MetricsController.FilterReportRowsToEnabledMetrics(
            rows, Enabled((7, new[] { 2 }), (8, new[] { 3 })));

        Assert.Equal(new[] { 1, 3 }, kept.Select(r => r.StudentId));
    }

    [Fact]
    public void StudentsWithNoInstitute_AreLeftAlone()
    {
        // Enablement cannot be determined, so hiding the row would lose data for no reason.
        var kept = MetricsController.FilterReportRowsToEnabledMetrics(
            new List<CacheMetrics> { Row(2, null), Row(3, 0) }, Enabled((7, new[] { 9 })));

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void AnInstituteWithNoEnabledMetrics_KeepsOnlyItsSentinels()
    {
        var rows = new List<CacheMetrics> { Row(0, 7), Row(-1, 7), Row(2, 7), Row(3, 7) };

        var kept = MetricsController.FilterReportRowsToEnabledMetrics(rows, Enabled((7, Array.Empty<int>())));

        Assert.Equal(new[] { 0, -1 }, kept.Select(r => r.MetricId));
    }

    [Fact]
    public void AnInstituteMissingFromTheLookup_HasItsMetricRowsHidden()
    {
        // No enabled-metrics entry at all means nothing is enabled, not "everything is".
        var kept = MetricsController.FilterReportRowsToEnabledMetrics(
            new List<CacheMetrics> { Row(2, 99) }, Enabled((7, new[] { 2 })));

        Assert.Empty(kept);
    }

    [Fact]
    public void NothingIsMutated_OnlyTheReturnedListIsFiltered()
    {
        var rows = new List<CacheMetrics> { Row(2, 7), Row(9, 7) };

        MetricsController.FilterReportRowsToEnabledMetrics(rows, Enabled((7, new[] { 9 })));

        Assert.Equal(2, rows.Count);
    }
}
