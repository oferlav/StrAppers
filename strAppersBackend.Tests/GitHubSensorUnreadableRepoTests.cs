using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// A repository the platform could not read must never be reported as a student who delivered
/// nothing.
///
/// Every GitHub lookup the sensor uses returns null on failure, so a 401, a 403 and a rate-limit are
/// indistinguishable from an empty branch. Before concluding "nothing delivered" the sensor now
/// confirms the repository was actually readable, and downgrades to "could not observe" if it was not.
/// </summary>
public class GitHubSensorUnreadableRepoTests
{
    private static MetricsController BuildController(IGitHubService github) =>
        new(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            Mock.Of<ITrelloService>(),
            github,
            new ConfigurationBuilder().Build(),
            NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(),
            Mock.Of<IAzureBlobStorageService>());

    /// <summary>A repo where every lookup comes back empty, with a configurable repo-status probe.</summary>
    private static Mock<IGitHubService> SilentGitHub(int repositoryStatus)
    {
        var github = new Mock<IGitHubService>();
        github.Setup(g => g.GetPullRequestForGapAnalysisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((GitHubPullRequest?)null);
        github.Setup(g => g.CountPullRequestsForHeadBranchPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((0, 0));
        github.Setup(g => g.GetCompareDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((GitHubCommitDiff?)null);
        github.Setup(g => g.GetRecentPullRequestHeadsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<GitHubPullRequestHead>());
        github.Setup(g => g.GetRepositoryHttpStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(repositoryStatus);
        return github;
    }

    private static Task<MetricsController.GitHubTrackEvidence> FetchAsync(Mock<IGitHubService> github) =>
        BuildController(github.Object).FetchGitHubTrackEvidenceAsync(
            "https://github.com/acme/web", "5-B", "naming convention", isBackend: true,
            token: "t", options: new MetricsController.GitHubSensorOptions(), ct: CancellationToken.None);

    [Fact]
    public async Task Unauthorised_IsReportedAsCouldNotObserve_NotAsNothingDelivered()
    {
        var evidence = await FetchAsync(SilentGitHub((int)HttpStatusCode.Unauthorized));

        Assert.Equal(MetricsController.GitHubEvidenceStatus.ApiError, evidence.Status);

        var text = MetricsController.DescribeGitHubDeliveryState(evidence, sprintWindowOpen: false);
        Assert.Contains("Could not observe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing delivered", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimited_IsReportedAsCouldNotObserve()
    {
        var evidence = await FetchAsync(SilentGitHub((int)HttpStatusCode.Forbidden));

        Assert.Equal(MetricsController.GitHubEvidenceStatus.ApiError, evidence.Status);
    }

    [Fact]
    public async Task NetworkFailure_IsReportedAsCouldNotObserve()
    {
        // 0 is what the probe returns when the request could not be made at all.
        var evidence = await FetchAsync(SilentGitHub(0));

        Assert.Equal(MetricsController.GitHubEvidenceStatus.ApiError, evidence.Status);
    }

    [Fact]
    public async Task MissingRepository_IsReportedAsAConfigurationProblem()
    {
        var evidence = await FetchAsync(SilentGitHub((int)HttpStatusCode.NotFound));

        Assert.Equal(MetricsController.GitHubEvidenceStatus.InvalidRepositoryUrl, evidence.Status);

        var text = MetricsController.DescribeGitHubDeliveryState(evidence, sprintWindowOpen: false);
        Assert.Contains("Could not observe", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of the guarantee: a readable repository that genuinely holds nothing must still
    /// report "nothing delivered". The probe must not turn every empty sprint into "could not observe".
    /// </summary>
    [Fact]
    public async Task ReadableButEmptyRepository_StillReportsNothingDelivered()
    {
        var evidence = await FetchAsync(SilentGitHub((int)HttpStatusCode.OK));

        Assert.Equal(MetricsController.GitHubEvidenceStatus.Observed, evidence.Status);

        var text = MetricsController.DescribeGitHubDeliveryState(evidence, sprintWindowOpen: false);
        Assert.Contains("Nothing delivered", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The probe costs an API call, so it must only run when the sprint branch produced nothing.</summary>
    [Fact]
    public async Task ProbeIsNotCalled_WhenTheSprintBranchHadWork()
    {
        var github = SilentGitHub((int)HttpStatusCode.OK);
        github.Setup(g => g.GetCompareDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new GitHubCommitDiff
            {
                CommitsCount = 2,
                TotalFilesChanged = 1,
                FileChanges = { new GitHubFileChange { FilePath = "a.cs", Status = "added", Additions = 10, Patch = "@@ +1 @@" } },
            });

        var evidence = await FetchAsync(github);

        Assert.True(evidence.HasAnyDelivery);
        github.Verify(g => g.GetRepositoryHttpStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ------------------------------------------------------------------ QuestBoards per-track fallback

    [Fact]
    public void QuestBoardUrls_FillEachTrackIndependently()
    {
        // Board carries backend only; the frontend URL must still come from QuestBoards.
        var (backend, frontend) = MetricsController.MergeQuestBoardUrls(
            "https://github.com/acme/api", null, "https://github.com/quest/api", "https://github.com/quest/web");

        Assert.Equal("https://github.com/acme/api", backend);
        Assert.Equal("https://github.com/quest/web", frontend);
    }

    [Fact]
    public void BoardUrls_AlwaysWinOverQuestBoards()
    {
        var (backend, frontend) = MetricsController.MergeQuestBoardUrls(
            "https://github.com/acme/api", "https://github.com/acme/web",
            "https://github.com/quest/api", "https://github.com/quest/web");

        Assert.Equal("https://github.com/acme/api", backend);
        Assert.Equal("https://github.com/acme/web", frontend);
    }

    [Fact]
    public void BothMissing_BothComeFromQuestBoards()
    {
        var (backend, frontend) = MetricsController.MergeQuestBoardUrls(
            null, "", "https://github.com/quest/api", "https://github.com/quest/web");

        Assert.Equal("https://github.com/quest/api", backend);
        Assert.Equal("https://github.com/quest/web", frontend);
    }
}
