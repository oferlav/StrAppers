using System.Text;
using strAppersBackend.Controllers;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the delivery-state vocabulary the Codebase &amp; GitHub sensor emits. Each state maps to a
/// different score, so the important guarantee is that "the platform could not observe the
/// repository" is never phrased as "the student delivered nothing".
/// </summary>
public class GitHubSensorDeliveryStateTests
{
    private static MetricsController.GitHubTrackEvidence Evidence(
        MetricsController.GitHubEvidenceStatus status = MetricsController.GitHubEvidenceStatus.Observed,
        bool branchExists = true,
        int commits = 0,
        int prs = 0,
        bool merged = false)
    {
        var e = new MetricsController.GitHubTrackEvidence
        {
            IsBackend = true,
            Branch = "1-B",
            Status = status,
            BranchExists = branchExists,
            CommitCount = commits,
            PullRequestCount = prs,
        };
        if (prs > 0)
        {
            e.PullRequests.Add(new MetricsController.GitHubPullRequestSummary
            {
                Number = 1, State = merged ? "closed" : "open", Merged = merged, Title = "Sprint 1",
            });
        }
        return e;
    }

    /// <summary>
    /// The statuses that mean the platform failed to read the repository. Kept as a [Fact] loop
    /// rather than a [Theory] because the status enum is internal and cannot appear in the public
    /// signature of a test method.
    /// </summary>
    [Fact]
    public void UnobservableTracks_SayCouldNotObserve_AndForbidScoring()
    {
        var unobservable = new[]
        {
            MetricsController.GitHubEvidenceStatus.NoToken,
            MetricsController.GitHubEvidenceStatus.NoRepositoryUrl,
            MetricsController.GitHubEvidenceStatus.InvalidRepositoryUrl,
            MetricsController.GitHubEvidenceStatus.ApiError,
        };

        foreach (var status in unobservable)
        {
            var text = MetricsController.DescribeGitHubDeliveryState(Evidence(status), sprintWindowOpen: false);

            Assert.Contains("Could not observe", text, StringComparison.Ordinal);
            Assert.Contains("Do not score", text, StringComparison.OrdinalIgnoreCase);
            // Must never be confusable with the student having delivered nothing.
            Assert.DoesNotContain("Nothing delivered", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MergedPullRequest_ReadsAsDelivered()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(commits: 4, prs: 1, merged: true), sprintWindowOpen: false);

        Assert.Contains("Delivered", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPullRequest_AfterSprintClosed_ReadsAsNotMerged()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(commits: 3, prs: 1), sprintWindowOpen: false);

        Assert.Contains("not merged", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenPullRequest_WhileSprintOpen_IsNotTreatedAsFailure()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(commits: 3, prs: 1), sprintWindowOpen: true);

        Assert.Contains("still in progress", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a failure to deliver", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommitsWithoutPullRequest_AfterSprintClosed_ReadsAsNeverSubmitted()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(commits: 5), sprintWindowOpen: false);

        Assert.Contains("Never submitted for review", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoBranchAndNoPullRequest_ReadsAsNothingDelivered()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(branchExists: false), sprintWindowOpen: false);

        Assert.Contains("Nothing delivered", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyBranch_ReadsAsNothingDelivered_ButMentionsTheBranchExists()
    {
        var text = MetricsController.DescribeGitHubDeliveryState(
            Evidence(branchExists: true), sprintWindowOpen: false);

        Assert.Contains("Nothing delivered", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exists", text, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ summary line

    [Fact]
    public void SummaryLine_AggregatesObservedTracksOnly()
    {
        var backend = Evidence(commits: 4, prs: 1, merged: true);
        backend.TotalAdditions = 120;
        backend.TotalDeletions = 30;
        backend.Files.Add(new GitHubFileChange { FilePath = "a.cs", Additions = 120, Deletions = 30 });

        var frontend = Evidence(MetricsController.GitHubEvidenceStatus.NoRepositoryUrl);

        var sb = new StringBuilder();
        MetricsController.AppendGitHubSummaryLine(sb, new[] { backend, frontend });
        var text = sb.ToString();

        Assert.Contains("1 repository track(s)", text, StringComparison.Ordinal);
        Assert.Contains("4 commit(s)", text, StringComparison.Ordinal);
        Assert.Contains("merged: yes", text, StringComparison.Ordinal);
        Assert.Contains("+120/-30", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SummaryLine_WhenNothingObservable_SaysSo()
    {
        var sb = new StringBuilder();
        MetricsController.AppendGitHubSummaryLine(sb, new[]
        {
            Evidence(MetricsController.GitHubEvidenceStatus.NoRepositoryUrl),
            Evidence(MetricsController.GitHubEvidenceStatus.ApiError),
        });

        Assert.Contains("no repository could be observed", sb.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ scoring rules

    [Fact]
    public void ScoringRules_CoverTheSquashMergeAndUnobservableCases()
    {
        var rules = MetricsController.BuildGitHubScoringRules();

        Assert.Contains("squash merge", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Could not observe", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sprint window is still open", rules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Generated files", rules, StringComparison.OrdinalIgnoreCase);
    }
}
