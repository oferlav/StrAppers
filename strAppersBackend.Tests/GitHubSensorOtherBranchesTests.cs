using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// When the sensor finds nothing on the sprint branch it must say what the repository DOES contain.
///
/// A real board exposed the problem: every sprint-1 pull request had head "Bugs-F", the numbered
/// branches 1-B and 1-F did not exist, and the sensor reported "Nothing delivered" for both tracks —
/// which reads as an empty repository to anyone (or any model) scoring from it.
/// </summary>
public class GitHubSensorOtherBranchesTests
{
    private static MetricsController.GitHubTrackEvidence Evidence(bool delivered = false)
    {
        var e = new MetricsController.GitHubTrackEvidence { IsBackend = false, Branch = "1-F" };
        if (delivered)
        {
            e.CommitCount = 3;
            e.PullRequestCount = 1;
        }
        return e;
    }

    private static string Render(MetricsController.GitHubTrackEvidence e)
    {
        var sb = new StringBuilder();
        MetricsController.AppendGitHubOtherBranchesLine(sb, e);
        return sb.ToString();
    }

    [Fact]
    public void ListsTheBranchesTheRepositoryActuallyHas()
    {
        var e = Evidence();
        e.OtherBranchHeads.Add(new GitHubPullRequestHead { Number = 3, HeadRef = "Bugs-F", Merged = true });

        var text = Render(e);

        Assert.Contains("NOT empty", text, StringComparison.Ordinal);
        Assert.Contains("Bugs-F", text, StringComparison.Ordinal);
        Assert.Contains("PR #3", text, StringComparison.Ordinal);
        Assert.Contains("merged", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reporting other branches must not become a route to credit for work outside the sprint.
    /// </summary>
    [Fact]
    public void StatesExplicitlyThatThisWorkMustNotBeScored()
    {
        var e = Evidence();
        e.OtherBranchHeads.Add(new GitHubPullRequestHead { Number = 3, HeadRef = "Bugs-F", Merged = true });

        // Flattened so the assertions test the instruction, not where the sentence wraps.
        var text = System.Text.RegularExpressions.Regex.Replace(Render(e), @"\s+", " ");

        Assert.Contains("do not score it as sprint work", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("none of it is this student's sprint deliverable", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("None of these is this sprint's branch", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BugsBranches_CarryThePolicyExplanation()
    {
        var e = Evidence();
        e.OtherBranchHeads.Add(new GitHubPullRequestHead { Number = 3, HeadRef = "Bugs-F", Merged = true });

        var text = Render(e);

        Assert.Contains("bug-fix track", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correct and expected", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonBugsBranches_DoNotCarryTheBugsPolicyNote()
    {
        var e = Evidence();
        e.OtherBranchHeads.Add(new GitHubPullRequestHead { Number = 9, HeadRef = "feature/reports", Merged = false });

        var text = Render(e);

        Assert.Contains("feature/reports", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bug-fix track", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NothingIsAddedWhenTheSprintBranchHadWork()
    {
        var e = Evidence(delivered: true);
        e.OtherBranchHeads.Add(new GitHubPullRequestHead { Number = 3, HeadRef = "Bugs-F", Merged = true });

        Assert.Equal(string.Empty, Render(e));
    }

    [Fact]
    public void NothingIsAddedWhenTheRepositoryIsGenuinelyEmpty()
    {
        Assert.Equal(string.Empty, Render(Evidence()));
    }

    // ------------------------------------------------------------------ service lookup

    private static GitHubService BuildService(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHub:AccessToken"] = "t" })
            .Build();

        return new GitHubService(new HttpClient(handler), NullLogger<GitHubService>.Instance, config,
            Mock.Of<IWebHostEnvironment>());
    }

    [Fact]
    public async Task HeadLookup_ReadsNumberHeadRefAndMergedState()
    {
        const string json = """
            [{"number":3,"merged_at":"2026-03-02T10:00:00Z","head":{"ref":"Bugs-F"}},
             {"number":2,"merged_at":null,"head":{"ref":"feature/x"}}]
            """;

        var heads = await BuildService(json).GetRecentPullRequestHeadsAsync("acme", "web");

        Assert.Equal(2, heads.Count);
        Assert.Equal("Bugs-F", heads[0].HeadRef);
        Assert.True(heads[0].Merged);
        Assert.NotNull(heads[0].MergedAt);
        Assert.False(heads[1].Merged);
    }

    [Fact]
    public async Task HeadLookup_ReturnsEmptyOnFailureRatherThanThrowing()
    {
        var heads = await BuildService("{\"message\":\"Not Found\"}", HttpStatusCode.NotFound)
            .GetRecentPullRequestHeadsAsync("acme", "web");

        Assert.Empty(heads);
    }

    [Fact]
    public async Task HeadLookup_SkipsEntriesWithNoHeadRef()
    {
        const string json = """[{"number":1,"head":{}},{"number":2,"head":{"ref":"1-B"}}]""";

        var heads = await BuildService(json).GetRecentPullRequestHeadsAsync("acme", "web");

        Assert.Single(heads);
        Assert.Equal("1-B", heads[0].HeadRef);
    }
}
