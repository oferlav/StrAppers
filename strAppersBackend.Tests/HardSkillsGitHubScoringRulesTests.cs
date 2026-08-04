using strAppersBackend.Controllers;

namespace strAppersBackend.Tests;

/// <summary>
/// The Code &amp; GitHub scoring rules must reach the Professional Skills prompt, not only the generic
/// assessment engine's.
///
/// This is where they matter most: when the role's Main Tool is GitHub, BuildHardSkillsMetric turns
/// every other sensor off, so the entire score rests on the GitHub section — including its two states
/// a model cannot interpret unaided (a platform failure to observe the repository, and the empty
/// branch compare a squash merge normally produces).
/// </summary>
public class HardSkillsGitHubScoringRulesTests
{
    [Fact]
    public void GitHubMainTool_EnablesTheCodebaseSensor()
    {
        var metric = MetricsController.BuildHardSkillsMetric("Category: Code quality", "github");

        Assert.True(metric.UseCodebaseQuality);
    }

    [Theory]
    [InlineData("trello")]
    [InlineData("figma")]
    [InlineData("crm")]
    public void OtherMainTools_LeaveTheCodebaseSensorOff(string mainTool)
    {
        var metric = MetricsController.BuildHardSkillsMetric("Category: Delivery", mainTool);

        Assert.False(metric.UseCodebaseQuality);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Other")]
    [InlineData("something-unrecognised")]
    public void UnsetOrUnknownMainTool_LeavesEverySensorOn(string? mainTool)
    {
        var metric = MetricsController.BuildHardSkillsMetric("Category: Delivery", mainTool);

        Assert.True(metric.UseCodebaseQuality);
    }

    /// <summary>
    /// The rules are only appended when the sensor is on. A Figma or CRM role must not carry
    /// instructions about pull requests and squash merges it has no evidence for.
    /// </summary>
    [Fact]
    public void RulesAreAppended_OnlyWhenTheSensorIsEnabled()
    {
        var withSensor = MetricsController.BuildHardSkillsMetric("Category: Code quality", "github");
        var withoutSensor = MetricsController.BuildHardSkillsMetric("Category: Design", "figma");

        var appliedWithSensor = withSensor.UseCodebaseQuality
            ? MetricsController.BuildGitHubScoringRules() : string.Empty;
        var appliedWithoutSensor = withoutSensor.UseCodebaseQuality
            ? MetricsController.BuildGitHubScoringRules() : string.Empty;

        Assert.NotEmpty(appliedWithSensor);
        Assert.Empty(appliedWithoutSensor);
    }

    /// <summary>
    /// Collapses line breaks and indentation so the assertions below test the instruction's meaning
    /// rather than where the prompt text happens to wrap.
    /// </summary>
    private static string Flatten(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

    /// <summary>
    /// The specific instructions that stop an unfair zero: a merged PR with an empty compare is
    /// delivered work, and an unobservable repository is not the student's fault.
    /// </summary>
    [Fact]
    public void RulesCoverTheCasesThatCauseUnfairZeros()
    {
        var rules = Flatten(MetricsController.BuildGitHubScoringRules());

        Assert.Contains("squash merge", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT evidence of missing work", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Could not observe", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a student outcome", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sprint window is still open", rules, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The block must open with a line break so it appends cleanly to the end of a prompt's scoring
    /// rules instead of running onto the previous bullet.
    /// </summary>
    [Fact]
    public void RulesBeginOnTheirOwnLine()
    {
        var rules = MetricsController.BuildGitHubScoringRules();

        Assert.StartsWith("\n", rules.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.StartsWith("Rules for the", rules.TrimStart('\r', '\n', ' '), StringComparison.Ordinal);
    }
}
