using System.Text;
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
/// Tests the CI Status sub-block of the Codebase &amp; GitHub sensor.
///
/// This is what the sprint-number capture fix unblocked: before it, only CI test rows carried a
/// SprintNumber, so code reviews, PR validations, merges and pushes were invisible to any per-sprint
/// query. These tests pin the two behaviours that were broken — the sprint filter and reading the
/// branch from the column the writers actually populate (GithubBranch, not BranchName).
/// </summary>
public class GitHubSensorCiStatusTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetricsController BuildController(ApplicationDbContext db) =>
        new(db,
            Mock.Of<ITrelloService>(),
            Mock.Of<IGitHubService>(),
            new ConfigurationBuilder().Build(),
            NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()),
            Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(),
            Mock.Of<IAzureBlobStorageService>());

    private static BoardState Row(
        int id, string boardId, string source, string? branch, int? sprint,
        string? build = null, string? test = null) =>
        new()
        {
            Id = id,
            BoardId = boardId,
            Source = source,
            GithubBranch = branch,
            SprintNumber = sprint,
            LastBuildStatus = build,
            LastTestStatus = test,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static async Task<string> RenderAsync(
        ApplicationDbContext db, string boardId, int sprint, params string[] branches)
    {
        var sb = new StringBuilder();
        await BuildController(db).AppendGitHubCiStatusAsync(sb, boardId, sprint, branches, CancellationToken.None);
        return sb.ToString();
    }

    [Fact]
    public async Task RowsFromTheRequestedSprint_AreIncluded()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "TestRunner", "3-B", 3, test: "PASS"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B");

        Assert.Contains("Tests: PASS", text, StringComparison.Ordinal);
        Assert.Contains("3-B", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowsFromOtherSprints_AreExcluded()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "TestRunner", "2-B", 2, test: "FAIL"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B");

        Assert.DoesNotContain("FAIL", text, StringComparison.Ordinal);
        Assert.Contains("no build or test results recorded", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PR-validation rows now carry a SprintNumber written from the branch name. Before the capture
    /// fix these were NULL and never reached the assessment prompt.
    /// </summary>
    [Fact]
    public async Task PrValidationRows_NowSurfaceForTheirSprint()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "PR-BackendValidation", "4-B", 4, build: "SUCCESS"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 4, "4-B");

        Assert.Contains("PR-BackendValidation", text, StringComparison.Ordinal);
        Assert.Contains("Build: SUCCESS", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The old renderer read BranchName, a column only the branch-creation action populates, so every
    /// other row rendered "Branch: —". The branch must come from GithubBranch.
    /// </summary>
    [Fact]
    public async Task BranchIsReadFromGithubBranchColumn()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "TestRunner", "5-F", 5, test: "PASS"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 5, "5-F");

        Assert.Contains("Branch: 5-F", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Branch: —", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowsForAnotherStudentsBranch_AreScopedOut()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "TestRunner", "3-B-1", 3, test: "PASS"));
        db.BoardStates.Add(Row(2, "b1", "TestRunner", "3-B-2", 3, test: "FAIL"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B-1");

        Assert.Contains("3-B-1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("3-B-2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FAIL", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowsWithNoBuildOrTestStatus_AreNotListed()
    {
        var db = CreateDb();
        // A merge row carries a sprint and branch but no CI outcome — it belongs in the GitHub
        // metadata block, not in CI Status.
        db.BoardStates.Add(Row(1, "b1", "GitHub-Merge", "3-B", 3));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B");

        Assert.Contains("no build or test results recorded", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OtherBoards_AreNeverIncluded()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "other-board", "TestRunner", "3-B", 3, test: "PASS"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B");

        Assert.Contains("no build or test results recorded", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When no row matches the resolved branches, the sprint's rows are still shown rather than
    /// dropping CI evidence entirely — branch naming can legitimately differ (e.g. BranchContext).
    /// </summary>
    [Fact]
    public async Task WhenNoBranchMatches_SprintRowsAreStillShown()
    {
        var db = CreateDb();
        db.BoardStates.Add(Row(1, "b1", "TestRunner", "Bugs-B", 3, test: "PASS"));
        await db.SaveChangesAsync();

        var text = await RenderAsync(db, "b1", 3, "3-B");

        Assert.Contains("Tests: PASS", text, StringComparison.Ordinal);
    }
}
