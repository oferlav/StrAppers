using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using strAppersBackend.Controllers;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Regression tests for the phantom-sprint assessment bug (report showed Sprint 2 on a
/// one-sprint board) and the squash-merge 0% gap-analysis bug (merged PR scored as
/// "no commits" because compare main...head is empty after a squash merge).
/// </summary>
public class PhantomSprintAndMergedPrEvidenceTests
{
    // ── GetRealMergedSprintNumbers: placeholder rows must not count as sprints ──

    private static ProjectBoard BoardWithMerges(params ProjectBoardSprintMerge[] merges) =>
        new ProjectBoard { Id = "board1", SprintMerges = merges.ToList() };

    [Fact]
    public void RealMergedSprints_ExcludesPreCreatedNextSprintPlaceholder()
    {
        // MergeType=Add board with 1 sprint: board creation seeds sprint 1 (merged, has list)
        // plus the sprint 2 trigger row (MergedAt=null, ListId=null). Sprint 2 must not appear.
        var pb = BoardWithMerges(
            new ProjectBoardSprintMerge { SprintNumber = 1, MergedAt = DateTime.UtcNow, ListId = "list1" },
            new ProjectBoardSprintMerge { SprintNumber = 2, MergedAt = null, ListId = null });

        Assert.Equal(new List<int> { 1 }, BoardsController.GetRealMergedSprintNumbers(pb));
    }

    [Fact]
    public void RealMergedSprints_IncludesUnmergedRowWithList()
    {
        // Merge-mode boards seed rows for lists that already exist on the board with
        // MergedAt=null but a real ListId — those sprints exist and must be included.
        var pb = BoardWithMerges(
            new ProjectBoardSprintMerge { SprintNumber = 1, MergedAt = null, ListId = "list1" },
            new ProjectBoardSprintMerge { SprintNumber = 2, MergedAt = null, ListId = null });

        Assert.Equal(new List<int> { 1 }, BoardsController.GetRealMergedSprintNumbers(pb));
    }

    [Fact]
    public void RealMergedSprints_LastSprintWithNoPlaceholder_IsKept()
    {
        // Regression guard for commit 0c9edef: when the course ends and no next-sprint row is
        // pre-created, the highest real sprint must not be dropped (old SkipLast(1) bug).
        var merges = Enumerable.Range(1, 8)
            .Select(n => new ProjectBoardSprintMerge { SprintNumber = n, MergedAt = DateTime.UtcNow, ListId = $"l{n}" })
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 8).ToList(), BoardsController.GetRealMergedSprintNumbers(BoardWithMerges(merges)));
    }

    [Fact]
    public void RealMergedSprints_EmptyAndDuplicates()
    {
        Assert.Empty(BoardsController.GetRealMergedSprintNumbers(BoardWithMerges()));

        var pb = BoardWithMerges(
            new ProjectBoardSprintMerge { SprintNumber = 2, MergedAt = DateTime.UtcNow, ListId = "a" },
            new ProjectBoardSprintMerge { SprintNumber = 2, MergedAt = DateTime.UtcNow, ListId = "b" },
            new ProjectBoardSprintMerge { SprintNumber = 1, MergedAt = DateTime.UtcNow, ListId = "c" });
        Assert.Equal(new List<int> { 1, 2 }, BoardsController.GetRealMergedSprintNumbers(pb));
    }

    // ── run-student-sprint guard: same "sprint exists" predicate, sprint 0 exempt ──

    private static bool SprintExistsForRun(List<ProjectBoardSprintMerge> rows, string boardId, int sprintNumber) =>
        sprintNumber < 1 || rows.Any(m => m.ProjectBoardId == boardId && m.SprintNumber == sprintNumber &&
                                          (m.MergedAt != null || m.ListId != null));

    [Fact]
    public void RunStudentSprintGuard_RejectsPlaceholderSprint_AllowsSprintZero()
    {
        var rows = new List<ProjectBoardSprintMerge>
        {
            new() { ProjectBoardId = "b1", SprintNumber = 1, MergedAt = DateTime.UtcNow, ListId = "l1" },
            new() { ProjectBoardId = "b1", SprintNumber = 2, MergedAt = null, ListId = null },
        };

        Assert.True(SprintExistsForRun(rows, "b1", 1));
        Assert.False(SprintExistsForRun(rows, "b1", 2)); // placeholder → phantom, must be rejected
        Assert.False(SprintExistsForRun(rows, "b1", 3)); // no row at all
        Assert.True(SprintExistsForRun(rows, "b1", 0));  // Bugs sprint has no merge row by design
    }

    // ── GitHubService.GetPullRequestFilesAsync: merged-PR delivery evidence ──

    private static GitHubService MakeGitHubService(MockHttpMessageHandler handler)
    {
        var env = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GitHub:AccessToken"] = "test-token" }).Build();
        return new GitHubService(new HttpClient(handler), NullLogger<GitHubService>.Instance, config, env.Object);
    }

    [Fact]
    public async Task GetPullRequestFiles_ParsesFilesAndTotals()
    {
        const string json = """
        [
          {"filename":"README.md","status":"modified","additions":2,"deletions":1,"patch":"@@ -1 +1,2 @@\n-old\n+new\n+test line"},
          {"filename":"src/app.cs","status":"added","additions":10,"deletions":0,"patch":"@@ +10 lines"}
        ]
        """;
        var handler = MockHttpMessageHandler.ReturnOk(json);
        var svc = MakeGitHubService(handler);

        var diff = await svc.GetPullRequestFilesAsync("owner", "repo", 1);

        Assert.NotNull(diff);
        Assert.Equal(2, diff!.TotalFilesChanged);
        Assert.Equal(12, diff.TotalAdditions);
        Assert.Equal(1, diff.TotalDeletions);
        Assert.Equal("README.md", diff.FileChanges[0].FilePath);
        Assert.Contains("test line", diff.FileChanges[0].Patch);
        Assert.Contains("/repos/owner/repo/pulls/1/files", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetPullRequestFiles_ApiError_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"message\":\"Not Found\"}", Encoding.UTF8, "application/json")
        });
        var svc = MakeGitHubService(handler);

        Assert.Null(await svc.GetPullRequestFilesAsync("owner", "repo", 1));
    }

    [Fact]
    public async Task GetPullRequestFiles_InvalidParameters_ReturnsNull()
    {
        var svc = MakeGitHubService(MockHttpMessageHandler.ReturnOk("[]"));
        Assert.Null(await svc.GetPullRequestFilesAsync("", "repo", 1));
        Assert.Null(await svc.GetPullRequestFilesAsync("owner", "repo", 0));
    }

    // ── TrelloService sprint-card lookup: 429 must be retried, not swallowed ──

    /// <summary>Returns queued responses in order; repeats the last one when exhausted.</summary>
    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public int CallCount { get; private set; }

        public SequenceHttpMessageHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responses.Count > 1 ? _responses.Dequeue() : _responses.Peek());
        }
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetSprintRoleCardId_RetriesRateLimitThenSucceeds()
    {
        var rateLimited = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("rate limited")
        };
        rateLimited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));

        var handler = new SequenceHttpMessageHandler(
            rateLimited,                                                          // lists → 429 (must retry)
            Ok("""[{"id":"L1","name":"Sprint 1"}]"""),                            // lists retry → OK
            Ok("""[{"id":"C1","idList":"L1","labels":[{"name":"Backend Developer"}]}]""")); // cards → OK

        var svc = new TrelloService(
            new HttpClient(handler),
            Options.Create(new TrelloConfig { ApiKey = "k", ApiToken = "t" }),
            NullLogger<TrelloService>.Instance);

        var cardId = await svc.GetSprintRoleCardIdAsync("board1", 1, "Backend Developer");

        Assert.Equal("C1", cardId);
        Assert.Equal(3, handler.CallCount); // 429 + retried lists + cards
    }
}
