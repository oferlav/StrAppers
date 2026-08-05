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
/// CI rows must say what they actually checked.
///
/// A live assessment was shown the bare line "PR-BackendValidation | Build: FAILED" and concluded the
/// student's code "may not compile or pass tests in CI" — making it the headline gap and marking two
/// categories down. The row in fact meant the board's hosting service and database were not
/// provisioned; the code built cleanly and its tests passed. Only the project's own test runner is
/// evidence about the student's code.
/// </summary>
public class GitHubSensorCiSourceLabellingTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MetricsController BuildController(ApplicationDbContext db) =>
        new(db, Mock.Of<ITrelloService>(), Mock.Of<IGitHubService>(),
            new ConfigurationBuilder().Build(), NullLogger<MetricsController>.Instance,
            Mock.Of<IChatCompletionService>(), Mock.Of<IHttpClientFactory>(),
            Options.Create(new PromptConfig()), Mock.Of<IMicrosoftGraphService>(),
            Mock.Of<ISmtpEmailService>(), Mock.Of<IAzureBlobStorageService>());

    private static BoardState Row(int id, string source, string? build = null, string? test = null) =>
        new()
        {
            Id = id, BoardId = "b1", Source = source, GithubBranch = "5-B", SprintNumber = 5,
            LastBuildStatus = build, LastTestStatus = test,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

    private static async Task<string> RenderAsync(params BoardState[] rows)
    {
        var db = CreateDb();
        db.BoardStates.AddRange(rows);
        await db.SaveChangesAsync();

        var sb = new StringBuilder();
        await BuildController(db).AppendGitHubCiStatusAsync(sb, "b1", 5, new[] { "5-B" }, CancellationToken.None);
        return sb.ToString();
    }

    // ------------------------------------------------------------------ source meanings

    [Fact]
    public void TestRunner_IsTheOnlySourceTreatedAsEvidenceAboutTheCode()
    {
        Assert.True(MetricsController.DescribeCiSource("TestRunner").IsCodeEvidence);

        foreach (var other in new[]
                 {
                     "PR-BackendValidation", "PR-FrontendValidation", "BuildValidation",
                     "GithubPages", "Railway", "GitHub-Merge", "GitHub-Success-PR-1", "Junior-2", "Unknown",
                 })
        {
            Assert.False(MetricsController.DescribeCiSource(other).IsCodeEvidence, other + " must not be code evidence");
        }
    }

    [Fact]
    public void ValidationSources_AreDescribedAsProvisioningChecks()
    {
        var meaning = MetricsController.DescribeCiSource("PR-BackendValidation").Meaning;

        Assert.Contains("deployment", meaning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provisioning", meaning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SequencedSources_AreRecognisedByPrefix()
    {
        // Code-review rows carry a numeric suffix, so exact matching would miss them.
        Assert.Contains("code review", MetricsController.DescribeCiSource("GitHub-Success-PR-7").Meaning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("code review", MetricsController.DescribeCiSource("Junior-3").Meaning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MergeMarker_IsNotPresentedAsABuildResult()
    {
        Assert.Contains("not a build", MetricsController.DescribeCiSource("GitHub-Merge").Meaning, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ rendering

    [Fact]
    public async Task EveryRowStatesWhatItChecked()
    {
        var text = await RenderAsync(Row(1, "PR-BackendValidation", build: "FAILED"));

        Assert.Contains("PR-BackendValidation —", text, StringComparison.Ordinal);
        Assert.Contains("hosting service", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The exact live failure: a provisioning failure must not read as broken code.</summary>
    [Fact]
    public async Task PlatformFailure_CarriesAnExplicitDoNotScoreWarning()
    {
        var text = await RenderAsync(Row(1, "PR-BackendValidation", build: "FAILED"));

        Assert.Contains("NOT evidence that the code fails to build", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be scored", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlatformFailureWithNoTestRun_SaysThereIsNoEvidenceEitherWay()
    {
        var text = await RenderAsync(Row(1, "PR-BackendValidation", build: "FAILED"));

        Assert.Contains("no evidence either way", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlatformFailureAlongsideTestResults_DoesNotClaimEvidenceIsMissing()
    {
        var text = await RenderAsync(
            Row(1, "PR-BackendValidation", build: "FAILED"),
            Row(2, "TestRunner", test: "PASS"));

        Assert.Contains("must not be scored", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no evidence either way", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFailingTestRun_IsNotExcusedByTheWarning()
    {
        // A real test failure IS the student's problem, so no platform disclaimer should appear.
        var text = await RenderAsync(Row(1, "TestRunner", test: "FAIL"));

        Assert.Contains("Tests: FAIL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("must not be scored", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PassingPlatformChecks_AddNoWarning()
    {
        var text = await RenderAsync(Row(1, "PR-BackendValidation", build: "SUCCESS"));

        Assert.DoesNotContain("must not be scored", text, StringComparison.OrdinalIgnoreCase);
    }
}
