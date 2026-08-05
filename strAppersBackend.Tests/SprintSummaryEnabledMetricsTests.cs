using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// The Sprint Summary must only roll up metrics the institute currently has enabled.
///
/// CacheMetrics rows outlive configuration changes: a metric assessed while it was switched on keeps
/// its cached row afterwards. Because the summary is a scored roll-up rather than a display, those
/// stale rows kept influencing the score — built-in metrics disabled for the institute were still
/// appearing in the summary.
/// </summary>
public class SprintSummaryEnabledMetricsTests
{
    private static CacheMetrics Row(int metricId, string name) =>
        new()
        {
            MetricId = metricId,
            BoardId = "b1",
            StudentId = 65,
            SprintNumber = 5,
            ReviewContent = $"## {name} — score 70",
            Metric = new Metric { Id = metricId, Name = name },
        };

    [Fact]
    public void DisabledMetricRows_AreDropped()
    {
        var rows = new List<CacheMetrics> { Row(2, "Gap Analysis"), Row(5, "Adherence"), Row(9, "Custom") };

        var kept = MetricsController.FilterToEnabledMetrics(rows, new[] { 9 });

        Assert.Single(kept);
        Assert.Equal(9, kept[0].MetricId);
    }

    [Fact]
    public void EnabledMetricRows_AreKeptInOrder()
    {
        var rows = new List<CacheMetrics> { Row(2, "Gap Analysis"), Row(5, "Adherence"), Row(9, "Custom") };

        var kept = MetricsController.FilterToEnabledMetrics(rows, new[] { 2, 9 });

        Assert.Equal(new[] { 2, 9 }, kept.Select(r => r.MetricId));
    }

    /// <summary>
    /// An institute with everything switched off must summarise nothing rather than everything — the
    /// caller turns this into an explanatory message instead of an empty-data one.
    /// </summary>
    [Fact]
    public void NoEnabledMetrics_DropsEverything()
    {
        var rows = new List<CacheMetrics> { Row(2, "Gap Analysis"), Row(5, "Adherence") };

        Assert.Empty(MetricsController.FilterToEnabledMetrics(rows, Array.Empty<int>()));
    }

    [Fact]
    public void EnabledMetricsWithNoCachedRows_ChangeNothing()
    {
        var rows = new List<CacheMetrics> { Row(2, "Gap Analysis") };

        var kept = MetricsController.FilterToEnabledMetrics(rows, new[] { 2, 3, 4 });

        Assert.Single(kept);
    }

    /// <summary>
    /// Built-in metrics are ordinary rows dispatched by name, so nothing about them exempts them from
    /// the filter — which is exactly the case that was reported.
    /// </summary>
    [Theory]
    [InlineData(2, "Gap Analysis")]
    [InlineData(3, "Adherence")]
    [InlineData(4, "Attendance")]
    [InlineData(6, "CustomerEngagement")]
    [InlineData(7, "Communication")]
    public void BuiltInMetrics_AreFilteredLikeAnyOther(int metricId, string name)
    {
        var kept = MetricsController.FilterToEnabledMetrics(
            new List<CacheMetrics> { Row(metricId, name) }, new[] { 999 });

        Assert.Empty(kept);
    }
}
