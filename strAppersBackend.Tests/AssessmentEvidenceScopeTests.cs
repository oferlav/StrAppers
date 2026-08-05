using System.Text;
using strAppersBackend.Controllers;
using strAppersBackend.Models;

namespace strAppersBackend.Tests;

/// <summary>
/// A sensor switched off must never be read as work the student failed to do.
///
/// Disabled sensors emit no section at all, so the model cannot distinguish "not examined" from "did
/// not happen" — and it assumes the latter. A live assessment with User Stories, Meeting Transcripts
/// and Group Chat disabled reported that the user story was not filled out, no meetings were held and
/// no sprint summary was published, and scored a "substantial delivery shortfall" for a PM on no
/// evidence at all. The evidence-scope header exists to make that inference impossible.
/// </summary>
public class AssessmentEvidenceScopeTests
{
    private static Metric MetricWith(Action<Metric> configure)
    {
        var metric = new Metric { Id = 1, Name = "Delivery" };
        configure(metric);
        return metric;
    }

    private static string Render(Metric metric)
    {
        var sb = new StringBuilder();
        MetricsController.AppendEvidenceScopeHeader(sb, metric);
        return sb.ToString();
    }

    /// <summary>The exact configuration from the live failure.</summary>
    private static Metric PmMetric() => MetricWith(m =>
    {
        m.UseTrelloUserStory = false;
        m.UseMeetingTranscripts = false;
        m.UseGroupChat = false;
    });

    [Fact]
    public void DisabledSensorsAreNamed_SoSilenceIsNotMistakenForFailure()
    {
        var text = Render(PmMetric());

        Assert.Contains("Sources NOT examined", text, StringComparison.Ordinal);
        Assert.Contains("User Stories", text, StringComparison.Ordinal);
        Assert.Contains("Meeting Transcripts", text, StringComparison.Ordinal);
        Assert.Contains("Group Chat (Squad)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EnabledSensorsAreListedAsTheOnlyBasisForJudgement()
    {
        var text = Render(PmMetric());

        Assert.Contains("Sources examined:", text, StringComparison.Ordinal);
        Assert.Contains("AI Mentor Chat", text, StringComparison.Ordinal);
        Assert.Contains("only on the sources listed as examined", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheInstructionForbidsReportingUnexaminedWorkAsMissing()
    {
        var text = System.Text.RegularExpressions.Regex.Replace(Render(PmMetric()), @"\s+", " ");

        Assert.Contains("is NOT evidence that the related work was skipped", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not lower any score because of it", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not create a score category for it", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnabledAndDisabledSensorsNeverOverlap()
    {
        var metric = PmMetric();

        var enabled = MetricsController.DescribeSensors(metric);
        var disabled = MetricsController.DescribeDisabledSensors(metric);

        Assert.Empty(enabled.Intersect(disabled));
        Assert.Equal(12, enabled.Count + disabled.Count);
        Assert.Equal(3, disabled.Count);
    }

    [Fact]
    public void AllSensorsOn_AddsNoWarning()
    {
        // Every flag defaults to true, so a metric that narrows nothing must not carry the caveat.
        var text = Render(MetricWith(_ => { }));

        Assert.Contains("Sources examined:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT examined", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AllSensorsOff_StillStatesTheScopeHonestly()
    {
        var text = Render(MetricWith(m =>
        {
            m.UseCustomerChat = m.UseMentorChat = m.UseCodebaseQuality = m.UseResources =
                m.UseStakeholders = m.UseProjectModule = m.UseMeetingTranscripts = m.UseGroupChat =
                    m.UsePrivateChat = m.UseTrelloTasks = m.UseTrelloUserStory = m.UseFigmaDesign = false;
        }));

        Assert.Contains("Sources examined: (none)", text, StringComparison.Ordinal);
        Assert.Contains("NOT examined", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Professional Skills path narrows sensors hard — GitHub Main Tool leaves only two on — so it
    /// is the most exposed to this failure and must carry the same header.
    /// </summary>
    [Fact]
    public void HardSkillsGitHubMetric_NamesTheNineSourcesItDoesNotExamine()
    {
        var metric = MetricsController.BuildHardSkillsMetric("Category: Code quality", "github");

        var disabled = MetricsController.DescribeDisabledSensors(metric);
        var text = Render(metric);

        Assert.Equal(10, disabled.Count);
        Assert.Contains("User Stories", text, StringComparison.Ordinal);
        Assert.Contains("Meeting Transcripts", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sources examined: (none)", text, StringComparison.Ordinal);
    }
}
