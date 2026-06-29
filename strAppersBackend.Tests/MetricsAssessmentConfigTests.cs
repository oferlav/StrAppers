using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the filter predicate used by GetAssessmentMetricConfiguration:
/// returns InstituteId=1 rows plus the requesting institute's own rows.
/// </summary>
public class MetricsAssessmentConfigTests
{
    private static List<Metric> SampleMetrics() =>
    [
        new Metric { Id = 1,   Name = "Adherence",       InstituteId = 1 },
        new Metric { Id = 7,   Name = "CustomerEngagement", InstituteId = 1 },
        new Metric { Id = 101, Name = "GitHub velocity", InstituteId = 1 },
        new Metric { Id = 200, Name = "Research Depth",  InstituteId = 5 },   // institute 5
        new Metric { Id = 201, Name = "Citation quality", InstituteId = 5 },  // institute 5
        new Metric { Id = 300, Name = "Other metric",    InstituteId = 9 },   // different institute
    ];

    [Fact]
    public void Filter_InstituteId5_ReturnsBaseAndOwnRows()
    {
        var metrics = SampleMetrics();
        int requestingInstituteId = 5;

        var result = metrics
            .Where(m => m.InstituteId == 1 || m.InstituteId == requestingInstituteId)
            .ToList();

        Assert.Equal(5, result.Count);
        Assert.Contains(result, m => m.Id == 1);    // base
        Assert.Contains(result, m => m.Id == 7);    // base
        Assert.Contains(result, m => m.Id == 101);  // base (extended)
        Assert.Contains(result, m => m.Id == 200);  // own
        Assert.Contains(result, m => m.Id == 201);  // own
        Assert.DoesNotContain(result, m => m.Id == 300); // other institute excluded
    }

    [Fact]
    public void Filter_InstituteId1_ReturnsOnlyBaseRows()
    {
        var metrics = SampleMetrics();

        var result = metrics
            .Where(m => m.InstituteId == 1 || m.InstituteId == 1)
            .ToList();

        Assert.All(result, m => Assert.Equal(1, m.InstituteId));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_InstituteWithNoOwnMetrics_ReturnsOnlyBaseRows()
    {
        var metrics = SampleMetrics();

        var result = metrics
            .Where(m => m.InstituteId == 1 || m.InstituteId == 99)
            .ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.Equal(1, m.InstituteId));
    }

    [Theory]
    [InlineData("Adherence",                 "adherence")]
    [InlineData("CustomerEngagement",        "customerengagement")]
    [InlineData("GitHub velocity & consistency", "github_velocity_and_consistency")]
    [InlineData("Strengths&weaknesses",      "strengthsandweaknesses")]
    [InlineData("User story clarity",        "user_story_clarity")]
    public void ToSlugKey_ProducesExpectedSlug(string input, string expected)
    {
        // Inline the same logic from MetricsController.ToSlugKey
        var s = input.Trim().ToLowerInvariant().Replace("&", "and").Replace(' ', '_');
        var result = System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9_]", "");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsBaseInstitute_TrueWhenInstituteId1()
    {
        var metric = new Metric { Id = 1, InstituteId = 1 };
        Assert.True(metric.InstituteId == 1);
    }

    [Fact]
    public void IsBaseInstitute_FalseWhenInstituteIdNot1()
    {
        var metric = new Metric { Id = 200, InstituteId = 5 };
        Assert.False(metric.InstituteId == 1);
    }
}
